using System.Text.Json.Serialization;

namespace WSMMonitor;

public sealed record MemInfo(
    [property: JsonPropertyName("totalMiB")] ulong TotalMiB,
    [property: JsonPropertyName("usedMiB")] ulong UsedMiB,
    [property: JsonPropertyName("freeMiB")] ulong FreeMiB,
    [property: JsonPropertyName("usedPct")] double UsedPct,
    [property: JsonPropertyName("commitPct")] double? CommitPct);

/// <summary>Extra RAM / pool stats from performance counters.</summary>
public sealed record MemoryCountersRow(
    [property: JsonPropertyName("nonPagedPoolMiB")] double? NonPagedPoolMiB,
    [property: JsonPropertyName("availableBytesMiB")] double? AvailableBytesMiB,
    [property: JsonPropertyName("cacheResidentMiB")] double? CacheResidentMiB,
    [property: JsonPropertyName("modifiedPageListMiB")] double? ModifiedPageListMiB,
    [property: JsonPropertyName("standbyListMiB")] double? StandbyListMiB,
    [property: JsonPropertyName("compressedMiB")] double? CompressedMiB);

public sealed record DiskRow(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("freeGB")] double FreeGb,
    [property: JsonPropertyName("sizeGB")] double SizeGb,
    [property: JsonPropertyName("freePct")] double FreePct);

public sealed record PhysicalDiskRow(
    [property: JsonPropertyName("friendlyName")] string FriendlyName,
    [property: JsonPropertyName("mediaType")] string MediaType,
    [property: JsonPropertyName("healthStatus")] string HealthStatus,
    [property: JsonPropertyName("operationalStatus")] string OperationalStatus);

public sealed record DiskPerfRow(
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("queueLength")] double? QueueLength,
    [property: JsonPropertyName("readLatencyMs")] double? ReadLatencyMs,
    [property: JsonPropertyName("writeLatencyMs")] double? WriteLatencyMs,
    [property: JsonPropertyName("readsPerSec")] double? ReadsPerSec,
    [property: JsonPropertyName("writesPerSec")] double? WritesPerSec);

public sealed record DiskSmartRow(
    [property: JsonPropertyName("disk")] string Disk,
    [property: JsonPropertyName("wearPercent")] int? WearPercent,
    [property: JsonPropertyName("temperatureC")] int? TemperatureC,
    [property: JsonPropertyName("readErrorsTotal")] long? ReadErrorsTotal,
    [property: JsonPropertyName("writeErrorsTotal")] long? WriteErrorsTotal);

public sealed record NetRow(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("linkSpeed")] string LinkSpeed,
    [property: JsonPropertyName("rxPerSec")] string RxPerSec,
    [property: JsonPropertyName("txPerSec")] string TxPerSec);

public sealed record NetErrorRow(
    [property: JsonPropertyName("counterInstance")] string CounterInstance,
    [property: JsonPropertyName("rxErrorsPerSec")] double? RxErrorsPerSec,
    [property: JsonPropertyName("txErrorsPerSec")] double? TxErrorsPerSec,
    [property: JsonPropertyName("rxDiscardedPerSec")] double? RxDiscardedPerSec,
    [property: JsonPropertyName("txDiscardedPerSec")] double? TxDiscardedPerSec);

public sealed record TcpStateRow(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("count")] int Count);

public sealed record TempRow(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("celsius")] double Celsius);

public sealed record ProcRow(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("cpuPctApprox")] double CpuPctApprox,
    [property: JsonPropertyName("ws_MB")] double WsMb);

public sealed record CpuCoreRow(
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("pct")] double Pct);

public sealed record CpuPackageRow(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("loadPct")] int? LoadPct,
    [property: JsonPropertyName("currentMHz")] uint? CurrentMHz,
    [property: JsonPropertyName("maxMHz")] uint? MaxMHz);

public sealed record ServiceRow(
    [property: JsonPropertyName("serviceName")] string ServiceName,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("status")] string Status);

public sealed record EventRow(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("log")] string Log,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("recordId")] long? RecordId = null,
    [property: JsonPropertyName("eventId")] int? WinEventId = null);

public sealed record SecurityEventRow(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("eventId")] int EventId,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("commandLine")] string CommandLine,
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("recordId")] long? RecordId = null);

public sealed record DetectionRow(
    [property: JsonPropertyName("ruleId")] string RuleId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("eventTime")] string EventTime,
    [property: JsonPropertyName("eventId")] int EventId,
    [property: JsonPropertyName("matchReason")] string MatchReason,
    [property: JsonPropertyName("recordId")] long? RecordId = null);

