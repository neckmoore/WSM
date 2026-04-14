using System.Diagnostics.Eventing.Reader;
using System.Xml.Linq;

namespace WSMMonitor.Plugins;

public sealed class SysmonSourcePlugin : IEventSourcePlugin
{
    private const string SysmonLog = "Microsoft-Windows-Sysmon/Operational";
    public string Name => "Sysmon";

    public SecurityEnvelope Collect()
    {
        var events = new List<SecurityEventRow>();
        try
        {
            var q = new EventLogQuery(SysmonLog, PathType.LogName) { ReverseDirection = true };
            using var reader = new EventLogReader(q);
            for (int i = 0; i < 80; i++)
            {
                using var e = reader.ReadEvent();
                if (e == null) break;
                var sec = ParseSysmonEvent(e);
                if (sec != null) events.Add(sec);
            }

            return new SecurityEnvelope(
                events,
                [new PluginHealthRow(Name, true, $"ok, events={events.Count}", DateTimeOffset.Now.ToString("o"))]);
        }
        catch (EventLogNotFoundException)
        {
            return new SecurityEnvelope(
                [],
                [new PluginHealthRow(Name, false, "Sysmon log not found", DateTimeOffset.Now.ToString("o"))]);
        }
        catch (Exception ex)
        {
            return new SecurityEnvelope(
                [],
                [new PluginHealthRow(Name, false, ex.Message, DateTimeOffset.Now.ToString("o"))]);
        }
    }

    private static SecurityEventRow? ParseSysmonEvent(EventRecord e)
    {
        try
        {
            string xml = e.ToXml();
            var doc = XDocument.Parse(xml);

            string data(string name) =>
                doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Data" && (string?)x.Attribute("Name") == name)?.Value ?? "";

            var image = data("Image");
            var commandLine = data("CommandLine");
            var user = data("User");
            var msg = commandLine.Length > 0 ? commandLine : image;
            if (msg.Length > 220) msg = msg[..217] + "...";

            long? recordId = e is EventLogRecord el ? el.RecordId : null;
            return new SecurityEventRow(
                (e.TimeCreated ?? DateTime.Now).ToString("o"),
                "sysmon",
                e.Id,
                e.ProviderName ?? "",
                image,
                commandLine,
                user,
                msg,
                recordId);
        }
        catch
        {
            return null;
        }
    }
}
