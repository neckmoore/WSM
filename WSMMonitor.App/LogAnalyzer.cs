using System.Diagnostics.Eventing.Reader;

namespace WSMMonitor;

public sealed record LogEntryRow(DateTime Time, string Log, string Level, string Provider, int? EventId, string Message);

public sealed record LogAnalysisResult(
    DateTime From,
    DateTime To,
    int Total,
    int Critical,
    int Error,
    int Warning,
    IReadOnlyList<(string Provider, int Count)> TopProviders,
    IReadOnlyList<(int EventId, int Count)> TopEventIds,
    IReadOnlyList<LogEntryRow> Samples);

public sealed class LogAnalyzer
{
    public LogAnalysisResult Analyze(DateTime fromLocal, DateTime toLocal, bool includeSystem, bool includeApplication, int maxSamples = 200)
    {
        var logs = new List<string>();
        if (includeSystem) logs.Add("System");
        if (includeApplication) logs.Add("Application");
        if (logs.Count == 0) throw new InvalidOperationException("Select at least one log.");
        if (toLocal <= fromLocal) throw new InvalidOperationException("End time must be after start time.");

        var fromUtc = fromLocal.ToUniversalTime();
        var toUtc = toLocal.ToUniversalTime();
        var samples = new List<LogEntryRow>();

        int total = 0, crit = 0, err = 0, warn = 0;
        var providers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ids = new Dictionary<int, int>();

        // TimeCreated SystemTime uses UTC format in Event Log XML query.
        var fromIso = fromUtc.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
        var toIso = toUtc.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
        var xPath = $"*[System[TimeCreated[@SystemTime>='{fromIso}' and @SystemTime<='{toIso}']]]";

        foreach (var log in logs)
        {
            var q = new EventLogQuery(log, PathType.LogName, xPath) { ReverseDirection = true };
            using var reader = new EventLogReader(q);
            for (;;)
            {
                using var e = reader.ReadEvent();
                if (e == null) break;

                total++;
                var level = (e.Level ?? 0) switch
                {
                    1 => "Critical",
                    2 => "Error",
                    3 => "Warning",
                    4 => "Info",
                    5 => "Verbose",
                    _ => "Unknown"
                };

                if (level == "Critical") crit++;
                else if (level == "Error") err++;
                else if (level == "Warning") warn++;

                var provider = e.ProviderName ?? "(unknown)";
                providers[provider] = providers.TryGetValue(provider, out var pc) ? pc + 1 : 1;

                var id = (int?)e.Id;
                if (id is int iid)
                    ids[iid] = ids.TryGetValue(iid, out var ec) ? ec + 1 : 1;

                if (samples.Count < maxSamples)
                {
                    string msg;
                    try { msg = e.FormatDescription() ?? ""; }
                    catch { msg = "(unable to format)"; }
                    if (msg.Length > 240) msg = msg[..237] + "...";

                    samples.Add(new LogEntryRow(
                        e.TimeCreated?.ToLocalTime() ?? DateTime.MinValue,
                        log,
                        level,
                        provider,
                        id,
                        msg));
                }
            }
        }

        return new LogAnalysisResult(
            fromLocal,
            toLocal,
            total,
            crit,
            err,
            warn,
            providers.OrderByDescending(x => x.Value).Take(10).Select(x => (x.Key, x.Value)).ToList(),
            ids.OrderByDescending(x => x.Value).Take(10).Select(x => (x.Key, x.Value)).ToList(),
            samples.OrderByDescending(x => x.Time).ToList());
    }
}
