using System.IO.Compression;
using System.Text.RegularExpressions;
using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed record BackupTarget(string SourceDirectory, string ArchiveRootName);

public sealed class BackupService
{
    private static readonly Regex ProfileRootRegex = new(
        @"^profile_(?<id>.+)_(?<kind>local|roaming)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppPaths _paths;
    private readonly LogService _log;
    private readonly FileCopyService _copy;
    private string _profilesRoot;
    private string _storeMode = "Portable";

    public BackupService(AppPaths paths, LogService log, FileCopyService copy, string profilesRoot)
    {
        _paths = paths;
        _log = log;
        _copy = copy;
        _profilesRoot = profilesRoot;
    }

    public string ProfilesRoot
    {
        get => _profilesRoot;
        set => _profilesRoot = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string StoreMode
    {
        get => _storeMode;
        set => _storeMode = string.IsNullOrWhiteSpace(value) ? "Portable" : value;
    }

    public void CreateBackup(IEnumerable<string> directories, IProgress<CopyProgress>? progress = null) =>
        CreateBackup(
            directories.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(dir => new BackupTarget(dir, new DirectoryInfo(dir).Name)),
            progress);

    public void CreateBackup(IEnumerable<BackupTarget> targets, IProgress<CopyProgress>? progress = null)
    {
        var list = targets.Where(t => Directory.Exists(t.SourceDirectory)).ToList();
        if (list.Count == 0)
            return;

        _paths.EnsureBackupDir();

        var totalItems = list.Sum(t =>
            _copy.CountFiles(t.SourceDirectory, ExcludeFor(t.SourceDirectory)));
        var completed = 0;

        var tempPath = _paths.BackupZipPath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
        {
            foreach (var target in list)
            {
                var exclude = ExcludeFor(target.SourceDirectory);
                foreach (var file in EnumerateFiles(target.SourceDirectory, exclude))
                {
                    var relative = Path.GetRelativePath(target.SourceDirectory, file).Replace('\\', '/');
                    var entryName = $"{target.ArchiveRootName}/{relative}";
                    archive.CreateEntryFromFile(file, entryName, GetCompressionLevel(file));
                    completed++;
                    progress?.Report(new CopyProgress(completed, Math.Max(totalItems, completed), entryName, "Backup"));
                }
            }
        }

        if (File.Exists(_paths.BackupZipPath))
        {
            File.Delete(_paths.BackupZipPath);
            _log.Info("Removed previous backup.");
        }

        File.Move(tempPath, _paths.BackupZipPath);
        _log.Info($"Created backup: {_paths.BackupZipPath}");
    }

    public bool BackupExists() => File.Exists(_paths.BackupZipPath);

    public DateTime? GetBackupDate() =>
        File.Exists(_paths.BackupZipPath) ? File.GetLastWriteTime(_paths.BackupZipPath) : null;

    public void RestoreBackup(IProgress<CopyProgress>? progress = null)
    {
        if (!File.Exists(_paths.BackupZipPath))
            throw new FileNotFoundException("Backup file not found.", _paths.BackupZipPath);

        using var archive = ZipFile.OpenRead(_paths.BackupZipPath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        var completed = 0;

        foreach (var entry in entries)
        {
            var targetPath = MapBackupEntryToTarget(entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
            completed++;
            progress?.Report(new CopyProgress(completed, entries.Count, entry.FullName, "Restore"));
        }

        _log.Info("Backup restored.");
    }

    private static CompressionLevel GetCompressionLevel(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".db", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mmdb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".fastresume", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".torrent", StringComparison.OrdinalIgnoreCase)
            ? CompressionLevel.NoCompression
            : CompressionLevel.Fastest;
    }

    /// <summary>
    /// Maps archive entry roots:
    /// live_local_qBittorrent, live_roaming_qBittorrent,
    /// profile_{id}_local, profile_{id}_roaming.
    /// </summary>
    public string MapBackupEntryToTarget(string entryFullName)
    {
        var normalized = entryFullName.Replace('\\', '/');
        var slashIndex = normalized.IndexOf('/');
        if (slashIndex < 0)
            throw new InvalidDataException($"Invalid backup entry: {entryFullName}");

        var root = normalized[..slashIndex];
        var relative = normalized[(slashIndex + 1)..].Replace('/', Path.DirectorySeparatorChar);

        if (string.Equals(root, "live_local_qBittorrent", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(_paths.LiveLocal, relative);

        if (string.Equals(root, "live_roaming_qBittorrent", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(_paths.LiveRoaming, relative);

        var match = ProfileRootRegex.Match(root);
        if (match.Success)
        {
            var id = match.Groups["id"].Value;
            var kind = match.Groups["kind"].Value;
            var config = new Models.AppConfig { StoreMode = StoreMode, CustomProfilesRoot = _profilesRoot };
            // For Custom mode ProfilesRoot is the custom folder; for Portable Resolve uses AppDir.
            if (string.Equals(StoreMode, "Custom", StringComparison.OrdinalIgnoreCase))
                config.CustomProfilesRoot = _profilesRoot;

            var baseDir = kind.Equals("local", StringComparison.OrdinalIgnoreCase)
                ? _paths.ProfileLocal(config, id)
                : _paths.ProfileRoaming(config, id);
            return Path.Combine(baseDir, relative);
        }

        throw new InvalidDataException($"Unknown backup root folder: {root}");
    }

    private string[]? ExcludeFor(string sourceDirectory) =>
        _paths.IsLiveRoot(sourceDirectory) ? [AppPaths.ProfilesFolderName] : null;

    private static IEnumerable<string> EnumerateFiles(string root, string[]? excludeDirNames)
    {
        if (!Directory.Exists(root))
            yield break;

        if (excludeDirNames is null || excludeDirNames.Length == 0)
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                yield return f;
            yield break;
        }

        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files;
            string[] subs;
            try
            {
                files = Directory.GetFiles(dir);
                subs = Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (var f in files)
                yield return f;

            foreach (var sub in subs)
            {
                var name = Path.GetFileName(sub);
                if (excludeDirNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                stack.Push(sub);
            }
        }
    }
}
