using System.Text.Json.Serialization;

namespace WSMMonitor;

public static class MetricsHistoryCompact
{
    public static MetricsHistorySample FromMetricsDto(MetricsDto m)
    {
        double? lat = null;
        var perf = m.DiskPerf;
        if (perf.Count > 0)
        {
            double s = 0;
            int c = 0;
            foreach (var x in perf)
            {
                if (x.ReadLatencyMs is { } r)
                {
                    s += r;
                    c++;
                }
                if (x.WriteLatencyMs is { } w)
                {
                    s += w;
                    c++;
                }
            }
            if (c > 0) lat = s / c;
        }

        double net = 0;
        foreach (var n in m.Network)
        {
            net += ParseRateToMiB(n.RxPerSec);
            net += ParseRateToMiB(n.TxPerSec);
        }

        return new MetricsHistorySample(DateTimeOffset.UtcNow, m.CpuTotalPct, m.Memory.UsedPct, lat, net, m.HealthScore);
    }

    private static double ParseRateToMiB(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(s.Trim(), @"([0-9.]+)\s*(B|KiB|MiB)/s", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return 0;
        var v = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var u = m.Groups[2].Value.ToLowerInvariant();
        if (u == "mib") return v;
        if (u == "kib") return v / 1024;
        if (u == "b") return v / 1048576;
        return 0;
    }
}

/// <summary>In-memory ring buffer of compact metric samples for dashboard time ranges.</summary>
public sealed class MetricsHistoryRing
{
    private readonly object _sync = new();
    private readonly List<MetricsHistorySample> _samples = [];
    private const int MaxSamples = 20_000;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(8);

    public void Add(MetricsHistorySample sample)
    {
        var cutoff = DateTimeOffset.UtcNow - MaxAge;
        lock (_sync)
        {
            _samples.RemoveAll(s => s.AtUtc < cutoff);
            _samples.Add(sample);
            while (_samples.Count > MaxSamples)
                _samples.RemoveAt(0);
        }
    }

    public MetricsHistoryResponseDto GetSeries(string preset, int maxPoints = 500)
    {
        var window = preset.ToLowerInvariant() switch
        {
            "15m" => TimeSpan.FromMinutes(15),
            "1h" => TimeSpan.FromHours(1),
            "24h" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromMinutes(15)
        };

        var now = DateTimeOffset.UtcNow;
        var from = now - window;
        List<MetricsHistorySample> slice;
        lock (_sync)
        {
            slice = _samples.Where(s => s.AtUtc >= from && s.AtUtc <= now).OrderBy(s => s.AtUtc).ToList();
        }

        if (slice.Count == 0)
            return new MetricsHistoryResponseDto(preset, from.ToString("o"), now.ToString("o"), []);

        if (slice.Count <= maxPoints)
        {
            return new MetricsHistoryResponseDto(
                preset,
                from.ToString("o"),
                now.ToString("o"),
                slice.Select(s => s.ToDto()).ToList());
        }

        var bucketed = Downsample(slice, maxPoints, from, now);
        return new MetricsHistoryResponseDto(preset, from.ToString("o"), now.ToString("o"), bucketed);
    }

    private static List<MetricsHistoryPointDto> Downsample(
        IReadOnlyList<MetricsHistorySample> slice,
        int buckets,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var span = (to - from).TotalMilliseconds;
        if (span <= 0) span = 1;
        var result = new List<MetricsHistoryPointDto>(buckets);
        for (var b = 0; b < buckets; b++)
        {
            var t0 = from.AddMilliseconds(span * b / buckets);
            var t1 = from.AddMilliseconds(span * (b + 1) / buckets);
            var grp = slice.Where(s => s.AtUtc >= t0 && s.AtUtc < t1).ToList();
            if (grp.Count == 0) continue;
            static double Avg(IEnumerable<MetricsHistorySample> g, Func<MetricsHistorySample, double> sel) =>
                g.Average(sel);
            var mid = grp[grp.Count / 2].AtUtc.ToString("o");
            result.Add(new MetricsHistoryPointDto(
                mid,
                Avg(grp, x => x.CpuPct ?? 0),
                Avg(grp, x => x.MemUsedPct),
                Avg(grp, x => x.DiskLatMs ?? 0),
                Avg(grp, x => x.NetMiBps),
                (int)Math.Round(Avg(grp, x => x.HealthScore))));
        }

        return result;
    }
}

public sealed record MetricsHistorySample(
    DateTimeOffset AtUtc,
    double? CpuPct,
    double MemUsedPct,
    double? DiskLatMs,
    double NetMiBps,
    int HealthScore)
{
    public MetricsHistoryPointDto ToDto() => new(
        AtUtc.ToString("o"),
        CpuPct,
        MemUsedPct,
        DiskLatMs,
        NetMiBps,
        HealthScore);
}

public sealed record MetricsHistoryPointDto(
    [property: JsonPropertyName("t")] string Utc,
    [property: JsonPropertyName("cpu")] double? CpuPct,
    [property: JsonPropertyName("mem")] double MemUsedPct,
    [property: JsonPropertyName("lat")] double? DiskLatMs,
    [property: JsonPropertyName("net")] double NetMiBps,
    [property: JsonPropertyName("score")] int HealthScore);

public sealed record MetricsHistoryResponseDto(
    [property: JsonPropertyName("preset")] string Preset,
    [property: JsonPropertyName("fromUtc")] string FromUtc,
    [property: JsonPropertyName("toUtc")] string ToUtc,
    [property: JsonPropertyName("points")] IReadOnlyList<MetricsHistoryPointDto> Points);
