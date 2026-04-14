using System.Text.Json;

namespace WSMMonitor.Plugins;

public sealed class SigmaSuppressionStore
{
    private readonly string _path;
    private List<SuppressionEntry> _entries = [];
    private long _lastReadTicks;

    public SigmaSuppressionStore(string? pathOverride)
    {
        _path = string.IsNullOrWhiteSpace(pathOverride)
            ? Path.Combine(AppContext.BaseDirectory, "rules", "sigma", "suppressions.json")
            : (Path.IsPathRooted(pathOverride) ? pathOverride : Path.Combine(AppContext.BaseDirectory, pathOverride));
    }

    public void ReloadIfStale(TimeSpan minInterval)
    {
        var now = DateTime.UtcNow.Ticks;
        if (_lastReadTicks != 0 && now - _lastReadTicks < minInterval.Ticks) return;
        _lastReadTicks = now;
        Reload();
    }

    public void Reload()
    {
        _entries = [];
        if (!File.Exists(_path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            if (!doc.RootElement.TryGetProperty("suppressions", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;
            foreach (var el in arr.EnumerateArray())
            {
                var ruleId = ReadString(el, "ruleId");
                var image = ReadString(el, "imageContains");
                var cmd = ReadString(el, "commandLineContains");
                long? recordId = null;
                if (el.TryGetProperty("recordId", out var rid) && rid.ValueKind == JsonValueKind.Number)
                    recordId = rid.GetInt64();
                _entries.Add(new SuppressionEntry(ruleId, image, cmd, recordId));
            }
        }
        catch
        {
            _entries = [];
        }
    }

    private static string? ReadString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    /// <summary>Returns true if this detection should be dropped (false positive / allowlist).</summary>
    public bool IsSuppressed(DetectionRow d, IReadOnlyList<SecurityEventRow> events)
    {
        if (_entries.Count == 0) return false;
        var ev = d.RecordId is { } rid
            ? events.FirstOrDefault(x => x.RecordId == rid)
            : events.FirstOrDefault(x =>
                x.EventId == d.EventId && string.Equals(x.Time, d.EventTime, StringComparison.Ordinal));

        foreach (var s in _entries)
        {
            if (!RuleMatches(s.RuleId, d.RuleId))
                continue;
            if (s.RecordId is { } sr && (d.RecordId != sr))
                continue;
            if (!string.IsNullOrEmpty(s.ImageContains))
            {
                if (ev == null || ev.Image.IndexOf(s.ImageContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            if (!string.IsNullOrEmpty(s.CommandLineContains))
            {
                if (ev == null || ev.CommandLine.IndexOf(s.CommandLineContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            return true;
        }

        return false;
    }

    private static bool RuleMatches(string? pattern, string detectionRuleId)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        if (pattern == "*") return true;
        return string.Equals(pattern.Trim(), detectionRuleId, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SuppressionEntry(string? RuleId, string? ImageContains, string? CommandLineContains, long? RecordId);
}
