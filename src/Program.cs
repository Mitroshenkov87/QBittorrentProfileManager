using QBittorrentProfileManager.Forms;
using QBittorrentProfileManager.Services;

namespace QBittorrentProfileManager;

static class Program
{
    [STAThread]
    static void Main()
    {
        // 1) Runtime check (English MessageBox; no localization dependency)
        if (!RuntimeChecker.IsDesktopRuntimeInstalled(out _, out var dotnetRoot))
        {
            var answer = RuntimeChecker.ShowMissingRuntimeDialog(dotnetRoot);
            if (answer == DialogResult.Yes)
                RuntimeChecker.TryOpenDownloadPage();
            return;
        }

        ApplicationConfiguration.Initialize();

        // 2) Load config + localization
        var paths = new AppPaths();
        var configService = new ConfigService(paths);
        var config = configService.Load();
        var loc = new LocalizationService(paths.AppDir);
        loc.SetLanguage(string.IsNullOrWhiteSpace(config.Language) ? "en" : config.Language);

        // 3) First-run setup wizard
        if (!config.SetupCompleted)
        {
            using var wizard = new SetupWizardForm(paths, configService, loc);
            if (wizard.ShowDialog() != DialogResult.OK)
                return;
            config = configService.Load();
            loc.SetLanguage(config.Language);
        }

        // 4) Main window
        Application.Run(new MainForm(paths, configService, loc));
    }
}
