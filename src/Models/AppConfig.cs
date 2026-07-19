namespace QBittorrentProfileManager.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public bool SetupCompleted { get; set; }
    public string Language { get; set; } = "en"; // en, ru, be, uk, kk
    /// <summary>Portable | AppData | Custom</summary>
    public string StoreMode { get; set; } = "Portable";
    public string? CustomProfilesRoot { get; set; }
    public string? ActiveProfileId { get; set; }
    public string? LastOperation { get; set; }
    public DateTime? LastOperationUtc { get; set; }
    public DateTime? LastBackupUtc { get; set; }
    /// <summary>slow | average | fast | gigabit</summary>
    public string ConnectionClass { get; set; } = "average";
    public List<ProfileInfo> Profiles { get; set; } = new();
}
