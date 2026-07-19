namespace QBittorrentProfileManager.Services;

public sealed class LogService
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public LogService(AppPaths paths)
    {
        _logPath = paths.LogPath;
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERROR", message);

    public void Warn(string message) => Write("WARN", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Ignore logging failures.
            }
        }
    }
}
