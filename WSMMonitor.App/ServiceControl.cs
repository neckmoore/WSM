using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;

namespace WSMMonitor;

public static class ServiceControl
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string InstallService(string exePath)
    {
        // sc.exe expects: binPath= "\"C:\Path With Spaces\exe.exe\" --args" (see Microsoft docs for sc create).
        RunSc($"create {WindowsServiceHost.ServiceNameConst} binPath= \"\\\"{exePath}\\\" --service\" start= auto");
        RunSc($"description {WindowsServiceHost.ServiceNameConst} \"WSM Monitor Service\"");
        return "Service installed.";
    }

    public static string UninstallService()
    {
        try { RunSc($"stop {WindowsServiceHost.ServiceNameConst}"); } catch { /* */ }
        RunSc($"delete {WindowsServiceHost.ServiceNameConst}");
        return "Service removed.";
    }

    public static string StartService()
    {
        using var sc = new ServiceController(WindowsServiceHost.ServiceNameConst);
        if (sc.Status != ServiceControllerStatus.Running)
            sc.Start();
        return "Service start requested.";
    }

    public static string StopService()
    {
        using var sc = new ServiceController(WindowsServiceHost.ServiceNameConst);
        if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
            sc.Stop();
        return "Service stop requested.";
    }

    public static string RestartService()
    {
        try { StopService(); } catch { /* */ }
        return StartService();
    }

    /// <summary>Best-effort read of <c>sc qc</c> output (no admin required for query on many systems).</summary>
    public static string? TryGetConfiguredServiceBinaryPath()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"qc {WindowsServiceHost.ServiceNameConst}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            if (!p.WaitForExit(8000)) return null;
            foreach (var raw in p.StandardOutput.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                const string key = "BINARY_PATH_NAME:";
                if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    return line[key.Length..].Trim();
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    private static void RunSc(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to run sc.exe");
        p.WaitForExit();
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"sc {args} failed ({p.ExitCode}). {output} {error}");
    }
}
