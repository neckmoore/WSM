namespace WSMMonitor.Plugins;

public sealed record SigmaMvpRule(
    string Id,
    string Title,
    string Severity,
    int? EventId,
    string ProviderContains,
    string ImageContains,
    string CommandLineContains,
    string MessageContains);

public sealed class SigmaRuleStore
{
    private readonly string _rulesDir;

    public SigmaRuleStore(string? rulesDir = null)
    {
        _rulesDir = rulesDir ?? Path.Combine(AppContext.BaseDirectory, "rules", "sigma");
    }

    public IReadOnlyList<SigmaMvpRule> LoadRules()
    {
        if (!Directory.Exists(_rulesDir)) return [];
        var outRules = new List<SigmaMvpRule>();
        foreach (var file in Directory.EnumerateFiles(_rulesDir, "*.yml", SearchOption.TopDirectoryOnly))
        {
            var r = ParseRule(File.ReadAllLines(file));
            if (r != null) outRules.Add(r);
        }
        return outRules;
    }

    private static SigmaMvpRule? ParseRule(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim().Trim('"', '\'');
            map[key] = val;
        }

        if (!map.TryGetValue("id", out var id) || string.IsNullOrWhiteSpace(id)) return null;
        var title = map.TryGetValue("title", out var t) ? t : id;
        var severity = map.TryGetValue("severity", out var s) ? s : "warning";
        int? eventId = null;
        if (map.TryGetValue("eventId", out var eid) && int.TryParse(eid, out var parsed)) eventId = parsed;

        return new SigmaMvpRule(
            id,
            title,
            severity,
            eventId,
            map.TryGetValue("providerContains", out var p) ? p : "",
            map.TryGetValue("imageContains", out var i) ? i : "",
            map.TryGetValue("commandLineContains", out var c) ? c : "",
            map.TryGetValue("messageContains", out var m) ? m : "");
    }
}
