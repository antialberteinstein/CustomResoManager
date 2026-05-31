using System;
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

    /// <summary>
    /// Theo dõi cửa sổ đang được FOCUS (foreground). Khi app đang focus khớp một profile
    /// đang bật, màn hình tự đổi sang độ phân giải của profile đó. Khi focus sang app
    /// ngoài danh sách, màn hình trả về độ phân giải gốc (baseline) lúc Start().
    /// </summary>
    public class AppEngine : IAppEngine, IDisposable
    {
        private readonly IResolutionManager _resolutionManager;
        private readonly IProfileManager _profileManager;

        private IntPtr _hook = IntPtr.Zero;
        // Giữ tham chiếu delegate sống để GC không thu hồi khi callback đang được Windows dùng.
        private NativeMethods.WinEventDelegate? _winEventProc;

        private ResolutionModel? _baseline;        // Độ phân giải desktop khi Start()
        private GameProfile? _activeProfile;        // Profile đang được áp dụng (null = đang ở baseline)
        private readonly object _gate = new();
        private int _generation;                    // Dùng để debounce: chỉ lần focus mới nhất mới được áp

        /// <summary>Bỏ qua các lần focus thoáng qua nhanh hơn khoảng này (chống nhấp nháy khi alt-tab).</summary>
        public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(150);

        public bool IsRunning => _hook != IntPtr.Zero;
        public GameProfile? CurrentActiveProfile => _activeProfile;

        public event EventHandler<GameProfile>? ProfileActivated;
        public event EventHandler? ProfileDeactivated;
        public event EventHandler<string>? EngineError;

        public AppEngine(IResolutionManager resolutionManager, IProfileManager profileManager)
        {
            _resolutionManager = resolutionManager ?? throw new ArgumentNullException(nameof(resolutionManager));
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        }

        /// <summary>
        /// Phải gọi trên luồng UI (có message pump) để WinEvent hook nhận được callback.
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _baseline = _resolutionManager.GetCurrentResolution();
            }
            catch (Exception ex)
            {
                EngineError?.Invoke(this, $"Không đọc được độ phân giải hiện tại: {ex.Message}");
                return;
            }

            _winEventProc = OnForegroundChanged;
            _hook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventProc,
                0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (_hook == IntPtr.Zero)
            {
                _winEventProc = null;
                EngineError?.Invoke(this, "Không đăng ký được hook theo dõi cửa sổ đang focus.");
                return;
            }

            // Áp ngay cho cửa sổ đang focus tại thời điểm bật engine
            HandleForeground(NativeMethods.GetForegroundWindow());
        }

        public void Stop()
        {
            bool wasRunning = _hook != IntPtr.Zero;

            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
            _winEventProc = null;

            // Vô hiệu mọi lần áp đang chờ debounce
            Interlocked.Increment(ref _generation);

            lock (_gate)
            {
                bool hadActive = _activeProfile != null;
                _activeProfile = null;

                // LUÔN trả màn hình về độ phân giải gốc của máy khi dừng engine.
                if (wasRunning)
                    RestoreBaseline();

                if (hadActive)
                    ProfileDeactivated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnForegroundChanged(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            HandleForeground(hwnd);
        }

        private void HandleForeground(IntPtr hwnd)
        {
            string? processName = GetProcessName(hwnd);
            int gen = Interlocked.Increment(ref _generation);

            // Chạy ngoài luồng UI để việc đổi độ phân giải (chậm) không làm treo giao diện.
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelay).ConfigureAwait(false);
                    if (gen != Volatile.Read(ref _generation)) return; // đã bị lần focus mới hơn thay thế
                    Apply(processName);
                }
                catch (Exception ex)
                {
                    EngineError?.Invoke(this, ex.Message);
                }
            });
        }

        private void Apply(string? processName)
        {
            lock (_gate)
            {
                if (!IsRunning) return;

                try
                {
                    GameProfile? match = processName is null
                        ? null
                        : _profileManager.GetAllProfiles().FirstOrDefault(p =>
                            p.IsEnabled &&
                            p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        // App đang focus có profile -> đổi sang độ phân giải của nó
                        if (_activeProfile == null || !SameTarget(_activeProfile, match))
                        {
                            EnsureResolution(match.TargetWidth, match.TargetHeight, match.TargetRefreshRate);
                            _activeProfile = match;
                            ProfileActivated?.Invoke(this, match);
                        }
                    }
                    else
                    {
                        // App ngoài danh sách (kể cả chính app này) -> trả về baseline
                        if (_activeProfile != null)
                        {
                            RestoreBaseline();
                            _activeProfile = null;
                            ProfileDeactivated?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
                catch (Exception ex)
                {
                    EngineError?.Invoke(this, ex.Message);
                    Debug.WriteLine($"[AppEngine Error]: {ex.Message}");
                }
            }
        }

        /// <summary>Chỉ đổi độ phân giải khi khác với hiện tại, tránh nhấp nháy thừa.</summary>
        private void EnsureResolution(int width, int height, int? refreshRate)
        {
            var current = _resolutionManager.GetCurrentResolution();
            bool sameSize = current.Width == width && current.Height == height;
            bool sameRate = !refreshRate.HasValue || current.RefreshRate == refreshRate.Value;
            if (sameSize && sameRate) return;

            _resolutionManager.ChangeResolution(width, height, refreshRate);
        }

        private void RestoreBaseline()
        {
            if (_baseline != null)
                EnsureResolution(_baseline.Width, _baseline.Height, _baseline.RefreshRate);
        }

        private static bool SameTarget(GameProfile a, GameProfile b) =>
            a.TargetWidth == b.TargetWidth &&
            a.TargetHeight == b.TargetHeight &&
            a.TargetRefreshRate == b.TargetRefreshRate;

        private static string? GetProcessName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            try
            {
                using var p = Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
            catch
            {
                // Tiến trình đã thoát hoặc không đủ quyền (vd app chạy Admin) -> coi như ngoài danh sách
                return null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
