using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CustomResoManager.Core
{
    public interface IWindowScaler
    {
        bool MakeFullScreenBorderless(string processName);
        void RestoreWindow(string processName);
    }

    public class WindowScaler : IWindowScaler
    {
        // Lưu lại Styles và Tọa độ cũ để trả về như cũ khi tắt app
        private readonly Dictionary<IntPtr, int> _originalStyles = new Dictionary<IntPtr, int>();
        private readonly Dictionary<IntPtr, RECT> _originalRects = new Dictionary<IntPtr, RECT>();

        public bool MakeFullScreenBorderless(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0) return false;

                Process targetProcess = processes[0];
                targetProcess.Refresh(); // BẮT BUỘC: Làm mới trạng thái tiến trình để lấy Handle mới nhất
                IntPtr hWnd = targetProcess.MainWindowHandle;

                // Nếu game chưa kịp vẽ cửa sổ hoặc chạy ngầm thì bỏ qua (đợi lần quét sau)
                if (hWnd == IntPtr.Zero) return false;

                // 1. Lưu lại thuộc tính cũ để khôi phục sau này
                if (!_originalStyles.ContainsKey(hWnd))
                {
                    _originalStyles[hWnd] = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE);
                    NativeMethods.GetWindowRect(hWnd, out RECT rect);
                    _originalRects[hWnd] = rect;
                }

                // 2. Lấy Style hiện tại
                int style = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE);

                // 3. Gỡ bỏ Viền (Borders), Thanh tiêu đề (Title bar), Nút thu phóng
                int borderlessStyle = style & ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_SYSMENU);
                NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_STYLE, borderlessStyle);

                // 4. Lấy kích thước Màn hình Gốc cực to của máy tính (VD: 1920x1080)
                int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
                int screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

                // 5. Cưỡng ép cửa sổ của Game (chỉ đang 800x600) phóng to trùm kín không gian 1920x1080 
                //    => Việc này sẽ ép Card đồ họa tự Scaling hình ảnh lên khít tỷ lệ.
                NativeMethods.SetWindowPos(
                    hWnd, 
                    (IntPtr)NativeMethods.HWND_TOP, 
                    0, 0, screenWidth, screenHeight,
                    NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RestoreWindow(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0) return;

                Process targetProcess = processes[0];
                IntPtr hWnd = targetProcess.MainWindowHandle;

                if (hWnd != IntPtr.Zero && _originalStyles.ContainsKey(hWnd))
                {
                    // Phục hồi viền mỏng và tiêu đề
                    NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_STYLE, _originalStyles[hWnd]);

                    // Phục hồi kích thước cửa sổ 800x600 bé xíu
                    var rect = _originalRects[hWnd];
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    
                    NativeMethods.SetWindowPos(
                        hWnd, 
                        (IntPtr)NativeMethods.HWND_TOP, 
                        rect.Left, rect.Top, width, height, 
                        NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);

                    // Giải phóng RAM
                    _originalStyles.Remove(hWnd);
                    _originalRects.Remove(hWnd);
                }
            }
            catch { }
        }
    }
}