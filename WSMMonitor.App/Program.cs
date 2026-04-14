namespace WSMMonitor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--export-icon", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length)
                continue;
            var path = args[i + 1];
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using var ic = AgentIconFactory.CreateShieldPulseIcon(256);
            using (var fs = File.Create(path))
                ic.Save(fs);
            return;
        }

        WsmConfiguration.Load();
        WsmLog.Initialize(WsmConfiguration.Current.Logging);

        if (IsWindowsServiceProcess(args))
        {
            try
            {
                try
                {
                    ApplicationConfiguration.Initialize();
                }
                catch
                {
                    /* WinForms bootstrap can fail on some hosts; service may still run */
                }

                try
                {
                    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
                }
                catch
                {
                    /* ignore */
                }

                WindowsServiceHost.RunAsService();
            }
            finally
            {
                WsmLog.Close();
            }

            return;
        }

        if (args.Any(a => string.Equals(a, "--install-service", StringComparison.OrdinalIgnoreCase)))
        {
            HandleInstallUninstall(install: true);
            return;
        }

        if (args.Any(a => string.Equals(a, "--uninstall-service", StringComparison.OrdinalIgnoreCase)))
        {
            HandleInstallUninstall(install: false);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.ApplicationExit += (_, _) => WsmLog.Close();
        Application.Run(new MainForm(args));
    }

    /// <summary>Detect <c>--service</c> from argv and raw command line (SCM sometimes mangles split args).</summary>
    private static bool IsWindowsServiceProcess(string[] args)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, "--service", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        try
        {
            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, "--service", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            /* ignore */
        }

        var raw = Environment.CommandLine ?? "";
        if (raw.Contains(" --service", StringComparison.OrdinalIgnoreCase))
            return true;
        var t = raw.TrimEnd();
        if (t.EndsWith("--service", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static void HandleInstallUninstall(bool install)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (!ServiceControl.IsAdministrator())
            {
                MessageBox.Show(WsmLocalization.T("MsgInstallAdmin"), WsmLocalization.T("MsgBoxTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot detect executable path.");
            var msg = install ? ServiceControl.InstallService(exe) : ServiceControl.UninstallService();
            MessageBox.Show(msg, WsmLocalization.T("MsgBoxTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, WsmLocalization.T("MsgBoxTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
