using System.Text.Json;
using WSMMonitor.Plugins;

namespace WSMMonitor.Tests;

public sealed class SigmaSuppressionStoreTests
{
    [Fact]
    public void IsSuppressed_Matches_RuleId_And_Image()
    {
        var path = Path.Combine(Path.GetTempPath(), "wsm-suppress-test-" + Guid.NewGuid() + ".json");
        File.WriteAllText(
            path,
            """
            {"suppressions":[{"ruleId":"test_rule","imageContains":"goodtool.exe"}]}
            """);

        try
        {
            var store = new SigmaSuppressionStore(path);
            store.Reload();
            var events = new List<SecurityEventRow>
            {
                new("t1", "src", 1, "Microsoft-Windows-Sysmon", @"C:\goodtool.exe", "", "", "", 99L)
            };
            var d1 = new DetectionRow("test_rule", "T", "high", "sysmon", "t1", 1, "r", 99);
            Assert.True(store.IsSuppressed(d1, events));

            var d2 = new DetectionRow("test_rule", "T", "high", "sysmon", "t1", 1, "r", 100);
            Assert.False(store.IsSuppressed(d2, events));
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    [Fact]
    public void AgentStatusDto_RoundTrip_Includes_Diagnostics()
    {
        var dto = new AgentStatusDto(
            true, false, true, "2026-01-01T00:00:00Z", "",
            "1.2.3", "2026-01-02T00:00:00Z", 4242, 8787, "C:\\wsm.exe", "sqlite", true);
        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<AgentStatusDto>(json);
        Assert.NotNull(back);
        Assert.Equal("1.2.3", back.WsmVersion);
        Assert.Equal(4242, back.ProcessId);
        Assert.True(back.Ready);
        Assert.Equal("sqlite", back.HistoryPersistence);
    }
}
