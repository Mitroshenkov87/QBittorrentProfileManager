using System.Diagnostics;
using System.Text;
using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed class RobocopyService
{
    public bool IsAvailable { get; } = File.Exists(Path.Combine(Environment.SystemDirectory, "robocopy.exe"));

    public async Task MirrorDirectoryAsync(
        string sourceDir,
        string destinationDir,
        IProgress<CopyProgress>? progress = null,
        int completedOffset = 0,
        int totalItems = 0,
        string phase = "",
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? excludeDirNames = null)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("robocopy.exe is not available.");

        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Directory not found: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        var args = new StringBuilder();
        args.Append($"\"{sourceDir}\" \"{destinationDir}\" /MIR /MT:8 /R:2 /W:1 /NFL /NDL /NJH /NJS /NC /NS");
        if (excludeDirNames is not null)
        {
            foreach (var name in excludeDirNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
                args.Append($" /XD \"{name}\"");
        }

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "robocopy.exe"),
            Arguments = args.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start robocopy.");

        var readTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        _ = await readTask;

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode >= 8)
        {
            var error = await errorTask;
            throw new IOException($"Robocopy exited with code {process.ExitCode}. {error}".Trim());
        }

        var fileCount = CountFiles(sourceDir, excludeDirNames);
        progress?.Report(new CopyProgress(
            completedOffset + fileCount,
            Math.Max(totalItems, completedOffset + fileCount),
            Path.GetFileName(sourceDir),
            phase));
    }

    private static int CountFiles(string directory, IReadOnlyList<string>? excludeDirNames)
    {
        if (!Directory.Exists(directory))
            return 0;

        if (excludeDirNames is null || excludeDirNames.Count == 0)
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();

        // Lightweight: same exclusion semantics as FileCopyService (top-level name match in path).
        var count = 0;
        var stack = new Stack<string>();
        stack.Push(directory);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                count += Directory.EnumerateFiles(dir).Count();
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (excludeDirNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    stack.Push(sub);
                }
            }
            catch
            {
                // skip
            }
        }

        return count;
    }
}
