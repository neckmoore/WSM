namespace WSMMonitor.Plugins;

public sealed class SigmaMvpEngine
{
    private readonly SigmaRuleStore _store;

    public SigmaMvpEngine(SigmaRuleStore? store = null)
    {
        _store = store ?? new SigmaRuleStore();
    }

    public IReadOnlyList<DetectionRow> Match(IReadOnlyList<SecurityEventRow> events)
    {
        var rules = _store.LoadRules();
        if (rules.Count == 0 || events.Count == 0) return [];

        var result = new List<DetectionRow>();
        foreach (var e in events)
        {
            foreach (var r in rules)
            {
                if (r.EventId is int reid && reid != e.EventId) continue;
                if (!ContainsAny(e.Provider, r.ProviderContains)) continue;
                if (!ContainsAny(e.Image, r.ImageContains)) continue;
                if (!ContainsAny(e.CommandLine, r.CommandLineContains)) continue;
                if (!ContainsAny(e.Message, r.MessageContains)) continue;

                var reason = BuildReason(r);
                result.Add(new DetectionRow(r.Id, r.Title, r.Severity, e.Source, e.Time, e.EventId, reason, e.RecordId));
            }
        }

        return result
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenByDescending(x => x.EventTime)
            .Take(80)
            .ToList();
    }

    private static bool ContainsAny(string hay, string needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return true;
        return hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static int SeverityRank(string sev) => sev.ToLowerInvariant() switch
    {
        "critical" => 3,
        "high" => 3,
        "warning" => 2,
        "medium" => 2,
        _ => 1
    };

    private static string BuildReason(SigmaMvpRule r)
    {
        var parts = new List<string>();
        if (r.EventId is int id) parts.Add($"eventId={id}");
        if (!string.IsNullOrWhiteSpace(r.ProviderContains)) parts.Add($"provider~{r.ProviderContains}");
        if (!string.IsNullOrWhiteSpace(r.ImageContains)) parts.Add($"image~{r.ImageContains}");
        if (!string.IsNullOrWhiteSpace(r.CommandLineContains)) parts.Add($"cmd~{r.CommandLineContains}");
        if (!string.IsNullOrWhiteSpace(r.MessageContains)) parts.Add($"msg~{r.MessageContains}");
        return string.Join(", ", parts);
    }
}
