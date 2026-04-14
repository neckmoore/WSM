using Microsoft.Data.Sqlite;
using Serilog;

namespace WSMMonitor;

/// <summary>Persistent compact metric history (optional). All DB access is serialized on <see cref="_gate"/>.</summary>
public sealed class MetricsHistorySqlite : IDisposable
{
    private readonly object _gate = new();
    private readonly string _dbPath;
    private readonly int _retentionDays;
    private SqliteConnection? _conn;
    private int _insertCount;

    public MetricsHistorySqlite(string relativeOrAbsolutePath, int retentionDays)
    {
        _dbPath = Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath);
        _retentionDays = Math.Clamp(retentionDays, 1, 400);
    }

    private void EnsureOpenLocked()
    {
        if (_conn != null) return;
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _conn = new SqliteConnection(
            "Data Source=" + _dbPath +
            ";Mode=ReadWriteCreate;Cache=Shared;Default Timeout=60");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS metrics_history(
              at_utc TEXT NOT NULL PRIMARY KEY,
              cpu REAL,
              mem REAL,
              lat REAL,
              net REAL,
              score INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_metrics_history_at ON metrics_history(at_utc);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Append(MetricsHistorySample sample)
    {
        lock (_gate)
        {
            EnsureOpenLocked();
            using var cmd = _conn!.CreateCommand();
            cmd.CommandText =
                """
                INSERT OR REPLACE INTO metrics_history(at_utc, cpu, mem, lat, net, score)
                VALUES($a, $c, $m, $l, $n, $s);
                """;
            cmd.Parameters.AddWithValue("$a", sample.AtUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$c", (object?)sample.CpuPct ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$m", sample.MemUsedPct);
            cmd.Parameters.AddWithValue("$l", (object?)sample.DiskLatMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$n", sample.NetMiBps);
            cmd.Parameters.AddWithValue("$s", sample.HealthScore);
            cmd.ExecuteNonQuery();
            if (++_insertCount % 40 == 0)
                PruneLocked();
        }
    }

    private void PruneLocked()
    {
        try
        {
            var cut = DateTimeOffset.UtcNow.AddDays(-_retentionDays).ToString("o");
            using var cmd = _conn!.CreateCommand();
            cmd.CommandText = "DELETE FROM metrics_history WHERE at_utc < $c;";
            cmd.Parameters.AddWithValue("$c", cut);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SQLite metrics_history prune failed");
        }
    }

    public MetricsHistoryResponseDto Query(string preset, int maxPoints = 500)
    {
        lock (_gate)
        {
            EnsureOpenLocked();
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
            var list = new List<MetricsHistorySample>(256);
            using (var cmd = _conn!.CreateCommand())
            {
                cmd.CommandText =
                    """
                    SELECT at_utc, cpu, mem, lat, net, score
                    FROM metrics_history
                    WHERE at_utc >= $f AND at_utc <= $t
                    ORDER BY at_utc;
                    """;
                cmd.Parameters.AddWithValue("$f", from.ToString("o"));
                cmd.Parameters.AddWithValue("$t", now.ToString("o"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var at = DateTimeOffset.Parse(r.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    double? cpu = r.IsDBNull(1) ? null : r.GetDouble(1);
                    var mem = r.GetDouble(2);
                    double? lat = r.IsDBNull(3) ? null : r.GetDouble(3);
                    var net = r.GetDouble(4);
                    var score = r.GetInt32(5);
                    list.Add(new MetricsHistorySample(at, cpu, mem, lat, net, score));
                }
            }

            if (list.Count == 0)
                return new MetricsHistoryResponseDto(preset, from.ToString("o"), now.ToString("o"), []);

            if (list.Count <= maxPoints)
            {
                return new MetricsHistoryResponseDto(
                    preset,
                    from.ToString("o"),
                    now.ToString("o"),
                    list.Select(s => s.ToDto()).ToList());
            }

            var bucketed = Downsample(list, maxPoints, from, now);
            return new MetricsHistoryResponseDto(preset, from.ToString("o"), now.ToString("o"), bucketed);
        }
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
                (int)Math.Round(Avg(grp, x => (double)x.HealthScore))));
        }

        return result;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _conn?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SQLite metrics_history dispose failed");
            }

            _conn = null;
        }
    }
}
