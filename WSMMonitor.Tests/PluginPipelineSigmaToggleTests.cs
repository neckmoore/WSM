using WSMMonitor.Plugins;

namespace WSMMonitor.Tests;

public sealed class PluginPipelineSigmaToggleTests
{
    [Fact]
    public void Collect_WhenSigmaEnabled_ReturnsDetections()
    {
        var pipeline = BuildPipeline(sigmaEnabled: true);
        var (_, detections, health) = pipeline.Collect();

        Assert.NotEmpty(health);
        Assert.NotEmpty(detections);
    }

    [Fact]
    public void Collect_WhenSigmaDisabled_ReturnsNoDetections()
    {
        var pipeline = BuildPipeline(sigmaEnabled: false);
        var (_, detections, health) = pipeline.Collect();

        Assert.NotEmpty(health);
        Assert.Empty(detections);
    }

    private static PluginPipeline BuildPipeline(bool sigmaEnabled)
    {
        var plugins = new IEventSourcePlugin[] { new FakePlugin() };
        return new PluginPipeline(
            plugins: plugins,
            sigma: new SigmaMvpEngine(),
            suppressions: new SigmaSuppressionStore(null),
            sigmaEnabled: sigmaEnabled);
    }

    private sealed class FakePlugin : IEventSourcePlugin
    {
        public string Name => "fake";

        public SecurityEnvelope Collect()
        {
            var events = new List<SecurityEventRow>
            {
                new(
                    DateTimeOffset.UtcNow.ToString("o"),
                    "sysmon",
                    1,
                    "Microsoft-Windows-Sysmon",
                    @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                    "powershell.exe -enc aQBlAHgA",
                    "user",
                    "powershell encoded command",
                    42)
            };

            return new SecurityEnvelope(
                events,
                [new PluginHealthRow(Name, true, "ok", DateTimeOffset.UtcNow.ToString("o"))]);
        }
    }
}
