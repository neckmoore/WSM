using System.Text.Json;
using WSMMonitor;

namespace WSMMonitor.Tests;

public sealed class MetricsDtoJsonTests
{
    private static readonly JsonSerializerOptions JsonApi = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void MetricsDto_serializes_thermal_array_with_camel_property_names()
    {
        var thermal = new List<TempRow> { new("LHM", "CPU Package · Tdie", 61.5) };
        var dto = new MetricsDto(
            Timestamp: "2026-01-01T00:00:00+00:00",
            CpuTotalPct: 1,
            CpuLogicalCores: 4,
            CpuQueueLength: 0,
            Memory: new MemInfo(8192, 4096, 4096, 50, 40),
            MemoryCounters: null,
            CpuCores: [],
            CpuPackages: [],
            Disks: [],
            PhysicalDisks: [],
            DiskPerf: [],
            DiskSmart: [],
            Network: [],
            NetworkErrors: [],
            TcpStates: [],
            Thermal: thermal,
            TopCpu: [],
            TopMem: [],
            Services: [],
            Events: [],
            SecurityEvents: [],
            Detections: [],
            PluginHealth: [],
            HealthScore: 90,
            HealthBreakdown: [],
            HealthScoreInsights: [],
            Alerts: []);

        var json = JsonSerializer.Serialize(dto, JsonApi);
        Assert.Contains("\"thermal\"", json, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"LHM\"", json, StringComparison.Ordinal);
        Assert.Contains("\"celsius\":61.5", json, StringComparison.Ordinal);
    }
}
