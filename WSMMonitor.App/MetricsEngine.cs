using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using WSMMonitor.Plugins;

namespace WSMMonitor;

/// <summary>Collects extended Windows Server / Win11 metrics.</summary>
public sealed class MetricsEngine : IDisposable
{
    private PerformanceCounter? _cpu;
    private PerformanceCounter? _queue;
    private PerformanceCounter? _commit;
    private PerformanceCounter? _commitLimit;
    private readonly ConcurrentDictionary<string, (long rx, long tx)> _netPrev = new();
    private long _lastNetSampleTick;

    private readonly List<DiskPerfCounters> _diskPerf = new();
    private bool _diskPerfPrimed;
    private readonly List<NetIfCounters> _netIfPerf = new();
    private bool _netIfPrimed;

    private PerformanceCounter? _memNonPaged;
    private PerformanceCounter? _memAvailableBytes;
    private PerformanceCounter? _memCacheBytes;
    private PerformanceCounter? _memModifiedPageList;
    private PerformanceCounter? _memStandbyCache;
    private PerformanceCounter? _memCompressed;

    private readonly List<PerformanceCounter> _coreCounters = new();
    private readonly PluginPipeline _pluginPipeline;
    private readonly LibreHardwareThermalCollector? _libreHardware;

    private static readonly string[] CriticalServices =
    [
        "MSSQLSERVER", "SQLSERVERAGENT", "MSSQL$SQLEXPRESS",
        "W3SVC", "WAS", "FTPSVC",
        "NTDS", "DNS", "DHCPServer", "Netlogon", "NTFRS", "DFS",
        "RemoteAccess", "SstpSvc", "LanmanServer", "Spooler", "TermService",
        "RpcSs", "LSM", "Schedule", "Winmgmt"
    ];

    public MetricsEngine(PluginPipeline? pluginPipeline = null, WsmAppOptions? appOptions = null)
    {
        _pluginPipeline = pluginPipeline ?? new PluginPipeline();
        var o = appOptions ?? WsmConfiguration.Current;
        _libreHardware = o.LibreHardwareMonitor.Enabled
            ? new LibreHardwareThermalCollector(o.LibreHardwareMonitor)
            : null;
        try
        {
            _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _queue = new PerformanceCounter("System", "Processor Queue Length", true);
        }
        catch { /* */ }
        try
        {
            _commit = new PerformanceCounter("Memory", "Committed Bytes", true);
            _commitLimit = new PerformanceCounter("Memory", "Commit Limit", true);
        }
        catch { /* */ }
        InitMemoryCounters();
        TryInitCpuCoreCounters();
    }

    private void InitMemoryCounters()
    {
        TryMemCounter("Memory", "Pool Nonpaged Bytes", ref _memNonPaged);
        TryMemCounter("Memory", "Available Bytes", ref _memAvailableBytes);
        TryMemCounter("Memory", "Cache Bytes", ref _memCacheBytes);
        TryMemCounter("Memory", "Modified Page List Bytes", ref _memModifiedPageList);
        TryMemCounter("Memory", "Standby Cache Normal Priority", ref _memStandbyCache);
        TryMemCounter("Memory", "Compressed Bytes", ref _memCompressed);
        if (_memCompressed == null)
            TryMemCounter("Memory", "Memory Compression", ref _memCompressed);
    }

    private static void TryMemCounter(string cat, string name, ref PerformanceCounter? field)
    {
        if (field != null) return;
        try { field = new PerformanceCounter(cat, name, true); }
        catch { /* */ }
    }

