using QBittorrentProfileManager.Models;

namespace QBittorrentProfileManager.Services;

public sealed class FileCopyService
{
    private const int BufferSize = 1024 * 1024;
    private readonly RobocopyService _robocopy;

    public FileCopyService(RobocopyService robocopy)
    {
        _robocopy = robocopy;
    }

    public int CountFiles(string directory, IReadOnlyList<string>? excludeDirNames = null)
    {
        if (!Directory.Exists(directory))
            return 0;

        return EnumerateFilesSafe(directory, excludeDirNames).Count();
    }

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
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Directory not found: {sourceDir}");

        if (_robocopy.IsAvailable)
        {
            await _robocopy.MirrorDirectoryAsync(
                sourceDir,
                destinationDir,
                progress,
                completedOffset,
                totalItems,
                phase,
                cancellationToken,
                excludeDirNames);
            return;
        }

        Directory.CreateDirectory(destinationDir);

        var sourceFiles = EnumerateFilesSafe(sourceDir, excludeDirNames).ToList();
        var sourceSet = new HashSet<string>(
            sourceFiles.Select(f => Path.GetRelativePath(sourceDir, f)),
            StringComparer.OrdinalIgnoreCase);

        var completed = completedOffset;
        foreach (var file in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            var sourceInfo = new FileInfo(file);
            var needsCopy = !File.Exists(target)
                || new FileInfo(target).Length != sourceInfo.Length
                || File.GetLastWriteTimeUtc(target) != sourceInfo.LastWriteTimeUtc;

            if (needsCopy)
                await CopyFileAsync(file, target, cancellationToken);

            completed++;
            progress?.Report(new CopyProgress(completed, Math.Max(totalItems, completed), relative, phase));
        }

        foreach (var destFile in EnumerateFilesSafe(destinationDir, excludeDirNames))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(destinationDir, destFile);
            if (!sourceSet.Contains(relative))
            {
                File.SetAttributes(destFile, FileAttributes.Normal);
                File.Delete(destFile);
            }
        }

        foreach (var destDir in Directory.EnumerateDirectories(destinationDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (IsUnderExcludedDir(destinationDir, destDir, excludeDirNames))
                continue;

            if (!Directory.EnumerateFileSystemEntries(destDir).Any())
            {
                try
                {
                    Directory.Delete(destDir);
                }
                catch
                {
                    // ignore non-empty races
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, IReadOnlyList<string>? excludeDirNames)
    {
        if (excludeDirNames is null || excludeDirNames.Count == 0)
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);

        var result = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                    result.Add(file);

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
                // skip inaccessible
            }
        }

        return result;
    }

    private static bool IsUnderExcludedDir(string root, string dir, IReadOnlyList<string>? excludeDirNames)
    {
        if (excludeDirNames is null || excludeDirNames.Count == 0)
            return false;

        var relative = Path.GetRelativePath(root, dir);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => excludeDirNames.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var src = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        await using var dst = new FileStream(
            destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        await src.CopyToAsync(dst, cancellationToken);
        File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
    }
}
