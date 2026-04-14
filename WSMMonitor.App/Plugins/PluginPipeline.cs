namespace WSMMonitor.Plugins;

public sealed class PluginPipeline
{
    private readonly IReadOnlyList<IEventSourcePlugin> _plugins;
    private readonly SigmaMvpEngine _sigma;
    private readonly SigmaSuppressionStore _suppressions;
    private readonly bool _sigmaEnabled;

    public PluginPipeline(
        IEnumerable<IEventSourcePlugin>? plugins = null,
        SigmaMvpEngine? sigma = null,
        SigmaSuppressionStore? suppressions = null,
        bool sigmaEnabled = true)
    {
        _plugins = (plugins ?? [new SysmonSourcePlugin()]).ToList();
        _sigma = sigma ?? new SigmaMvpEngine();
        _suppressions = suppressions ?? new SigmaSuppressionStore(null);
        _sigmaEnabled = sigmaEnabled;
    }

    public (IReadOnlyList<SecurityEventRow> events, IReadOnlyList<DetectionRow> detections, IReadOnlyList<PluginHealthRow> health) Collect()
    {
        var allEvents = new List<SecurityEventRow>();
        var health = new List<PluginHealthRow>();
        foreach (var p in _plugins)
        {
            try
            {
                var envelope = p.Collect();
                allEvents.AddRange(envelope.Events);
                health.AddRange(envelope.Health);
            }
            catch (Exception ex)
            {
                health.Add(new PluginHealthRow(p.Name, false, ex.Message, DateTimeOffset.Now.ToString("o")));
            }
        }

        if (!_sigmaEnabled)
            return (allEvents.OrderByDescending(e => e.Time).Take(120).ToList(), [], health);

        _suppressions.ReloadIfStale(TimeSpan.FromSeconds(20));
        var orderedEvents = allEvents.OrderByDescending(e => e.Time).Take(120).ToList();
        var raw = _sigma.Match(orderedEvents);
        var detections = raw
            .Where(d => !_suppressions.IsSuppressed(d, orderedEvents))
            .ToList();
        return (orderedEvents, detections, health);
    }
}
