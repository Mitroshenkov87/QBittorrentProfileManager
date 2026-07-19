using System.Text.Json;

namespace QBittorrentProfileManager.Services;

public sealed class LocalizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false
    };

    private readonly string _appDir;
    private Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private Dictionary<string, string> _fallbackEn = new(StringComparer.Ordinal);

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage { get; private set; } = "en";

    public IReadOnlyList<(string Code, string DisplayName)> SupportedLanguages { get; } =
        new List<(string Code, string DisplayName)>
        {
            ("en", "English"),
            ("ru", "Русский"),
            ("be", "Беларуская"),
            ("uk", "Українська"),
            ("kk", "Қазақша"),
        };

    public LocalizationService(string appDir)
    {
        _appDir = appDir ?? throw new ArgumentNullException(nameof(appDir));
        _fallbackEn = LoadDictionary("en") ?? new Dictionary<string, string>(StringComparer.Ordinal);
        SetLanguage("en");
    }

    public void SetLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            code = "en";

        code = code.Trim().ToLowerInvariant();
        var dict = LoadDictionary(code);
        if (dict is null || dict.Count == 0)
        {
            if (!string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
                dict = LoadDictionary("en");
        }

        _strings = dict ?? new Dictionary<string, string>(StringComparer.Ordinal);
        if (_fallbackEn.Count == 0)
            _fallbackEn = LoadDictionary("en") ?? _fallbackEn;

        CurrentLanguage = code;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (_strings.TryGetValue(key, out var value) && value is not null)
            return value;

        if (_fallbackEn.TryGetValue(key, out var en) && en is not null)
            return en;

        return key;
    }

    public string Get(string key, params object[] args)
    {
        var format = Get(key);
        if (args is null || args.Length == 0)
            return format;

        try
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private Dictionary<string, string>? LoadDictionary(string code)
    {
        foreach (var path in ResolveCandidatePaths(code))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
                if (dict is not null)
                    return new Dictionary<string, string>(dict, StringComparer.Ordinal);
            }
            catch
            {
                // try next path
            }
        }

        return null;
    }

    private IEnumerable<string> ResolveCandidatePaths(string code)
    {
        var fileName = $"strings.{code}.json";
        // 1) appDir/Localization
        yield return Path.Combine(_appDir, "Localization", fileName);
        // 2) appDir/../Localization (e.g. project tree when running from bin)
        yield return Path.GetFullPath(Path.Combine(_appDir, "..", "Localization", fileName));
        // 3) one more level up (bin/Release/netX/win-x64 → src/Localization)
        yield return Path.GetFullPath(Path.Combine(_appDir, "..", "..", "..", "..", "Localization", fileName));
    }
}
