using System.Security.Cryptography;
using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed class ProfileDetector
{
    private readonly AppPaths _paths;
    private readonly Dictionary<string, (long Size, DateTime WriteUtc, string Fingerprint)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public ProfileDetector(AppPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// Detects the active profile by comparing torrents.db fingerprint
    /// (SHA256 of first 64KB + file size) between live and each profile local folder.
    /// Returns matching profile id, or null if none / ambiguous.
    /// </summary>
    public string? DetectActiveProfileId(AppConfig config)
    {
        var liveDb = Path.Combine(_paths.LiveLocal, "torrents.db");
        if (!File.Exists(liveDb))
            return null;

        var liveFingerprint = GetFingerprintCached(liveDb);
        var matches = new List<string>();

        foreach (var profile in config.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
                continue;

            var db = Path.Combine(_paths.ProfileLocal(config, profile.Id), "torrents.db");
            if (File.Exists(db) && GetFingerprintCached(db) == liveFingerprint)
                matches.Add(profile.Id);
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    public ProfileInfo? DetectActiveProfile(AppConfig config)
    {
        var id = DetectActiveProfileId(config);
        if (id is null)
            return null;

        return config.Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private string GetFingerprintCached(string path)
    {
        var info = new FileInfo(path);
        if (_cache.TryGetValue(path, out var cached)
            && cached.Size == info.Length
            && cached.WriteUtc == info.LastWriteTimeUtc)
        {
            return cached.Fingerprint;
        }

        var fingerprint = ComputeFingerprint(path, info.Length);
        _cache[path] = (info.Length, info.LastWriteTimeUtc, fingerprint);
        return fingerprint;
    }

    private static string ComputeFingerprint(string path, long size)
    {
        Span<byte> buffer = stackalloc byte[65536];
        var read = 0;

        using (var stream = File.OpenRead(path))
        {
            read = stream.Read(buffer);
        }

        var hashBytes = SHA256.HashData(buffer[..read]);
        return $"{size:X}:{Convert.ToHexString(hashBytes)}";
    }
}
