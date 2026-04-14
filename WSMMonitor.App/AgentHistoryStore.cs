namespace WSMMonitor;

/// <summary>In-memory ring plus optional SQLite persistence for dashboard history.</summary>
public sealed class AgentHistoryStore : IDisposable
{
    private readonly MetricsHistoryRing _ring = new();
    private readonly MetricsHistorySqlite? _sql;

    public AgentHistoryStore(WsmAppOptions options)
    {
        if (options.History.SqliteEnabled)
            _sql = new MetricsHistorySqlite(options.History.SqlitePath, options.History.RetentionDays);
    }

    public string PersistenceMode => _sql != null ? "sqlite" : "memory";

    public void Add(MetricsHistorySample sample)
    {
        _ring.Add(sample);
        try
        {
            _sql?.Append(sample);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "SQLite history append failed");
        }
    }

    public MetricsHistoryResponseDto GetSeries(string preset, int maxPoints = 500)
    {
        if (_sql != null)
        {
            try
            {
                var fromSql = _sql.Query(preset, maxPoints);
                if (fromSql.Points.Count > 0)
                    return fromSql;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "SQLite history query failed; using in-memory ring");
            }
        }

        return _ring.GetSeries(preset, maxPoints);
    }

    public void Dispose()
    {
        try { _sql?.Dispose(); } catch { /* */ }
    }
}
