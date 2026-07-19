namespace QBittorrentProfileManager.Models;

public sealed record CopyProgress(int CompletedItems, int TotalItems, string CurrentItem, string Phase = "");
