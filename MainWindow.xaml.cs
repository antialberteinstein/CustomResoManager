using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CustomResoManager.Core;
using CustomResoManager.Models;

namespace CustomResoManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IResolutionManager _resolutionManager;
    private readonly IProfileManager _profileManager;
    private readonly IAppEngine _appEngine;
    private readonly HttpClient _chatClient = new HttpClient();
    private const string ChatEndpoint = "http://127.0.0.1:8000/chat";

    public MainWindow()
    {
        InitializeComponent();

        _chatClient.Timeout = TimeSpan.FromMinutes(5);

        // 1. Khởi tạo Backend Services (Thay vì dùng framework DI phức tạp lúc test)
        _resolutionManager = new ResolutionManager();
        _profileManager = new ProfileManager();
        _appEngine = new AppEngine(_resolutionManager, _profileManager);

        // 2. Lắng nghe thông báo từ Backend (Do Engine chạy ngầm luồng Thread khác nên phải gọi qua Dispatcher)
        _appEngine.ProfileActivated += (s, profile) =>
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = $"Status: [ON] Res changed to {profile.TargetWidth}x{profile.TargetHeight} for '{profile.ProcessName}'";
                txtStatus.Foreground = Brushes.Green;
            });
        };

        _appEngine.ProfileDeactivated += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = "Status: [IDLE] Restored to Native Resolution";
                txtStatus.Foreground = Brushes.Gray;
            });
        };

        _appEngine.EngineError += (s, errMsg) =>
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = $"Status: [ERROR] {errMsg}";
                txtStatus.Foreground = Brushes.Red;
            });
        };

        // Hiện ds config ban đầu
        UpdateProfilesList();
        ShowSupportedResolutions();
    }

    private void ShowSupportedResolutions()
    {
        try
        {
            var resList = _resolutionManager.GetSupportedResolutions();
            // Lọc ra một số độ phân giải phổ biến để hiển thị nhỏ gọn
            var popularRes = resList.Where(r => r.Width >= 800)
                                    .Select(r => $"{r.Width}x{r.Height}")
                                    .Distinct()
                                    .Take(5)
                                    .ToList();
            
            txtProfiles.Text += "\n\nAvailable (Top 5):\n" + string.Join(", ", popularRes);
        }
        catch { }
    }

    private void btnAddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(txtWidth.Text, out int w) && int.TryParse(txtHeight.Text, out int h))
        {
            string process = txtProcessName.Text.Trim().Replace(".exe", "");
            
            var profile = new GameProfile
            {
                ProcessName = process,
                TargetWidth = w,
                TargetHeight = h,
                ExpectedAspectRatio = AspectRatioCalculator.CalculateAspectRatio(w, h),
                IsEnabled = true
            };
            
            _profileManager.AddOrUpdateProfile(profile);
            MessageBox.Show("Saved config to JSON!", "Success");
            UpdateProfilesList();
        }
        else
        {
            MessageBox.Show("Width / Height must be integer.", "Error");
        }
    }

    private void btnToggleEngine_Click(object sender, RoutedEventArgs e)
    {
        if (_appEngine.IsRunning)
        {
            _appEngine.Stop();
            btnToggleEngine.Content = "Start Engine";
            txtStatus.Text = "Status: Engine Stopped";
            txtStatus.Foreground = Brushes.Red;
        }
        else
        {
            _appEngine.Start();
            btnToggleEngine.Content = "Stop Engine";
            txtStatus.Text = "Status: Engine Running (Waiting for target app...)";
            txtStatus.Foreground = Brushes.Blue;
        }
    }

    private void UpdateProfilesList()
    {
        var profiles = _profileManager.GetAllProfiles();
        if (profiles.Any())
        {
            var text = "Saved Profiles:\n" + string.Join("\n", profiles.Select(p => $"- {p.ProcessName}: {p.TargetWidth}x{p.TargetHeight} ({p.ExpectedAspectRatio})"));
            txtProfiles.Text = text;
        }
        else
        {
            txtProfiles.Text = "Saved Profiles: None";
        }
    }

    private async void btnSendChat_Click(object sender, RoutedEventArgs e)
    {
        var userText = txtChatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(userText))
        {
            return;
        }

        AppendChatLine($"You: {userText}");
        AppendChatLine("Bot: Dang suy nghi...");
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
            ReplaceLastChatLine($"Bot: {reply}");
        }
        catch (Exception ex)
        {
            ReplaceLastChatLine($"Bot: [error] {ex.Message}");
        }
        finally
        {
            btnSendChat.IsEnabled = true;
        }
    }

    private void AppendChatLine(string line)
    {
        if (string.IsNullOrEmpty(txtChatLog.Text))
        {
            txtChatLog.Text = line;
        }
        else
        {
            txtChatLog.Text += Environment.NewLine + line;
        }

        txtChatLog.CaretIndex = txtChatLog.Text.Length;
        txtChatLog.ScrollToEnd();
    }

    private void ReplaceLastChatLine(string line)
    {
        var lines = txtChatLog.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            txtChatLog.Text = line;
        }
        else
        {
            lines[lines.Length - 1] = line;
            txtChatLog.Text = string.Join(Environment.NewLine, lines);
        }

        txtChatLog.CaretIndex = txtChatLog.Text.Length;
        txtChatLog.ScrollToEnd();
    }

    private void txtChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            btnSendChat_Click(sender, e);
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        // Rất Quen Trọng: Khôi phục màn hình khi thoát Test GUI
        _appEngine.Stop();
        _chatClient.Dispose();
        base.OnClosed(e);
    }
}