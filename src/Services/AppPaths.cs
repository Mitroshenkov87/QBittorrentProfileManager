using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed class AppPaths
{
    /// <summary>Reserved folder under live qBittorrent for AppData profile store.</summary>
    public const string ProfilesFolderName = "Profiles";

    public string AppDir { get; }
    public string ConfigPath { get; }
    public string LogPath { get; }
    public string LiveLocal { get; }
    public string LiveRoaming { get; }
    public string BackupDir { get; }
    public string BackupZipPath { get; }

    public AppPaths()
    {
        AppDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        ConfigPath = Path.Combine(AppDir, "config.json");
        LogPath = Path.Combine(AppDir, "app.log");
        LiveLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "qBittorrent");
        LiveRoaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "qBittorrent");
        BackupDir = Path.Combine(AppDir, "backups");
        BackupZipPath = Path.Combine(BackupDir, "qBittorrent_BAK.zip");
    }

    /// <summary>
    /// Primary profiles root (local side for AppData mode).
    /// Portable: AppDir/profiles
    /// AppData: %LOCALAPPDATA%\qBittorrent\Profiles
    /// Custom: CustomProfilesRoot
    /// </summary>
    public string ResolveProfilesRoot(AppConfig config)
    {
        return config.StoreMode switch
        {
            "AppData" => Path.Combine(LiveLocal, ProfilesFolderName),
            "Custom" when !string.IsNullOrWhiteSpace(config.CustomProfilesRoot)
                => config.CustomProfilesRoot!,
            _ => Path.Combine(AppDir, "profiles")
        };
    }

    /// <summary>
    /// Roaming profiles root for AppData: %APPDATA%\qBittorrent\Profiles.
    /// Portable/Custom: same tree as ResolveProfilesRoot (with roaming subfolder per profile).
    /// </summary>
    public string ResolveRoamingProfilesRoot(AppConfig config)
    {
        return string.Equals(config.StoreMode, "AppData", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(LiveRoaming, ProfilesFolderName)
            : ResolveProfilesRoot(config);
    }

    /// <summary>
    /// AppData: %LOCALAPPDATA%\qBittorrent\Profiles\{id}\
    /// Portable/Custom: {root}\{id}\local\
    /// </summary>
    public string ProfileLocal(AppConfig config, string profileId)
    {
        var root = ResolveProfilesRoot(config);
        if (string.Equals(config.StoreMode, "AppData", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(root, profileId);

        return Path.Combine(root, profileId, "local");
    }

    /// <summary>
    /// AppData: %APPDATA%\qBittorrent\Profiles\{id}\
    /// Portable/Custom: {root}\{id}\roaming\
    /// </summary>
    public string ProfileRoaming(AppConfig config, string profileId)
    {
        if (string.Equals(config.StoreMode, "AppData", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ResolveRoamingProfilesRoot(config), profileId);

        return Path.Combine(ResolveProfilesRoot(config), profileId, "roaming");
    }

    public void EnsureBackupDir() => Directory.CreateDirectory(BackupDir);

    public bool IsLiveLocalRoot(string path) => PathsEqual(path, LiveLocal);

    public bool IsLiveRoamingRoot(string path) => PathsEqual(path, LiveRoaming);

    public bool IsLiveRoot(string path) => IsLiveLocalRoot(path) || IsLiveRoamingRoot(path);

    private static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        var fa = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fb = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fa.Equals(fb, StringComparison.OrdinalIgnoreCase);
    }
}
