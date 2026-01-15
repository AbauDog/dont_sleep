using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace dont_sleep.Services
{
    [SupportedOSPlatform("windows")]
    public class HardwareMonitor : IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;
        // RAM can be calculated via GC or GlobalMemoryStatusEx, but PerformanceCounter is also okay. 
        // For Total/Available, Environment or ComputerInfo is easier.
        // We need "% Committed Bytes In Use" for logic, or just calculate from Total/Available.
        // Requirement: "RAM usage > 70% ... (same as Task Manager)" -> usually means Physical Memory Load.
        // PerformanceCounter("Memory", "% Committed Bytes In Use") tracks commit charge, not just physical.
        // Task Manager "Memory" % is usually Physical Memory Load.
        // Let's use a native call (GlobalMemoryStatusEx) or VisualBasic ComputerInfo for simplicity if avail, 
        // or just PerformanceCounter for simplicity in raw C# without VB ref.
        // We will use PerformanceCounter("Memory", "% Committed Bytes In Use") as a proxy effectively, 
        // BUT "Physical Memory" is better represented by evaluating Available vs Total.
        
        // Let's use PerformanceCounter for Disk and CPU. 
        // For RAM, we'll calculate manually to match Task Manager better (Load Percentage).
        
        private readonly PerformanceCounter _diskCounter;

        public HardwareMonitor()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            
            // Initialize counters
            _cpuCounter.NextValue();
            _diskCounter.NextValue();
        }

        public (float Cpu, float Ram, float Disk) GetUsage()
        {
            // CPU: 取兩次平均值以提高準確度
            float cpu1 = _cpuCounter.NextValue();
            System.Threading.Thread.Sleep(100); // 等待 100ms 再取一次樣本
            float cpu2 = _cpuCounter.NextValue();
            float cpu = (cpu1 + cpu2) / 2;
            
            // DISK: 使用較穩定的方式計算
            float disk = _diskCounter.NextValue();
            // Cap disk at 100 because it can go higher for multiple disks
            if (disk > 100) disk = 100;

            float ram = GetPhysicalMemoryLoad();

            return (cpu, ram, disk);
        }

        private float GetPhysicalMemoryLoad()
        {
            // Use MEMORYSTATUSEX via P/Invoke for best accuracy matching TaskMgr
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return (float)memStatus.dwMemoryLoad;
            }
            return 0;
        }

        public List<(string Name, long MemoryBytes)> GetTop3MemoryProcesses()
        {
            try
            {
                var processes = Process.GetProcesses();
                // 使用 PrivateMemorySize64 (Commit Size) 更接近工作管理員的「記憶體」欄位
                // WorkingSet64 = 實體記憶體（包含共享）
                // PrivateMemorySize64 = 私有認可記憶體（更準確反映程式實際使用量）
                var top3 = processes
                    .OrderByDescending(p => p.PrivateMemorySize64)
                    .Take(3)
                    .Select(p => (p.ProcessName + " (" + p.Threads.Count + ")", p.PrivateMemorySize64))
                    .ToList();
                return top3;
            }
            catch
            {
                return new List<(string, long)>();
            }
        }

        public void Dispose()
        {
            _cpuCounter?.Dispose();
            _diskCounter?.Dispose();
        }

        // P/Invoke for GlobalMemoryStatusEx
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
    }
}
