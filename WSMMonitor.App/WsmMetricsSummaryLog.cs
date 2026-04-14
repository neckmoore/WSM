using System.Text;
using Serilog;

namespace WSMMonitor;

/// <summary>One structured block per hour: key metrics + Windows log errors (not every scrape).</summary>
public static class WsmMetricsSummaryLog
{
    private const int MaxEventMessageLen = 480;
    private const int MaxEventsInBlock = 10;

    public static void WriteHourlyBlock(MetricsDto m, bool serviceMode)
    {
        var o = WsmConfiguration.Current.Logging;
        if (!o.Enabled || !o.HourlySummaryEnabled)
            return;

        try
        {
            var sb = new StringBuilder(4096);
            var utcHour = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0, DateTimeKind.Utc);
            var role = serviceMode ? "service" : "companion";

            sb.AppendLine($"[HOURLY-SUMMARY] utc_hour={utcHour:O}  role={role}  scrape_timestamp={m.Timestamp}");
            sb.AppendLine("# --- aggregate (this hour’s snapshot) ---");
            sb.AppendLine($"# health_score={m.HealthScore}  cpu_total_pct={m.CpuTotalPct:0.0}  cpu_queue={m.CpuQueueLength:0.00}  cores_logical={m.CpuLogicalCores}");
            var mem = m.Memory;
            sb.AppendLine(
                $"# memory_used_pct={mem.UsedPct:0.1}  mem_used_mib={mem.UsedMiB}  mem_total_mib={mem.TotalMiB}  commit_pct={(mem.CommitPct ?? 0):0.1}");

            sb.AppendLine("# --- disks (volumes) ---");
            foreach (var d in m.Disks.Take(12))
                sb.AppendLine($"# disk  label={d.Label}  free_pct={d.FreePct:0.1}  free_gb={d.FreeGb:0.1}  size_gb={d.SizeGb:0.1}");

            sb.AppendLine("# --- thermal ---");
            if (m.Thermal.Count == 0)
                sb.AppendLine("# thermal  (no rows)");
            else
            {
                var maxT = m.Thermal.Max(t => t.Celsius);
                sb.AppendLine($"# thermal  count={m.Thermal.Count}  max_c={maxT:0.1}");
                foreach (var t in m.Thermal.OrderByDescending(x => x.Celsius).Take(6))
                    sb.AppendLine($"# thermal  src={t.Source}  name={t.Name}  c={t.Celsius:0.1}");
            }

            sb.AppendLine("# --- top cpu processes (snapshot) ---");
            foreach (var p in m.TopCpu.Take(5))
                sb.AppendLine($"# top_cpu  name={p.Name}  pid={p.Id}  approx_cpu_pct={p.CpuPctApprox:0.1}");

            sb.AppendLine("# --- Windows log errors / critical (recent in scrape) ---");
            var bad = m.Events
                .Where(e => e.Level.Contains("Error", StringComparison.OrdinalIgnoreCase)
                            || e.Level.Contains("Critical", StringComparison.OrdinalIgnoreCase)
                            || e.Level.Contains("Ошибка", StringComparison.OrdinalIgnoreCase))
                .Take(MaxEventsInBlock)
                .ToList();
            if (bad.Count == 0)
                sb.AppendLine("# win_events  (none in this scrape)");
            else
            {
                foreach (var e in bad)
                {
                    var msg = (e.Message ?? "").Replace('\r', ' ').Replace('\n', ' ');
                    if (msg.Length > MaxEventMessageLen)
                        msg = msg[..MaxEventMessageLen] + "…";
                    sb.AppendLine(
                        $"# win_event  log={e.Log}  level={e.Level}  time={e.Time}  id={e.WinEventId}  provider={e.Provider}  msg={msg}");
                }
            }

            if (m.Alerts.Count > 0)
            {
                sb.AppendLine("# --- alerts ---");
                foreach (var a in m.Alerts.Take(6))
                    sb.AppendLine($"# alert  sev={a.Severity}  code={a.Code}  text={Truncate(a.Text, 200)}");
            }

            Log.Information("{HourlyBlock}", sb.ToString().TrimEnd());
        }
        catch
        {
            /* never break agent for logging */
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }
}
