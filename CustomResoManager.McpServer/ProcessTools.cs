using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace CustomResoManager.McpServer;

/// <summary>
/// Read-only discovery tools so the agent can find a process/app name to build a profile for,
/// the same way the WPF "Tiến trình chạy" and "Tất cả app" picker dialogs do. The returned
/// processName values are ready to pass to <c>add_profile</c>.
/// </summary>
[McpServerToolType]
public sealed class ProcessTools
{
    private readonly ILogger<ProcessTools> _logger;

    public ProcessTools(ILogger<ProcessTools> logger)
    {
        _logger = logger;
    }

    [McpServerTool(Name = "list_running_processes")]
    [Description("List currently running processes that have a real visible window (name + window title). "
        + "Use this to find the processName for add_profile when the user refers to an app that is open.")]
    public IReadOnlyList<RunningProcessDto> ListRunningProcesses()
    {
        _logger.LogInformation("list_running_processes() called");

        return Process.GetProcesses()
            .Where(HasRealWindow)
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RunningProcessDto(
                g.Key,
                g.Select(SafeTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty))
            .OrderBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [McpServerTool(Name = "list_installed_apps")]
    [Description("List installed applications by scanning Start Menu shortcuts (*.lnk) for the machine and "
        + "the current user. Each entry resolves to the target executable's processName for add_profile.")]
    public IReadOnlyList<InstalledAppDto> ListInstalledApps()
    {
        _logger.LogInformation("list_installed_apps() called");

        var startMenuDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };

        var rows = new List<(string Name, string Lnk)>();
        foreach (var dir in startMenuDirs)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            IEnumerable<string> lnkFiles;
            try { lnkFiles = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var lnk in lnkFiles)
            {
                var name = Path.GetFileNameWithoutExtension(lnk);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("gỡ cài đặt", StringComparison.OrdinalIgnoreCase))
                    continue;
                rows.Add((name, lnk));
            }
        }

        return rows
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new InstalledAppDto(r.Name, ResolveLnkToProcessName(r.Lnk, r.Name)))
            .ToList();
    }

    private static bool HasRealWindow(Process p)
    {
        try
        {
            return p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle);
        }
        catch
        {
            return false;
        }
    }

    private static string SafeTitle(Process p)
    {
        try { return p.MainWindowTitle; }
        catch { return string.Empty; }
    }

    /// <summary>Resolve a .lnk shortcut to its target .exe name via WScript.Shell COM (late-bound).</summary>
    private static string ResolveLnkToProcessName(string lnkPath, string fallbackName)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string target = shortcut.TargetPath;
                if (!string.IsNullOrWhiteSpace(target) &&
                    target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFileNameWithoutExtension(target);
                }
            }
        }
        catch
        {
            // fall back to the shortcut's display name
        }
        return fallbackName;
    }
}
