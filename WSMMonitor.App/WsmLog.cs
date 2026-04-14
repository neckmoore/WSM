using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace WSMMonitor;

public static class WsmLog
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    public static void Initialize(LoggingSection options)
    {
        lock (InitLock)
        {
            if (_initialized)
                return;
            InitializeCore(options);
            _initialized = true;
        }
    }

    /// <summary>Rebuild file sink after settings change (companion).</summary>
    public static void Reinitialize(LoggingSection options)
    {
        lock (InitLock)
        {
            CloseCore();
            _initialized = false;
            InitializeCore(options);
            _initialized = true;
        }
    }

    private static void InitializeCore(LoggingSection options)
    {
        if (!options.Enabled)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
            return;
        }

        var level = ParseLevel(options.MinimumLevel);
        var rel = options.Path.Trim();
        if (string.IsNullOrEmpty(rel)) rel = "logs/wsm-.log";
        var full = Path.IsPathRooted(rel) ? rel : Path.Combine(AppContext.BaseDirectory, rel);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var retention = Math.Clamp(options.RetentionDays, 1, 90);

        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    full,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retention,
                    shared: true)
                .CreateLogger();

            Log.Information(
                "WSM Monitor logging started ({Identity}, exe {Build}); log_retention_days={Retention}",
                WsmBuildInfo.BuildIdentity,
                WsmBuildInfo.BuildDateUtc,
                retention);
        }
        catch (Exception ex)
        {
            TryWriteInstallLogWarning($"WSM Monitor file logging disabled ({full}): {ex.Message}");
            Log.Logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
        }
    }

    private static void TryWriteInstallLogWarning(string message)
    {
        try
        {
            const string src = "WSMMonitor";
            if (!EventLog.SourceExists(src))
                EventLog.CreateEventSource(src, "Application");
            EventLog.WriteEntry(src, message, EventLogEntryType.Warning, 8787);
        }
        catch
        {
            /* ignore */
        }
    }

    private static LogEventLevel ParseLevel(string s)
    {
        return Enum.TryParse<LogEventLevel>(s, true, out var l) ? l : LogEventLevel.Information;
    }

    private static void CloseCore()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
            /* ignore */
        }
    }

    public static void Close()
    {
        lock (InitLock)
        {
            CloseCore();
            _initialized = false;
        }
    }
}
