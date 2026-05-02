using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomResoManager.Models;

namespace CustomResoManager.Core
{
    public interface IAppEngine
    {
        void Start();
        void Stop();
        bool IsRunning { get; }
        GameProfile? CurrentActiveProfile { get; }
        
        // Sự kiện để báo cho UI (ViewModel) biết trạng thái đang đổi/khôi phục
        event EventHandler<GameProfile>? ProfileActivated;
        event EventHandler? ProfileDeactivated;
        event EventHandler<string>? EngineError;
    }

    public class AppEngine : IAppEngine, IDisposable
    {
        private readonly IResolutionManager _resolutionManager;
        private readonly IProfileManager _profileManager;
        private readonly IWindowScaler _windowScaler; // Thêm WindowScaler
        
        private CancellationTokenSource? _cancellationTokenSource;
        private GameProfile? _activeProfile;

        public bool IsRunning => _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;
        
        /// <summary>
        /// Profile hiện đang được áp dụng (Game đang chạy). Nếu Null thì đang ở màn hình Native.
        /// </summary>
        public GameProfile? CurrentActiveProfile => _activeProfile;

        public event EventHandler<GameProfile>? ProfileActivated;
        public event EventHandler? ProfileDeactivated;
        public event EventHandler<string>? EngineError;

        /// <summary>
        /// Chu kỳ quét tiến trình (Mặc định 2 giây).
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

        public AppEngine(IResolutionManager resolutionManager, IProfileManager profileManager)
        {
            _resolutionManager = resolutionManager ?? throw new ArgumentNullException(nameof(resolutionManager));
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            
            // Khởi tạo luôn dịch vụ Scale để đỡ sửa Program.cs / MainWindow.xaml.cs ban đầu
            _windowScaler = new WindowScaler();
        }

        public void Start()
        {
            if (IsRunning) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            Task.Run(() => MonitoringLoop(token), token);
        }

        public void Stop()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            // Fallback an toàn: Khi App Engine tắt (hoặc người dùng đóng Tool), trả về màn hình gốc
            if (_activeProfile != null)
            {
                _windowScaler.RestoreWindow(_activeProfile.ProcessName);
                _activeProfile = null;
                ProfileDeactivated?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task MonitoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. Chỉ lấy những Profile đang được cho phép (IsEnabled = true)
                    var enabledProfiles = _profileManager.GetAllProfiles().Where(p => p.IsEnabled).ToList();
                    
                    if (enabledProfiles.Any())
                    {
                        // 2. Lấy tên của TẤT CẢ các tiến trình đang chạy trền system
                        var runningProcesses = Process.GetProcesses()
                                                      .Select(p => p.ProcessName)
                                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        // 3. Khớp kiểm tra xem có game nào trong danh sách profile đang chạy không
                        var matchedProfile = enabledProfiles.FirstOrDefault(p => runningProcesses.Contains(p.ProcessName));

                        // KỊCH BẢN A: Lúc nãy không có game, giờ phát hiện game MỚI CHẠY
                        if (matchedProfile != null && _activeProfile == null)
                        {
                            // Đảm bảo lệnh MakeFullScreenBorderless bám lấy Handle cửa sổ thành công mới tính là Active
                            if (_windowScaler.MakeFullScreenBorderless(matchedProfile.ProcessName))
                            {
                                _activeProfile = matchedProfile;
                                ProfileActivated?.Invoke(this, _activeProfile);
                            }
                        }
                        // KỊCH BẢN B: Lúc nãy có game, giờ game ĐÃ TẮT
                        else if (matchedProfile == null && _activeProfile != null)
                        {
                            _windowScaler.RestoreWindow(_activeProfile.ProcessName);
                            _activeProfile = null;
                            ProfileDeactivated?.Invoke(this, EventArgs.Empty);
                        }
                        // KỊCH BẢN C: Vừa thoát game này ra chuyển vội sang game kia (Cùng lúc 2 game)
                        else if (matchedProfile != null && _activeProfile != null && 
                                !matchedProfile.ProcessName.Equals(_activeProfile.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            _windowScaler.RestoreWindow(_activeProfile.ProcessName);
                            if (_windowScaler.MakeFullScreenBorderless(matchedProfile.ProcessName))
                            {
                                _activeProfile = matchedProfile;
                                ProfileActivated?.Invoke(this, _activeProfile);
                            }
                            else
                            {
                                _activeProfile = null;
                            }
                        }
                    }
                    else if (_activeProfile != null)
                    {
                        // Trường hợp App đang áp dụng Profile nhưng người dùng tắt tick IsEnabled trên UI
                        _windowScaler.RestoreWindow(_activeProfile.ProcessName);
                        _activeProfile = null;
                        ProfileDeactivated?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    // Thông báo lỗi lên UI
                    EngineError?.Invoke(this, ex.Message);
                    Debug.WriteLine($"[AppEngine Error]: {ex.Message}");
                }

                // Chờ cho lần quyét tiếp theo
                try
                {
                    await Task.Delay(PollingInterval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}