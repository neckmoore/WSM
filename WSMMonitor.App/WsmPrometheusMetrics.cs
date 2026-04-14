using Prometheus;

namespace WSMMonitor;

public static class WsmPrometheusMetrics
{
    private static readonly object Gate = new();
    private static bool _ready;
    private static Gauge? _cpuPercent;
    private static Gauge? _memoryUsedPercent;
    private static Gauge? _healthScore;
    private static Gauge? _cpuQueue;
    private static Gauge? _detectionsInSample;

    private static void Ensure()
    {
        if (_ready) return;
        lock (Gate)
        {
            if (_ready) return;
            Metrics.SuppressDefaultMetrics();
            _cpuPercent = Metrics.CreateGauge("wsm_cpu_percent", "Total CPU utilization percent");
            _memoryUsedPercent = Metrics.CreateGauge("wsm_memory_used_percent", "Physical memory used percent");
            _healthScore = Metrics.CreateGauge("wsm_health_score", "Aggregated health score 1-100");
            _cpuQueue = Metrics.CreateGauge("wsm_cpu_queue_length", "Processor queue length");
            _detectionsInSample = Metrics.CreateGauge(
                "wsm_security_detections_in_sample",
                "Detections in last metrics scrape by severity bucket",
                new[] { "severity" });
            _ready = true;
        }
    }

    public static void Observe(MetricsDto m)
    {
        Ensure();
        _cpuPercent!.Set(m.CpuTotalPct ?? 0);
        _memoryUsedPercent!.Set(m.Memory.UsedPct);
        _healthScore!.Set(m.HealthScore);
        _cpuQueue!.Set(m.CpuQueueLength ?? 0);

        int high = 0, med = 0, low = 0;
        foreach (var d in m.Detections)
        {
            var sev = d.Severity.ToLowerInvariant();
            if (sev.Contains("high") || sev.Contains("critical")) high++;
            else if (sev.Contains("medium") || sev.Contains("warning")) med++;
            else low++;
        }
        _detectionsInSample!.WithLabels("high").Set(high);
        _detectionsInSample.WithLabels("medium").Set(med);
        _detectionsInSample.WithLabels("low").Set(low);
    }
}
