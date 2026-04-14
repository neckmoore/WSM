using System.Net;
using System.Text;
using System.Text.Json;
using Prometheus;
using Serilog;

namespace WSMMonitor;

public sealed partial class DashboardServer
{
    private static readonly JsonSerializerOptions JsonApi = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string BuildDashboardHtml(string uiLang)
    {
        var lang = WsmLocalization.NormalizeLang(uiLang);
        return HtmlPage
            .Replace("{{WSM_BUILD_STAMP}}", WsmBuildInfo.FormatBuildStamp(), StringComparison.Ordinal)
            .Replace("{{WSM_UI_LANG}}", lang, StringComparison.Ordinal);
    }

    private static string? ParseUiLangFromQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return null;
        query = query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;
            if (!string.Equals(kv[0], "ui_lang", StringComparison.OrdinalIgnoreCase))
                continue;
            var v = Uri.UnescapeDataString(kv[1]).Trim();
            if (string.Equals(v, "en", StringComparison.OrdinalIgnoreCase))
                return "en";
            if (string.Equals(v, "ru", StringComparison.OrdinalIgnoreCase))
                return "ru";
        }

        return null;
    }

    private static void ApplyNoCacheHeaders(HttpListenerResponse response)
    {
        response.Headers.Set("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
        response.Headers.Set("Pragma", "no-cache");
        response.Headers.Set("Expires", "0");
    }

    private readonly MetricsEngine _engine;
    private readonly Func<MetricsDto>? _metricsProvider;
    private readonly Func<AgentStatusDto>? _statusProvider;
    private readonly Func<string, MetricsHistoryResponseDto>? _historyProvider;
    private readonly Func<bool>? _readyProvider;
    private readonly HttpListener _listener = new();
    private Thread? _thread;
    private volatile bool _run;

    public int Port { get; }

    public DashboardServer(
        MetricsEngine engine,
        int port = 8787,
        Func<MetricsDto>? metricsProvider = null,
        Func<AgentStatusDto>? statusProvider = null,
        Func<string, MetricsHistoryResponseDto>? historyProvider = null,
        Func<bool>? readyProvider = null)
    {
        _engine = engine;
        Port = port;
        _metricsProvider = metricsProvider;
        _statusProvider = statusProvider;
        _historyProvider = historyProvider;
        _readyProvider = readyProvider;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    private static void ApplyWsmApiHeaders(HttpListenerResponse response)
    {
        response.Headers.Set("X-WSM-Version", WsmBuildInfo.BuildIdentity);
        response.Headers.Set("X-WSM-BuildDate", WsmBuildInfo.BuildDateUtc);
        response.Headers.Set("X-WSM-ApiVersion", WsmBuildInfo.ApiVersion);
    }

    private static string NormalizeApiPath(string path)
    {
        const string v1 = "/api/v1/";
        if (path.StartsWith(v1, StringComparison.OrdinalIgnoreCase) && path.Length > v1.Length)
            return "/api/" + path.Substring(v1.Length);
        return path;
    }

    public void Start()
    {
        if (_run) return;
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            throw new InvalidOperationException(
                $"Cannot listen on http://127.0.0.1:{Port}/ (access denied or port in use). Stop other processes on this port or change Agent:Port.",
                ex);
        }

        _run = true;
        _thread = new Thread(Loop) { IsBackground = true };
        _thread.Start();
    }

    private void Loop()
    {
        while (_run)
        {
            try
            {
                var ctx = _listener.GetContext();
                _ = Task.Run(() => Handle(ctx));
            }
            catch when (!_run) { break; }
            catch (Exception ex)
            {
                Log.Warning(ex, "WSM metrics HTTP listener loop error");
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        try
        {
            var path = NormalizeApiPath(ctx.Request.Url?.AbsolutePath ?? "/");

            if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
            {
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.ContentType = "text/plain; charset=utf-8";
                var buf = Encoding.UTF8.GetBytes("ok");
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                return;
            }

            if (string.Equals(path, "/ready", StringComparison.OrdinalIgnoreCase))
            {
                var ok = _readyProvider?.Invoke() ?? true;
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.StatusCode = ok ? 200 : 503;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                var body = ok ? "{\"ready\":true}" : "{\"ready\":false}";
                var buf = Encoding.UTF8.GetBytes(body);
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                return;
            }

            if (string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ctx.Response.OutputStream, CancellationToken.None)
                    .GetAwaiter().GetResult();
                return;
            }

            if (string.Equals(path, "/favicon.ico", StringComparison.OrdinalIgnoreCase))
            {
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.ContentType = "image/x-icon";
                using var icon = AgentIconFactory.CreateShieldPulseIcon(32);
                using var ms = new MemoryStream();
                icon.Save(ms);
                var bytes = ms.ToArray();
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                return;
            }

            if (path.StartsWith("/api/metrics/history", StringComparison.OrdinalIgnoreCase))
            {
                var preset = ctx.Request.QueryString["preset"] ?? "15m";
                var dto = _historyProvider?.Invoke(preset) ?? new MetricsHistoryResponseDto(preset, "", "", []);
                var json = JsonSerializer.Serialize(dto, JsonApi);
                var buf = Encoding.UTF8.GetBytes(json);
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                return;
            }

            if (path.StartsWith("/api/log-event", StringComparison.OrdinalIgnoreCase))
            {
                var log = ctx.Request.QueryString["log"];
                var ridStr = ctx.Request.QueryString["recordId"];
                if (string.IsNullOrWhiteSpace(log) || !long.TryParse(ridStr, out var rid))
                {
                    ctx.Response.StatusCode = 400;
                    var err = Encoding.UTF8.GetBytes("{\"error\":\"log and recordId required\"}");
                    ApplyNoCacheHeaders(ctx.Response);
                    ApplyWsmApiHeaders(ctx.Response);
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var detail = EventLogDetailReader.TryRead(log, rid);
                if (detail == null)
                {
                    ctx.Response.StatusCode = 404;
                    var err = Encoding.UTF8.GetBytes("{\"error\":\"event not found\"}");
                    ApplyNoCacheHeaders(ctx.Response);
                    ApplyWsmApiHeaders(ctx.Response);
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var json = JsonSerializer.Serialize(detail, JsonApi);
                var buf = Encoding.UTF8.GetBytes(json);
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                return;
            }

            if (path.StartsWith("/api/metrics", StringComparison.OrdinalIgnoreCase))
            {
                var dto = _metricsProvider?.Invoke() ?? _engine.Collect();
                var json = JsonSerializer.Serialize(dto, JsonApi);
                var buf = Encoding.UTF8.GetBytes(json);
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                return;
            }

            if (path.StartsWith("/api/agent-status", StringComparison.OrdinalIgnoreCase))
            {
                var status = _statusProvider?.Invoke() ?? new AgentStatusDto(
                    false, false, false, "", "status provider not configured",
                    WsmBuildInfo.BuildIdentity, WsmBuildInfo.BuildDateUtc, 0, Port, "", "", false);
                var json = JsonSerializer.Serialize(status, JsonApi);
                var buf = Encoding.UTF8.GetBytes(json);
                ApplyNoCacheHeaders(ctx.Response);
                ApplyWsmApiHeaders(ctx.Response);
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                return;
            }

            var q = ctx.Request.Url?.Query ?? "";
            var langFromQuery = ParseUiLangFromQuery(q);
            var htmlLang = langFromQuery ?? WsmConfiguration.Current.Ui.Language;
            var html = Encoding.UTF8.GetBytes(BuildDashboardHtml(htmlLang));
            ApplyNoCacheHeaders(ctx.Response);
            ApplyWsmApiHeaders(ctx.Response);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.OutputStream.Write(html, 0, html.Length);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WSM HTTP request handling failed ({Method} {Path})", ctx.Request.HttpMethod, ctx.Request.Url?.PathAndQuery);
        }
        finally
        {
            try { ctx.Response.Close(); }
            catch (Exception ex)
            {
                Log.Debug(ex, "WSM HTTP response close failed");
            }
        }
    }

    public void Stop()
    {
        _run = false;
        try { _listener.Stop(); } catch { /* */ }
        try { _listener.Close(); } catch { /* */ }
    }

    public void Dispose() => Stop();
}
