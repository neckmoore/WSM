using System.Diagnostics;
using System.ServiceProcess;

namespace WSMMonitor;

public sealed class WindowsServiceHost : ServiceBase
{
    public const string ServiceNameConst = "WSMMonitor";
    private AgentRuntime? _runtime;
    private readonly object _sync = new();

    public WindowsServiceHost()
    {
        ServiceName = ServiceNameConst;
        CanStop = true;
        AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        // SCM defaults to ~30s; first WMI/perf hits on Server can be slow — extend before heavy work.
        RequestAdditionalTime(120_000);
        try
        {
            lock (_sync)
            {
                _runtime = new AgentRuntime(serviceMode: true, port: WsmConfiguration.Current.Agent.Port);
                _runtime.Start();
            }
        }
        catch (Exception ex)
        {
            TryWriteStartupError(ex);
            lock (_sync)
            {
                try { _runtime?.Dispose(); } catch { /* */ }
                _runtime = null;
            }

            throw;
        }
    }

    protected override void OnStop()
    {
        lock (_sync)
        {
            _runtime?.Dispose();
            _runtime = null;
        }

        WsmLog.Close();
    }

    private static void TryWriteStartupError(Exception ex)
    {
        try
        {
            const string src = "WSMMonitor";
            if (!EventLog.SourceExists(src))
                EventLog.CreateEventSource(src, "Application");
            var detail = ex.ToString();
            if (detail.Length > 30000)
                detail = detail[..30000] + "…";
            EventLog.WriteEntry(src, "WSM Monitor service failed to start:\n" + detail, EventLogEntryType.Error, 8788);
        }
        catch
        {
            /* ignore */
        }
    }

    public static void RunAsService()
    {
        ServiceBase.Run(new WindowsServiceHost());
    }
}
