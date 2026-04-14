namespace WSMMonitor.Plugins;

public sealed record SecurityEnvelope(
    IReadOnlyList<SecurityEventRow> Events,
    IReadOnlyList<PluginHealthRow> Health);

public interface IEventSourcePlugin
{
    string Name { get; }
    SecurityEnvelope Collect();
}
