using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Threading;

namespace WSMMonitor;

public sealed class MainForm : Form
{
    private const int MaxReservePorts = 16;

    private static readonly string AppBuildLabel = BuildAppLabel();

    private static string BuildAppLabel()
    {
        try
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
            var path = Environment.ProcessPath ?? "";
            var utc = string.IsNullOrEmpty(path) ? DateTime.UtcNow : File.GetLastWriteTimeUtc(path);
            return $"v{ver} · {utc:MM-dd HH:mm} UTC";
        }
        catch
        {
            return "";
        }
    }

    private HttpClient _http;
    private readonly SemaphoreSlim _dashboardHttpGate = new(1, 1);
    private readonly System.Windows.Forms.Timer _heartbeat = new() { Interval = 4000 };
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly Icon _trayIcon = AgentIconFactory.CreateShieldPulseIcon(32);
    private readonly Icon _windowIcon;

    private readonly Label _titleLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) };
    private readonly Label _agentStatus = new() { AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
    private readonly Label _serviceStatus = new() { AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
    private readonly Label _httpStatus = new() { AutoSize = true, Font = new Font("Segoe UI", 9F) };
    private readonly Label _lastSample = new() { AutoSize = true, Font = new Font("Segoe UI", 9F) };
    private readonly Label _details = new() { AutoSize = false, Height = 120, Dock = DockStyle.Top, Font = new Font("Segoe UI", 9F) };
    private Button _btnSettings = null!;
    private Button _btnDashboard = null!;
    private Button _btnCopyDiag = null!;
    private Button _btnSvcStart = null!;
    private Button _btnSvcStop = null!;
    private Button _btnSvcRestart = null!;
    private AgentRuntime? _embeddedRuntime;
    /// <summary>Configured primary listen port (from appsettings).</summary>
    private int _primaryPort = WsmConfiguration.Current.Agent.Port;
    /// <summary>URL port for "open dashboard" and heartbeat when embedded agent is used (8788+ if service holds primary).</summary>
    private int _dashboardPort;
    private AgentStatusDto? _lastStatus;
    private bool _balloonShown;
    private bool _allowClose;
    private bool _startupUiReady;

    /// <summary>Avoid stealing focus from the startup splash while the main window is still transparent.</summary>
    protected override bool ShowWithoutActivation => !_startupUiReady;

    public MainForm(string[] args)
    {
        _ = args;
        _dashboardPort = _primaryPort;
        _http = CreateHttpClient(_primaryPort);
        _windowIcon = (Icon)_trayIcon.Clone();
        Icon = _windowIcon;
        Text = WsmLocalization.T("AppTitle");
        Width = 560;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Opacity = 0;

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _titleLabel.Text = $"{WsmLocalization.T("CompanionLine")} · {AppBuildLabel}";
        flow.Controls.Add(_titleLabel);
        flow.Controls.Add(_agentStatus);
        flow.Controls.Add(_serviceStatus);
        flow.Controls.Add(_httpStatus);
        flow.Controls.Add(_lastSample);
        flow.Controls.Add(_details);

        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        _btnSettings = MkBtn(WsmLocalization.T("BtnSettings"), (_, _) => OpenSettingsDialog());
        _btnDashboard = MkBtn(WsmLocalization.T("BtnDashboard"), (_, _) => OpenWebDashboard());
        _btnCopyDiag = MkBtn(WsmLocalization.T("BtnCopyDiag"), (_, _) => CopyDiagnosticsToClipboard());
        _btnSvcStart = MkBtn(WsmLocalization.T("BtnSvcStart"), (_, _) => ExecuteServiceAction(ServiceControl.StartService));
        _btnSvcStop = MkBtn(WsmLocalization.T("BtnSvcStop"), (_, _) => ExecuteServiceAction(ServiceControl.StopService));
        _btnSvcRestart = MkBtn(WsmLocalization.T("BtnSvcRestart"), (_, _) => ExecuteServiceAction(ServiceControl.RestartService));
        actions.Controls.Add(_btnSettings);
        actions.Controls.Add(_btnDashboard);
        actions.Controls.Add(_btnCopyDiag);
        actions.Controls.Add(_btnSvcStart);
        actions.Controls.Add(_btnSvcStop);
        actions.Controls.Add(_btnSvcRestart);
        flow.Controls.Add(actions);

        root.Controls.Add(flow);
        Controls.Add(root);

        ApplyMainFormLocalization();
        _tray.DoubleClick += (_, _) => ShowStatusWindow();
        _heartbeat.Tick += async (_, _) => await HeartbeatAsync();
        Load += async (_, _) =>
        {
            using var splash = new StartupSplashForm();
            splash.Show();
            var splashMin = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await EnsureEmbeddedAgentAsync();
                await HeartbeatAsync();
            }
            finally
            {
                var remaining = StartupSplashForm.MinimumHostDisplayMs - (int)splashMin.ElapsedMilliseconds;
                if (remaining > 0)
                    await Task.Delay(remaining);
                splash.Close();
                _startupUiReady = true;
            }

            _heartbeat.Start();
            await HeartbeatAsync();
            BeginInvoke(() =>
            {
                // Refresh tray icon cache after replacing the exe.
                _tray.Visible = false;
                _tray.Icon = _trayIcon;
                _tray.Visible = true;
                if (_embeddedRuntime != null && !_balloonShown)
                {
                    _balloonShown = true;
                    try
                    {
                        _tray.ShowBalloonTip(
                            4500,
                            WsmLocalization.T("BalloonTitle"),
                            WsmLocalization.Tf("BalloonAgentUrl", _dashboardPort),
                            ToolTipIcon.Info);
                    }
                    catch { /* */ }
                }

                HideToTray();
            });
        };
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) HideToTray();
        };
        FormClosing += OnFormClosing;
    }

    private static HttpClient CreateHttpClient(int port) =>
        new() { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>Start or align embedded HTTP agent (primary or reserve ports) based on work mode and what listens on the primary port.</summary>
    private async Task EnsureEmbeddedAgentAsync()
    {
        if (WsmLocalization.IsCompanionWorkMode(WsmConfiguration.Current.Ui.WorkMode))
        {
            if (TryStartEmbedded(_primaryPort))
            {
                _dashboardPort = _embeddedRuntime!.Port;
                return;
            }

            if (TryStartEmbeddedOnReservedPorts(out var altCompanion))
            {
                _http.Dispose();
                _http = CreateHttpClient(altCompanion);
                _dashboardPort = altCompanion;
                if (altCompanion != _primaryPort)
                    _details.Text = WsmLocalization.Tf("DetailCompanionAltPortFmt", _primaryPort, altCompanion);
                return;
            }
        }

        AgentStatusDto? onPrimary = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            onPrimary = await _http.GetFromJsonAsync<AgentStatusDto>("api/v1/agent-status", cts.Token);
        }
        catch
        {
            /* nothing on 8787 or not our API */
        }

        if (onPrimary is null)
        {
            if (TryStartEmbedded(_primaryPort))
            {
                _dashboardPort = _embeddedRuntime!.Port;
                return;
            }

            // Port busy or not our API: use a reserve port so the browser does not stick to a foreign agent.
            if (TryStartEmbeddedOnReservedPorts(out var alt))
            {
                _http.Dispose();
                _http = CreateHttpClient(alt);
                _dashboardPort = alt;
                _details.Text = WsmLocalization.Tf("DetailPrimaryBadApiFmt", _primaryPort, alt);
                return;
            }

            _dashboardPort = _primaryPort;
            _details.Text = WsmLocalization.Tf("DetailEmbNotStartedFmt", _primaryPort, _primaryPort + MaxReservePorts);
            return;
        }

        var expect = WsmBuildInfo.BuildIdentity;
        var sameBuild = BuildIdentityMatches(onPrimary.WsmVersion, expect);

        // Same build already on primary: do not start a second embedded instance.
        if (sameBuild && !onPrimary.ServiceMode)
        {
            _dashboardPort = _primaryPort;
            return;
        }

        if (sameBuild && onPrimary.ServiceMode)
        {
            _dashboardPort = _primaryPort;
            return;
        }

        // Foreign version on primary: need reserve port or the dashboard would stay on the wrong build.
        if (TryStartEmbeddedOnReservedPorts(out var chosen))
        {
            _http.Dispose();
            _http = CreateHttpClient(chosen);
            _dashboardPort = chosen;
            var who = onPrimary.ServiceMode
                ? WsmLocalization.Tf("DetailWhoService", _primaryPort)
                : WsmLocalization.Tf("DetailWhoOtherFmt", _primaryPort, onPrimary.WsmVersion, expect);
            _details.Text = WsmLocalization.Tf("DetailDashThisBuildFmt", chosen, who);
            return;
        }

        _dashboardPort = _primaryPort;
        _details.Text = WsmLocalization.Tf(
            "DetailWrongVerNoReserveFmt",
            _primaryPort,
            onPrimary.WsmVersion,
            _primaryPort + 1,
            _primaryPort + MaxReservePorts);
    }

    /// <summary>After saving settings: sync HTTP port, restart embedded agent if work mode/port changed.</summary>
    private async Task ReapplyConfigurationAfterSettingsAsync()
    {
        _primaryPort = WsmConfiguration.Current.Agent.Port;
        try { _embeddedRuntime?.Dispose(); } catch { /* */ }
        _embeddedRuntime = null;
        _http.Dispose();
        _http = CreateHttpClient(_primaryPort);
        _dashboardPort = _primaryPort;
        await EnsureEmbeddedAgentAsync();
        await HeartbeatAsync();
    }

    private bool TryStartEmbedded(int port)
    {
        AgentRuntime? rt = null;
        try
        {
            rt = new AgentRuntime(serviceMode: false, port: port, options: WsmConfiguration.Current);
            rt.Start();
            _embeddedRuntime = rt;
            return true;
        }
        catch (Exception ex)
        {
            try { rt?.Dispose(); } catch { /* */ }
            _embeddedRuntime = null;
            _details.Text = WsmLocalization.Tf("EmbAgentFailFmt", port, ex.Message);
            return false;
        }
    }

    private static Button MkBtn(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        b.Click += click;
        return b;
    }

    private void BuildTray()
    {
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add(WsmLocalization.T("TrayShow"), null, (_, _) => ShowStatusWindow());
        _trayMenu.Items.Add(WsmLocalization.T("TraySettings"), null, (_, _) => OpenSettingsDialog());
        _trayMenu.Items.Add(WsmLocalization.T("TrayDashboard"), null, (_, _) => OpenWebDashboard());
        _trayMenu.Items.Add(WsmLocalization.T("TraySvcStart"), null, (_, _) => ExecuteServiceAction(ServiceControl.StartService));
        _trayMenu.Items.Add(WsmLocalization.T("TraySvcStop"), null, (_, _) => ExecuteServiceAction(ServiceControl.StopService));
        _trayMenu.Items.Add(WsmLocalization.T("TraySvcRestart"), null, (_, _) => ExecuteServiceAction(ServiceControl.RestartService));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(WsmLocalization.T("TrayExit"), null, (_, _) => { _allowClose = true; Close(); });

        _tray.ContextMenuStrip = _trayMenu;
        _tray.Icon = _trayIcon;
        _tray.Text = $"WSM {AppBuildLabel}";
        _tray.Visible = true;
    }

    private void ApplyMainFormLocalization()
    {
        Text = WsmLocalization.T("AppTitle");
        _titleLabel.Text = $"{WsmLocalization.T("CompanionLine")} · {AppBuildLabel}";
        _btnSettings.Text = WsmLocalization.T("BtnSettings");
        _btnDashboard.Text = WsmLocalization.T("BtnDashboard");
        _btnCopyDiag.Text = WsmLocalization.T("BtnCopyDiag");
        _btnSvcStart.Text = WsmLocalization.T("BtnSvcStart");
        _btnSvcStop.Text = WsmLocalization.T("BtnSvcStop");
        _btnSvcRestart.Text = WsmLocalization.T("BtnSvcRestart");
        BuildTray();
    }

    private async Task HeartbeatAsync()
    {
        await _dashboardHttpGate.WaitAsync();
        try
        {
            var status = await _http.GetFromJsonAsync<AgentStatusDto>("api/v1/agent-status");
            var svcText = ReadServiceStatus();
            if (status == null)
            {
                SetStatus(
                    WsmLocalization.T("AgentUnknown"),
                    svcText,
                    WsmLocalization.T("HttpUnreachable"),
                    "-",
                    WsmLocalization.T("NoPayload"));
                return;
            }

            _lastStatus = status;
            var modeNote = _embeddedRuntime != null
                ? _dashboardPort == _primaryPort
                    ? WsmLocalization.Tf("ModeEmbeddedPrimary", _primaryPort)
                    : WsmLocalization.Tf("ModeEmbeddedAlt", _dashboardPort, _primaryPort)
                : status.ServiceMode
                    ? WsmLocalization.Tf("ModeServiceOnly", _primaryPort)
                    : "";
            var detail = string.IsNullOrWhiteSpace(status.LastError)
                ? WsmLocalization.T("DetailNoErrors")
                : status.LastError;
            if (!string.IsNullOrEmpty(modeNote))
                detail = modeNote + " " + detail;
            var diagLine = WsmLocalization.Tf(
                "DiagHbFmt",
                status.WsmVersion,
                string.IsNullOrWhiteSpace(status.BuildDateUtc) ? "—" : status.BuildDateUtc,
                status.ProcessId,
                status.ListenPort > 0 ? status.ListenPort : _dashboardPort,
                string.IsNullOrEmpty(status.HistoryPersistence) ? "-" : status.HistoryPersistence,
                status.Ready);
            var diag = new StringBuilder();
            diag.Append(diagLine);
            if (!string.IsNullOrWhiteSpace(status.ExePath))
                diag.Append("\r\n").Append(status.ExePath);
            diag.Append("\r\n");
            SetStatus(
                status.AgentRunning ? WsmLocalization.T("AgentRunning") : WsmLocalization.T("AgentStopped"),
                svcText,
                status.HttpListening ? WsmLocalization.T("HttpListening") : WsmLocalization.T("HttpNotListening"),
                string.IsNullOrWhiteSpace(status.LastMetricsAt) ? "-" : status.LastMetricsAt,
                diag + "\r\n" + WsmLocalization.T("DetailsPrefix") + " " + detail.Trim());
        }
        catch (Exception ex)
        {
            SetStatus(
                WsmLocalization.T("AgentUnavailable"),
                ReadServiceStatus(),
                WsmLocalization.T("HttpUnavailable"),
                "-",
                ex.Message);
        }
        finally
        {
            _dashboardHttpGate.Release();
        }
    }

    private static string ReadServiceStatus()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController(WindowsServiceHost.ServiceNameConst);
            return $"Service {sc.Status}";
        }
        catch
        {
            return "Service not installed";
        }
    }

    private void SetStatus(string agent, string service, string http, string last, string details)
    {
        _agentStatus.Text = WsmLocalization.Tf("StatusAgentFmt", agent);
        _serviceStatus.Text = WsmLocalization.Tf("StatusServiceFmt", service);
        _httpStatus.Text = WsmLocalization.Tf("StatusHttpFmt", http, _dashboardPort);
        _lastSample.Text = WsmLocalization.Tf("StatusLastHbFmt", last);
        _details.Text = WsmLocalization.Tf("DetailsLineFmt", details);

        _tray.Text = WsmLocalization.Tf("TrayTextFmt", AppBuildLabel, agent, service);
    }

    private void ExecuteServiceAction(Func<string> action)
    {
        try
        {
            if (!ServiceControl.IsAdministrator())
            {
                MessageBox.Show(WsmLocalization.T("AdminRequired"), WsmLocalization.T("MsgBoxTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var msg = action();
            _details.Text = WsmLocalization.Tf("DetailsLineFmt", msg);
            _ = HeartbeatAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, WsmLocalization.T("MsgBoxTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettingsDialog()
    {
        var before = WsmConfiguration.CloneCurrent();
        using var dlg = new SettingsForm();
        dlg.Icon = Icon;
        dlg.SettingsPersisted += async (_, _) =>
        {
            var after = WsmConfiguration.Current;
            if (NeedLoggingReinitialize(before, after))
            {
                try
                {
                    WsmLog.Reinitialize(after.Logging);
                }
                catch
                {
                    /* ignore */
                }
            }

            ApplyMainFormLocalization();
            try
            {
                if (NeedEmbeddedAgentReapply(before, after))
                    await ReapplyConfigurationAfterSettingsAsync();
                else
                    await HeartbeatAsync();
                before = WsmConfiguration.CloneCurrent();
            }
            catch
            {
                /* ignore */
            }
        };
        dlg.ShowDialog(this);
    }

    private static bool NeedEmbeddedAgentReapply(WsmAppOptions before, WsmAppOptions after)
    {
        var portChanged = before.Agent.Port != after.Agent.Port;
        var beforeMode = string.Equals(before.Ui.WorkMode, "service", StringComparison.OrdinalIgnoreCase)
            ? "service"
            : "companion";
        var afterMode = string.Equals(after.Ui.WorkMode, "service", StringComparison.OrdinalIgnoreCase)
            ? "service"
            : "companion";
        var modeChanged = !string.Equals(beforeMode, afterMode, StringComparison.Ordinal);
        return portChanged || modeChanged;
    }

    private static bool NeedLoggingReinitialize(WsmAppOptions before, WsmAppOptions after)
    {
        var b = before.Logging;
        var a = after.Logging;
        if (b.Enabled != a.Enabled) return true;
        if (!string.Equals((b.Path ?? "").Trim(), (a.Path ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals((b.MinimumLevel ?? "").Trim(), (a.MinimumLevel ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        if (b.RetentionDays != a.RetentionDays) return true;
        if (b.HourlySummaryEnabled != a.HourlySummaryEnabled) return true;
        if (b.HourlySummaryIntervalHours != a.HourlySummaryIntervalHours) return true;
        return false;
    }

    private void CopyDiagnosticsToClipboard()
    {
        var s = _lastStatus;
        if (s == null)
        {
            try { Clipboard.SetText(WsmLocalization.T("DiagNoData")); } catch { /* */ }
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(WsmLocalization.T("DiagHeader"));
        sb.Append(WsmLocalization.T("DiagVersion")).Append(": ").AppendLine(s.WsmVersion);
        sb.Append(WsmLocalization.T("DiagBuild")).Append(": ").AppendLine(s.BuildDateUtc);
        sb.Append(WsmLocalization.T("DiagPid")).Append(": ").AppendLine(s.ProcessId.ToString());
        sb.Append(WsmLocalization.T("DiagDashPort")).Append(": ").AppendLine(_dashboardPort.ToString());
        sb.Append(WsmLocalization.T("DiagApiPort")).Append(": ").AppendLine(s.ListenPort.ToString());
        sb.Append(WsmLocalization.T("DiagHistory")).Append(": ").AppendLine(s.HistoryPersistence);
        sb.Append(WsmLocalization.T("DiagReady")).Append(": ").AppendLine(s.Ready.ToString());
        sb.Append(WsmLocalization.T("DiagSvcMode")).Append(": ").AppendLine(s.ServiceMode ? "service" : "embedded");
        sb.Append(WsmLocalization.T("DiagAgent")).Append(": ").AppendLine(s.AgentRunning ? "running" : "stopped");
        sb.Append(WsmLocalization.T("DiagHttp")).Append(": ").AppendLine(s.HttpListening ? "listening" : "not listening");
        sb.Append(WsmLocalization.T("DiagLastSample")).Append(": ").AppendLine(s.LastMetricsAt);
        sb.Append(WsmLocalization.T("DiagError")).Append(": ").AppendLine(string.IsNullOrEmpty(s.LastError) ? "-" : s.LastError);
        sb.Append(WsmLocalization.T("DiagExe")).Append(": ").AppendLine(s.ExePath);
        try { Clipboard.SetText(sb.ToString()); } catch { /* */ }
        try
        {
            _tray.ShowBalloonTip(2500, WsmLocalization.T("BalloonTitle"), WsmLocalization.T("DiagCopied"), ToolTipIcon.Info);
        }
        catch { /* */ }
    }

    private void OpenWebDashboard() => _ = OpenWebDashboardAsync();

    private async Task OpenWebDashboardAsync()
    {
        await _dashboardHttpGate.WaitAsync();
        try
        {
            await ReconcileDashboardPortBeforeOpenAsync();

            var bust = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expect = WsmBuildInfo.BuildIdentity;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var st = await _http.GetFromJsonAsync<AgentStatusDto>("api/v1/agent-status", cts.Token);
                if (!BuildIdentityMatches(st?.WsmVersion, expect))
                {
                    var svcBin = ServiceControl.TryGetConfiguredServiceBinaryPath();
                    var sb = new StringBuilder();
                    sb.AppendLine(WsmLocalization.Tf("VerMismatchOpen1Fmt", _dashboardPort, expect, st?.WsmVersion ?? "—"));
                    sb.AppendLine();
                    sb.AppendLine(WsmLocalization.T("VerMismatchOpen2"));
                    sb.AppendLine(WsmLocalization.T("VerMismatchOpen3"));
                    if (!string.IsNullOrWhiteSpace(svcBin))
                    {
                        sb.AppendLine();
                        sb.AppendLine(WsmLocalization.Tf("VerMismatchOpenBinFmt", svcBin));
                    }

                    MessageBox.Show(sb.ToString(), WsmLocalization.T("MsgVersionTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch
            {
                /* ignore */
            }

            var lang = Uri.EscapeDataString(WsmLocalization.DashboardLang);
            var url =
                $"http://127.0.0.1:{_dashboardPort}/?nocache={bust}&wsm_expect={Uri.EscapeDataString(expect)}&ui_lang={lang}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        finally
        {
            _dashboardHttpGate.Release();
        }
    }

    /// <summary>Align HTTP client with embedded port or a reserve port if the primary agent build mismatches.</summary>
    private async Task ReconcileDashboardPortBeforeOpenAsync()
    {
        if (_embeddedRuntime != null)
        {
            var ep = _embeddedRuntime.Port;
            if (_dashboardPort != ep)
                _dashboardPort = ep;
            if (_http.BaseAddress is null || _http.BaseAddress.Port != ep)
            {
                _http.Dispose();
                _http = CreateHttpClient(ep);
            }

            return;
        }

        var expect = WsmBuildInfo.BuildIdentity;
        AgentStatusDto? st = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            st = await _http.GetFromJsonAsync<AgentStatusDto>("api/v1/agent-status", cts.Token);
        }
        catch
        {
            /* ignore */
        }

        if (BuildIdentityMatches(st?.WsmVersion, expect))
            return;

        if (!TryStartEmbeddedOnReservedPorts(out var chosen))
            return;

        _http.Dispose();
        _http = CreateHttpClient(chosen);
        _dashboardPort = chosen;
        _details.Text = WsmLocalization.Tf(
            "DetailReconcileDashFmt",
            chosen,
            _primaryPort,
            expect,
            st?.WsmVersion ?? "—");
    }

    /// <summary>Try embedded agent on primary+1 … primary+MaxReservePorts.</summary>
    private bool TryStartEmbeddedOnReservedPorts(out int chosenPort)
    {
        for (var p = _primaryPort + 1; p <= _primaryPort + MaxReservePorts; p++)
        {
            if (!TryStartEmbedded(p))
                continue;
            chosenPort = p;
            return true;
        }

        chosenPort = _primaryPort;
        return false;
    }

    /// <summary>Strict equality; empty remote is not a match (old agents without wsmVersion must not keep us on :8787).</summary>
    private static bool BuildIdentityMatches(string? remote, string local)
    {
        if (string.IsNullOrWhiteSpace(remote) || string.IsNullOrWhiteSpace(local))
            return false;
        return string.Equals(remote.Trim(), local.Trim(), StringComparison.Ordinal);
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowStatusWindow()
    {
        Opacity = 1;
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _heartbeat.Stop();
        try { _embeddedRuntime?.Dispose(); } catch { /* */ }
        _embeddedRuntime = null;
        _tray.Visible = false;
        _tray.Dispose();
        _trayMenu.Dispose();
        _http.Dispose();
        _trayIcon.Dispose();
        _windowIcon.Dispose();
    }
}
