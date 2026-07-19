using System.Diagnostics;

namespace QBittorrentProfileManager.Services;

/// <summary>
/// Checks for required .NET runtime presence. English UI messages only — no localization.
/// </summary>
public static class RuntimeChecker
{
    private static readonly Version MinimumVersion = new(8, 0, 0);

    public const string DownloadPageUrl = "https://dotnet.microsoft.com/download/dotnet/8.0";
    public const string DirectInstallerUrl =
        "https://aka.ms/dotnet-core-applaunch?framework=Microsoft.WindowsDesktop.App&framework_version=8.0.0&arch=x64&rid=win-x64";

    public static bool IsDesktopRuntimeInstalled(out string? installedVersion, out string dotnetRoot)
    {
        dotnetRoot = ResolveDotnetRoot();
        var desktopPath = Path.Combine(dotnetRoot, "shared", "Microsoft.WindowsDesktop.App");
        if (!Directory.Exists(desktopPath))
        {
            installedVersion = null;
            return false;
        }

        Version? best = null;
        foreach (var directory in Directory.EnumerateDirectories(desktopPath))
        {
            var name = Path.GetFileName(directory);
            if (!Version.TryParse(name, out var version))
                continue;

            if (version >= MinimumVersion && (best is null || version > best))
                best = version;
        }

        if (best is null)
        {
            installedVersion = null;
            return false;
        }

        installedVersion = best.ToString();
        return true;
    }

    public static bool IsAspNetCoreOrBaseRuntimeInstalled(out string? installedVersion, out string dotnetRoot)
    {
        // Prefer desktop check first; fall back to Microsoft.NETCore.App for headless hosts.
        if (IsDesktopRuntimeInstalled(out installedVersion, out dotnetRoot))
            return true;

        var corePath = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(corePath))
        {
            installedVersion = null;
            return false;
        }

        Version? best = null;
        foreach (var directory in Directory.EnumerateDirectories(corePath))
        {
            var name = Path.GetFileName(directory);
            if (!Version.TryParse(name, out var version))
                continue;

            if (version >= MinimumVersion && (best is null || version > best))
                best = version;
        }

        if (best is null)
        {
            installedVersion = null;
            return false;
        }

        installedVersion = best.ToString();
        return true;
    }

    public static string GetMissingRuntimeMessage(string dotnetRoot) =>
        "This application requires the .NET 8 Desktop Runtime.\n\n" +
        $"It was not found under:\n{dotnetRoot}\\shared\\Microsoft.WindowsDesktop.App\\\n\n" +
        "Download \"Desktop Runtime\" for Windows x64 from:\n" +
        DownloadPageUrl;

    /// <summary>
    /// Shows an English Yes/No dialog offering to open the runtime download page.
    /// </summary>
    public static DialogResult ShowMissingRuntimeDialog(string dotnetRoot)
    {
        var message = GetMissingRuntimeMessage(dotnetRoot)
            + "\n\nOpen the download page now?";
        return MessageBox.Show(
            message,
            "qBittorrent Profile Manager — Missing Runtime",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Error);
    }

    public static bool TryOpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DirectInstallerUrl) { UseShellExecute = true });
            return true;
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo(DownloadPageUrl) { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static string ResolveDotnetRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "dotnet");
    }
}
