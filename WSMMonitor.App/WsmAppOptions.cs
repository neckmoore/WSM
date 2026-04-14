namespace WSMMonitor;

public sealed class WsmAppOptions
{
    public AgentSection Agent { get; set; } = new();
    public LoggingSection Logging { get; set; } = new();
    public HistorySection History { get; set; } = new();
    public SigmaSection Sigma { get; set; } = new();
    public LibreHardwareMonitorSection LibreHardwareMonitor { get; set; } = new();
    public UiSection Ui { get; set; } = new();
}

/// <summary>Companion UI: language and how the tray app attaches to metrics HTTP.</summary>
public sealed class UiSection
{
    /// <summary><c>ru</c> (default) or <c>en</c>.</summary>
    public string Language { get; set; } = "ru";

    /// <summary><c>service</c> = prefer service agent on the port; <c>companion</c> = prefer embedded agent while tray runs.</summary>
    public string WorkMode { get; set; } = "companion";
}

public sealed class AgentSection
{
    public int Port { get; set; } = 8787;
    /// <summary>Background history sample interval (seconds).</summary>
    public int HistorySampleSeconds { get; set; } = 12;
}

public sealed class LoggingSection
{
    public bool Enabled { get; set; } = true;
    /// <summary>Relative to app base directory unless absolute.</summary>
    public string Path { get; set; } = "logs/wsm-.log";
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>Daily rolling log files kept (then oldest deleted). Default 7.</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Append one structured hourly block (CPU / memory / disks / thermal / Windows errors).</summary>
    public bool HourlySummaryEnabled { get; set; } = true;

    /// <summary>Hours between hourly summary lines (1–24).</summary>
    public int HourlySummaryIntervalHours { get; set; } = 1;
}

public sealed class HistorySection
{
    public bool SqliteEnabled { get; set; }
    public string SqlitePath { get; set; } = "data/metrics-history.db";
    public int RetentionDays { get; set; } = 30;
}

public sealed class SigmaSection
{
    /// <summary>JSON suppressions file; empty uses rules/sigma/suppressions.json next to exe.</summary>
    public string SuppressionsPath { get; set; } = "";
}

/// <summary>Optional LibreHardwareMonitorLib sensors (temperatures). WMI ACPI path in MetricsEngine is unchanged.</summary>
public sealed class LibreHardwareMonitorSection
{
    public bool Enabled { get; set; }

    /// <summary>CPU package / core temps.</summary>
    public bool Cpu { get; set; } = true;

    public bool Gpu { get; set; } = true;
    public bool Motherboard { get; set; } = true;

    /// <summary>DIMM / module sensors (can add noise).</summary>
    public bool MemoryModules { get; set; }

    public bool Storage { get; set; }
    public bool FanControllers { get; set; }
    public bool Psu { get; set; }

    /// <summary>Cap rows merged into API (after WMI list).</summary>
    public int MaxSensors { get; set; } = 48;
}
