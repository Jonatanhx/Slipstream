using System.Diagnostics;
using Hardware.Info;

namespace ServerHealthDashboard.Services
{
    public interface ISystemMetricsService
    {
        Task<CpuMetrics> GetCpuMetricsAsync();
        Task<MemoryMetrics> GetMemoryMetricsAsync();
        Task<SystemInfo> GetSystemInfoAsync();
        Task<List<ProcessesMetrics>> GetProcessesMetricsAsync();
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
        public async Task<List<ProcessesMetrics>> GetProcessesMetricsAsync()
        {
            return await Task.Run(() =>
            {

                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("Process metrics are only available on Windows systems.");
                }

                var result = new List<ProcessesMetrics>();
                var category = new PerformanceCounterCategory("Process");
                var instances = category.GetInstanceNames();

                foreach (var instance in instances)
                {
                    if (instance.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
                        instance.Equals("_Total", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using var idCounter = new PerformanceCounter("Process", "ID Process", instance, true);
                    int pid = (int)idCounter.NextValue();

                    if (pid == 0) continue;

                    var process = Process.GetProcessById(pid);
                    if (process == null) continue;

                    using var cpuCounter = new PerformanceCounter("Process", "% Processor Time", instance, true);
                    using var memCounter = new PerformanceCounter("Process", "Working Set - Private", instance, true);
                    using var ioCounter = new PerformanceCounter("Process", "IO Data Bytes/sec", instance, true);
                    using var threadCounter = new PerformanceCounter("Process", "Thread Count", instance, true);

                    cpuCounter.NextValue();

                    var cpuUsage = cpuCounter.NextValue() / Environment.ProcessorCount;
                    var memUsage = memCounter.NextValue() / (1024 * 1024);
                    var ioUsage = ioCounter.NextValue() / 1024;
                    var threadCount = (int)threadCounter.NextValue();

                    if (cpuUsage > 0.1 || memUsage > 5)
                    {
                        result.Add(new ProcessesMetrics
                        {
                            ProcessId = pid,
                            Name = process.ProcessName,
                            CpuUsage = Math.Round(cpuUsage, 1),
                            MemoryMB = Math.Round(memUsage, 1),
                            IoKBps = Math.Round(ioUsage, 1),
                            Threads = threadCount
                        });
                    }
                }
                return result.OrderByDescending(p => p.CpuUsage).ToList();
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

public class ProcessesMetrics
{
    public int ProcessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public double MemoryMB { get; set; }
    public double IoKBps { get; set; }
    public int Threads { get; set; }

}

public class SystemInfo
{
    public string OperatingSystem { get; set; } = string.Empty;
}