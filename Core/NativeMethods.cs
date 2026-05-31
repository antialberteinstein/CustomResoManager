using System;
using System.Runtime.InteropServices;
using CustomResoManager.Models;

namespace CustomResoManager.Core
{
    public static class NativeMethods
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int ENUM_REGISTRY_SETTINGS = -2;

        public const int CDS_UPDATEREGISTRY = 0x01;
        public const int CDS_TEST = 0x02;

        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISP_CHANGE_RESTART = 1;
        public const int DISP_CHANGE_FAILED = -1;
        public const int DISP_CHANGE_BADMODE = -2;
        public const int DISP_CHANGE_NOTUPDATED = -3;
        public const int DISP_CHANGE_BADFLAGS = -4;
        public const int DISP_CHANGE_BADPARAM = -5;

        public const int DM_BITSPERPEL = 0x00040000;
        public const int DM_PELSWIDTH = 0x00080000;
        public const int DM_PELSHEIGHT = 0x00100000;
        public const int DM_DISPLAYFREQUENCY = 0x00400000;

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        // --- Theo dõi cửa sổ đang được FOCUS (foreground) ---

        /// <summary>Lấy handle của cửa sổ đang được focus ở foreground.</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>Lấy Process ID (và Thread ID) của tiến trình sở hữu cửa sổ.</summary>
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>Sự kiện hệ thống: một cửa sổ vừa được đưa lên foreground.</summary>
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

        /// <summary>Hook chạy ngoài tiến trình đích, callback được đưa vào message queue của luồng đăng ký.</summary>
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        /// <summary>Callback của WinEvent hook. Phải giữ tham chiếu sống để GC không thu hồi.</summary>
        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}
