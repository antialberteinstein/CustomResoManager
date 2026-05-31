using System.ComponentModel;
using CustomResoManager.Core;
using CustomResoManager.Models;
using ModelContextProtocol.Server;

namespace CustomResoManager.McpServer;

/// <summary>
/// MCP tools for managing app→resolution profiles, backed by the same
/// <see cref="IProfileManager"/> the WPF UI uses. Changes persist to profiles.json and
/// are picked up live by the running engine, so an agent can build the same profile list
/// a user would create by hand in the DataGrid.
/// </summary>
[McpServerToolType]
public sealed class ProfileTools
{
    private readonly IProfileManager _profiles;
    private readonly ILogger<ProfileTools> _logger;

    public ProfileTools(IProfileManager profiles, ILogger<ProfileTools> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }

    [McpServerTool(Name = "list_profiles")]
    [Description("List all saved app→resolution profiles (process name, target resolution, refresh rate, enabled).")]
    public IReadOnlyList<ProfileDto> ListProfiles()
    {
        _logger.LogInformation("list_profiles() called");
        return _profiles.GetAllProfiles().Select(ToDto).ToList();
    }

    [McpServerTool(Name = "add_profile")]
    [Description("Create or update a profile that switches the screen to width x height while the given "
        + "process is focused. processName is the executable name WITHOUT '.exe' (e.g. \"notepad\"). "
        + "refreshRate is optional (omit/null = system default). enabled defaults to true.")]
    public ProfileResultDto AddProfile(
        [Description("Process name without .exe, e.g. \"valorant\"")] string processName,
        [Description("Target width in pixels, e.g. 1280")] int width,
        [Description("Target height in pixels, e.g. 720")] int height,
        [Description("Optional target refresh rate in Hz; null = system default")] int? refreshRate = null,
        [Description("Whether the profile is active; defaults to true")] bool enabled = true)
    {
        _logger.LogInformation("add_profile(process={Process}, {W}x{H}, hz={Hz}, enabled={Enabled})",
            processName, width, height, refreshRate, enabled);

        if (string.IsNullOrWhiteSpace(processName))
            return new ProfileResultDto(false, null, "Tên tiến trình không được để trống.");
        if (width <= 0 || height <= 0)
            return new ProfileResultDto(false, null, "Chiều rộng và chiều cao phải là số dương.");

        var name = NormalizeProcessName(processName);
        var profile = new GameProfile
        {
            ProcessName = name,
            TargetWidth = width,
            TargetHeight = height,
            TargetRefreshRate = refreshRate,
            ExpectedAspectRatio = AspectRatioCalculator.CalculateAspectRatio(width, height),
            IsEnabled = enabled,
        };

        try
        {
            _profiles.AddOrUpdateProfile(profile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "add_profile failed for {Process}", name);
            return new ProfileResultDto(false, null, ex.Message);
        }

        return new ProfileResultDto(true, ToDto(profile), null);
    }

    [McpServerTool(Name = "remove_profile")]
    [Description("Delete the profile for the given process name (without '.exe'). "
        + "Returns success:false if no such profile exists.")]
    public ProfileResultDto RemoveProfile(
        [Description("Process name without .exe, e.g. \"valorant\"")] string processName)
    {
        var name = NormalizeProcessName(processName);
        _logger.LogInformation("remove_profile(process={Process})", name);

        var existing = _profiles.GetProfile(name);
        if (existing is null || !_profiles.RemoveProfile(name))
            return new ProfileResultDto(false, null, $"Không tìm thấy profile cho \"{name}\".");

        return new ProfileResultDto(true, ToDto(existing), null);
    }

    [McpServerTool(Name = "set_profile_enabled")]
    [Description("Turn a profile on or off without deleting it. The engine only applies enabled profiles.")]
    public ProfileResultDto SetProfileEnabled(
        [Description("Process name without .exe, e.g. \"valorant\"")] string processName,
        [Description("true = enable, false = disable")] bool enabled)
    {
        var name = NormalizeProcessName(processName);
        _logger.LogInformation("set_profile_enabled(process={Process}, enabled={Enabled})", name, enabled);

        var existing = _profiles.GetProfile(name);
        if (existing is null)
            return new ProfileResultDto(false, null, $"Không tìm thấy profile cho \"{name}\".");

        existing.IsEnabled = enabled;
        try
        {
            _profiles.AddOrUpdateProfile(existing);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "set_profile_enabled failed for {Process}", name);
            return new ProfileResultDto(false, null, ex.Message);
        }

        return new ProfileResultDto(true, ToDto(existing), null);
    }

    /// <summary>Strip a trailing ".exe" and surrounding whitespace so input matches stored process names.</summary>
    private static string NormalizeProcessName(string raw)
    {
        var name = raw.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }

    internal static ProfileDto ToDto(GameProfile p) =>
        new(p.ProcessName, p.TargetWidth, p.TargetHeight, p.TargetRefreshRate, p.ExpectedAspectRatio, p.IsEnabled);
}