public sealed record PluginHealthRow(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isHealthy")] bool IsHealthy,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("lastUpdate")] string LastUpdate);

public sealed record AgentStatusDto(
    [property: JsonPropertyName("agentRunning")] bool AgentRunning,
    [property: JsonPropertyName("serviceMode")] bool ServiceMode,
    [property: JsonPropertyName("httpListening")] bool HttpListening,
    [property: JsonPropertyName("lastMetricsAt")] string LastMetricsAt,
    [property: JsonPropertyName("lastError")] string LastError,
    [property: JsonPropertyName("wsmVersion")] string WsmVersion = "",
    [property: JsonPropertyName("buildDateUtc")] string BuildDateUtc = "",
    [property: JsonPropertyName("processId")] int ProcessId = 0,
    [property: JsonPropertyName("listenPort")] int ListenPort = 0,
    [property: JsonPropertyName("exePath")] string ExePath = "",
    [property: JsonPropertyName("historyPersistence")] string HistoryPersistence = "",
    [property: JsonPropertyName("ready")] bool Ready = false);

public sealed record AlertRefDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("log")] string? Log,
    [property: JsonPropertyName("recordId")] long? RecordId,
    [property: JsonPropertyName("ruleId")] string? RuleId,
    [property: JsonPropertyName("eventTime")] string? EventTime,
    [property: JsonPropertyName("metricCode")] string? MetricCode);

public sealed record AlertRow(
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("ref")] AlertRefDto? Ref = null);

public sealed record HealthFactorRow(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("penalty")] int Penalty,
    [property: JsonPropertyName("cap")] int Cap,
    [property: JsonPropertyName("detail")] string Detail);

/// <summary>Per-dimension view for health score UI: good / warning / bad.</summary>
public sealed record HealthScoreInsightRow(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("impact")] string Impact);

public sealed record LogEventDetailDto(
    [property: JsonPropertyName("log")] string Log,
    [property: JsonPropertyName("recordId")] long RecordId,
    [property: JsonPropertyName("eventId")] int? EventId,
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("xml")] string? Xml);

public sealed record MetricsDto(
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("cpuTotalPct")] double? CpuTotalPct,
    [property: JsonPropertyName("cpuLogicalCores")] int CpuLogicalCores,
    [property: JsonPropertyName("cpuQueueLength")] double? CpuQueueLength,
    [property: JsonPropertyName("memory")] MemInfo Memory,
    [property: JsonPropertyName("memoryCounters")] MemoryCountersRow? MemoryCounters,
    [property: JsonPropertyName("cpuCores")] IReadOnlyList<CpuCoreRow> CpuCores,
    [property: JsonPropertyName("cpuPackages")] IReadOnlyList<CpuPackageRow> CpuPackages,
    [property: JsonPropertyName("disks")] IReadOnlyList<DiskRow> Disks,
    [property: JsonPropertyName("physicalDisks")] IReadOnlyList<PhysicalDiskRow> PhysicalDisks,
    [property: JsonPropertyName("diskPerf")] IReadOnlyList<DiskPerfRow> DiskPerf,
    [property: JsonPropertyName("diskSmart")] IReadOnlyList<DiskSmartRow> DiskSmart,
    [property: JsonPropertyName("network")] IReadOnlyList<NetRow> Network,
    [property: JsonPropertyName("networkErrors")] IReadOnlyList<NetErrorRow> NetworkErrors,
    [property: JsonPropertyName("tcpStates")] IReadOnlyList<TcpStateRow> TcpStates,
    [property: JsonPropertyName("thermal")] IReadOnlyList<TempRow> Thermal,
    [property: JsonPropertyName("topCpu")] IReadOnlyList<ProcRow> TopCpu,
    [property: JsonPropertyName("topMem")] IReadOnlyList<ProcRow> TopMem,
    [property: JsonPropertyName("services")] IReadOnlyList<ServiceRow> Services,
    [property: JsonPropertyName("events")] IReadOnlyList<EventRow> Events,
    [property: JsonPropertyName("securityEvents")] IReadOnlyList<SecurityEventRow> SecurityEvents,
    [property: JsonPropertyName("detections")] IReadOnlyList<DetectionRow> Detections,
    [property: JsonPropertyName("pluginHealth")] IReadOnlyList<PluginHealthRow> PluginHealth,
    [property: JsonPropertyName("healthScore")] int HealthScore,
    [property: JsonPropertyName("healthBreakdown")] IReadOnlyList<HealthFactorRow> HealthBreakdown,
    [property: JsonPropertyName("healthScoreInsights")] IReadOnlyList<HealthScoreInsightRow> HealthScoreInsights,
    [property: JsonPropertyName("alerts")] IReadOnlyList<AlertRow> Alerts);
