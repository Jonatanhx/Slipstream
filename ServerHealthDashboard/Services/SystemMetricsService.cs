using System.Diagnostics;
using Hardware.Info;

namespace ServerHealthDashboard.Services
{
    public interface ISystemMetricsService
    {
        Task<CpuMetrics> GetCpuMetricsAsync();
        Task<MemoryMetrics> GetMemoryMetricsAsync();
        Task<SystemInfo> GetSystemInfoAsync();
    }

    public class SystemMetricsService : ISystemMetricsService
    {
        private readonly IHardwareInfo _hardwareInfo;

        public SystemMetricsService()
        {
            _hardwareInfo = new HardwareInfo();
        }
        public async Task<CpuMetrics> GetCpuMetricsAsync()
        {
            return await Task.Run(() =>
            {
                _hardwareInfo.RefreshCPUList();
                var cpu = _hardwareInfo.CpuList.FirstOrDefault();

                var numberOfCores = Environment.ProcessorCount;
                var perCoreUsage = new List<double>();
         
                if (OperatingSystem.IsWindows())
                {
                    for (int i = 0; i < numberOfCores; i++)
                    {
                        using var cpuCounter = new PerformanceCounter(
                            "Processor",
                            "% Processor Time",
                            i.ToString()
                        );
                        cpuCounter.NextValue();
                        System.Threading.Thread.Sleep(100);
                        perCoreUsage.Add(cpuCounter.NextValue());
                    }
                }

                return new CpuMetrics
                {
                    Usage = cpu?.PercentProcessorTime ?? 0,
                    Name = cpu?.Name ?? "Unknown",
                    NumberOfCores = (uint)numberOfCores,
                    PerCoreUsage = perCoreUsage,
                };
            });
        }
        public async Task<MemoryMetrics> GetMemoryMetricsAsync()
        {
            return await Task.Run(() =>
            {
                _hardwareInfo.RefreshMemoryStatus();
                var memory = _hardwareInfo.MemoryStatus;

                return new MemoryMetrics
                {
                    TotalPhysical = memory?.TotalPhysical ?? 0,
                    AvailablePhysical = memory?.AvailablePhysical ?? 0,
                    UsedPhysical = memory?.TotalPhysical - memory?.AvailablePhysical ?? 0
                };
            });
        }
        public async Task<SystemInfo> GetSystemInfoAsync()
        {
            return await Task.Run(() =>
            {
                _hardwareInfo.RefreshOperatingSystem();
                var os = _hardwareInfo.OperatingSystem;
                return new SystemInfo
                {
                    OperatingSystem = $"{os.Name} {os.VersionString}"
                };
            });
        }
    }
}

public class CpuMetrics
{
    public double Usage { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint NumberOfCores { get; set; }

    public List<double> PerCoreUsage { get; set; } = [];
}

public class MemoryMetrics
{
    public ulong TotalPhysical { get; set; }
    public ulong AvailablePhysical { get; set; }
    public ulong UsedPhysical { get; set; }

}

public class SystemInfo
{
    public string OperatingSystem { get; set; } = string.Empty;
}