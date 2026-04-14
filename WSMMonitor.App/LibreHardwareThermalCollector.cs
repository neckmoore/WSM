using LibreHardwareMonitor.Hardware;

namespace WSMMonitor;

/// <summary>Optional LibreHardwareMonitor-based temperature sensors. WMI thermal path stays separate in <see cref="MetricsEngine"/>.</summary>
public sealed class LibreHardwareThermalCollector : IDisposable
{
    private readonly object _sync = new();
    private readonly LibreHardwareMonitorSection _cfg;
    private Computer? _computer;
    private bool _openFailed;
    private bool _disposed;
    private bool _loggedCollectFailure;

    public LibreHardwareThermalCollector(LibreHardwareMonitorSection cfg)
    {
        _cfg = cfg;
    }

    public IReadOnlyList<TempRow> CollectTemperatures()
    {
        if (!_cfg.Enabled || _disposed)
            return [];

        lock (_sync)
        {
            if (_openFailed)
                return [];

            try
            {
                EnsureOpen();
                if (_computer == null)
                    return [];

                var list = new List<TempRow>(Math.Clamp(_cfg.MaxSensors, 4, 96));
                foreach (var hw in _computer.Hardware)
                {
                    try
                    {
                        hw.Update();
                    }
                    catch
                    {
                        /* ignore per-device */
                    }

                    WalkHardware(hw, list);
                    if (list.Count >= _cfg.MaxSensors)
                        break;
                }

                return list;
            }
            catch (Exception ex)
            {
                if (!_loggedCollectFailure)
                {
                    _loggedCollectFailure = true;
                    try
                    {
                        Serilog.Log.Warning(ex, "LibreHardwareMonitor thermal collect failed (will retry next scrape)");
                    }
                    catch
                    {
                        /* */
                    }
                }

                return [];
            }
        }
    }

    private void WalkHardware(IHardware hw, List<TempRow> list)
    {
        if (list.Count >= _cfg.MaxSensors)
            return;

        foreach (var sh in hw.SubHardware)
        {
            try
            {
                sh.Update();
            }
            catch
            {
                /* */
            }

            WalkHardware(sh, list);
            if (list.Count >= _cfg.MaxSensors)
                return;
        }

        foreach (var s in hw.Sensors)
        {
            if (list.Count >= _cfg.MaxSensors)
                return;
            if (s.SensorType != SensorType.Temperature)
                continue;
            if (s.Value is not { } v)
                continue;
            // On some hosts, LHM can briefly expose 0.0 for CPU package/core sensors.
            // Treat that as invalid startup/stale value to avoid false temperature rows.
            if (v is <= 1 or > 170)
                continue;

            var hwName = Truncate(hw.Name ?? "?", 56);
            var sn = Truncate(s.Name ?? "?", 40);
            list.Add(new TempRow("LHM", $"{hwName} · {sn}", Math.Round(v, 1)));
        }
    }

    private void EnsureOpen()
    {
        if (_computer != null || _openFailed)
            return;

        try
        {
            var c = new Computer
            {
                IsCpuEnabled = _cfg.Cpu,
                IsGpuEnabled = _cfg.Gpu,
                IsMotherboardEnabled = _cfg.Motherboard,
                IsMemoryEnabled = _cfg.MemoryModules,
                IsStorageEnabled = _cfg.Storage,
                IsNetworkEnabled = false,
                IsControllerEnabled = _cfg.FanControllers,
                IsPsuEnabled = _cfg.Psu
            };
            c.Open();
            _computer = c;
        }
        catch (Exception ex)
        {
            _openFailed = true;
            try
            {
                Serilog.Log.Warning(ex, "LibreHardwareMonitor Computer.Open failed; thermal LHM path disabled");
            }
            catch
            {
                /* */
            }
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                _computer?.Close();
            }
            catch
            {
                /* */
            }

            _computer = null;
        }
    }
}
