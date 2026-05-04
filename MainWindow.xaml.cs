using System.Linq;
using System.Windows;
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

    public MainWindow()
    {
        InitializeComponent();

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

    protected override void OnClosed(System.EventArgs e)
    {
        // Rất Quen Trọng: Khôi phục màn hình khi thoát Test GUI
        _appEngine.Stop();
        base.OnClosed(e);
    }
}