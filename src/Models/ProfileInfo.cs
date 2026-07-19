namespace QBittorrentProfileManager.Models;

public sealed class ProfileInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>general | media | software | seed | custom</summary>
    public string Role { get; set; } = "general";
}
