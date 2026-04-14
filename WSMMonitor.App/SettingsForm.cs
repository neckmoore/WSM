using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

namespace WSMMonitor;

/// <summary>Tray / companion UI for editing <see cref="WsmAppOptions"/> (saved to appsettings.local.json).</summary>
public sealed class SettingsForm : Form
{
    /// <summary>Raised after settings were written and <see cref="WsmConfiguration.Reload"/> completed.</summary>
    public event EventHandler? SettingsPersisted;

    private readonly List<(Control Control, string Key)> _localized = new();

    private readonly NumericUpDown _numPort = new() { Minimum = 1024, Maximum = 65535, Width = 100 };
    private readonly NumericUpDown _numHistSample = new() { Minimum = 3, Maximum = 300, Width = 100 };
    private readonly CheckBox _chkLog = new() { AutoSize = true };
    private readonly TextBox _txtLogPath = new() { Width = 420 };
    private readonly ComboBox _cmbLogLevel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly NumericUpDown _numLogRetention = new() { Minimum = 1, Maximum = 90, Width = 100 };
    private readonly CheckBox _chkHourlySummary = new() { AutoSize = true };
    private readonly NumericUpDown _numHourlyInterval = new() { Minimum = 1, Maximum = 24, Width = 100 };
    private readonly CheckBox _chkSqlite = new() { AutoSize = true };
    private readonly TextBox _txtSqlitePath = new() { Width = 360 };
    private readonly Button _btnSqliteBrowse = new() { AutoSize = true };
    private readonly NumericUpDown _numRetention = new() { Minimum = 1, Maximum = 730, Width = 100 };
    private readonly TextBox _txtSigmaPath = new() { Width = 360 };
    private readonly Button _btnSigmaBrowse = new() { AutoSize = true };
    private readonly CheckBox _chkLhm = new() { AutoSize = true };
    private readonly CheckBox _chkLhmCpu = new() { AutoSize = true };
    private readonly CheckBox _chkLhmGpu = new() { AutoSize = true };
    private readonly CheckBox _chkLhmMobo = new() { AutoSize = true };
    private readonly CheckBox _chkLhmMem = new() { AutoSize = true };
    private readonly CheckBox _chkLhmStorage = new() { AutoSize = true };
    private readonly CheckBox _chkLhmFan = new() { AutoSize = true };
    private readonly CheckBox _chkLhmPsu = new() { AutoSize = true };
    private readonly NumericUpDown _numLhmMax = new() { Minimum = 4, Maximum = 96, Width = 80 };
    private readonly ComboBox _cmbWorkMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _cmbLang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    private Button _btnOk = null!;
    private Button _btnApply = null!;
    private Button _btnCancel = null!;
    private Button _btnTest = null!;

    public SettingsForm()
    {
        Text = WsmLocalization.T("SettingsTitle");
        Width = 560;
        Height = 500;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);

