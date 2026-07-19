using QBittorrentProfileManager.Models;
using QBittorrentProfileManager.Services;

namespace QBittorrentProfileManager.Forms;

public sealed class SetupWizardForm : Form
{
    private const int StepCount = 5;

    private readonly AppPaths _paths;
    private readonly ConfigService _configService;
    private readonly LocalizationService _loc;

    private int _step;

    // Shared chrome
    private readonly Label _lblTitle = new();
    private readonly Button _btnBack = new();
    private readonly Button _btnNext = new();
    private readonly Button _btnFinish = new();
    private readonly Button _btnCancel = new();
    private readonly Panel _contentHost = new();

    // Step 0 — Language
    private readonly Panel _pageLanguage = new();
    private readonly Label _lblLanguage = new();
    private readonly ComboBox _cmbLanguage = new();

    // Step 1 — Intro / live detect
    private readonly Panel _pageIntro = new();
    private readonly Label _lblIntro = new();
    private readonly Label _lblWarnFirstRun = new();
    private readonly Label _lblLiveLocal = new();
    private readonly Label _lblLiveRoaming = new();

    // Step 2 — Store mode
    private readonly Panel _pageStore = new();
    private readonly Label _lblStoreMode = new();
    private readonly RadioButton _rbPortable = new();
    private readonly RadioButton _rbAppData = new();
    private readonly RadioButton _rbCustom = new();
    private readonly TextBox _txtCustomPath = new();
    private readonly Button _btnBrowse = new();

    // Step 3 — Profiles
    private readonly Panel _pageProfiles = new();
    private readonly Label _lblProfiles = new();
    private readonly TextBox _txtProfiles = new();
    private readonly Label _lblConnection = new();
    private readonly ComboBox _cmbConnection = new();
    private readonly Label _lblImport = new();
    private readonly ComboBox _cmbImport = new();

    // Step 4 — Finish
    private readonly Panel _pageFinish = new();
    private readonly Label _lblFinish = new();

    private static readonly string[] ConnectionClasses = ["slow", "average", "fast", "gigabit"];