    private void TryInitCpuCoreCounters()
    {
        if (_coreCounters.Count > 0) return;
        try
        {
            if (PerformanceCounterCategory.Exists("Processor Information"))
            {
                foreach (var inst in new PerformanceCounterCategory("Processor Information").GetInstanceNames())
                {
                    if (inst.Equals("_Total", StringComparison.OrdinalIgnoreCase)) continue;
                    if (inst.EndsWith(",_Total", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        _coreCounters.Add(new PerformanceCounter("Processor Information", "% Processor Time", inst, true));
                    }
                    catch { /* */ }
                }
            }
        }
        catch { /* */ }
        if (_coreCounters.Count == 0)
        {
            try
            {
                foreach (var inst in new PerformanceCounterCategory("Processor").GetInstanceNames())
                {
                    if (inst.Equals("_Total", StringComparison.OrdinalIgnoreCase)) continue;
                    try { _coreCounters.Add(new PerformanceCounter("Processor", "% Processor Time", inst, true)); }
                    catch { /* */ }
                }
            }
            catch { /* */ }
        }
    }

    private void EnsureDiskPerfCounters()
    {
        if (_diskPerf.Count > 0) return;
        lock (_diskPerf)
        {
            if (_diskPerf.Count > 0) return;
            try
            {
                if (!PerformanceCounterCategory.Exists("PhysicalDisk")) return;
                var cat = new PerformanceCounterCategory("PhysicalDisk");
                foreach (var inst in cat.GetInstanceNames())
                {
                    if (string.Equals(inst, "_Total", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        _diskPerf.Add(new DiskPerfCounters(inst));
                    }
                    catch { /* */ }
                }
            }
            catch { /* */ }
        }
    }

    private void EnsureNetIfCounters()
    {
        if (_netIfPerf.Count > 0) return;
        lock (_netIfPerf)
        {
            if (_netIfPerf.Count > 0) return;
            try
            {
                if (!PerformanceCounterCategory.Exists("Network Interface")) return;
                foreach (var inst in new PerformanceCounterCategory("Network Interface").GetInstanceNames())
                {
                    try { _netIfPerf.Add(new NetIfCounters(inst)); }
                    catch { /* */ }
                }
            }
            catch { /* */ }
        }
    }

    public void Dispose()
    {
        _cpu?.Dispose();
        _queue?.Dispose();
        _commit?.Dispose();
        _commitLimit?.Dispose();
        _memNonPaged?.Dispose();
        _memAvailableBytes?.Dispose();
        _memCacheBytes?.Dispose();
        _memModifiedPageList?.Dispose();
        _memStandbyCache?.Dispose();
        _memCompressed?.Dispose();
        foreach (var d in _diskPerf) d.Dispose();
        _diskPerf.Clear();
        foreach (var n in _netIfPerf) n.Dispose();
        _netIfPerf.Clear();
        foreach (var c in _coreCounters) c.Dispose();
        _coreCounters.Clear();
        try
        {
            _libreHardware?.Dispose();
        }
        catch
        {
            /* */
        }
    }

    public MetricsDto Collect()
    {
        var ts = DateTimeOffset.Now.ToString("o");
        int cores = Environment.ProcessorCount;

        EnsureDiskPerfCounters();
        EnsureNetIfCounters();
        TryInitCpuCoreCounters();

        double? cpuPct = ReadCpu();
        double? qLen = ReadQueue();
        var mem = ReadMemory();
        var memCounters = ReadMemoryCounters();
        var cpuCores = ReadCpuCores();
        var cpuPkgs = ReadCpuPackagesWmi();

        var disks = ReadDisks();
        var phys = ReadPhysicalDisksWmi();
        var diskPerf = ReadDiskPerfSamples();
        var smart = ReadStorageReliabilityWmi();
        var net = ReadNetwork();
        var netErr = ReadNetworkErrors();
        var tcp = ReadTcpStates();
        var thermal = ReadThermal();
        thermal = AppendLibreHardwareThermal(thermal);
        var (topCpu, topMem) = ReadProcesses(cores);
        var services = ReadCriticalServices();
        var events = ReadRecentCriticalEvents();
        var (securityEvents, detections, pluginHealth) = _pluginPipeline.Collect();
        var alerts = BuildAlerts(cpuPct, qLen, mem, memCounters, disks, phys, diskPerf, smart, services, thermal, events, netErr, detections);
        var healthScore = CalculateHealthScore(cpuPct, qLen, mem, disks, perf: diskPerf, netErr, services, alerts, detections, out var healthBreakdown);
        var healthInsights = BuildHealthScoreInsights(cpuPct, qLen, mem, disks, diskPerf, netErr, services, alerts, detections, thermal, pluginHealth);

        return new MetricsDto(ts, cpuPct, cores, qLen, mem, memCounters, cpuCores, cpuPkgs,
            disks, phys, diskPerf, smart, net, netErr, tcp, thermal, topCpu, topMem, services, events, securityEvents, detections, pluginHealth, healthScore, healthBreakdown, healthInsights, alerts);
    }

    private MemoryCountersRow? ReadMemoryCounters()
    {
        double? np = ReadMb(_memNonPaged);
        double? av = ReadMb(_memAvailableBytes);
        double? ca = ReadMb(_memCacheBytes);
        double? mod = ReadMb(_memModifiedPageList);
        double? st = ReadMb(_memStandbyCache);
        double? comp = ReadMb(_memCompressed);
        if (np == null && av == null && ca == null && mod == null && st == null && comp == null)
            return null;
        return new MemoryCountersRow(np, av, ca, mod, st, comp);
    }

    private static double? ReadMb(PerformanceCounter? c)
    {
        if (c == null) return null;
        try { return Math.Round(c.NextValue() / 1_048_576.0, 1); }
        catch { return null; }
    }

    private List<CpuCoreRow> ReadCpuCores()
    {
        var list = new List<CpuCoreRow>();
        foreach (var c in _coreCounters)
        {
            try
            {
                _ = c.NextValue();
            }
            catch { /* */ }
        }
        if (_coreCounters.Count > 0)
            Thread.Sleep(100);
        foreach (var c in _coreCounters)
        {
            try
            {
                var v = Math.Round(c.NextValue(), 1);
                list.Add(new CpuCoreRow(c.InstanceName, v));
            }
            catch { /* */ }
        }
        return list;
    }

    private static List<CpuPackageRow> ReadCpuPackagesWmi()
    {
        var list = new List<CpuPackageRow>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT DeviceID, Name, LoadPercentage, CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject o in s.Get())
            {
                int? load = o["LoadPercentage"] is null ? null : Convert.ToInt32(o["LoadPercentage"]);
                uint? cur = o["CurrentClockSpeed"] is null ? null : Convert.ToUInt32(o["CurrentClockSpeed"]);
                uint? mx = o["MaxClockSpeed"] is null ? null : Convert.ToUInt32(o["MaxClockSpeed"]);
                list.Add(new CpuPackageRow(
                    o["DeviceID"]?.ToString() ?? "",
                    o["Name"]?.ToString() ?? "",
                    load,
                    cur is > 0 ? cur : null,
                    mx is > 0 ? mx : null));
            }
        }
        catch { /* */ }
        return list;
    }

