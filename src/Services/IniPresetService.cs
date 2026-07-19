using System.Text;
using System.Text.RegularExpressions;

namespace QBittorrentProfileManager.Services;

/// <summary>
/// Reads/writes qBittorrent.ini keys and applies connection/role presets.
/// </summary>
public sealed class IniPresetService
{
    private static readonly Regex SectionRegex = new(@"^\s*\[([^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^\s*([^=]+?)\s*=\s*(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Reads ini as section|key -> value map (case-sensitive keys as stored).
    /// </summary>
    public Dictionary<string, string> ReadIni(string iniPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(iniPath))
            return result;

        string? section = null;
        foreach (var raw in File.ReadAllLines(iniPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            var sectionMatch = SectionRegex.Match(line);
            if (sectionMatch.Success)
            {
                section = sectionMatch.Groups[1].Value.Trim();
                continue;
            }

            var kv = KeyValueRegex.Match(raw);
            if (!kv.Success || section is null)
                continue;

            var key = kv.Groups[1].Value.Trim();
            var value = kv.Groups[2].Value.Trim();
            result[$"{section}|{key}"] = value;
        }

        return result;
    }

    /// <summary>
    /// Copies <paramref name="iniPath"/> to the same directory with a
    /// <c>.bak-yyyyMMdd-HHmmss</c> suffix and returns the backup path.
    /// </summary>
    public string BackupIni(string iniPath)
    {
        if (!File.Exists(iniPath))
            throw new FileNotFoundException("INI file not found.", iniPath);

        var directory = Path.GetDirectoryName(iniPath);
        if (string.IsNullOrEmpty(directory))
            directory = ".";

        var fileName = Path.GetFileName(iniPath);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(directory, $"{fileName}.bak-{stamp}");
        File.Copy(iniPath, backupPath, overwrite: false);
        return backupPath;
    }

    /// <summary>
    /// Applies connection/role preset settings. If the INI exists, backs it up first.
    /// </summary>
    /// <returns>Backup path when a backup was created; otherwise <c>null</c>.</returns>
    public string? ApplyPreset(string iniPath, string connectionClass, string role)
    {
        string? backupPath = null;
        if (File.Exists(iniPath))
            backupPath = BackupIni(iniPath);

        var settings = BuildPresetSettings(connectionClass, role);
        ApplySettings(iniPath, settings);
        return backupPath;
    }

    public void ApplySettings(string iniPath, IReadOnlyDictionary<string, string> settings)
    {
        var lines = File.Exists(iniPath)
            ? File.ReadAllLines(iniPath).ToList()
            : new List<string>();

        // Track which keys were updated in-place.
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? currentSection = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var trimmed = raw.Trim();

            var sectionMatch = SectionRegex.Match(trimmed);
            if (sectionMatch.Success)
            {
                currentSection = sectionMatch.Groups[1].Value.Trim();
                continue;
            }

            if (currentSection is null)
                continue;

            var kv = KeyValueRegex.Match(raw);
            if (!kv.Success)
                continue;

            var key = kv.Groups[1].Value.Trim();
            var mapKey = $"{currentSection}|{key}";

            if (settings.TryGetValue(mapKey, out var newValue)
                || TryFindSetting(settings, currentSection, key, out mapKey, out newValue!))
            {
                lines[i] = $"{key}={newValue}";
                applied.Add(mapKey);
            }
        }

        // Append missing keys under the correct section.
        var missingBySection = new Dictionary<string, List<(string Key, string Value)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (mapKey, value) in settings)
        {
            if (applied.Contains(mapKey))
                continue;

            var parts = mapKey.Split('|', 2);
            if (parts.Length != 2)
                continue;

            var section = parts[0];
            var key = parts[1];
            if (!missingBySection.TryGetValue(section, out var list))
            {
                list = new List<(string, string)>();
                missingBySection[section] = list;
            }

            list.Add((key, value));
        }

        foreach (var (section, keys) in missingBySection)
        {
            var sectionIndex = FindSectionIndex(lines, section);
            if (sectionIndex < 0)
            {
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                    lines.Add("");
                lines.Add($"[{section}]");
                sectionIndex = lines.Count - 1;
            }

            var insertAt = sectionIndex + 1;
            while (insertAt < lines.Count && !SectionRegex.IsMatch(lines[insertAt].Trim()))
                insertAt++;

            foreach (var (key, value) in keys)
            {
                lines.Insert(insertAt, $"{key}={value}");
                insertAt++;
            }
        }

        var directory = Path.GetDirectoryName(iniPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllLines(iniPath, lines, Encoding.UTF8);
    }

