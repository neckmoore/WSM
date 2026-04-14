using System.Reflection;

namespace WSMMonitor;

/// <summary>Version and build stamp for HTTP headers, diagnostics, and dashboard footer.</summary>
public static class WsmBuildInfo
{
    private static readonly string InformationalRaw = ReadInformationalRaw();

    /// <summary>Full label, e.g. <c>1.0.3+20260413120055</c> when published with <c>SourceRevisionId</c>.</summary>
    public static string BuildIdentity { get; } = ResolveBuildIdentity();

    /// <summary>Semver base without <c>+metadata</c> (for short display where needed).</summary>
    public static string Version { get; } = ResolveVersionShort();

    /// <summary>UTC file timestamp of the running exe.</summary>
    public static string BuildDateUtc { get; } = ResolveBuildDateUtc();

    public static string ApiVersion => "1";

    private static string ReadInformationalRaw()
    {
        var asm = typeof(WsmBuildInfo).Assembly;
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Trim() ?? "";
    }

    private static string ResolveBuildIdentity()
    {
        if (!string.IsNullOrEmpty(InformationalRaw))
            return InformationalRaw;
        return typeof(WsmBuildInfo).Assembly.GetName().Version?.ToString() ?? "dev";
    }

    private static string ResolveVersionShort()
    {
        var id = BuildIdentity;
        var plus = id.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? id[..plus] : id;
    }

    private static string ResolveBuildDateUtc()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                return File.GetLastWriteTimeUtc(exe).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { /* */ }

        return "";
    }

    /// <summary>Footer line: build identity + exe file time.</summary>
    public static string FormatBuildStamp()
    {
        return string.IsNullOrEmpty(BuildDateUtc)
            ? BuildIdentity
            : BuildIdentity + " · " + BuildDateUtc;
    }
}