    public SetupWizardForm(AppPaths paths, ConfigService configService, LocalizationService loc)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        Text = "qBittorrent Profile Manager";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 420);
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 249, 252);
        UiAssets.ApplyFormIcon(this);

        BuildUi();
        WireEvents();
        PopulateLanguageCombo();
        PopulateConnectionCombo();
        RefreshLiveDetection();
        RefreshImportOptions();
        ApplyTexts();
        ShowStep(0);
    }

    private void BuildUi()
    {
        _lblTitle.Location = new Point(16, 12);
        _lblTitle.Size = new Size(528, 24);
        _lblTitle.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        _lblTitle.AutoEllipsis = true;

        _contentHost.Location = new Point(12, 44);
        _contentHost.Size = new Size(536, 320);
        _contentHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        BuildLanguagePage();
        BuildIntroPage();
        BuildStorePage();
        BuildProfilesPage();
        BuildFinishPage();

        foreach (var page in new[] { _pageLanguage, _pageIntro, _pageStore, _pageProfiles, _pageFinish })
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _contentHost.Controls.Add(page);
        }

        const int btnY = 380;
        const int btnW = 90;
        const int btnH = 28;

        _btnBack.Size = new Size(btnW, btnH);
        _btnBack.Location = new Point(12, btnY);

        _btnNext.Size = new Size(btnW, btnH);
        _btnNext.Location = new Point(110, btnY);

        _btnFinish.Size = new Size(btnW, btnH);
        _btnFinish.Location = new Point(208, btnY);
        _btnFinish.Visible = false;

        _btnCancel.Size = new Size(btnW, btnH);
        _btnCancel.Location = new Point(458, btnY);
        _btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _btnCancel.DialogResult = DialogResult.None;

        AcceptButton = _btnNext;
        CancelButton = _btnCancel;

        Controls.Add(_lblTitle);
        Controls.Add(_contentHost);
        Controls.Add(_btnBack);
        Controls.Add(_btnNext);
        Controls.Add(_btnFinish);
        Controls.Add(_btnCancel);
    }

    private void BuildLanguagePage()
    {
        _lblLanguage.Location = new Point(8, 12);
        _lblLanguage.Size = new Size(520, 20);
        _lblLanguage.AutoEllipsis = true;

        _cmbLanguage.Location = new Point(8, 40);
        _cmbLanguage.Size = new Size(240, 24);
        _cmbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;

        _pageLanguage.Controls.Add(_lblLanguage);
        _pageLanguage.Controls.Add(_cmbLanguage);
    }

    private void BuildIntroPage()
    {
        _lblIntro.Location = new Point(8, 8);
        _lblIntro.Size = new Size(520, 48);
        _lblIntro.AutoSize = false;

        _lblWarnFirstRun.Location = new Point(8, 64);
        _lblWarnFirstRun.Size = new Size(520, 48);
        _lblWarnFirstRun.AutoSize = false;
        _lblWarnFirstRun.BackColor = Color.FromArgb(255, 255, 220);
        _lblWarnFirstRun.ForeColor = Color.FromArgb(120, 80, 0);
        _lblWarnFirstRun.Padding = new Padding(6);
        _lblWarnFirstRun.BorderStyle = BorderStyle.FixedSingle;

        _lblLiveLocal.Location = new Point(8, 128);
        _lblLiveLocal.Size = new Size(520, 80);
        _lblLiveLocal.AutoSize = false;

        _lblLiveRoaming.Location = new Point(8, 216);
        _lblLiveRoaming.Size = new Size(520, 80);
        _lblLiveRoaming.AutoSize = false;

        _pageIntro.Controls.Add(_lblIntro);
        _pageIntro.Controls.Add(_lblWarnFirstRun);
        _pageIntro.Controls.Add(_lblLiveLocal);
        _pageIntro.Controls.Add(_lblLiveRoaming);
    }

    private void BuildStorePage()
    {
        _lblStoreMode.Location = new Point(8, 8);
        _lblStoreMode.Size = new Size(520, 20);

        _rbPortable.Location = new Point(8, 40);
        _rbPortable.Size = new Size(520, 24);
        _rbPortable.Checked = true;

        _rbAppData.Location = new Point(8, 72);
        _rbAppData.Size = new Size(520, 24);

        _rbCustom.Location = new Point(8, 104);
        _rbCustom.Size = new Size(520, 24);

        _txtCustomPath.Location = new Point(28, 136);
        _txtCustomPath.Size = new Size(400, 24);
        _txtCustomPath.Enabled = false;

        _btnBrowse.Location = new Point(436, 134);
        _btnBrowse.Size = new Size(90, 28);
        _btnBrowse.Enabled = false;

        _pageStore.Controls.Add(_lblStoreMode);
        _pageStore.Controls.Add(_rbPortable);
        _pageStore.Controls.Add(_rbAppData);
        _pageStore.Controls.Add(_rbCustom);
        _pageStore.Controls.Add(_txtCustomPath);
        _pageStore.Controls.Add(_btnBrowse);
    }

    private void BuildProfilesPage()
    {
        _lblProfiles.Location = new Point(8, 8);
        _lblProfiles.Size = new Size(520, 20);

        _txtProfiles.Location = new Point(8, 32);
        _txtProfiles.Size = new Size(520, 140);
        _txtProfiles.Multiline = true;
        _txtProfiles.ScrollBars = ScrollBars.Vertical;
        _txtProfiles.AcceptsReturn = true;
        _txtProfiles.WordWrap = false;
        _txtProfiles.Text = "All\r\nEntertainment\r\nSoftware\r\nXXX";

        _lblConnection.Location = new Point(8, 184);
        _lblConnection.Size = new Size(520, 20);

        _cmbConnection.Location = new Point(8, 208);
        _cmbConnection.Size = new Size(240, 24);
        _cmbConnection.DropDownStyle = ComboBoxStyle.DropDownList;

        _lblImport.Location = new Point(8, 244);
        _lblImport.Size = new Size(520, 20);

        _cmbImport.Location = new Point(8, 268);
        _cmbImport.Size = new Size(320, 24);
        _cmbImport.DropDownStyle = ComboBoxStyle.DropDownList;

        _pageProfiles.Controls.Add(_lblProfiles);
        _pageProfiles.Controls.Add(_txtProfiles);
        _pageProfiles.Controls.Add(_lblConnection);
        _pageProfiles.Controls.Add(_cmbConnection);
        _pageProfiles.Controls.Add(_lblImport);
        _pageProfiles.Controls.Add(_cmbImport);
    }

    private void BuildFinishPage()
    {
        _lblFinish.Location = new Point(8, 8);
        _lblFinish.Size = new Size(520, 280);
        _lblFinish.AutoSize = false;

        _pageFinish.Controls.Add(_lblFinish);
    }

    private void WireEvents()
    {
        _cmbLanguage.SelectedIndexChanged += (_, _) =>
        {
            var code = GetSelectedLanguageCode();
            if (!string.IsNullOrWhiteSpace(code))
            {
                _loc.SetLanguage(code);
                ApplyTexts();
            }
        };

        _rbPortable.CheckedChanged += (_, _) => UpdateCustomPathEnabled();
        _rbAppData.CheckedChanged += (_, _) => UpdateCustomPathEnabled();
        _rbCustom.CheckedChanged += (_, _) => UpdateCustomPathEnabled();

        _btnBrowse.Click += (_, _) => BrowseCustomFolder();
        _txtProfiles.TextChanged += (_, _) => RefreshImportOptions();

        _btnBack.Click += (_, _) =>
        {
            if (_step > 0)
                ShowStep(_step - 1);
        };

        _btnNext.Click += (_, _) =>
        {
            if (!ValidateCurrentStep())
                return;
            if (_step < StepCount - 1)
                ShowStep(_step + 1);
        };

        _btnFinish.Click += async (_, _) => await FinishAsync();
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
    }

    private void PopulateLanguageCombo()
    {
        _cmbLanguage.Items.Clear();
        var preferred = 0;
        foreach (var (code, displayName) in _loc.SupportedLanguages)
        {
            var item = new LanguageItem(code, displayName);
            var index = _cmbLanguage.Items.Add(item);
            if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, _loc.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
            {
                preferred = index;
            }
        }

        _cmbLanguage.SelectedIndex = _cmbLanguage.Items.Count > 0 ? preferred : -1;
    }

    private string GetSelectedLanguageCode()
    {
        if (_cmbLanguage.SelectedItem is LanguageItem item)
            return item.Code;
        return "en";
    }

    private sealed class LanguageItem
    {
        public LanguageItem(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public string Code { get; }
        public string DisplayName { get; }
        public override string ToString() => DisplayName;
    }

    private void PopulateConnectionCombo()
    {
        _cmbConnection.Items.Clear();
        foreach (var c in ConnectionClasses)
            _cmbConnection.Items.Add(c);

        var avg = Array.IndexOf(ConnectionClasses, "average");
        _cmbConnection.SelectedIndex = avg >= 0 ? avg : 0;
    }

    private void UpdateCustomPathEnabled()
    {
        var custom = _rbCustom.Checked;
        _txtCustomPath.Enabled = custom;
        _btnBrowse.Enabled = custom;
    }

    private void BrowseCustomFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = _loc.Get("wizard.browse"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(_txtCustomPath.Text) && Directory.Exists(_txtCustomPath.Text))
            dlg.SelectedPath = _txtCustomPath.Text;

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtCustomPath.Text = dlg.SelectedPath;
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, StepCount - 1);

        _pageLanguage.Visible = _step == 0;
        _pageIntro.Visible = _step == 1;
        _pageStore.Visible = _step == 2;
        _pageProfiles.Visible = _step == 3;
        _pageFinish.Visible = _step == 4;

        if (_step == 1)
            RefreshLiveDetection();
        if (_step == 3)
            RefreshImportOptions();
        if (_step == 4)
            RefreshFinishSummary();

        _btnBack.Enabled = _step > 0;
        _btnNext.Visible = _step < StepCount - 1;
        _btnFinish.Visible = _step == StepCount - 1;
        AcceptButton = _step == StepCount - 1 ? _btnFinish : _btnNext;

        ApplyStepTitle();
    }

    private void ApplyStepTitle()
    {
        var key = _step switch
        {
            0 => "wizard.step_language",
            1 => "wizard.step_intro",
            2 => "wizard.step_store",
            3 => "wizard.step_profiles",
            4 => "wizard.step_finish",
            _ => "wizard.title"
        };
        _lblTitle.Text = _loc.Get(key);
    }

    private void ApplyTexts()
    {
        Text = _loc.Get("wizard.title");
        ApplyStepTitle();

        _btnBack.Text = _loc.Get("buttons.back");
        _btnNext.Text = _loc.Get("buttons.next");
        _btnFinish.Text = _loc.Get("buttons.finish");
        _btnCancel.Text = _loc.Get("buttons.cancel");

        _lblLanguage.Text = _loc.Get("wizard.language");
        _lblIntro.Text = _loc.Get("wizard.intro");
        _lblWarnFirstRun.Text = _loc.Get("wizard.warn_first_run");

        _lblStoreMode.Text = _loc.Get("wizard.store_mode");
        _rbPortable.Text = _loc.Get("wizard.store_portable");
        _rbAppData.Text = _loc.Get("wizard.store_appdata");
        _rbCustom.Text = _loc.Get("wizard.store_custom");
        _btnBrowse.Text = _loc.Get("wizard.browse");

        _lblProfiles.Text = _loc.Get("wizard.profiles");
        _lblConnection.Text = _loc.Get("wizard.connection_class");
        _lblImport.Text = _loc.Get("wizard.import");

        RefreshLiveDetection();
        RefreshImportOptions();
        if (_step == 4)
            RefreshFinishSummary();
    }

    private void RefreshLiveDetection()
    {
        _lblLiveLocal.Text = FormatLiveFolderStatus(
            _loc.Get("wizard.live_local"),
            _paths.LiveLocal);
        _lblLiveRoaming.Text = FormatLiveFolderStatus(
            _loc.Get("wizard.live_roaming"),
            _paths.LiveRoaming);
    }

    private string FormatLiveFolderStatus(string title, string path)
    {
        var dirExists = Directory.Exists(path);
        var iniExists = File.Exists(Path.Combine(path, "qBittorrent.ini"));
        var dbExists = File.Exists(Path.Combine(path, "torrents.db"));

        var dirLabel = dirExists ? _loc.Get("wizard.path_exists") : _loc.Get("wizard.path_missing");
        var iniLabel = iniExists ? _loc.Get("wizard.file_found") : _loc.Get("wizard.file_missing");
        var dbLabel = dbExists ? _loc.Get("wizard.file_found") : _loc.Get("wizard.file_missing");

        return
            $"{title}\r\n" +
            $"{path}\r\n" +
            $"{dirLabel}\r\n" +
            $"qBittorrent.ini: {iniLabel}\r\n" +
            $"torrents.db: {dbLabel}";
    }

    private void RefreshImportOptions()
    {
        var previous = _cmbImport.SelectedItem?.ToString();
        var noneText = _loc.Get("wizard.import_none");

        _cmbImport.Items.Clear();
        _cmbImport.Items.Add(noneText);

        foreach (var name in GetProfileNames())
            _cmbImport.Items.Add(name);

        var select = 0;
        if (!string.IsNullOrEmpty(previous) && previous != noneText)
        {
            for (var i = 1; i < _cmbImport.Items.Count; i++)
            {
                if (string.Equals(_cmbImport.Items[i]?.ToString(), previous, StringComparison.Ordinal))
                {
                    select = i;
                    break;
                }
            }
        }

        _cmbImport.SelectedIndex = _cmbImport.Items.Count > 0 ? select : -1;
    }

    private void RefreshFinishSummary()
    {
        var mode = GetSelectedStoreMode();
        var custom = _txtCustomPath.Text.Trim();
        var profiles = string.Join(", ", GetProfileNames());
        var connection = _cmbConnection.SelectedItem?.ToString() ?? "average";
        var import = GetSelectedImportProfileName() ?? _loc.Get("wizard.import_none");
        var language = GetSelectedLanguageCode();

        _lblFinish.Text =
            $"{_loc.Get("wizard.finish_ready")}\r\n\r\n" +
            $"{_loc.Get("wizard.language")}: {language}\r\n" +
            $"{_loc.Get("wizard.store_mode")}: {mode}" +
            (mode == "Custom" && custom.Length > 0 ? $"\r\n  {custom}" : "") + "\r\n" +
            $"{_loc.Get("wizard.connection_class")}: {connection}\r\n" +
            $"{_loc.Get("wizard.profiles")}: {profiles}\r\n" +
            $"{_loc.Get("wizard.import")}: {import}";
    }

    private IReadOnlyList<string> GetProfileNames()
    {
        return _txtProfiles.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetSelectedStoreMode()
    {
        if (_rbAppData.Checked)
            return "AppData";
        if (_rbCustom.Checked)
            return "Custom";
        return "Portable";
    }

    private string? GetSelectedImportProfileName()
    {
        if (_cmbImport.SelectedIndex <= 0)
            return null;

        var name = _cmbImport.SelectedItem?.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private bool ValidateCurrentStep()
    {
        return _step switch
        {
            2 => ValidateStoreMode(),
            3 => ValidateProfiles(),
            _ => true
        };
    }

    private bool ValidateStoreMode()
    {
        if (!_rbCustom.Checked)
            return true;

        var path = _txtCustomPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(
                this,
                _loc.Get("wizard.validation_custom_path"),
                _loc.Get("wizard.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            // Ensure the path is usable.
            var probe = Path.Combine(path, ".qpm_write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"{_loc.Get("wizard.validation_custom_path")}\r\n{ex.Message}",
                _loc.Get("wizard.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private bool ValidateProfiles()
    {
        if (GetProfileNames().Count == 0)
        {
            MessageBox.Show(
                this,
                _loc.Get("wizard.validation_profiles"),
                _loc.Get("wizard.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async Task FinishAsync()
    {
        if (!ValidateStoreMode() || !ValidateProfiles())
            return;

        var language = GetSelectedLanguageCode();
        var storeMode = GetSelectedStoreMode();
        var customRoot = storeMode == "Custom" ? _txtCustomPath.Text.Trim() : null;
        var connection = _cmbConnection.SelectedItem?.ToString() ?? "average";
        var profileNames = GetProfileNames();
        var importName = GetSelectedImportProfileName();

        SetBusy(true);
        try
        {
            var config = _configService.Load();
            config.SetupCompleted = true;
            config.Language = language;
            config.StoreMode = storeMode;
            config.CustomProfilesRoot = storeMode == "Custom" ? customRoot : null;
            config.ConnectionClass = connection;
            config.Profiles.Clear();
            config.ActiveProfileId = null;

            // Persist store settings before profile ops that re-load config.
            _configService.Save(config);

            var log = new LogService(_paths);
            var robocopy = new RobocopyService();
            var copy = new FileCopyService(robocopy);
            var backup = new BackupService(_paths, log, copy, _paths.ResolveProfilesRoot(config));
            var profiles = new ProfileService(_paths, copy, backup, _configService, log);

            ProfileInfo? importTarget = null;
            foreach (var name in profileNames)
            {
                var created = profiles.CreateProfile(config, name, InferRole(name));
                if (importName is not null
                    && string.Equals(created.Name, importName, StringComparison.OrdinalIgnoreCase))
                {
                    importTarget = created;
                }
            }

            if (importTarget is not null)
            {
                // Profiles already exist — mirror live data into the selected one.
                await profiles.SaveLiveToProfileAsync(importTarget.Id);
                config = _configService.Load();
            }

            config.SetupCompleted = true;
            config.Language = language;
            config.StoreMode = storeMode;
            config.CustomProfilesRoot = storeMode == "Custom" ? customRoot : null;
            config.ConnectionClass = connection;
            _configService.Save(config);

            _loc.SetLanguage(language);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"{_loc.Get("wizard.error_finish")}\r\n{ex.Message}",
                _loc.Get("wizard.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _btnBack.Enabled = !busy && _step > 0;
        _btnNext.Enabled = !busy;
        _btnFinish.Enabled = !busy;
        _btnCancel.Enabled = !busy;
        _contentHost.Enabled = !busy;
    }

    private static string InferRole(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "entertainment" or "media" => "media",
            "software" => "software",
            "seed" or "seeding" => "seed",
            "xxx" or "adult" => "custom",
            _ => "general"
        };
    }
}
