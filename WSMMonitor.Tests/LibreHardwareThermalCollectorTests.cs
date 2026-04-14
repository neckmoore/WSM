namespace WSMMonitor.Tests;

public sealed class LibreHardwareThermalCollectorTests
{
    [Fact]
    public void Disabled_returns_empty_without_throwing()
    {
        using var c = new LibreHardwareThermalCollector(new LibreHardwareMonitorSection { Enabled = false });
        var t = c.CollectTemperatures();
        Assert.Empty(t);
    }

    [Fact]
    public void Enabled_collect_does_not_throw_on_headless_or_restricted_host()
    {
        using var c = new LibreHardwareThermalCollector(new LibreHardwareMonitorSection
        {
            Enabled = true,
            Cpu = true,
            Gpu = false,
            Motherboard = false,
            MaxSensors = 8
        });
        var t = c.CollectTemperatures();
        Assert.NotNull(t);
    }
}
