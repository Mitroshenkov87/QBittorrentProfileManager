using System.Text.RegularExpressions;
using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed class ProfileService
{
    private static readonly Regex NonSlugChars = new(@"[^a-z0-9_]+", RegexOptions.Compiled);
    private static readonly string[] LiveExcludeDirs = [AppPaths.ProfilesFolderName];

    private readonly AppPaths _paths;
    private readonly FileCopyService _copy;
    private readonly BackupService _backup;
    private readonly ConfigService _configService;
    private readonly LogService _log;

    public ProfileService(
        AppPaths paths,
        FileCopyService copy,
        BackupService backup,
        ConfigService configService,
        LogService log)
    {
        _paths = paths;
        _copy = copy;
        _backup = backup;
        _configService = configService;
        _log = log;
    }

    public void EnsureProfileDirs(AppConfig config, ProfileInfo profile)
    {
        Directory.CreateDirectory(_paths.ProfileLocal(config, profile.Id));
        Directory.CreateDirectory(_paths.ProfileRoaming(config, profile.Id));
    }

    public async Task SaveLiveToProfileAsync(
        string profileId,
        IProgress<CopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var config = _configService.Load();
        SyncProfilesRoot(config);
        var localTarget = _paths.ProfileLocal(config, profileId);
        var roamingTarget = _paths.ProfileRoaming(config, profileId);

        Directory.CreateDirectory(localTarget);
        Directory.CreateDirectory(roamingTarget);

        var exclude = LiveExcludeDirs;
        var totalItems = _copy.CountFiles(_paths.LiveLocal, exclude)
            + _copy.CountFiles(_paths.LiveRoaming, exclude)
            + _copy.CountFiles(localTarget)
            + _copy.CountFiles(roamingTarget);

        await Task.Run(() => _backup.CreateBackup(
        [
            new BackupTarget(_paths.LiveLocal, "live_local_qBittorrent"),
            new BackupTarget(_paths.LiveRoaming, "live_roaming_qBittorrent"),
            new BackupTarget(localTarget, $"profile_{profileId}_local"),
            new BackupTarget(roamingTarget, $"profile_{profileId}_roaming")
        ], progress), cancellationToken);

        var completed = Math.Min(
            _copy.CountFiles(localTarget) + _copy.CountFiles(roamingTarget)
            + _copy.CountFiles(_paths.LiveLocal, exclude) + _copy.CountFiles(_paths.LiveRoaming, exclude),
            totalItems);

        await _copy.MirrorDirectoryAsync(
            _paths.LiveLocal, localTarget, progress, completed, totalItems, "Save Local",
            cancellationToken, exclude);
        completed += _copy.CountFiles(_paths.LiveLocal, exclude);
        await _copy.MirrorDirectoryAsync(
            _paths.LiveRoaming, roamingTarget, progress, completed, totalItems, "Save Roaming",
            cancellationToken, exclude);

        UpdateOperationMetadata(config, $"Saved live to profile ({profileId})");
        _log.Info($"Live qBittorrent data saved to profile '{profileId}'.");
    }

    public async Task LoadProfileToLiveAsync(
        string profileId,
        IProgress<CopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var config = _configService.Load();
        SyncProfilesRoot(config);
        var localSource = _paths.ProfileLocal(config, profileId);
        var roamingSource = _paths.ProfileRoaming(config, profileId);

        if (!Directory.Exists(localSource) && !Directory.Exists(roamingSource))
            throw new DirectoryNotFoundException(
                $"Profile '{profileId}' was not found under {_paths.ResolveProfilesRoot(config)}.");

        Directory.CreateDirectory(localSource);
        Directory.CreateDirectory(roamingSource);

        var exclude = LiveExcludeDirs;
        var totalItems = _copy.CountFiles(localSource)
            + _copy.CountFiles(roamingSource)
            + _copy.CountFiles(_paths.LiveLocal, exclude)
            + _copy.CountFiles(_paths.LiveRoaming, exclude);

        await Task.Run(() => _backup.CreateBackup(
        [
            new BackupTarget(_paths.LiveLocal, "live_local_qBittorrent"),
            new BackupTarget(_paths.LiveRoaming, "live_roaming_qBittorrent")
        ], progress), cancellationToken);

        var completed = _copy.CountFiles(_paths.LiveLocal, exclude) + _copy.CountFiles(_paths.LiveRoaming, exclude);

        // /XD Profiles keeps %...%\qBittorrent\Profiles intact under /MIR.
        await _copy.MirrorDirectoryAsync(
            localSource, _paths.LiveLocal, progress, completed, totalItems, "Load Local",
            cancellationToken, exclude);
        completed += _copy.CountFiles(localSource);
        await _copy.MirrorDirectoryAsync(
            roamingSource, _paths.LiveRoaming, progress, completed, totalItems, "Load Roaming",
            cancellationToken, exclude);

        SetActiveProfile(config, profileId, $"Loaded profile to live ({profileId})");
        _log.Info($"Profile '{profileId}' loaded into live qBittorrent folders.");
    }

    public async Task ActivateProfileAsync(
        AppConfig config,
        string profileId,
        bool saveCurrentFirst,
        IProgress<CopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        SyncProfilesRoot(config);

        if (saveCurrentFirst
            && !string.IsNullOrWhiteSpace(config.ActiveProfileId)
            && !string.Equals(config.ActiveProfileId, profileId, StringComparison.OrdinalIgnoreCase))
        {
            await SaveLiveToProfileAsync(config.ActiveProfileId, progress, cancellationToken);
        }

        await LoadProfileToLiveAsync(profileId, progress, cancellationToken);
        SetActiveProfile(config, profileId, $"Activated profile ({profileId})");
        _log.Info($"Activated profile '{profileId}'.");
    }

    public ProfileInfo CreateProfile(AppConfig config, string name, string role = "general")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name is required.", nameof(name));

        var id = Slugify(name);
        id = EnsureUniqueId(config, id);

        var profile = new ProfileInfo
        {
            Id = id,
            Name = name.Trim(),
            Role = string.IsNullOrWhiteSpace(role) ? "general" : role.Trim().ToLowerInvariant()
        };

        SyncProfilesRoot(config);
        EnsureProfileDirs(config, profile);
        config.Profiles.Add(profile);
        _configService.Save(config);
        _log.Info($"Created profile '{profile.Name}' (id={profile.Id}, role={profile.Role}).");
        return profile;
    }

    public void DeleteProfile(AppConfig config, string id, bool deleteDirectories = true)
    {
        var profile = config.Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
            return;

        config.Profiles.Remove(profile);

        if (string.Equals(config.ActiveProfileId, id, StringComparison.OrdinalIgnoreCase))
            config.ActiveProfileId = null;

        if (deleteDirectories)
        {
            SyncProfilesRoot(config);
            TryDeleteDir(_paths.ProfileLocal(config, id));
            TryDeleteDir(_paths.ProfileRoaming(config, id));

            // Portable/Custom may also have {root}\{id}\ wrapper.
            if (!string.Equals(config.StoreMode, "AppData", StringComparison.OrdinalIgnoreCase))
                TryDeleteDir(Path.Combine(_paths.ResolveProfilesRoot(config), id));
        }

        _configService.Save(config);
        _log.Info($"Deleted profile '{id}'.");
    }

    public async Task ImportLiveAsProfileAsync(
        AppConfig config,
        string name,
        string role = "general",
        IProgress<CopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var profile = CreateProfile(config, name, role);
        await SaveLiveToProfileAsync(profile.Id, progress, cancellationToken);
        SetActiveProfile(config, profile.Id, $"Imported live as profile ({profile.Id})");
        _log.Info($"Imported live data as profile '{profile.Id}'.");
    }

    public static string Slugify(string name)
    {
        var original = name.Trim();
        var s = original.ToLowerInvariant().Replace(' ', '_');
        s = NonSlugChars.Replace(s, "");
        s = Regex.Replace(s, "_+", "_").Trim('_');
        if (s.Length > 40)
            s = s[..40].TrimEnd('_');

        if (string.IsNullOrEmpty(s))
        {
            var hash = unchecked((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(original));
            return "profile_" + hash.ToString("x8");
        }

        return s;
    }

    private void TryDeleteDir(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete profile directory '{path}': {ex.Message}");
        }
    }

    private static string EnsureUniqueId(AppConfig config, string baseId)
    {
        if (config.Profiles.All(p => !string.Equals(p.Id, baseId, StringComparison.OrdinalIgnoreCase)))
            return baseId;

        for (var i = 2; i < 1000; i++)
        {
            var suffix = $"_{i}";
            var maxBase = Math.Max(1, 40 - suffix.Length);
            var candidate = (baseId.Length > maxBase ? baseId[..maxBase] : baseId) + suffix;
            if (config.Profiles.All(p => !string.Equals(p.Id, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return baseId + "_" + Guid.NewGuid().ToString("N")[..8];
    }

    private string SyncProfilesRoot(AppConfig config)
    {
        var root = _paths.ResolveProfilesRoot(config);
        _backup.ProfilesRoot = root;
        _backup.StoreMode = config.StoreMode;
        Directory.CreateDirectory(root);
        if (string.Equals(config.StoreMode, "AppData", StringComparison.OrdinalIgnoreCase))
            Directory.CreateDirectory(_paths.ResolveRoamingProfilesRoot(config));
        return root;
    }

    private void UpdateOperationMetadata(AppConfig config, string operation)
    {
        config.LastOperation = operation;
        config.LastOperationUtc = DateTime.UtcNow;
        config.LastBackupUtc = _backup.GetBackupDate()?.ToUniversalTime();
        _configService.Save(config);
    }

    private void SetActiveProfile(AppConfig config, string profileId, string operation)
    {
        config.ActiveProfileId = profileId;
        config.LastOperation = operation;
        config.LastOperationUtc = DateTime.UtcNow;
        config.LastBackupUtc = _backup.GetBackupDate()?.ToUniversalTime();
        _configService.Save(config);
    }
}
