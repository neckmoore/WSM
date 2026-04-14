using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace WSMMonitor;

public static class WsmTelemetry
{
    private static readonly object Gate = new();
    private static MeterProvider? _provider;
    private static Meter? _meter;
    private static Histogram<double>? _collectMs;
    private static long _collectCount;

    public static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_provider != null) return;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
                return;

            try
            {
                _meter = new Meter("WSMMonitor", "1.0.0");
                _collectMs = _meter.CreateHistogram<double>("wsm_collect_duration_ms", unit: "ms", description: "MetricsEngine.Collect duration");
                _meter.CreateObservableGauge("wsm_health_score_otel", () => new Measurement<int>(_lastHealth), description: "Last health score");
                _meter.CreateObservableGauge("wsm_cpu_percent_otel", () => new Measurement<double>(_lastCpu), description: "Last CPU %");

                _provider = Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("wsm-monitor"))
                    .AddMeter("WSMMonitor")
                    .AddOtlpExporter()
                    .Build();
            }
            catch
            {
                try { _meter?.Dispose(); } catch { /* */ }
                _meter = null;
                _collectMs = null;
                _provider = null;
            }
        }
    }

    private static int _lastHealth;
    private static double _lastCpu;

    public static void AfterCollect(MetricsDto m, TimeSpan elapsed)
    {
        _lastHealth = m.HealthScore;
        _lastCpu = m.CpuTotalPct ?? 0;
        if (_collectMs != null)
            _collectMs.Record(elapsed.TotalMilliseconds);
        Interlocked.Increment(ref _collectCount);
    }

    public static void Dispose()
    {
        lock (Gate)
        {
            try { _provider?.Dispose(); } catch { /* */ }
            _provider = null;
            try { _meter?.Dispose(); } catch { /* */ }
            _meter = null;
            _collectMs = null;
        }
    }
}
