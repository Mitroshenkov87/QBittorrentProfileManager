using System.Text.Json;
using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed class ConfigService
{
    private readonly string _configPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ConfigService(AppPaths paths)
    {
        _configPath = paths.ConfigPath;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
