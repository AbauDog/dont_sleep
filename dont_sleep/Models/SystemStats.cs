namespace dont_sleep.Models
{
    public class SystemStats
    {
        public float CpuUsage { get; set; }
        public float RamUsagePercent { get; set; } // Load %
        public float DiskUsage { get; set; }
        public List<(string Name, long MemoryBytes)> TopProcesses { get; set; } = new();

        public override string ToString()
        {
            return $"C{CpuUsage:F0}% R{RamUsagePercent:F0}% D{DiskUsage:F0}%";
        }
    }
}
