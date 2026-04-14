using WSMMonitor;

namespace WSMMonitor.Tests;

public sealed class MetricsHistorySqliteConcurrencyTests
{
    [Fact]
    public async Task Concurrent_Append_and_Query_Does_NotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "wsm-test-" + Guid.NewGuid().ToString("n") + ".db");
        try
        {
            using var db = new MetricsHistorySqlite(path, retentionDays: 7);
            var start = DateTimeOffset.UtcNow.AddMinutes(-10);
            var writers = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            {
                for (var j = 0; j < 25; j++)
                {
                    var at = start.AddSeconds(i * 25 + j);
                    db.Append(new MetricsHistorySample(at, 10 + j, 50, 1, 0.1, 80));
                }
            })).ToArray();

            var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                for (var j = 0; j < 40; j++)
                    db.Query("15m", maxPoints: 200);
            })).ToArray();

            await Task.WhenAll(writers.Concat(readers));
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }
}
