using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CustomResoManager.Core;
using CustomResoManager.McpServer;
using CustomResoManager.Models;

namespace CustomResoManager;

public partial class MainWindow : Window
{
    private readonly IResolutionManager _resolutionManager;
    private readonly IProfileManager _profileManager;
    private readonly IAppEngine _appEngine;
    private readonly HttpClient _chatClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private List<ResolutionModel> _cachedResolutions = [];

    // Agent chat endpoint comes from config.yaml (agent section); set its host to the
    // agent machine's LAN IP when the Python backend runs on a different computer.
    private static readonly AgentConfig AgentCfg = App.Config.Agent;
    private static readonly string ChatEndpoint = AgentCfg.ChatUrl;

    // ── Display model for DataGrid ────────────────────────────────────────────

    public sealed class ProfileDisplayItem
    {
        public string ProcessName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public int? TargetRefreshRate { get; set; }
        public string ExpectedAspectRatio { get; set; } = string.Empty;

        /// <summary>Lựa chọn độ phân giải hiện tại của dòng (bind tới ComboBox trong DataGrid).</summary>
        public ResolutionOption? SelectedResolution { get; set; }

        public string ResolutionDisplay => $"{TargetWidth}×{TargetHeight}";
        public string RefreshRateDisplay => TargetRefreshRate.HasValue ? $"{TargetRefreshRate}" : "—";

        public static ProfileDisplayItem From(GameProfile p) => new()
        {
            ProcessName = p.ProcessName,
            IsEnabled = p.IsEnabled,
            TargetWidth = p.TargetWidth,
            TargetHeight = p.TargetHeight,
            TargetRefreshRate = p.TargetRefreshRate,
            ExpectedAspectRatio = p.ExpectedAspectRatio,
            SelectedResolution = new ResolutionOption(p.TargetWidth, p.TargetHeight)
        };
    }

    public record ResolutionOption(int Width, int Height)
    {
        public override string ToString() => $"{Width} × {Height}";
    }

    /// <summary>Danh sách độ phân giải dùng cho ComboBox chỉnh sửa trong DataGrid.</summary>
    public List<ResolutionOption> ResolutionOptions { get; private set; } = [];

    // ── Init ──────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();

        _resolutionManager = App.ResolutionManager;
        _profileManager = App.ProfileManager;
        _appEngine = App.AppEngine;

        _appEngine.ProfileActivated += OnProfileActivated;
        _appEngine.ProfileDeactivated += OnProfileDeactivated;
        _appEngine.EngineError += OnEngineError;

        // Tự cập nhật giao diện khi trạng thái đổi do AGENT/MCP điều khiển (không qua nút bấm).
        _appEngine.Started += OnEngineStartedExternally;
        _appEngine.Stopped += OnEngineStoppedExternally;
        _profileManager.ProfilesChanged += OnProfilesChangedExternally;
        _resolutionManager.ResolutionChanged += OnResolutionChangedExternally;

