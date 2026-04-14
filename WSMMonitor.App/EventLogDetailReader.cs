using System.Diagnostics.Eventing.Reader;

namespace WSMMonitor;

public static class EventLogDetailReader
{
    /// <summary>Reads a single event by log name and EventRecordID (classic / operational logs).</summary>
    public static LogEventDetailDto? TryRead(string logName, long recordId)
    {
        if (string.IsNullOrWhiteSpace(logName)) return null;
        try
        {
            var xpath = $"*[System[EventRecordID={recordId}]]";
            var q = new EventLogQuery(logName, PathType.LogName, xpath) { ReverseDirection = true };
            using var reader = new EventLogReader(q);
            using var e = reader.ReadEvent();
            if (e == null) return null;

            string level = (e.Level ?? 0) switch
            {
                1 => "Critical",
                2 => "Error",
                3 => "Warning",
                4 => "Information",
                _ => e.Level?.ToString() ?? ""
            };
            string msg;
            try { msg = e.FormatDescription() ?? ""; }
            catch { msg = "(unable to format)"; }

            return new LogEventDetailDto(
                logName,
                recordId,
                e.Id,
                e.TimeCreated?.ToString("o") ?? "",
                level,
                e.ProviderName ?? "",
                msg,
                e.ToXml());
        }
        catch
        {
            return null;
        }
    }
}