    private static List<PhysicalDiskRow> ReadPhysicalDisksWmi()
    {
        var list = new List<PhysicalDiskRow>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            var q = new ObjectQuery("SELECT FriendlyName, MediaType, HealthStatus, OperationalStatus FROM MSFT_PhysicalDisk");
            using var s = new ManagementObjectSearcher(scope, q);
            foreach (ManagementObject o in s.Get())
            {
                list.Add(new PhysicalDiskRow(
                    o["FriendlyName"]?.ToString() ?? "?",
                    o["MediaType"]?.ToString() ?? "",
                    o["HealthStatus"]?.ToString() ?? "",
                    o["OperationalStatus"]?.ToString() ?? ""));
            }
        }
        catch { /* */ }
        return list;
    }

    private List<DiskPerfRow> ReadDiskPerfSamples()
    {
        var rows = new List<DiskPerfRow>();
        if (_diskPerf.Count == 0) return rows;

        if (!_diskPerfPrimed)
        {
            foreach (var d in _diskPerf)
            {
                try { d.Prime(); } catch { /* */ }
            }
            Thread.Sleep(150);
            _diskPerfPrimed = true;
        }

        foreach (var d in _diskPerf)
        {
            try
            {
                rows.Add(d.Sample());
            }
            catch
            {
                rows.Add(new DiskPerfRow(d.Instance, null, null, null, null, null));
            }
        }
        return rows;
    }

    private static List<DiskSmartRow> ReadStorageReliabilityWmi()
    {
        var list = new List<DiskSmartRow>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            var q = new ObjectQuery("SELECT * FROM MSFT_StorageReliabilityCounter");
            using var s = new ManagementObjectSearcher(scope, q);
            foreach (ManagementObject o in s.Get())
            {
                string? disk = o["DeviceId"]?.ToString() ?? o["FriendlyName"]?.ToString() ?? "?";
                int? wear = FirstIntProp(o, "Wear", "WearLevel", "WearPercent", "PercentageWearRemaining");
                int? temp = FirstIntProp(o, "Temperature", "TemperatureCelsius");
                temp = NormalizeStorageTemp(temp);
                long? rr = FirstLongProp(o, "ReadErrorsTotal");
                long? wr = FirstLongProp(o, "WriteErrorsTotal");
                list.Add(new DiskSmartRow(disk!, wear, temp, rr, wr));
            }
        }
        catch { /* optional */ }
        return list;
    }

    private static int? FirstIntProp(ManagementObject o, params string[] names)
    {
        foreach (var n in names)
        {
            if (o[n] == null) continue;
            try { return Convert.ToInt32(o[n]); }
            catch { /* */ }
        }
        return null;
    }

    private static long? FirstLongProp(ManagementObject o, params string[] names)
    {
        foreach (var n in names)
        {
            if (o[n] == null) continue;
            try { return Convert.ToInt64(o[n]); }
            catch { /* */ }
        }
        return null;
    }

    /// <summary>WMI often stores temperature in tenths of Kelvin (e.g. 2982 ≈ 25°C).</summary>
    private static int? NormalizeStorageTemp(int? raw)
    {
        if (raw is null or <= 0) return null;
        int v = raw.Value;
        if (v > 1000)
            return (int)Math.Round(v / 10.0 - 273.15, MidpointRounding.AwayFromZero);
        if (v > 130)
            return null;
        return v;
    }

    private List<NetErrorRow> ReadNetworkErrors()
    {
        var rows = new List<NetErrorRow>();
        if (_netIfPerf.Count == 0) return rows;

        if (!_netIfPrimed)
        {
            foreach (var n in _netIfPerf) { try { n.Prime(); } catch { /* */ } }
            Thread.Sleep(80);
            _netIfPrimed = true;
        }
        foreach (var n in _netIfPerf)
        {
            try
            {
                rows.Add(n.Sample());
            }
            catch { /* */ }
        }
        return rows;
    }

    private static List<TcpStateRow> ReadTcpStates()
    {
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var tcp = props.GetActiveTcpConnections();
            return tcp.GroupBy(c => c.State.ToString())
                .Select(g => new TcpStateRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<ServiceRow> ReadCriticalServices()
    {
        var list = new List<ServiceRow>();
        foreach (var name in CriticalServices)
        {
            try
            {
                using var sc = new ServiceController(name);
                var dn = sc.DisplayName ?? name;
                try { sc.Refresh(); } catch { /* */ }
                list.Add(new ServiceRow(name, dn, sc.Status.ToString()));
            }
            catch
            {
                list.Add(new ServiceRow(name, name, "Not installed"));
            }
        }
        return list;
    }

    private static List<EventRow> ReadRecentCriticalEvents()
    {
        var list = new List<EventRow>();
        foreach (var logName in new[] { "System", "Application" })
        {
            try
            {
                var q = new EventLogQuery(logName, PathType.LogName, "*[System[(Level=1 or Level=2)]]")
                {
                    ReverseDirection = true
                };
                using var reader = new EventLogReader(q);
                for (int i = 0; i < 8; i++)
                {
                    using var e = reader.ReadEvent();
                    if (e == null) break;
                    string level = (e.Level ?? 0) switch
                    {
                        1 => "Critical",
                        2 => "Error",
                        _ => e.Level?.ToString() ?? ""
                    };
                    string msg;
                    try { msg = e.FormatDescription() ?? ""; }
                    catch { msg = "(unable to format)"; }
                    if (!string.IsNullOrEmpty(msg) && msg.Length > 180)
                        msg = msg[..177] + "...";
                    list.Add(new EventRow(
                        e.TimeCreated?.ToString("u") ?? "",
                        logName,
                        level,
                        e.ProviderName ?? "",
                        msg ?? "",
                        e.RecordId,
                        e.Id));
                }
            }
            catch { /* */ }
        }
        return list.OrderByDescending(e => e.Time).Take(20).ToList();
    }

    private double? ReadCpu()
    {
        if (_cpu == null) return null;
        try
        {
            _ = _cpu.NextValue();
            Thread.Sleep(120);
            return Math.Round(_cpu.NextValue(), 1);
        }
        catch { return null; }
    }

    private double? ReadQueue()
    {
        if (_queue == null) return null;
        try { return Math.Round(_queue.NextValue(), 2); }
        catch { return null; }
    }

    private MemInfo ReadMemory()
    {
        if (GlobalMemoryStatus(out var m))
        {
            ulong total = m.ullTotalPhys / (1024 * 1024);
            ulong avail = m.ullAvailPhys / (1024 * 1024);
            ulong used = total - avail;
            double pct = total > 0 ? 100.0 * used / total : 0;
            double? commitPct = ReadCommitPct();
            return new MemInfo(total, used, avail, Math.Round(pct, 1), commitPct);
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject o in searcher.Get())
            {
                ulong totalKb = Convert.ToUInt64(o["TotalVisibleMemorySize"]);
                ulong freeKb = Convert.ToUInt64(o["FreePhysicalMemory"]);
                ulong total = totalKb / 1024;
                ulong free = freeKb / 1024;
                ulong used = total - free;
                double pct = total > 0 ? 100.0 * used / total : 0;
                return new MemInfo(total, used, free, Math.Round(pct, 1), ReadCommitPct());
            }
        }
        catch { /* */ }

        return new MemInfo(0, 0, 0, 0, null);
    }

    private double? ReadCommitPct()
    {
        if (_commit == null || _commitLimit == null) return null;
        try
        {
            double c = _commit.NextValue();
            double lim = _commitLimit.NextValue();
            if (lim <= 0) return null;
            return Math.Round(100.0 * c / lim, 1);
        }
        catch { return null; }
    }

    private static List<DiskRow> ReadDisks()
    {
        var list = new List<DiskRow>();
        foreach (var di in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            try
            {
                long t = di.TotalSize;
                long f = di.AvailableFreeSpace;
                if (t <= 0) continue;
                double fp = 100.0 * f / t;
                list.Add(new DiskRow(di.Name, di.VolumeLabel ?? "", Math.Round(f / 1_073_741_824.0, 1), Math.Round(t / 1_073_741_824.0, 1), Math.Round(fp, 1)));
            }
            catch { /* */ }
        }
        return list;
    }

    private List<NetRow> ReadNetwork()
    {
        var nowTick = Environment.TickCount64;
        double dtSec = 3.0;
        if (_lastNetSampleTick > 0)
        {
            dtSec = (nowTick - _lastNetSampleTick) / 1000.0;
            if (dtSec < 0.15) dtSec = 0.15;
        }
        _lastNetSampleTick = nowTick;

        var list = new List<NetRow>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var st = ni.GetIPv4Statistics();
                long rx = st.BytesReceived;
                long tx = st.BytesSent;
                string name = ni.Name;
                string speed = ni.Speed > 0 ? $"{ni.Speed / 1_000_000} Mbps" : "-";

                string rxS = "-", txS = "-";
                if (_netPrev.TryGetValue(name, out var prev))
                {
                    rxS = FormatRate((rx - prev.rx) / dtSec);
                    txS = FormatRate((tx - prev.tx) / dtSec);
                }
                _netPrev[name] = (rx, tx);

                list.Add(new NetRow(name, speed, rxS, txS));
            }
            catch { /* */ }
        }
        return list;
    }

    private static string FormatRate(double bps)
    {
        if (bps < 0) bps = 0;
        if (bps >= 1_048_576) return $"{bps / 1_048_576:F1} MiB/s";
        if (bps >= 1024) return $"{bps / 1024:F1} KiB/s";
        return $"{bps:F0} B/s";
    }

    private static List<TempRow> ReadThermal()
    {
        var list = new List<TempRow>();
        try
        {
            using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject o in s.Get())
            {
                double raw = Convert.ToDouble(o["CurrentTemperature"]);
                double k = raw / 10.0;
                if (k > 400) k /= 10.0;
                double c = k - 273.15;
                if (c is > -50 and < 150)
                    list.Add(new TempRow("ACPI", "ThermalZone", Math.Round(c, 1)));
            }
        }
        catch { /* */ }
        return list;
    }

    private List<TempRow> AppendLibreHardwareThermal(List<TempRow> wmiThermal)
    {
        if (_libreHardware == null)
            return wmiThermal;
        try
        {
            var extra = _libreHardware.CollectTemperatures();
            if (extra.Count == 0)
                return wmiThermal;
            var merged = new List<TempRow>(wmiThermal.Count + extra.Count);
            merged.AddRange(wmiThermal);
            merged.AddRange(extra);
            return merged;
        }
        catch
        {
            return wmiThermal;
        }
    }

    private (List<ProcRow> topCpu, List<ProcRow> topMem) ReadProcesses(int cores)
    {
        var topCpu = new List<ProcRow>();
        var topMem = new List<ProcRow>();
        try
        {
            var snap1 = new Dictionary<int, TimeSpan>();
            foreach (var p in Process.GetProcesses())
            {
                try { snap1[p.Id] = p.TotalProcessorTime; }
                catch { /* */ }
            }
            Thread.Sleep(800);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (!snap1.TryGetValue(p.Id, out var t0)) continue;
                    var dt = p.TotalProcessorTime - t0;
                    double sec = dt.TotalSeconds;
                    double pct = Math.Min(100 * cores, Math.Round(100.0 * sec / (0.8 * Math.Max(1, cores)), 1));
                    topCpu.Add(new ProcRow(p.ProcessName, p.Id, pct, Math.Round(p.WorkingSet64 / 1_048_576.0, 1)));
                }
                catch { /* */ }
            }
            topCpu = topCpu.OrderByDescending(x => x.CpuPctApprox).Take(12).ToList();

            topMem = Process.GetProcesses()
                .Select(p =>
                {
                    try { return new ProcRow(p.ProcessName, p.Id, 0, Math.Round(p.WorkingSet64 / 1_048_576.0, 1)); }
                    catch { return null; }
                })
                .Where(x => x != null)
                .Cast<ProcRow>()
                .OrderByDescending(x => x.WsMb)
                .Take(10)
                .ToList();
        }
        catch { /* */ }
        return (topCpu, topMem);
    }

    private const string SysmonOperationalLog = "Microsoft-Windows-Sysmon/Operational";

    private static AlertRefDto MetricRef(string code) => new("metric", null, null, null, null, code);

    private static List<AlertRow> BuildAlerts(
        double? cpu, double? queue, MemInfo mem, MemoryCountersRow? memCtr,
        IReadOnlyList<DiskRow> disks, IReadOnlyList<PhysicalDiskRow> phys,
        IReadOnlyList<DiskPerfRow> perf, IReadOnlyList<DiskSmartRow> smart,
        IReadOnlyList<ServiceRow> services, IReadOnlyList<TempRow> temps,
        IReadOnlyList<EventRow> events, IReadOnlyList<NetErrorRow> netErr,
        IReadOnlyList<DetectionRow> detections)
    {
        var a = new List<AlertRow>();
        if (cpu is >= 95) a.Add(new AlertRow("critical", "CPU", $"CPU load: {cpu:F1}%", MetricRef("CPU")));
        else if (cpu is >= 85) a.Add(new AlertRow("warning", "CPU", $"High CPU: {cpu:F1}%", MetricRef("CPU")));

        if (queue is >= 8) a.Add(new AlertRow("warning", "QUEUE", $"Processor queue: {queue}", MetricRef("QUEUE")));

        if (mem.UsedPct >= 92) a.Add(new AlertRow("critical", "RAM", $"Memory: {mem.UsedPct:F1}% used", MetricRef("RAM")));
        else if (mem.UsedPct >= 85) a.Add(new AlertRow("warning", "RAM", $"Memory: {mem.UsedPct:F1}% used", MetricRef("RAM")));

        if (mem.CommitPct is >= 98) a.Add(new AlertRow("critical", "COMMIT", $"Commit: {mem.CommitPct:F1}%", MetricRef("COMMIT")));
        else if (mem.CommitPct is >= 90) a.Add(new AlertRow("warning", "COMMIT", $"Commit: {mem.CommitPct:F1}%", MetricRef("COMMIT")));

        if (memCtr?.NonPagedPoolMiB is >= 2048)
            a.Add(new AlertRow("warning", "NONPAGED", $"NonPaged pool: {memCtr.NonPagedPoolMiB:F0} MiB (check drivers)", MetricRef("NONPAGED")));

        foreach (var d in disks)
        {
            if (d.FreePct <= 5) a.Add(new AlertRow("critical", "DISK", $"{d.DeviceId} free {d.FreePct:F1}%", MetricRef("DISK")));
            else if (d.FreePct <= 12) a.Add(new AlertRow("warning", "DISK", $"{d.DeviceId} low free space {d.FreePct:F1}%", MetricRef("DISK")));
        }

        foreach (var p in phys)
        {
            if (!string.IsNullOrEmpty(p.HealthStatus) && !p.HealthStatus.Contains("Healthy", StringComparison.OrdinalIgnoreCase))
                a.Add(new AlertRow("critical", "DISK_HW", $"{p.FriendlyName} Health: {p.HealthStatus}", MetricRef("DISK_HW")));
            if (!string.IsNullOrEmpty(p.OperationalStatus) && !p.OperationalStatus.Contains("OK", StringComparison.OrdinalIgnoreCase)
                && !p.OperationalStatus.Contains("Online", StringComparison.OrdinalIgnoreCase))
                a.Add(new AlertRow("warning", "DISK_OP", $"{p.FriendlyName} Operational: {p.OperationalStatus}", MetricRef("DISK_OP")));
        }

        foreach (var x in perf)
        {
            if (x.ReadLatencyMs is > 80) a.Add(new AlertRow("warning", "DISK_LAT", $"{x.Instance} read latency high: {x.ReadLatencyMs:F1} ms", MetricRef("DISK_LAT")));
            if (x.WriteLatencyMs is > 80) a.Add(new AlertRow("warning", "DISK_LAT", $"{x.Instance} write latency high: {x.WriteLatencyMs:F1} ms", MetricRef("DISK_LAT")));
            if (x.QueueLength is >= 8) a.Add(new AlertRow("warning", "DISK_Q", $"{x.Instance} disk queue: {x.QueueLength}", MetricRef("DISK_Q")));
        }

        foreach (var z in smart)
        {
            if (z.WearPercent is >= 95) a.Add(new AlertRow("critical", "SMART", $"{z.Disk} wear: {z.WearPercent}%", MetricRef("SMART")));
            else if (z.WearPercent is >= 85) a.Add(new AlertRow("warning", "SMART", $"{z.Disk} wear: {z.WearPercent}%", MetricRef("SMART")));
            if (z.TemperatureC is >= 65) a.Add(new AlertRow("warning", "DISK_TEMP", $"{z.Disk} {z.TemperatureC} C", MetricRef("DISK_TEMP")));
        }

        foreach (var n in netErr)
        {
            var rx = n.RxErrorsPerSec ?? 0;
            var tx = n.TxErrorsPerSec ?? 0;
            if (rx > 0.01 || tx > 0.01)
                a.Add(new AlertRow("warning", "NET_ERR", $"{Short(n.CounterInstance)} rxErr/s={rx:F2} txErr/s={tx:F2}", MetricRef("NET_ERR")));
        }

        foreach (var svc in services)
        {
            if (svc.Status is "Not installed") continue;
            if (svc.Status != "Running")
                a.Add(new AlertRow("warning", "SVC", $"Service {svc.ServiceName} is {svc.Status}", MetricRef("SVC")));
        }

        foreach (var t in temps)
        {
            if (t.Celsius >= 90) a.Add(new AlertRow("critical", "TEMP", $"{t.Source}/{t.Name}: {t.Celsius:F0} C", MetricRef("TEMP")));
            else if (t.Celsius >= 80) a.Add(new AlertRow("warning", "TEMP", $"{t.Source}/{t.Name}: {t.Celsius:F0} C", MetricRef("TEMP")));
        }

        foreach (var ev in events.Where(e => e.Level is "Critical" or "Error").Take(4))
        {
            var sev = ev.Level == "Critical" ? "critical" : "warning";
            var evRef = ev.RecordId is long rid
                ? new AlertRefDto("event", ev.Log, rid, null, null, null)
                : new AlertRefDto("event", ev.Log, null, null, null, null);
            a.Add(new AlertRow(sev, "EVT", $"{ev.Log} {ev.Time}: {ev.Message}", evRef));
        }

        foreach (var d in detections.Take(10))
        {
            var sev = d.Severity.Contains("high", StringComparison.OrdinalIgnoreCase) || d.Severity.Contains("critical", StringComparison.OrdinalIgnoreCase)
                ? "critical"
                : "warning";
            var secRef = new AlertRefDto("security", d.RecordId != null ? SysmonOperationalLog : null, d.RecordId, d.RuleId, d.EventTime, null);
            a.Add(new AlertRow(sev, "SEC", $"{d.Title} ({d.RuleId}) at {d.EventTime}", secRef));
        }

        return a;
    }

    private static int CalculateHealthScore(
        double? cpu,
        double? queue,
        MemInfo mem,
        IReadOnlyList<DiskRow> disks,
        IReadOnlyList<DiskPerfRow> perf,
        IReadOnlyList<NetErrorRow> netErr,
        IReadOnlyList<ServiceRow> services,
        IReadOnlyList<AlertRow> alerts,
        IReadOnlyList<DetectionRow> detections,
        out List<HealthFactorRow> breakdown)
    {
        breakdown = [];
        int score = 100;

        if (cpu is > 80 and <= 90)
        {
            score -= 8;
            breakdown.Add(new HealthFactorRow("CPU", 8, 16, $"Load {cpu:F1}% (moderate band)"));
        }
        else if (cpu is > 90)
        {
            score -= 16;
            breakdown.Add(new HealthFactorRow("CPU", 16, 16, $"Load {cpu:F1}% (high)"));
        }

        if (mem.UsedPct > 85 && mem.UsedPct <= 92)
        {
            score -= 10;
            breakdown.Add(new HealthFactorRow("Memory", 10, 18, $"Used {mem.UsedPct:F1}%"));
        }
        else if (mem.UsedPct > 92)
        {
            score -= 18;
            breakdown.Add(new HealthFactorRow("Memory", 18, 18, $"Used {mem.UsedPct:F1}%"));
        }

        if (queue is > 4 and <= 8)
        {
            score -= 5;
            breakdown.Add(new HealthFactorRow("CPU queue", 5, 10, $"Queue length {queue}"));
        }
        else if (queue is > 8)
        {
            score -= 10;
            breakdown.Add(new HealthFactorRow("CPU queue", 10, 10, $"Queue length {queue}"));
        }

        var diskPen = 0;
        var diskDetail = new List<string>();
        foreach (var d in disks)
        {
            if (d.FreePct <= 12)
            {
                diskPen += 6;
                diskDetail.Add($"{d.DeviceId} ≤12% free");
            }
            if (d.FreePct <= 5)
            {
                diskPen += 8;
                diskDetail.Add($"{d.DeviceId} ≤5% free");
            }
        }
        if (diskPen > 0)
            breakdown.Add(new HealthFactorRow("Disk space", diskPen, 999, string.Join("; ", diskDetail)));

        score -= diskPen;

        var latPen = 0;
        var latParts = new List<string>();
        foreach (var p in perf)
        {
            if (p.ReadLatencyMs is > 80 || p.WriteLatencyMs is > 80)
            {
                latPen += 4;
                latParts.Add($"{p.Instance} latency");
            }
            if (p.QueueLength is >= 8)
            {
                latPen += 4;
                latParts.Add($"{p.Instance} Q≥8");
            }
        }
        if (latPen > 0)
            breakdown.Add(new HealthFactorRow("Disk I/O", latPen, 999, string.Join("; ", latParts)));
        score -= latPen;

        var netPen = 0;
        var netBad = new List<string>();
        foreach (var n in netErr)
        {
            var hasErr = (n.RxErrorsPerSec ?? 0) > 0.01 || (n.TxErrorsPerSec ?? 0) > 0.01;
            if (hasErr)
            {
                netPen += 4;
                netBad.Add(Short(n.CounterInstance));
            }
        }
        if (netPen > 0)
            breakdown.Add(new HealthFactorRow("Network errors", netPen, 999, string.Join("; ", netBad)));
        score -= netPen;

        int svcStops = services.Count(s => s.Status != "Running" && s.Status != "Not installed");
        var svcPen = Math.Min(20, svcStops * 3);
        if (svcPen > 0)
        {
            score -= svcPen;
            breakdown.Add(new HealthFactorRow("Services", svcPen, 20, $"{svcStops} critical service(s) not running"));
        }

        int critical = alerts.Count(a => a.Severity == "critical");
        int warning = alerts.Count(a => a.Severity == "warning");
        var alertPen = Math.Min(25, critical * 6 + warning * 2);
        if (alertPen > 0)
        {
            score -= alertPen;
            breakdown.Add(new HealthFactorRow("Alerts", alertPen, 25, $"{critical} critical, {warning} warning"));
        }

        int secCritical = detections.Count(d => d.Severity.Contains("high", StringComparison.OrdinalIgnoreCase) || d.Severity.Contains("critical", StringComparison.OrdinalIgnoreCase));
        int secWarn = detections.Count - secCritical;
        var secPen = Math.Min(30, secCritical * 5 + secWarn);
        if (secPen > 0)
        {
            score -= secPen;
            breakdown.Add(new HealthFactorRow("Security detections", secPen, 30, $"{secCritical} high/critical rules, {secWarn} other"));
        }

        return Math.Clamp(score, 1, 100);
    }

    private static List<HealthScoreInsightRow> BuildHealthScoreInsights(
        double? cpu,
        double? queue,
        MemInfo mem,
        IReadOnlyList<DiskRow> disks,
        IReadOnlyList<DiskPerfRow> perf,
        IReadOnlyList<NetErrorRow> netErr,
        IReadOnlyList<ServiceRow> services,
        IReadOnlyList<AlertRow> alerts,
        IReadOnlyList<DetectionRow> detections,
        IReadOnlyList<TempRow> thermal,
        IReadOnlyList<PluginHealthRow> pluginHealth)
    {
        var rows = new List<HealthScoreInsightRow>();

        if (cpu is > 90) rows.Add(new HealthScoreInsightRow("CPU", $"{cpu:F1}% load — critical band for score", "bad"));
        else if (cpu is > 80) rows.Add(new HealthScoreInsightRow("CPU", $"{cpu:F1}% load — elevated (score penalty zone)", "warning"));
        else rows.Add(new HealthScoreInsightRow("CPU", cpu.HasValue ? $"{cpu:F1}% — within normal range for score" : "n/a", "good"));

        if (mem.UsedPct > 92) rows.Add(new HealthScoreInsightRow("Memory", $"{mem.UsedPct:F1}% used — critical pressure", "bad"));
        else if (mem.UsedPct > 85) rows.Add(new HealthScoreInsightRow("Memory", $"{mem.UsedPct:F1}% used — elevated", "warning"));
        else rows.Add(new HealthScoreInsightRow("Memory", $"{mem.UsedPct:F1}% used — OK", "good"));

        if (queue is > 8) rows.Add(new HealthScoreInsightRow("CPU queue", $"Length {queue:F1} — high contention", "bad"));
        else if (queue is > 4) rows.Add(new HealthScoreInsightRow("CPU queue", $"Length {queue:F1} — elevated", "warning"));
        else rows.Add(new HealthScoreInsightRow("CPU queue", queue.HasValue ? $"{queue:F1} — OK" : "n/a", "good"));

        var diskCrit = disks.Any(d => d.FreePct <= 5);
        var diskWarn = disks.Any(d => d.FreePct <= 12 && d.FreePct > 5);
        if (diskCrit) rows.Add(new HealthScoreInsightRow("Disk space", "One or more volumes at ≤5% free — strong score impact", "bad"));
        else if (diskWarn) rows.Add(new HealthScoreInsightRow("Disk space", "Volume(s) at ≤12% free — score penalty", "warning"));
        else rows.Add(new HealthScoreInsightRow("Disk space", "Free space OK on monitored volumes", "good"));

        int ioStress = 0;
        foreach (var p in perf)
        {
            if (p.ReadLatencyMs is > 80 || p.WriteLatencyMs is > 80) ioStress++;
            if (p.QueueLength is >= 8) ioStress++;
        }
        if (ioStress >= 4) rows.Add(new HealthScoreInsightRow("Disk I/O", "Multiple instances with high latency or queue — score impact", "bad"));
        else if (ioStress > 0) rows.Add(new HealthScoreInsightRow("Disk I/O", "Some disks show high latency or queue length", "warning"));
        else rows.Add(new HealthScoreInsightRow("Disk I/O", "Read/write latency and queues in safe band", "good"));

        var netBad = netErr.Count(n => (n.RxErrorsPerSec ?? 0) > 0.01 || (n.TxErrorsPerSec ?? 0) > 0.01);
        if (netBad > 0) rows.Add(new HealthScoreInsightRow("Network", $"{netBad} counter instance(s) with RX/TX errors — reduces score", "bad"));
        else rows.Add(new HealthScoreInsightRow("Network", "No significant interface error rates", "good"));

        int svcStops = services.Count(s => s.Status != "Running" && s.Status != "Not installed");
        if (svcStops > 0) rows.Add(new HealthScoreInsightRow("Services", $"{svcStops} monitored critical service(s) not running", "bad"));
        else rows.Add(new HealthScoreInsightRow("Services", "Monitored critical services are running", "good"));

        int critA = alerts.Count(a => a.Severity == "critical");
        int warnA = alerts.Count(a => a.Severity == "warning");
        if (critA > 0) rows.Add(new HealthScoreInsightRow("Alert load", $"{critA} critical, {warnA} warning active problems — counted in score", critA >= 3 ? "bad" : "warning"));
        else if (warnA > 0) rows.Add(new HealthScoreInsightRow("Alert load", $"{warnA} warning-level problems — light score impact", "warning"));
        else rows.Add(new HealthScoreInsightRow("Alert load", "No active alert penalties in score composition", "good"));

        int secHi = detections.Count(d => d.Severity.Contains("high", StringComparison.OrdinalIgnoreCase) || d.Severity.Contains("critical", StringComparison.OrdinalIgnoreCase));
        int secOther = detections.Count - secHi;
        if (secHi > 0) rows.Add(new HealthScoreInsightRow("Security (Sigma)", $"{secHi} high/critical detection(s) — strong score weight", secHi >= 5 ? "bad" : "warning"));
        else if (secOther > 0) rows.Add(new HealthScoreInsightRow("Security (Sigma)", $"{secOther} lower-severity detection(s)", "warning"));
        else rows.Add(new HealthScoreInsightRow("Security (Sigma)", "No detections in current sample — neutral for security slice", "good"));

        var tempCrit = thermal.FirstOrDefault(t => t.Celsius >= 90);
        var tempWarn = thermal.FirstOrDefault(t => t.Celsius >= 80);
        if (tempCrit != null) rows.Add(new HealthScoreInsightRow("Temperature", $"{tempCrit.Source}/{tempCrit.Name}: {tempCrit.Celsius:F0}°C — critical", "bad"));
        else if (tempWarn != null) rows.Add(new HealthScoreInsightRow("Temperature", $"{tempWarn.Source}/{tempWarn.Name}: {tempWarn.Celsius:F0}°C — elevated", "warning"));
        else if (thermal.Count > 0) rows.Add(new HealthScoreInsightRow("Temperature", "Sensor readings within typical range", "good"));
        else rows.Add(new HealthScoreInsightRow("Temperature", "No thermal data — not scored", "good"));

        var badPlugins = pluginHealth.Where(p => !p.IsHealthy).ToList();
        if (badPlugins.Count > 0) rows.Add(new HealthScoreInsightRow("Plugins", $"{badPlugins.Count} unhealthy: {string.Join(", ", badPlugins.Select(p => p.Name))}", "warning"));
        else if (pluginHealth.Count > 0) rows.Add(new HealthScoreInsightRow("Plugins", "All plugins report healthy", "good"));
        else rows.Add(new HealthScoreInsightRow("Plugins", "No plugin health rows", "good"));

        static int ImpactOrder(string i) => i == "bad" ? 0 : i == "warning" ? 1 : 2;
        return rows.OrderBy(r => ImpactOrder(r.Impact)).ThenBy(r => r.Category, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Short(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= 48 ? s : s[..45] + "...";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
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
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static bool GlobalMemoryStatus(out MEMORYSTATUSEX ms)
    {
        ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref ms);
    }

    private sealed class DiskPerfCounters : IDisposable
    {
        public string Instance { get; }
        private readonly PerformanceCounter? _queue;
        private readonly PerformanceCounter? _rl;
        private readonly PerformanceCounter? _wl;
        private readonly PerformanceCounter? _rps;
        private readonly PerformanceCounter? _wps;

        public DiskPerfCounters(string instance)
        {
            Instance = instance;
            _queue = new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", instance, true);
            _rl = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Read", instance, true);
            _wl = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Write", instance, true);
            _rps = new PerformanceCounter("PhysicalDisk", "Disk Reads/sec", instance, true);
            _wps = new PerformanceCounter("PhysicalDisk", "Disk Writes/sec", instance, true);
        }

        public void Prime()
        {
            _ = _queue?.NextValue();
            _ = _rl?.NextValue();
            _ = _wl?.NextValue();
            _ = _rps?.NextValue();
            _ = _wps?.NextValue();
        }

        public DiskPerfRow Sample()
        {
            double? q = TryD(_queue);
            double? rl = TryD(_rl) is { } x ? Math.Round(x * 1000.0, 3) : null;
            double? wl = TryD(_wl) is { } w ? Math.Round(w * 1000.0, 3) : null;
            double? rps = TryD(_rps);
            double? wps = TryD(_wps);
            return new DiskPerfRow(Instance, q, rl, wl, rps, wps);
        }

        private static double? TryD(PerformanceCounter? c)
        {
            if (c == null) return null;
            try { return Math.Round(c.NextValue(), 4); }
            catch { return null; }
        }

        public void Dispose()
        {
            _queue?.Dispose();
            _rl?.Dispose();
            _wl?.Dispose();
            _rps?.Dispose();
            _wps?.Dispose();
        }
    }

    private sealed class NetIfCounters : IDisposable
    {
        public string Instance { get; }
        private readonly PerformanceCounter? _rxErr;
        private readonly PerformanceCounter? _txErr;
        private readonly PerformanceCounter? _rxDisc;
        private readonly PerformanceCounter? _txDisc;

        public NetIfCounters(string instance)
        {
            Instance = instance;
            _rxErr = new PerformanceCounter("Network Interface", "Packets Received Errors", instance, true);
            _txErr = new PerformanceCounter("Network Interface", "Packets Outbound Errors", instance, true);
            _rxDisc = new PerformanceCounter("Network Interface", "Packets Received Discarded", instance, true);
            _txDisc = new PerformanceCounter("Network Interface", "Packets Outbound Discarded", instance, true);
        }

        public void Prime()
        {
            _ = _rxErr?.NextValue();
            _ = _txErr?.NextValue();
            _ = _rxDisc?.NextValue();
            _ = _txDisc?.NextValue();
        }

        public NetErrorRow Sample()
        {
            return new NetErrorRow(
                Instance,
                TryD(_rxErr),
                TryD(_txErr),
                TryD(_rxDisc),
                TryD(_txDisc));
        }

        private static double? TryD(PerformanceCounter? c)
        {
            if (c == null) return null;
            try { return Math.Round(c.NextValue(), 4); }
            catch { return null; }
        }

        public void Dispose()
        {
            _rxErr?.Dispose();
            _txErr?.Dispose();
            _rxDisc?.Dispose();
            _txDisc?.Dispose();
        }
    }
}