        // LoadResolutionOptions phải chạy trước RefreshProfileList để ComboBox trong
        // DataGrid có sẵn nguồn dữ liệu (ResolutionOptions) khi các dòng được dựng.
        LoadResolutionOptions();
        RefreshProfileList();
        RefreshCurrentResolution();
        _ = CheckChatConnectionAsync();
    }

    /// <summary>
    /// Trả tên tiến trình (không đuôi) từ đường dẫn file được chọn. Hỗ trợ .exe và shortcut .lnk
    /// (resolve target qua WScript.Shell COM, late-binding để không cần thêm COM reference).
    /// </summary>
    private static string? ResolveFileToProcessName(string path)
    {
        try
        {
            string target = path;

            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(path);
                    string resolved = shortcut.TargetPath;
                    if (!string.IsNullOrWhiteSpace(resolved))
                        target = resolved;
                }
            }

            return System.IO.Path.GetFileNameWithoutExtension(target);
        }
        catch
        {
            // Nếu resolve .lnk lỗi, fallback dùng chính tên file được kéo vào.
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void RefreshProfileList()
    {
        dgProfiles.ItemsSource = _profileManager.GetAllProfiles()
            .Select(ProfileDisplayItem.From)
            .ToList();
    }

    private void LoadResolutionOptions()
    {
        try
        {
            _cachedResolutions = _resolutionManager.GetSupportedResolutions()
                .Where(r => r.Width >= 640)
                .OrderByDescending(r => r.Width)
                .ThenByDescending(r => r.Height)
                .ToList();

            var options = _cachedResolutions
                .GroupBy(r => (r.Width, r.Height))
                .Select(g => new ResolutionOption(g.Key.Width, g.Key.Height))
                .ToList();

            ResolutionOptions = options;   // nguồn cho ComboBox chỉnh sửa trong DataGrid
            cmbResolution.ItemsSource = options;
            if (options.Count > 0) cmbResolution.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load display resolutions: {ex.Message}",
                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshCurrentResolution()
    {
        try
        {
            var res = _resolutionManager.GetCurrentResolution();
            txtCurrentRes.Text = $"{res.Width}×{res.Height} @ {res.RefreshRate}Hz";
        }
        catch
        {
            txtCurrentRes.Text = "—";
        }
    }

    private async Task CheckChatConnectionAsync()
    {
        try
        {
            using var ping = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await ping.GetAsync(AgentCfg.HealthUrl);
            SetChatStatus(resp.IsSuccessStatusCode);
        }
        catch
        {
            SetChatStatus(false);
        }
    }

    private void SetChatStatus(bool connected)
    {
        chatStatusDot.Background = connected
            ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
            : new SolidColorBrush(Color.FromRgb(158, 158, 158));
        txtChatStatus.Text = connected
            ? $"Connected — {AgentCfg.BaseUrl}"
            : "Disconnected — start the Python agent";
        txtChatStatus.Foreground = connected
            ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
            : new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    private void SetEngineStatus(bool running, string statusText)
    {
        _ = statusText; // không còn hiển thị status text trên header (trạng thái thể hiện qua nút)

        btnToggleEngine.Content = running ? "⏹  Stop Engine" : "▶  Start Engine";
        btnToggleEngine.Background = running
            ? new SolidColorBrush(Color.FromRgb(183, 28, 28))
            : new SolidColorBrush(Color.FromRgb(46, 125, 50));
    }

    // ── Engine events ─────────────────────────────────────────────────────────

    private void OnProfileActivated(object? sender, GameProfile profile)
    {
        Dispatcher.Invoke(() =>
        {
            SetEngineStatus(true, $"Active: {profile.ProcessName} → {profile.TargetWidth}×{profile.TargetHeight}");
            activeProfileBanner.Visibility = Visibility.Visible;
            txtActiveProfile.Text = $"Profile active: {profile.ProcessName}  ({profile.TargetWidth}×{profile.TargetHeight})";
            RefreshCurrentResolution();
        });
    }

    private void OnProfileDeactivated(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            SetEngineStatus(true, "Engine đang chạy — theo dõi app đang focus...");
            activeProfileBanner.Visibility = Visibility.Collapsed;
            RefreshCurrentResolution();
        });
    }

    // ── Đồng bộ giao diện khi agent/MCP thay đổi trạng thái ────────────────────
    // Các sự kiện này có thể bắn từ luồng Kestrel (MCP) hoặc luồng nền của engine,
    // nên luôn marshal về UI thread bằng Dispatcher trước khi đụng tới control.

    private void OnEngineStartedExternally(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _lastEngineError = null;
            SetEngineStatus(true, "");
            RefreshCurrentResolution();
        });
    }

    private void OnEngineStoppedExternally(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            SetEngineStatus(false, "");
            activeProfileBanner.Visibility = Visibility.Collapsed;
            RefreshCurrentResolution();
        });
    }

    private void OnProfilesChangedExternally(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(RefreshProfileList);

    private void OnResolutionChangedExternally(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(RefreshCurrentResolution);

    private string? _lastEngineError;

    private void OnEngineError(object? sender, string errMsg)
    {
        Dispatcher.Invoke(() =>
        {
            // Hiện lỗi nhưng tránh spam: chỉ báo khi nội dung lỗi đổi.
            if (errMsg == _lastEngineError) return;
            _lastEngineError = errMsg;
            MessageBox.Show(errMsg, "Engine error", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void btnToggleEngine_Click(object sender, RoutedEventArgs e)
    {
        if (_appEngine.IsRunning)
        {
            _appEngine.Stop();
            SetEngineStatus(false, "");
            activeProfileBanner.Visibility = Visibility.Collapsed;
            RefreshCurrentResolution();
        }
        else
        {
            _lastEngineError = null; // cho phép báo lại lỗi mới ở lần chạy này
            _appEngine.Start();
            SetEngineStatus(true, "");
        }
    }

    private void btnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string processName = txtProcessName.Text.Trim()
            .Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(processName))
        {
            MessageBox.Show("Please enter a process name.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (cmbResolution.SelectedItem is not ResolutionOption res)
        {
            MessageBox.Show("Please select a resolution.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int? refreshRate = null;
        if (cmbRefreshRate.SelectedItem is string rateStr
            && rateStr != "Default"
            && int.TryParse(rateStr.Replace(" Hz", ""), out int parsedRate))
        {
            refreshRate = parsedRate;
        }

        var profile = new GameProfile
        {
            ProcessName = processName,
            TargetWidth = res.Width,
            TargetHeight = res.Height,
            TargetRefreshRate = refreshRate,
            ExpectedAspectRatio = AspectRatioCalculator.CalculateAspectRatio(res.Width, res.Height),
            IsEnabled = true
        };

        _profileManager.AddOrUpdateProfile(profile);
        RefreshProfileList();
        txtProcessName.Text = string.Empty;
    }

    private void btnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string processName })
        {
            var confirm = MessageBox.Show(
                $"Delete profile for '{processName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                _profileManager.RemoveProfile(processName);
                RefreshProfileList();
            }
        }
    }

    private void chkEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: ProfileDisplayItem item } chk)
        {
            var profile = _profileManager.GetProfile(item.ProcessName);
            if (profile is null) return;
            profile.IsEnabled = chk.IsChecked == true;
            _profileManager.SaveChanges();
        }
    }

    private void btnPickProcess_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ProcessPickerWindow { Owner = this };
        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedProcessName))
        {
            txtProcessName.Text = picker.SelectedProcessName;
        }
    }

    private void btnAllApps_Click(object sender, RoutedEventArgs e)
    {
        var picker = new InstalledAppPickerWindow { Owner = this };
        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedProcessName))
        {
            txtProcessName.Text = picker.SelectedProcessName;
        }
    }

    private void btnBrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn file game (.exe) hoặc shortcut (.lnk)",
            Filter = "Ứng dụng & shortcut (*.exe;*.lnk)|*.exe;*.lnk|Tất cả file (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true) return;

        string? name = ResolveFileToProcessName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(name))
            txtProcessName.Text = name;
    }

    private void dgResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { Tag: string processName, SelectedItem: ResolutionOption opt })
            return;

        var profile = _profileManager.GetProfile(processName);
        if (profile is null) return;

        // Bỏ qua khi giá trị không đổi (gồm cả lần bind đầu tiên) để tránh lưu/đệ quy thừa.
        if (profile.TargetWidth == opt.Width && profile.TargetHeight == opt.Height)
            return;

        profile.TargetWidth = opt.Width;
        profile.TargetHeight = opt.Height;
        profile.TargetRefreshRate = null; // Hz cũ có thể không hợp lệ với độ phân giải mới
        profile.ExpectedAspectRatio = AspectRatioCalculator.CalculateAspectRatio(opt.Width, opt.Height);
        _profileManager.AddOrUpdateProfile(profile);

        RefreshProfileList(); // cập nhật cột Ratio / Hz hiển thị
    }

    private void cmbResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbResolution.SelectedItem is not ResolutionOption selected) return;

        var rates = _cachedResolutions
            .Where(r => r.Width == selected.Width && r.Height == selected.Height)
            .Select(r => r.RefreshRate)
            .Distinct()
            .OrderByDescending(r => r)
            .Select(r => $"{r} Hz")
            .ToList();

        rates.Insert(0, "Default");
        cmbRefreshRate.ItemsSource = rates;
        cmbRefreshRate.SelectedIndex = 0;
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    private async void btnSendChat_Click(object sender, RoutedEventArgs e)
    {
        var userText = txtChatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;

        AppendChat($"You: {userText}");
        AppendChat("Bot: Đang suy nghĩ...");
        txtChatInput.Text = string.Empty;
        btnSendChat.IsEnabled = false;

        try
        {
            var payload = JsonSerializer.Serialize(new { message = userText });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _chatClient.PostAsync(ChatEndpoint, content);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var reply = JsonDocument.Parse(body).RootElement.GetProperty("reply").GetString() ?? string.Empty;
            ReplaceLastChat($"Bot: {reply}");

            RefreshCurrentResolution();
            SetChatStatus(true);
        }
        catch (Exception ex)
        {
            ReplaceLastChat($"Bot: [lỗi] {ex.Message}");
            SetChatStatus(false);
        }
        finally
        {
            btnSendChat.IsEnabled = true;
        }
    }

    private void txtChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; btnSendChat_Click(sender, e); }
    }

    private void AppendChat(string line)
    {
        txtChatLog.Text = string.IsNullOrEmpty(txtChatLog.Text)
            ? line
            : txtChatLog.Text + Environment.NewLine + line;
        txtChatLog.ScrollToEnd();
    }

    private void ReplaceLastChat(string line)
    {
        var lines = txtChatLog.Text.Split(Environment.NewLine);
        lines[^1] = line;
        txtChatLog.Text = string.Join(Environment.NewLine, lines);
        txtChatLog.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        _appEngine.Stop();
        _chatClient.Dispose();
        base.OnClosed(e);
    }
}
