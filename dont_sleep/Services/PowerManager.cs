using System.Runtime.InteropServices;

namespace dont_sleep.Services
{
    public static class PowerManager
    {
        // EXECUTION_STATE flags
        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private const uint SPI_SETSCREENSAVEACTIVE = 0x0011;
        private const uint SPI_GETSCREENSAVEACTIVE = 0x0010;
        private const uint SPIF_SENDCHANGE = 0x0002;
        private const uint SPIF_UPDATEINIFILE = 0x0001;

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MONITOR_OFF = 2;
        private const int MONITOR_ON = -1;
        private const int MONITOR_STANBY = 1;

        private static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;

        /// <summary>
        /// Prevent the system from entering sleep or turning off the display.
        /// 這會保持螢幕開啟。如果要模擬螢幕保護，我們需要只保持系統運作但允許(或強制)關閉螢幕。
        /// </summary>
        /// <param name="keepDisplay">True to keep display on, False to only keep system awake.</param>
        public static void KeepAwake(bool keepDisplay = true)
        {
            EXECUTION_STATE flags = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;
            if (keepDisplay)
            {
                flags |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
            }
            SetThreadExecutionState(flags);
        }

        /// <summary>
        /// Allow the system to go to sleep normally.
        /// </summary>
        public static void RestoreDefaults()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }

        /// <summary>
        /// Turn off the monitor.
        /// </summary>
        public static void TurnOffMonitor()
        {
            // HWND_BROADCAST is 0xFFFF, or use Form handle if available. 
            // Better to use a valid window handle if possible, but typically passing a handle is needed.
            // Using -1 (HWND_BROADCAST) might work but sometimes requires privileges or specific handle.
            // Safe way: create a dummy message only if we have a handle. 
            // For now, since this is a static method, we might need a handle passed in.
            // However, many examples use GetDesktopWindow() or just Handle of the main form.
            // We will accept a handle.
        }

        public static void TurnOffMonitor(IntPtr handle)
        {
            IntPtr target = handle == IntPtr.Zero ? HWND_BROADCAST : handle;
            SendMessage(target, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
        }
        
        public static void TurnOnMonitor(IntPtr handle)
        {
            IntPtr target = handle == IntPtr.Zero ? HWND_BROADCAST : handle;
            SendMessage(target, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_ON);
        }

        /// <summary>
        /// Disable Windows screensaver to prevent it from interfering with our lock screen.
        /// </summary>
        public static void DisableWindowsScreensaver()
        {
            SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 0, IntPtr.Zero, SPIF_SENDCHANGE);
        }

        /// <summary>
        /// Re-enable Windows screensaver.
        /// </summary>
        public static void EnableWindowsScreensaver()
        {
            SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, IntPtr.Zero, SPIF_SENDCHANGE);
        }
    }
}
