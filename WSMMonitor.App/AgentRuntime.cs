using System.Diagnostics;
using Serilog;
using WSMMonitor.Plugins;

namespace WSMMonitor;

public sealed class AgentRuntime : IDisposable
{
    private readonly object _sync = new();
    private MetricsEngine? _engine;
    private DashboardServer? _server;
    private AgentHistoryStore? _history;
    private System.Threading.Timer? _historyTimer;
    private System.Threading.Timer? _hourlySummaryTimer;
    private MetricsDto? _lastMetricsSnapshot;
    private bool _running;
    private string _lastError = "";
    private DateTimeOffset _lastMetricsAt;
    private readonly WsmAppOptions _options;

    public bool ServiceMode { get; }
    public int Port { get; }

    public AgentRuntime(bool serviceMode, int port = 8787, WsmAppOptions? options = null)
    {
        ServiceMode = serviceMode;
        _options = options ?? WsmConfiguration.Current;
        Port = port;
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_running) return;
            WsmTelemetry.EnsureStarted();
            var suppressPath = string.IsNullOrWhiteSpace(_options.Sigma.SuppressionsPath)
                ? null
                : _options.Sigma.SuppressionsPath;
            var suppressions = new SigmaSuppressionStore(suppressPath);
            var pipeline = new PluginPipeline(plugins: null, sigma: new SigmaMvpEngine(), suppressions: suppressions);
            _engine = new MetricsEngine(pipeline, _options);
            _history = new AgentHistoryStore(_options);
            _server = new DashboardServer(
                _engine,
                Port,
                CollectMetrics,
                GetStatus,
                GetHistoryForPreset,
                IsReady);
            _server.Start();
            _running = true;
            _lastError = "";
            var interval = Math.Clamp(_options.Agent.HistorySampleSeconds, 3, 300);
            _historyTimer = new System.Threading.Timer(_ => SampleHistoryTick(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(interval));

            if (_options.Logging.HourlySummaryEnabled)
            {
                var h = Math.Clamp(_options.Logging.HourlySummaryIntervalHours, 1, 24);
                _hourlySummaryTimer = new System.Threading.Timer(
                    _ => HourlySummaryTick(),
                    null,
                    TimeSpan.FromHours(h),
                    TimeSpan.FromHours(h));
            }
        }
    }

    private void HourlySummaryTick()
    {
        try
        {
            MetricsDto? snap;
            lock (_sync)
            {
                snap = _lastMetricsSnapshot;
            }

            if (snap != null)
                WsmMetricsSummaryLog.WriteHourlyBlock(snap, ServiceMode);
        }
        catch (Exception ex)
        {
            try
            {
                Log.Warning(ex, "Hourly metrics summary tick failed");
            }
            catch
            {
                /* */
            }
        }
    }

    private bool IsReady()
    {
        lock (_sync)
        {
            return _running && _lastMetricsAt != default && string.IsNullOrEmpty(_lastError);
        }
    }

    private MetricsHistoryResponseDto GetHistoryForPreset(string preset)
    {
        lock (_sync)
        {
            return _history?.GetSeries(string.IsNullOrWhiteSpace(preset) ? "15m" : preset.Trim())
                   ?? new MetricsHistoryResponseDto(preset, "", "", []);
        }
    }

    private void SampleHistoryTick()
    {
        try
        {
            MetricsEngine? engine;
            AgentHistoryStore? history;
            MetricsDto? m = null;
            var elapsed = TimeSpan.Zero;
            lock (_sync)
            {
                if (!_running || _engine == null || _history == null) return;
                engine = _engine;
                history = _history;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                m = engine.Collect();
                sw.Stop();
                elapsed = sw.Elapsed;
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _lastError = ex.Message;
                }
                try
                {
                    Log.Warning(ex, "Background metrics collect failed");
                }
                catch
                {
                    /* Serilog unavailable */
                }
                return;
            }

            lock (_sync)
            {
                if (!_running)
                    return;
                _lastMetricsAt = DateTimeOffset.Now;
                _lastError = "";
                _lastMetricsSnapshot = m;
                if (m != null && history != null && ReferenceEquals(_history, history))
                    history.Add(MetricsHistoryCompact.FromMetricsDto(m));
            }

            if (m == null) return;
            try
            {
                WsmPrometheusMetrics.Observe(m);
                WsmTelemetry.AfterCollect(m, elapsed);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Warning(ex, "Post-collect (background) metrics export failed");
                }
                catch
                {
                    /* */
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                Log.Warning(ex, "SampleHistoryTick failed");
            }
            catch
            {
                /* timer must not throw */
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _running = false;
            try { _hourlySummaryTimer?.Dispose(); } catch { /* */ }
            _hourlySummaryTimer = null;
            try { _historyTimer?.Dispose(); } catch { /* */ }
            _historyTimer = null;
            try { _server?.Dispose(); } catch { /* */ }
            _server = null;
            try { _engine?.Dispose(); } catch { /* */ }
            _engine = null;
            try { _history?.Dispose(); } catch { /* */ }
            _history = null;
        }

        WsmTelemetry.Dispose();
    }

    public MetricsDto CollectMetrics()
    {
        MetricsEngine? engine;
        MetricsDto m;
        var elapsed = TimeSpan.Zero;
        lock (_sync)
        {
            if (!_running || _engine == null)
                throw new InvalidOperationException("Agent is not running.");
            engine = _engine;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            m = engine.Collect();
            sw.Stop();
            elapsed = sw.Elapsed;
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _lastError = ex.Message;
            }
            throw;
        }

        lock (_sync)
        {
            if (!_running)
                throw new InvalidOperationException("Agent is not running.");
            try
            {
                _lastMetricsAt = DateTimeOffset.Now;
                _lastError = "";
                _lastMetricsSnapshot = m;
            }
            catch
            {
                /* */
            }
        }

        try
        {
            WsmPrometheusMetrics.Observe(m);
            WsmTelemetry.AfterCollect(m, elapsed);
        }
        catch (Exception ex)
        {
            try
            {
                Log.Warning(ex, "Post-collect metrics export failed");
            }
            catch
            {
                /* */
            }
        }

        return m;
    }

    public AgentStatusDto GetStatus()
    {
        lock (_sync)
        {
            var exe = Environment.ProcessPath ?? "";
            var persistence = _history?.PersistenceMode ?? "memory";
            var ready = _running && _lastMetricsAt != default && string.IsNullOrEmpty(_lastError);
            return new AgentStatusDto(
                AgentRunning: _running,
                ServiceMode: ServiceMode,
                HttpListening: _running,
                LastMetricsAt: _lastMetricsAt == default ? "" : _lastMetricsAt.ToString("o"),
                LastError: _lastError,
                WsmVersion: WsmBuildInfo.BuildIdentity,
                BuildDateUtc: WsmBuildInfo.BuildDateUtc,
                ProcessId: Environment.ProcessId,
                ListenPort: Port,
                ExePath: exe,
                HistoryPersistence: persistence,
                Ready: ready);
        }
    }

    public void Dispose() => Stop();
}
