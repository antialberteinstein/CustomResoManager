using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomResoManager.Core
{
    public class ProcessMonitor : IDisposable
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isProcessCurrentlyRunning;

        /// <summary>
        /// Tên tiến trình cần theo dõi (không bao gồm đuôi .exe, ví dụ: "csgo", "hl2").
        /// </summary>
        public string TargetProcessName { get; private set; }

        /// <summary>
        /// Chu kỳ kiểm tra tiến trình (mặc định 2 giây).
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Sự kiện kích hoạt khi phát hiện game/ứng dụng bắt đầu chạy.
        /// </summary>
        public event EventHandler? ProcessStarted;

        /// <summary>
        /// Sự kiện kích hoạt khi phát hiện game/ứng dụng đã tắt.
        /// </summary>
        public event EventHandler? ProcessExited;

        public ProcessMonitor(string targetProcessName)
        {
            if (string.IsNullOrWhiteSpace(targetProcessName))
                throw new ArgumentException("Process name cannot be empty.", nameof(targetProcessName));

            TargetProcessName = targetProcessName;
        }

        /// <summary>
        /// Bắt đầu vòng lặp giám sát tiến trình ngầm.
        /// </summary>
        public void StartMonitoring()
        {
            if (_cancellationTokenSource != null) return; // Đã đang chạy

            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            // Kiểm tra trạng thái ban đầu
            _isProcessCurrentlyRunning = CheckIfProcessIsRunning();

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    bool isRunningNow = CheckIfProcessIsRunning();

                    if (isRunningNow && !_isProcessCurrentlyRunning)
                    {
                        _isProcessCurrentlyRunning = true;
                        ProcessStarted?.Invoke(this, EventArgs.Empty);
                    }
                    else if (!isRunningNow && _isProcessCurrentlyRunning)
                    {
                        _isProcessCurrentlyRunning = false;
                        ProcessExited?.Invoke(this, EventArgs.Empty);
                    }

                    try
                    {
                        await Task.Delay(PollingInterval, token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Bỏ qua lỗi khi task bị hủy
                        break;
                    }
                }
            }, token);
        }

        /// <summary>
        /// Dừng giám sát.
        /// </summary>
        public void StopMonitoring()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Hàm nội bộ để kiểm tra xem tiến trình có đang chạy không.
        /// </summary>
        private bool CheckIfProcessIsRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(TargetProcessName);
                return processes.Length > 0;
            }
            catch
            {
                // Xử lý an toàn nếu không có quyền truy cập thông tin tiến trình
                return false;
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}