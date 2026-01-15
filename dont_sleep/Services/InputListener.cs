using System.Runtime.InteropServices;

namespace dont_sleep.Services
{
    public class InputListener
    {
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (GetLastInputInfo(ref lastInputInfo))
            {
                // Environment.TickCount is int (wraps ~24.9 days), TickCount64 is long.
                // dwTime is uint (wraps ~49.7 days).
                // Use Environment.TickCount and cast to uint for simpler subtraction safe for wrap-around 
                // as long as idle time < 49 days.
                uint envTicks = (uint)Environment.TickCount;
                uint lastInputTicks = lastInputInfo.dwTime;
                
                // Handle potential wrap-around if needed, but uint subtraction handles it naturally 
                // if TickCount also wrapped same way. 
                // Note: Environment.TickCount returns signed int, so we cast to uint.
                
                uint idleTicks = envTicks - lastInputTicks;
                return TimeSpan.FromMilliseconds(idleTicks);
            }
            return TimeSpan.Zero;
        }
    }
}