        _cmbLogLevel.Items.AddRange(["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"]);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(8, 8) };
        tabs.TabPages.Add(BuildAgentTab());
        tabs.TabPages.Add(BuildLoggingTab());
        tabs.TabPages.Add(BuildHistoryTab());
        tabs.TabPages.Add(BuildSigmaTab());
        tabs.TabPages.Add(BuildLhmTab());
        tabs.TabPages.Add(BuildUiTab());

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        _btnOk = new Button { DialogResult = DialogResult.None, AutoSize = true };
        _btnOk.Click += (_, _) =>
        {
            if (TryPersist(closeForm: true))
                DialogResult = DialogResult.OK;
        };
        _btnApply = new Button { AutoSize = true };
        _btnApply.Click += (_, _) => TryPersist(closeForm: false);
        _btnTest = new Button { AutoSize = true };
        _btnTest.Click += async (_, _) => await RunConfigurationTestAsync();
        _btnCancel = new Button { DialogResult = DialogResult.Cancel, AutoSize = true };
        bottom.Controls.Add(_btnCancel);
        bottom.Controls.Add(_btnApply);
        bottom.Controls.Add(_btnTest);
        bottom.Controls.Add(_btnOk);
        RegisterText(_btnOk, "BtnOk");
        RegisterText(_btnApply, "BtnApply");
        RegisterText(_btnTest, "BtnTestConfig");
        RegisterText(_btnCancel, "BtnCancel");

        Controls.Add(tabs);
        Controls.Add(bottom);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        _chkLhm.CheckedChanged += (_, _) => SyncLhmSubEnabled();
        _chkLog.CheckedChanged += (_, _) => SyncLogRelatedEnabled();
        _chkHourlySummary.CheckedChanged += (_, _) => SyncLogRelatedEnabled();
        _btnSqliteBrowse.Click += (_, _) => BrowseSqlite();
        _btnSigmaBrowse.Click += (_, _) => BrowseSigma();

        Load += (_, _) =>
        {
            ApplyModel(WsmConfiguration.CloneCurrent());
            SyncLhmSubEnabled();
            SyncLogRelatedEnabled();
            RefreshAllTexts();
        };
    }

    private void RegisterText(Control c, string key)
    {
        c.Text = WsmLocalization.T(key);
        _localized.Add((c, key));
    }

    private void RefreshAllTexts()
    {
        Text = WsmLocalization.T("SettingsTitle");
        foreach (var (c, key) in _localized)
            c.Text = WsmLocalization.T(key);
        SyncWorkCombo(WsmConfiguration.Current.Ui.WorkMode);
        SyncLangCombo(WsmConfiguration.Current.Ui.Language);
    }

    private void SyncWorkCombo(string? workMode)
    {
        _cmbWorkMode.Items.Clear();
        _cmbWorkMode.Items.Add(WsmLocalization.T("WorkModeService"));
        _cmbWorkMode.Items.Add(WsmLocalization.T("WorkModeCompanion"));
        _cmbWorkMode.SelectedIndex = string.Equals(workMode, "service", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private void SyncLangCombo(string? languageCode)
    {
        var idx = WsmLocalization.NormalizeLang(languageCode) == "en" ? 1 : 0;
        _cmbLang.Items.Clear();
        _cmbLang.Items.Add(WsmLocalization.T("LangRu"));
        _cmbLang.Items.Add(WsmLocalization.T("LangEn"));
        _cmbLang.SelectedIndex = idx;
    }

    private static TableLayoutPanel CreateTwoColumnGrid(int rowCount)
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = rowCount,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(12, 12, 12, 8),
            Padding = new Padding(0)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < rowCount; i++)
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return t;
    }

    private static TableLayoutPanel CreateThreeColumnGrid(int rowCount)
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = rowCount,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(12, 12, 12, 8),
            Padding = new Padding(0)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < rowCount; i++)
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return t;
    }

    private TabPage BuildAgentTab()
    {
        var p = new TabPage();
        RegisterText(p, "TabAgent");
        var t = CreateTwoColumnGrid(2);
        AddRow(t, 0, "SettingsAgentPort", _numPort);
        AddRow(t, 1, "SettingsAgentHist", _numHistSample);
        p.Controls.Add(t);
        return p;
    }

    private TabPage BuildLoggingTab()
    {
        var p = new TabPage();
        RegisterText(p, "TabLogging");
        var t = CreateTwoColumnGrid(6);
        _chkLog.Margin = new Padding(0, 0, 0, 4);
        t.Controls.Add(_chkLog, 0, 0);
        t.SetColumnSpan(_chkLog, 2);
        RegisterText(_chkLog, "SettingsChkLog");
        AddRow(t, 1, "SettingsLblLogPath", _txtLogPath);
        AddRow(t, 2, "SettingsLblLogLevel", _cmbLogLevel);
        AddRow(t, 3, "SettingsLblLogRetentionDays", _numLogRetention);
        _chkHourlySummary.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(_chkHourlySummary, 0, 4);
        t.SetColumnSpan(_chkHourlySummary, 2);
        RegisterText(_chkHourlySummary, "SettingsChkHourlySummary");
        AddRow(t, 5, "SettingsLblHourlySummaryHours", _numHourlyInterval);
        p.Controls.Add(t);
        return p;
    }

    private TabPage BuildHistoryTab()
    {
        var p = new TabPage();
        RegisterText(p, "TabHistory");
        var t = CreateThreeColumnGrid(3);
        _chkSqlite.Margin = new Padding(0, 0, 0, 4);
        t.Controls.Add(_chkSqlite, 0, 0);
        t.SetColumnSpan(_chkSqlite, 3);
        RegisterText(_chkSqlite, "SettingsChkSqlite");
        var lblDb = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 6, 8, 6)
        };
        RegisterText(lblDb, "SettingsLblSqliteFile");
        t.Controls.Add(lblDb, 0, 1);
        _txtSqlitePath.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _txtSqlitePath.Margin = new Padding(0, 4, 8, 4);
        t.Controls.Add(_txtSqlitePath, 1, 1);
        RegisterText(_btnSqliteBrowse, "SettingsBrowse");
        _btnSqliteBrowse.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(_btnSqliteBrowse, 2, 1);
        AddRow(t, 2, "SettingsLblRetention", _numRetention);
        p.Controls.Add(t);
        return p;
    }

    private TabPage BuildSigmaTab()
    {
        var p = new TabPage();
        RegisterText(p, "TabSigma");
        var t = CreateThreeColumnGrid(1);
        var lbl = new Label { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 8, 6) };
        RegisterText(lbl, "SettingsLblSigmaFile");
        t.Controls.Add(lbl, 0, 0);
        _txtSigmaPath.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _txtSigmaPath.Margin = new Padding(0, 4, 8, 4);
        t.Controls.Add(_txtSigmaPath, 1, 0);
        RegisterText(_btnSigmaBrowse, "SettingsBrowse");
        _btnSigmaBrowse.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(_btnSigmaBrowse, 2, 0);
        p.Controls.Add(t);
        return p;
    }

    private TabPage BuildLhmTab()
    {
        var p = new TabPage();
        RegisterText(p, "TabSensors");
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 12, 12, 8)
        };
        flow.Controls.Add(_chkLhm);
        RegisterText(_chkLhm, "SettingsLhmMain");
        flow.Controls.Add(_chkLhmCpu);
        RegisterText(_chkLhmCpu, "SettingsLhmCpu");
        flow.Controls.Add(_chkLhmGpu);
        RegisterText(_chkLhmGpu, "SettingsLhmGpu");
        flow.Controls.Add(_chkLhmMobo);
        RegisterText(_chkLhmMobo, "SettingsLhmMobo");
        flow.Controls.Add(_chkLhmMem);
        RegisterText(_chkLhmMem, "SettingsLhmMem");
        flow.Controls.Add(_chkLhmStorage);
        RegisterText(_chkLhmStorage, "SettingsLhmStorage");
        flow.Controls.Add(_chkLhmFan);
        RegisterText(_chkLhmFan, "SettingsLhmFan");
        flow.Controls.Add(_chkLhmPsu);
        RegisterText(_chkLhmPsu, "SettingsLhmPsu");
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var lblMax = new Label { AutoSize = true, Padding = new Padding(0, 6, 8, 0) };
        RegisterText(lblMax, "SettingsLhmMaxRow");
        row.Controls.Add(lblMax);
        row.Controls.Add(_numLhmMax);
        flow.Controls.Add(row);
        p.Controls.Add(flow);
        return p;
    }

    private TabPage BuildUiTab()
    {
        var p = new TabPage();
        RegisterText(p, "TabUi");
        var t = CreateTwoColumnGrid(2);
        var lblMode = new Label { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 10, 6) };
        RegisterText(lblMode, "LblWorkMode");
        t.Controls.Add(lblMode, 0, 0);
        _cmbWorkMode.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        _cmbWorkMode.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(_cmbWorkMode, 1, 0);
        var lbl = new Label { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 10, 6) };
        RegisterText(lbl, "LblLang");
        t.Controls.Add(lbl, 0, 1);
        _cmbLang.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        _cmbLang.Margin = new Padding(0, 4, 0, 4);
        t.Controls.Add(_cmbLang, 1, 1);
        p.Controls.Add(t);
        return p;
    }

    private void AddRow(TableLayoutPanel t, int row, string captionKey, Control valueControl)
    {
        var caption = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 6, 10, 6),
            TextAlign = ContentAlignment.MiddleLeft
        };
        RegisterText(caption, captionKey);
        valueControl.Margin = new Padding(0, 4, 0, 4);
        if (valueControl is TextBox)
            valueControl.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        else
            valueControl.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        t.Controls.Add(caption, 0, row);
        t.Controls.Add(valueControl, 1, row);
    }

    private void SyncLhmSubEnabled()
    {
        var on = _chkLhm.Checked;
        foreach (var c in new Control[] { _chkLhmCpu, _chkLhmGpu, _chkLhmMobo, _chkLhmMem, _chkLhmStorage, _chkLhmFan, _chkLhmPsu, _numLhmMax })
            c.Enabled = on;
    }

    private void SyncLogRelatedEnabled()
    {
        var logOn = _chkLog.Checked;
        _txtLogPath.Enabled = logOn;
        _cmbLogLevel.Enabled = logOn;
        _numLogRetention.Enabled = logOn;
        _chkHourlySummary.Enabled = logOn;
        _numHourlyInterval.Enabled = logOn && _chkHourlySummary.Checked;
    }

    private void BrowseSqlite()
    {
        using var dlg = new SaveFileDialog
        {
            Title = WsmLocalization.T("SettingsDlgSqliteTitle"),
            Filter = WsmLocalization.T("SettingsDlgSqliteFilter"),
            FileName = string.IsNullOrWhiteSpace(_txtSqlitePath.Text) ? "metrics-history.db" : Path.GetFileName(_txtSqlitePath.Text),
            InitialDirectory = TryDir(_txtSqlitePath.Text)
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtSqlitePath.Text = dlg.FileName;
    }

    private void BrowseSigma()
    {
        using var dlg = new OpenFileDialog
        {
            Title = WsmLocalization.T("SettingsDlgSigmaTitle"),
            Filter = WsmLocalization.T("SettingsDlgSigmaFilter"),
            InitialDirectory = TryDir(_txtSigmaPath.Text)
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtSigmaPath.Text = dlg.FileName;
    }

    private static string TryDir(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return AppContext.BaseDirectory;
            var d = Path.IsPathRooted(path) ? Path.GetDirectoryName(path) : Path.GetDirectoryName(Path.Combine(AppContext.BaseDirectory, path));
            return Directory.Exists(d) ? d! : AppContext.BaseDirectory;
        }
        catch
        {
            return AppContext.BaseDirectory;
        }
    }

    private void ApplyModel(WsmAppOptions m)
    {
        _numPort.Value = Math.Clamp(m.Agent.Port, (int)_numPort.Minimum, (int)_numPort.Maximum);
        _numHistSample.Value = Math.Clamp(m.Agent.HistorySampleSeconds, (int)_numHistSample.Minimum, (int)_numHistSample.Maximum);
        _chkLog.Checked = m.Logging.Enabled;
        _txtLogPath.Text = m.Logging.Path ?? "";
        var lvl = m.Logging.MinimumLevel ?? "Information";
        var idx = _cmbLogLevel.Items.IndexOf(lvl);
        _cmbLogLevel.SelectedIndex = idx >= 0 ? idx : _cmbLogLevel.Items.IndexOf("Information");
        _numLogRetention.Value = Math.Clamp(m.Logging.RetentionDays, (int)_numLogRetention.Minimum, (int)_numLogRetention.Maximum);
        _chkHourlySummary.Checked = m.Logging.HourlySummaryEnabled;
        _numHourlyInterval.Value = Math.Clamp(m.Logging.HourlySummaryIntervalHours, (int)_numHourlyInterval.Minimum, (int)_numHourlyInterval.Maximum);
        _chkSqlite.Checked = m.History.SqliteEnabled;
        _txtSqlitePath.Text = m.History.SqlitePath ?? "";
        _numRetention.Value = Math.Clamp(m.History.RetentionDays, (int)_numRetention.Minimum, (int)_numRetention.Maximum);
        _txtSigmaPath.Text = m.Sigma.SuppressionsPath ?? "";
        var l = m.LibreHardwareMonitor;
        _chkLhm.Checked = l.Enabled;
        _chkLhmCpu.Checked = l.Cpu;
        _chkLhmGpu.Checked = l.Gpu;
        _chkLhmMobo.Checked = l.Motherboard;
        _chkLhmMem.Checked = l.MemoryModules;
        _chkLhmStorage.Checked = l.Storage;
        _chkLhmFan.Checked = l.FanControllers;
        _chkLhmPsu.Checked = l.Psu;
        _numLhmMax.Value = Math.Clamp(l.MaxSensors, (int)_numLhmMax.Minimum, (int)_numLhmMax.Maximum);
        SyncWorkCombo(m.Ui.WorkMode);
        SyncLangCombo(m.Ui.Language);
    }

    private WsmAppOptions ReadModelFromControls()
    {
        var l = new LibreHardwareMonitorSection
        {
            Enabled = _chkLhm.Checked,
            Cpu = _chkLhmCpu.Checked,
            Gpu = _chkLhmGpu.Checked,
            Motherboard = _chkLhmMobo.Checked,
            MemoryModules = _chkLhmMem.Checked,
            Storage = _chkLhmStorage.Checked,
            FanControllers = _chkLhmFan.Checked,
            Psu = _chkLhmPsu.Checked,
            MaxSensors = (int)_numLhmMax.Value
        };

        return new WsmAppOptions
        {
            Agent = new AgentSection
            {
                Port = (int)_numPort.Value,
                HistorySampleSeconds = (int)_numHistSample.Value
            },
            Logging = new LoggingSection
            {
                Enabled = _chkLog.Checked,
                Path = _txtLogPath.Text.Trim(),
                MinimumLevel = (_cmbLogLevel.SelectedItem as string) ?? "Information",
                RetentionDays = (int)_numLogRetention.Value,
                HourlySummaryEnabled = _chkHourlySummary.Checked,
                HourlySummaryIntervalHours = (int)_numHourlyInterval.Value
            },
            History = new HistorySection
            {
                SqliteEnabled = _chkSqlite.Checked,
                SqlitePath = _txtSqlitePath.Text.Trim(),
                RetentionDays = (int)_numRetention.Value
            },
            Sigma = new SigmaSection
            {
                SuppressionsPath = _txtSigmaPath.Text.Trim()
            },
            LibreHardwareMonitor = l,
            Ui = new UiSection
            {
                WorkMode = _cmbWorkMode.SelectedIndex == 0 ? "service" : "companion",
                Language = _cmbLang.SelectedIndex == 1 ? "en" : "ru"
            }
        };
    }

    private bool TryPersist(bool closeForm)
    {
        try
        {
            var model = ReadModelFromControls();
            WsmConfiguration.SaveLocal(model);
            WsmConfiguration.Reload();
            SettingsPersisted?.Invoke(this, EventArgs.Empty);
            MessageBox.Show(
                WsmLocalization.T("SettingsSaved"),
                WsmLocalization.T("MsgBoxTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            RefreshAllTexts();
            if (closeForm)
                Close();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(WsmLocalization.T("SettingsSaveErr") + ex.Message, WsmLocalization.T("MsgBoxTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task RunConfigurationTestAsync()
    {
        var model = ReadModelFromControls();
        var sb = new StringBuilder();
        sb.AppendLine(WsmLocalization.T("ConfigTestHeader"));
        sb.AppendLine();

        var apiOk = false;
        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{model.Agent.Port}/"),
                Timeout = TimeSpan.FromSeconds(3)
            };
            var st = await http.GetFromJsonAsync<AgentStatusDto>("api/v1/agent-status");
            apiOk = st != null;
            sb.AppendLine($"{WsmLocalization.T("ConfigTestApi")}: {(apiOk ? WsmLocalization.T("ConfigTestOk") : WsmLocalization.T("ConfigTestFail"))}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{WsmLocalization.T("ConfigTestApi")}: {WsmLocalization.T("ConfigTestFail")} ({ex.Message})");
        }

        try
        {
            var logPath = model.Logging.Path?.Trim();
            if (string.IsNullOrWhiteSpace(logPath))
                logPath = "logs/wsm-.log";
            var full = Path.IsPathRooted(logPath) ? logPath : Path.Combine(AppContext.BaseDirectory, logPath);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir ?? AppContext.BaseDirectory, $"wsm-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "wsm");
            File.Delete(probe);
            sb.AppendLine($"{WsmLocalization.T("ConfigTestLogWrite")}: {WsmLocalization.T("ConfigTestOk")}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{WsmLocalization.T("ConfigTestLogWrite")}: {WsmLocalization.T("ConfigTestFail")} ({ex.Message})");
        }

        var svc = ServiceControl.TryGetStatus();
        sb.AppendLine($"{WsmLocalization.T("ConfigTestService")}: {(svc?.ToString() ?? WsmLocalization.T("ConfigTestSvcMissing"))}");

        if (!model.LibreHardwareMonitor.Enabled)
        {
            sb.AppendLine($"{WsmLocalization.T("ConfigTestSensors")}: {WsmLocalization.T("ConfigTestSkipped")}");
        }
        else
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var c = new LibreHardwareThermalCollector(model.LibreHardwareMonitor);
                var rows = c.CollectTemperatures();
                sw.Stop();
                sb.AppendLine(
                    $"{WsmLocalization.T("ConfigTestSensors")}: {WsmLocalization.T("ConfigTestOk")} (rows={rows.Count}, {sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{WsmLocalization.T("ConfigTestSensors")}: {WsmLocalization.T("ConfigTestFail")} ({ex.Message})");
            }
        }

        MessageBox.Show(sb.ToString().TrimEnd(), WsmLocalization.T("ConfigTestTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
