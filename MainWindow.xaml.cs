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
using CustomResoManager.Models;

namespace CustomResoManager;

public partial class MainWindow : Window
{
    private readonly IResolutionManager _resolutionManager;
    private readonly IProfileManager _profileManager;
    private readonly IAppEngine _appEngine;
    private readonly HttpClient _chatClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private List<ResolutionModel> _cachedResolutions = [];
    private const string ChatEndpoint = "http://127.0.0.1:8000/chat";

    // ── Display model for DataGrid ────────────────────────────────────────────

    public sealed class ProfileDisplayItem
    {
        public string ProcessName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public int? TargetRefreshRate { get; set; }
        public string ExpectedAspectRatio { get; set; } = string.Empty;

        public string ResolutionDisplay => $"{TargetWidth}×{TargetHeight}";
        public string RefreshRateDisplay => TargetRefreshRate.HasValue ? $"{TargetRefreshRate}" : "—";

        public static ProfileDisplayItem From(GameProfile p) => new()
        {
            ProcessName = p.ProcessName,
            IsEnabled = p.IsEnabled,
            TargetWidth = p.TargetWidth,
            TargetHeight = p.TargetHeight,
            TargetRefreshRate = p.TargetRefreshRate,
            ExpectedAspectRatio = p.ExpectedAspectRatio
        };
    }

    private record ResolutionOption(int Width, int Height)
    {
        public override string ToString() => $"{Width} × {Height}";
    }

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

        RefreshProfileList();
        LoadResolutionOptions();
        RefreshCurrentResolution();
        _ = CheckChatConnectionAsync();
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
            var resp = await ping.GetAsync("http://127.0.0.1:8000/health");
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
            ? "Connected — http://127.0.0.1:8000"
            : "Disconnected — start the Python agent";
        txtChatStatus.Foreground = connected
            ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
            : new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    private void SetEngineStatus(bool running, string statusText)
    {
        statusDot.Background = running
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
            : new SolidColorBrush(Color.FromRgb(96, 125, 139));

        txtStatus.Text = statusText;
        txtStatus.Foreground = running
            ? new SolidColorBrush(Color.FromRgb(128, 203, 196))
            : new SolidColorBrush(Color.FromRgb(144, 164, 174));

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
            SetEngineStatus(true, "Engine Running — watching for games...");
            activeProfileBanner.Visibility = Visibility.Collapsed;
            RefreshCurrentResolution();
        });
    }

    private void OnEngineError(object? sender, string errMsg)
    {
        Dispatcher.Invoke(() =>
        {
            statusDot.Background = new SolidColorBrush(Colors.OrangeRed);
            txtStatus.Text = $"Error: {errMsg}";
            txtStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
        });
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void btnToggleEngine_Click(object sender, RoutedEventArgs e)
    {
        if (_appEngine.IsRunning)
        {
            _appEngine.Stop();
            SetEngineStatus(false, "Engine Stopped");
            activeProfileBanner.Visibility = Visibility.Collapsed;
            RefreshCurrentResolution();
        }
        else
        {
            _appEngine.Start();
            SetEngineStatus(true, "Engine Running — watching for games...");
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

    private void txtProcessName_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void txtProcessName_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            txtProcessName.Text = System.IO.Path.GetFileNameWithoutExtension(files[0]);
        }
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