    /// <summary>
    /// Builds preset key map using Preferences Connection\ / Bittorrent\ style keys
    /// and mirrored Session\ variants for broader qBT compatibility.
    /// Values are kb/s for limits (-1 = unlimited).
    /// </summary>
    public static Dictionary<string, string> BuildPresetSettings(string connectionClass, string role)
    {
        var cls = (connectionClass ?? "average").Trim().ToLowerInvariant();
        var roleName = (role ?? "general").Trim().ToLowerInvariant();

        // Upload limits (KB/s). Download unlimited (-1) for average+.
        int upLimit;
        int dlLimit;
        int maxConnecs;
        int maxConnecsPerTorrent;
        int maxUploads;
        int maxUploadsPerTorrent;

        switch (cls)
        {
            case "slow":
                upLimit = 50;
                dlLimit = 500;
                maxConnecs = 200;
                maxConnecsPerTorrent = 50;
                maxUploads = 4;
                maxUploadsPerTorrent = 2;
                break;
            case "fast":
                upLimit = 2000;
                dlLimit = -1;
                maxConnecs = 800;
                maxConnecsPerTorrent = 120;
                maxUploads = 12;
                maxUploadsPerTorrent = 6;
                break;
            case "gigabit":
                upLimit = 20000;
                dlLimit = -1;
                maxConnecs = 2000;
                maxConnecsPerTorrent = 200;
                maxUploads = 20;
                maxUploadsPerTorrent = 10;
                break;
            default: // average
                upLimit = 200;
                dlLimit = -1;
                maxConnecs = 500;
                maxConnecsPerTorrent = 100;
                maxUploads = 8;
                maxUploadsPerTorrent = 4;
                break;
        }

        // Role adjustments.
        string preAllocation = "false";
        string sequentialDownload = "false";

        switch (roleName)
        {
            case "media":
                preAllocation = "true";
                sequentialDownload = "true";
                break;
            case "software":
                preAllocation = "true";
                sequentialDownload = "false";
                break;
            case "seed":
                // Prefer higher upload capacity for seeding.
                upLimit = cls switch
                {
                    "slow" => 80,
                    "fast" => 4000,
                    "gigabit" => 40000,
                    _ => 400
                };
                maxUploads = Math.Max(maxUploads, 16);
                maxUploadsPerTorrent = Math.Max(maxUploadsPerTorrent, 8);
                maxConnecs = Math.Max(maxConnecs, 600);
                preAllocation = "false";
                break;
            // general / custom: balanced defaults already set
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Preferences — modern slash-key style (qBittorrent 4.x+)
            ["Preferences|Connection\\GlobalDLLimit"] = dlLimit.ToString(),
            ["Preferences|Connection\\GlobalUPLimit"] = upLimit.ToString(),
            ["Preferences|Connection\\GlobalDLLimitAlt"] = "100",
            ["Preferences|Connection\\GlobalUPLimitAlt"] = "50",
            ["Preferences|Bittorrent\\MaxConnecs"] = maxConnecs.ToString(),
            ["Preferences|Bittorrent\\MaxConnecsPerTorrent"] = maxConnecsPerTorrent.ToString(),
            ["Preferences|Bittorrent\\MaxUploads"] = maxUploads.ToString(),
            ["Preferences|Bittorrent\\MaxUploadsPerTorrent"] = maxUploadsPerTorrent.ToString(),
            ["Preferences|Downloads\\PreAllocation"] = preAllocation,
            ["Preferences|Downloads\\SequentialDownload"] = sequentialDownload,

            // Session section — alternate layout used by some builds
            ["Session|GlobalDLSpeedLimit"] = dlLimit.ToString(),
            ["Session|GlobalUPSpeedLimit"] = upLimit.ToString(),
            ["Session|MaxConnections"] = maxConnecs.ToString(),
            ["Session|MaxConnectionsPerTorrent"] = maxConnecsPerTorrent.ToString(),
            ["Session|MaxUploads"] = maxUploads.ToString(),
            ["Session|MaxUploadsPerTorrent"] = maxUploadsPerTorrent.ToString(),

            // Legacy BitTorrent section
            ["BitTorrent|Session\\GlobalDLSpeedLimit"] = dlLimit.ToString(),
            ["BitTorrent|Session\\GlobalUPSpeedLimit"] = upLimit.ToString(),
            ["BitTorrent|Session\\MaxConnections"] = maxConnecs.ToString(),
            ["BitTorrent|Session\\MaxConnectionsPerTorrent"] = maxConnecsPerTorrent.ToString(),
            ["BitTorrent|Session\\MaxUploads"] = maxUploads.ToString(),
            ["BitTorrent|Session\\MaxUploadsPerTorrent"] = maxUploadsPerTorrent.ToString(),
        };

        return settings;
    }

    private static bool TryFindSetting(
        IReadOnlyDictionary<string, string> settings,
        string section,
        string key,
        out string mapKey,
        out string? value)
    {
        mapKey = $"{section}|{key}";
        foreach (var pair in settings)
        {
            if (string.Equals(pair.Key, mapKey, StringComparison.OrdinalIgnoreCase))
            {
                mapKey = pair.Key;
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static int FindSectionIndex(List<string> lines, string section)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var match = SectionRegex.Match(lines[i].Trim());
            if (match.Success
                && string.Equals(match.Groups[1].Value.Trim(), section, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
