using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WSMMonitor;

public static class WsmConfiguration
{
    public static WsmAppOptions Current { get; private set; } = new();

    public static string AppSettingsJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static string AppSettingsLocalJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>Load appsettings.json, appsettings.local.json, then environment variables (prefix WSMMONITOR_).</summary>
    public static void Load()
    {
        var basePath = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("WSMMONITOR_")
            .Build();

        var o = new WsmAppOptions();
        config.Bind(o);
        Current = o;
    }

    /// <summary>Re-read configuration from disk (after saving local file).</summary>
    public static void Reload() => Load();

    /// <summary>Persist options to appsettings.local.json (UI settings editor).</summary>
    public static void SaveLocal(WsmAppOptions options)
    {
        var json = JsonSerializer.Serialize(options, JsonWriteOptions);
        File.WriteAllText(AppSettingsLocalJsonPath, json);
    }

    /// <summary>Deep clone of current merged options (for editing).</summary>
    public static WsmAppOptions CloneCurrent()
    {
        var json = JsonSerializer.Serialize(Current);
        return JsonSerializer.Deserialize<WsmAppOptions>(json) ?? new WsmAppOptions();
    }
}
