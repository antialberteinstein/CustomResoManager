using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace CustomResoManager;

/// <summary>
/// Liệt kê các tiến trình đang chạy có cửa sổ thật, cho người dùng chọn để thêm vào
/// danh sách profile. Trả tên tiến trình (không đuôi .exe) qua <see cref="SelectedProcessName"/>.
/// </summary>
public partial class ProcessPickerWindow : Window
{
    public sealed class ProcessRow
    {
        public string ProcessName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
    }

    /// <summary>Tên tiến trình người dùng đã chọn (null nếu hủy).</summary>
    public string? SelectedProcessName { get; private set; }

    private List<ProcessRow> _all = new();

    public ProcessPickerWindow()
    {
        InitializeComponent();
        LoadProcesses();
    }

    private void LoadProcesses()
    {
        _all = Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    return p.MainWindowHandle != IntPtr.Zero
                           && !string.IsNullOrWhiteSpace(p.MainWindowTitle);
                }
                catch
                {
                    return false;
                }
            })
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcessRow
            {
                ProcessName = g.Key,
                Title = g.Select(SafeTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty
            })
            .OrderBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyFilter();
    }

    private static string SafeTitle(Process p)
    {
        try { return p.MainWindowTitle; }
        catch { return string.Empty; }
    }

    private void ApplyFilter()
    {
        string q = txtSearch.Text.Trim();
        dgProcesses.ItemsSource = string.IsNullOrWhiteSpace(q)
            ? _all
            : _all.Where(r =>
                    r.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                  .ToList();
    }

    private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void btnRefresh_Click(object sender, RoutedEventArgs e) => LoadProcesses();

    private void dgProcesses_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void btnOk_Click(object sender, RoutedEventArgs e) => Accept();

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Accept()
    {
        if (dgProcesses.SelectedItem is not ProcessRow row)
        {
            MessageBox.Show("Hãy chọn một tiến trình.", "Chưa chọn",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedProcessName = row.ProcessName;
        DialogResult = true;
        Close();
    }
}
