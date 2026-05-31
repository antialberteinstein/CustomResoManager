using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace CustomResoManager;

/// <summary>
/// Liệt kê các ứng dụng đã cài trên máy bằng cách quét shortcut (*.lnk) trong Start Menu
/// (cả của máy lẫn của người dùng). Khi chọn, resolve shortcut về .exe đích để lấy tên
/// tiến trình. Trả tên qua <see cref="SelectedProcessName"/>.
/// </summary>
public partial class InstalledAppPickerWindow : Window
{
    public sealed class AppRow
    {
        public string DisplayName { get; init; } = string.Empty;
        public string LnkPath { get; init; } = string.Empty;
    }

    /// <summary>Tên tiến trình người dùng đã chọn (null nếu hủy).</summary>
    public string? SelectedProcessName { get; private set; }

    private List<AppRow> _all = new();

    public InstalledAppPickerWindow()
    {
        InitializeComponent();
        LoadApps();
    }

    private void LoadApps()
    {
        var startMenuDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        var rows = new List<AppRow>();
        foreach (var dir in startMenuDirs)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

            IEnumerable<string> lnkFiles;
            try
            {
                lnkFiles = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var lnk in lnkFiles)
            {
                string name = Path.GetFileNameWithoutExtension(lnk);
                // Bỏ các shortcut không phải app (gỡ cài đặt, trang web trợ giúp...)
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("gỡ cài đặt", StringComparison.OrdinalIgnoreCase))
                    continue;

                rows.Add(new AppRow { DisplayName = name, LnkPath = lnk });
            }
        }

        _all = rows
            .GroupBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string q = txtSearch.Text.Trim();
        var view = string.IsNullOrWhiteSpace(q)
            ? _all
            : _all.Where(r => r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        dgApps.ItemsSource = view;
        txtCount.Text = $"{view.Count} ứng dụng";
    }

    /// <summary>
    /// Resolve shortcut .lnk về .exe đích qua WScript.Shell COM (late-binding).
    /// Trả tên tiến trình (không đuôi); fallback dùng tên hiển thị nếu không lấy được .exe.
    /// </summary>
    private static string ResolveLnkToProcessName(string lnkPath, string fallbackName)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
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
            // bỏ qua, dùng fallback
        }
        return fallbackName;
    }

    private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void dgApps_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void btnOk_Click(object sender, RoutedEventArgs e) => Accept();

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Accept()
    {
        if (dgApps.SelectedItem is not AppRow row)
        {
            MessageBox.Show("Hãy chọn một ứng dụng.", "Chưa chọn",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedProcessName = ResolveLnkToProcessName(row.LnkPath, row.DisplayName);
        DialogResult = true;
        Close();
    }
}
