using QBittorrentProfileManager.Models;
using QBittorrentProfileManager.Services;

namespace QBittorrentProfileManager.Forms;

public sealed class MainForm : Form
{
    private readonly AppPaths _paths;
    private readonly ConfigService _configService;
    private readonly LocalizationService _loc;
    private readonly LogService _log;
    private readonly QBittorrentProcessService _processService;
    private readonly RobocopyService _robocopy;
    private readonly FileCopyService _copy;
    private readonly BackupService _backup;
    private readonly ProfileService _profileService;
    private readonly ProfileDetector _profileDetector;
    private readonly System.Windows.Forms.Timer _statusTimer;

    private bool _operationInProgress;
    private string? _selectedProfileId;

    private MenuStrip menuStrip = null!;
    private ToolStripMenuItem menuFile = null!;
    private ToolStripMenuItem menuExit = null!;
    private ToolStripMenuItem menuLanguage = null!;
    private ToolStripMenuItem menuPresets = null!;
    private ToolStripMenuItem menuAbout = null!;

    private Label lblStore = null!;
    private Label lblLiveLocal = null!;
    private Label lblLiveRoaming = null!;
    private Label lblQbtStatus = null!;
    private Label lblActiveProfile = null!;
    private Label lblBackup = null!;
    private Label lblLastOperation = null!;
    private Label lblCopyEngine = null!;

    private ListView lvProfiles = null!;
    private Button btnCloseQbt = null!;
    private Button btnActivate = null!;
    private Button btnSaveLive = null!;
    private Button btnNewProfile = null!;
    private Button btnDeleteProfile = null!;
    private Button btnRestoreBackup = null!;
    private Button btnRefresh = null!;
    private ProgressBar progressBar = null!;
    private Label lblProgress = null!;
    private TextBox txtLog = null!;
    private PictureBox picHeader = null!;
    private ImageList roleImages = null!;

    public MainForm(AppPaths paths, ConfigService configService, LocalizationService loc)
    {
        _paths = paths;
        _configService = configService;
        _loc = loc;

        _log = new LogService(_paths);
        _processService = new QBittorrentProcessService();
        _robocopy = new RobocopyService();
        _copy = new FileCopyService(_robocopy);

        var config = _configService.Load();
        var profilesRoot = _paths.ResolveProfilesRoot(config);
        _backup = new BackupService(_paths, _log, _copy, profilesRoot);
        _profileService = new ProfileService(_paths, _copy, _backup, _configService, _log);
        _profileDetector = new ProfileDetector(_paths);

        InitializeComponent();
        ApplyBranding();
        ApplyLocalization();
        RefreshStatus();

        _statusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();

        _log.Info("Main window started.");
        AppendLog("Ready.");
    }

    private void ApplyBranding()
    {
        UiAssets.ApplyFormIcon(this);
        if (UiAssets.HeaderImage is not null)
            picHeader.Image = UiAssets.HeaderImage;

        UiAssets.ApplyButtonIcon(btnActivate, "activate");
        UiAssets.ApplyButtonIcon(btnSaveLive, "save");
        UiAssets.ApplyButtonIcon(btnNewProfile, "new");
        UiAssets.ApplyButtonIcon(btnDeleteProfile, "delete");
        UiAssets.ApplyButtonIcon(btnRestoreBackup, "restore");
        UiAssets.ApplyButtonIcon(btnRefresh, "refresh");

        BackColor = Color.FromArgb(248, 249, 252);
        lvProfiles.BackColor = Color.White;
        txtLog.BackColor = Color.FromArgb(250, 250, 252);
        txtLog.BorderStyle = BorderStyle.FixedSingle;
    }

    private void InitializeComponent()
    {
        Font = new Font("Segoe UI", 9F);
        Text = "qBittorrent Profile Manager";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(700, 600);
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(7F, 15F);

        menuStrip = new MenuStrip();
        menuFile = new ToolStripMenuItem();
        menuExit = new ToolStripMenuItem();
        menuLanguage = new ToolStripMenuItem();
        menuPresets = new ToolStripMenuItem();
        menuAbout = new ToolStripMenuItem();

        menuExit.Click += (_, _) => Close();
        menuPresets.Click += menuPresets_Click;
        menuAbout.Click += menuAbout_Click;

        menuFile.DropDownItems.Add(menuExit);
        menuStrip.Items.AddRange([menuFile, menuLanguage, menuPresets, menuAbout]);
        MainMenuStrip = menuStrip;

        const int top = 88; // below header banner

        picHeader = new PictureBox
        {
            Location = new Point(0, 24),
            Size = new Size(700, 60),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(55, 70, 140)
        };

        lblStore = MakeLabel(12, top);
        lblLiveLocal = MakeLabel(12, top + 18);
        lblLiveRoaming = MakeLabel(12, top + 36);
        lblQbtStatus = MakeLabel(12, top + 60);
        lblQbtStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblActiveProfile = MakeLabel(12, top + 78);
        lblBackup = MakeLabel(12, top + 96);
        lblLastOperation = MakeLabel(12, top + 114);
        lblCopyEngine = MakeLabel(12, top + 132);

        btnCloseQbt = MakeButton(500, top + 56, 180, 30);
        btnCloseQbt.Click += async (_, _) =>
        {
            await CloseQBittorrentAsync();
            RefreshStatus();
        };

        roleImages = UiAssets.CreateRoleImageList();
        lvProfiles = new ListView
        {
            Location = new Point(12, top + 156),
            Size = new Size(676, 140),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            GridLines = true,
            SmallImageList = roleImages
        };
        lvProfiles.Columns.Add("Profile", 260);
        lvProfiles.Columns.Add("Role", 140);
        lvProfiles.Columns.Add("Status", 120);
        lvProfiles.SelectedIndexChanged += (_, _) =>
        {
            _selectedProfileId = lvProfiles.SelectedItems.Count > 0
                ? lvProfiles.SelectedItems[0].Tag as string
                : null;
            UpdateButtonStates(_processService.IsRunning(out _), _operationInProgress);
        };

        var by = top + 304;
        btnActivate = MakeButton(12, by, 150, 32);
        btnActivate.Click += btnActivate_Click;
        btnSaveLive = MakeButton(168, by, 170, 32);
        btnSaveLive.Click += btnSaveLive_Click;
        btnNewProfile = MakeButton(344, by, 120, 32);
        btnNewProfile.Click += btnNewProfile_Click;
        btnDeleteProfile = MakeButton(470, by, 120, 32);
        btnDeleteProfile.Click += btnDeleteProfile_Click;
        btnRestoreBackup = MakeButton(596, by, 92, 32);
        btnRestoreBackup.Click += btnRestoreBackup_Click;

        btnRefresh = MakeButton(12, by + 36, 110, 28);
        btnRefresh.Click += (_, _) => RefreshStatus(force: true);

        progressBar = new ProgressBar
        {
            Location = new Point(128, by + 38),
            Size = new Size(560, 22),
            Style = ProgressBarStyle.Continuous
        };

        lblProgress = MakeLabel(12, by + 68);
        lblProgress.AutoEllipsis = true;
        lblProgress.MaximumSize = new Size(676, 18);
        lblProgress.Size = new Size(676, 18);

        txtLog = new TextBox
        {
            Location = new Point(12, by + 90),
            Size = new Size(676, 90),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            TabStop = false,
            WordWrap = false
        };

        Controls.Add(menuStrip);
        Controls.Add(picHeader);
        Controls.Add(lblStore);
        Controls.Add(lblLiveLocal);
        Controls.Add(lblLiveRoaming);
        Controls.Add(lblQbtStatus);
        Controls.Add(lblActiveProfile);
        Controls.Add(lblBackup);
        Controls.Add(lblLastOperation);
        Controls.Add(lblCopyEngine);
        Controls.Add(btnCloseQbt);
        Controls.Add(lvProfiles);
        Controls.Add(btnActivate);
        Controls.Add(btnSaveLive);
        Controls.Add(btnNewProfile);
        Controls.Add(btnDeleteProfile);
        Controls.Add(btnRestoreBackup);
        Controls.Add(btnRefresh);
        Controls.Add(progressBar);
        Controls.Add(lblProgress);
        Controls.Add(txtLog);
    }

    private static Label MakeLabel(int x, int y) =>
        new()
        {
            AutoSize = true,
            Location = new Point(x, y),
            MaximumSize = new Size(680, 0)
        };

    private static Button MakeButton(int x, int y, int w, int h) =>
        new()
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            UseVisualStyleBackColor = true
        };

    private void ApplyLocalization()
    {
        Text = _loc.Get("app.title");

        menuFile.Text = _loc.Get("menu.file");
        menuExit.Text = _loc.Get("menu.exit");
        menuLanguage.Text = _loc.Get("menu.language");
        menuPresets.Text = _loc.Get("menu.presets");
        menuAbout.Text = _loc.Get("menu.about");

        btnCloseQbt.Text = _loc.Get("btn.close_qbt");
        btnActivate.Text = _loc.Get("btn.activate");
        btnSaveLive.Text = _loc.Get("btn.save_live");
        btnNewProfile.Text = _loc.Get("btn.new_profile");
        btnDeleteProfile.Text = _loc.Get("btn.delete_profile");
        btnRestoreBackup.Text = _loc.Get("btn.restore_backup");
        btnRefresh.Text = _loc.Get("btn.refresh");

        if (lvProfiles.Columns.Count >= 3)
        {
            lvProfiles.Columns[0].Text = _loc.Get("col.profile");
            lvProfiles.Columns[1].Text = _loc.Get("col.role");
            lvProfiles.Columns[2].Text = _loc.Get("col.status");
        }

        RebuildLanguageMenu();
        RefreshStaticPathLabels();
        PopulateProfilesList(_configService.Load());
    }

    private void RebuildLanguageMenu()
    {
        menuLanguage.DropDownItems.Clear();
        foreach (var (code, displayName) in _loc.SupportedLanguages)
        {
            var item = new ToolStripMenuItem(displayName)
            {
                Tag = code,
                Checked = string.Equals(code, _loc.CurrentLanguage, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false
            };
            item.Click += LanguageMenuItem_Click;
            menuLanguage.DropDownItems.Add(item);
        }
    }

    private void LanguageMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not string code)
            return;

        if (string.Equals(code, _loc.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
            return;

        var config = _configService.Load();
        config.Language = code;
        _configService.Save(config);
        _loc.SetLanguage(code);
        ApplyLocalization();
        RefreshStatus(force: true);
    }

    private void RefreshStaticPathLabels()
    {
        var config = _configService.Load();
        var store = _paths.ResolveProfilesRoot(config);
        lblStore.Text = _loc.Get("status.store", store);
        lblLiveLocal.Text = _loc.Get("status.live_local", _paths.LiveLocal);
        lblLiveRoaming.Text = _loc.Get("status.live_roaming", _paths.LiveRoaming);

        var engine = _robocopy.IsAvailable ? "Robocopy" : "Managed";
        lblCopyEngine.Text = _loc.Get("status.copy_engine", engine);
    }

    private void RefreshStatus(bool force = false)
    {
        if (_operationInProgress && !force)
            return;

        if (IsDisposed)
            return;

        try
        {
            var config = _configService.Load();
            _backup.ProfilesRoot = _paths.ResolveProfilesRoot(config);

            var running = _processService.IsRunning(out var pid);
            lblQbtStatus.Text = running
                ? _loc.Get("status.qbt_running", pid ?? 0)
                : _loc.Get("status.qbt_stopped");
            lblQbtStatus.ForeColor = running ? Color.DarkRed : Color.DarkGreen;

            var detected = _profileDetector.DetectActiveProfile(config);
            string activeName;
            if (detected is not null)
            {
                activeName = detected.Name;
            }
            else if (!string.IsNullOrWhiteSpace(config.ActiveProfileId))
            {
                var byId = config.Profiles.FirstOrDefault(p =>
                    string.Equals(p.Id, config.ActiveProfileId, StringComparison.OrdinalIgnoreCase));
                activeName = byId?.Name ?? config.ActiveProfileId!;
            }
            else
            {
                activeName = _loc.Get("status.unknown_profile");
            }

            lblActiveProfile.Text = _loc.Get("status.active_profile", activeName);

            var backupDate = _backup.GetBackupDate();
            lblBackup.Text = backupDate.HasValue
                ? _loc.Get("status.backup", backupDate.Value.ToString("g"))
                : _loc.Get("status.no_backup");

            var lastOp = string.IsNullOrWhiteSpace(config.LastOperation) ? "—" : config.LastOperation!;
            lblLastOperation.Text = _loc.Get("status.last_op", lastOp);

            RefreshStaticPathLabels();
            PopulateProfilesList(config, detected?.Id ?? config.ActiveProfileId);
            UpdateButtonStates(running, _operationInProgress);
        }
        catch (Exception ex)
        {
            _log.Error($"RefreshStatus failed: {ex.Message}");
        }
    }

    private void PopulateProfilesList(AppConfig config, string? activeId = null)
    {
        activeId ??= _profileDetector.DetectActiveProfileId(config) ?? config.ActiveProfileId;
        var keepId = _selectedProfileId;

        lvProfiles.BeginUpdate();
        try
        {
            lvProfiles.Items.Clear();
            foreach (var profile in config.Profiles)
            {
                var isActive = !string.IsNullOrWhiteSpace(activeId)
                    && string.Equals(profile.Id, activeId, StringComparison.OrdinalIgnoreCase);

                var roleKey = string.IsNullOrWhiteSpace(profile.Role) ? "general" : profile.Role.Trim().ToLowerInvariant();
                if (roleImages.Images.ContainsKey(roleKey) is false)
                    roleKey = "custom";

                var item = new ListViewItem(profile.Name, isActive ? "active" : roleKey)
                {
                    Tag = profile.Id
                };
                item.SubItems.Add(LocalizeRole(profile.Role));
                item.SubItems.Add(isActive ? _loc.Get("profile.active") : _loc.Get("profile.inactive"));
                if (isActive)
                    item.Font = new Font(lvProfiles.Font, FontStyle.Bold);

                lvProfiles.Items.Add(item);

                if (keepId is not null
                    && string.Equals(profile.Id, keepId, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                }
            }

            if (lvProfiles.SelectedItems.Count == 0 && lvProfiles.Items.Count > 0 && keepId is null)
            {
                // leave unselected
            }
        }
        finally
        {
            lvProfiles.EndUpdate();
        }

        _selectedProfileId = lvProfiles.SelectedItems.Count > 0
            ? lvProfiles.SelectedItems[0].Tag as string
            : keepId;
    }

    private string LocalizeRole(string role)
    {
        var key = "role." + (string.IsNullOrWhiteSpace(role) ? "general" : role.Trim().ToLowerInvariant());
        var text = _loc.Get(key);
        return text == key ? role : text;
    }

    private void UpdateButtonStates(bool qbtRunning, bool opInProgress)
    {
        var canMutate = !qbtRunning && !opInProgress;
        var hasSelection = !string.IsNullOrWhiteSpace(_selectedProfileId)
            || lvProfiles.SelectedItems.Count > 0;

        btnCloseQbt.Enabled = qbtRunning && !opInProgress;
        btnActivate.Enabled = canMutate && hasSelection;
        btnSaveLive.Enabled = canMutate && hasSelection;
        btnNewProfile.Enabled = !opInProgress;
        btnDeleteProfile.Enabled = !opInProgress && hasSelection;
        btnRestoreBackup.Enabled = canMutate;
        btnRefresh.Enabled = !opInProgress;
        menuPresets.Enabled = !opInProgress;
        menuLanguage.Enabled = !opInProgress;
    }

    private ProfileInfo? GetSelectedProfile(AppConfig config)
    {
        var id = lvProfiles.SelectedItems.Count > 0
            ? lvProfiles.SelectedItems[0].Tag as string
            : _selectedProfileId;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        return config.Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private async void btnActivate_Click(object? sender, EventArgs e)
    {
        var config = _configService.Load();
        var profile = GetSelectedProfile(config);
        if (profile is null)
        {
            MessageBox.Show(this, _loc.Get("msg.need_selection"), _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            _loc.Get("msg.confirm_activate", profile.Name),
            _loc.Get("btn.activate"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;

        var saveFirst = false;
        if (!string.IsNullOrWhiteSpace(config.ActiveProfileId)
            && !string.Equals(config.ActiveProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            var saveAnswer = MessageBox.Show(
                this,
                _loc.Get("msg.save_current_first"),
                _loc.Get("btn.activate"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            saveFirst = saveAnswer == DialogResult.Yes;
        }

        await RunOperationAsync(
            _loc.Get("op.activate", profile.Name),
            async ct =>
            {
                // Reload so ActivateProfileAsync sees latest ActiveProfileId / profiles.
                var fresh = _configService.Load();
                await _profileService.ActivateProfileAsync(fresh, profile.Id, saveFirst, CreateProgress(), ct);
            });
    }

    private async void btnSaveLive_Click(object? sender, EventArgs e)
    {
        var config = _configService.Load();
        var profile = GetSelectedProfile(config);
        if (profile is null)
        {
            MessageBox.Show(this, _loc.Get("msg.need_selection"), _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await RunOperationAsync(
            _loc.Get("op.save", profile.Name),
            async ct =>
            {
                await _profileService.SaveLiveToProfileAsync(profile.Id, CreateProgress(), ct);
                // Reload for display after save (ActiveProfileId may be set by service).
                _ = _configService.Load();
            });
    }

    private void btnNewProfile_Click(object? sender, EventArgs e)
    {
        if (_operationInProgress)
            return;

        var name = PromptProfileName();
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            var config = _configService.Load();
            var created = _profileService.CreateProfile(config, name.Trim(), role: "general");
            _selectedProfileId = created.Id;
            AppendLog(_loc.Get("msg.profile_created") + $" ({created.Name})");
            MessageBox.Show(this, _loc.Get("msg.profile_created"), _loc.Get("msg.done"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshStatus(force: true);
        }
        catch (Exception ex)
        {
            _log.Error($"Create profile failed: {ex}");
            MessageBox.Show(this, ex.Message, _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnDeleteProfile_Click(object? sender, EventArgs e)
    {
        if (_operationInProgress)
            return;

        var config = _configService.Load();
        var profile = GetSelectedProfile(config);
        if (profile is null)
        {
            MessageBox.Show(this, _loc.Get("msg.need_selection"), _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            _loc.Get("msg.confirm_delete", profile.Name),
            _loc.Get("btn.delete_profile"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
            return;

        var deleteDirs = MessageBox.Show(
            this,
            _loc.Get("msg.confirm_delete_files"),
            _loc.Get("btn.delete_profile"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        try
        {
            _profileService.DeleteProfile(config, profile.Id, deleteDirectories: deleteDirs);
            if (string.Equals(_selectedProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
                _selectedProfileId = null;
            AppendLog($"Deleted profile '{profile.Name}'.");
            RefreshStatus(force: true);
        }
        catch (Exception ex)
        {
            _log.Error($"Delete profile failed: {ex}");
            MessageBox.Show(this, ex.Message, _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnRestoreBackup_Click(object? sender, EventArgs e)
    {
        if (!_backup.BackupExists())
        {
            MessageBox.Show(this, _loc.Get("msg.backup_missing"), _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            _loc.Get("msg.confirm_restore"),
            _loc.Get("btn.restore_backup"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
            return;

        await RunOperationAsync(
            _loc.Get("op.backup"),
            ct => Task.Run(() => _backup.RestoreBackup(CreateProgress()), ct));
    }

    private void menuPresets_Click(object? sender, EventArgs e)
    {
        using var form = new PresetsForm(_paths, _configService, _loc);
        form.ShowDialog(this);
        RefreshStatus(force: true);
    }

    private void menuAbout_Click(object? sender, EventArgs e)
    {
        MessageBox.Show(
            this,
            _loc.Get("about.text"),
            _loc.Get("menu.about"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task<bool> EnsureQBittorrentClosedAsync()
    {
        if (!_processService.IsRunning(out _))
            return true;

        var answer = MessageBox.Show(
            this,
            _loc.Get("msg.qbt_must_close"),
            _loc.Get("msg.qbt_close_title"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
            return false;

        return await CloseQBittorrentAsync();
    }

    private async Task<bool> CloseQBittorrentAsync()
    {
        btnCloseQbt.Enabled = false;
        lblProgress.Text = _loc.Get("btn.close_qbt") + "...";

        try
        {
            var closed = await _processService.TryCloseGracefullyAsync(TimeSpan.FromSeconds(15));
            if (closed)
            {
                AppendLog("qBittorrent closed.");
                return true;
            }

            var force = MessageBox.Show(
                this,
                _loc.Get("msg.qbt_force"),
                _loc.Get("msg.qbt_close_title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (force != DialogResult.Yes)
                return false;

            _processService.ForceKill();
            await Task.Delay(1000);

            if (_processService.IsRunning(out _))
            {
                MessageBox.Show(this, _loc.Get("msg.error"), _loc.Get("msg.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            AppendLog("qBittorrent force-killed.");
            return true;
        }
        finally
        {
            lblProgress.Text = string.Empty;
            RefreshStatus(force: true);
        }
    }

    private async Task RunOperationAsync(string title, Func<CancellationToken, Task> operation)
    {
        if (_operationInProgress)
        {
            MessageBox.Show(this, _loc.Get("msg.operation_in_progress"), _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!await EnsureQBittorrentClosedAsync())
            return;

        _operationInProgress = true;
        UpdateButtonStates(false, true);
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        lblProgress.Text = title;
        AppendLog(title + "...");

        try
        {
            using var cts = new CancellationTokenSource();
            await operation(cts.Token);
            AppendLog(_loc.Get("msg.done") + ": " + title);
            progressBar.Value = progressBar.Maximum;
            MessageBox.Show(this, title, _loc.Get("msg.done"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Cancelled: " + title);
        }
        catch (Exception ex)
        {
            _log.Error($"{title}: {ex}");
            AppendLog(_loc.Get("msg.error") + ": " + ex.Message);
            MessageBox.Show(this, ex.Message, _loc.Get("msg.error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationInProgress = false;
            progressBar.Value = 0;
            lblProgress.Text = string.Empty;
            RefreshStatus(force: true);
        }
    }

    private IProgress<CopyProgress> CreateProgress() =>
        new Progress<CopyProgress>(p =>
        {
            void Apply()
            {
                if (IsDisposed)
                    return;

                if (p.TotalItems > 0)
                {
                    progressBar.Maximum = Math.Max(1, p.TotalItems);
                    progressBar.Value = Math.Min(Math.Max(0, p.CompletedItems), progressBar.Maximum);
                }

                lblProgress.Text = string.IsNullOrWhiteSpace(p.Phase)
                    ? $"{p.CompletedItems}/{p.TotalItems} — {p.CurrentItem}"
                    : $"{p.Phase}: {p.CompletedItems}/{p.TotalItems} — {p.CurrentItem}";
            }

            if (InvokeRequired)
                BeginInvoke(Apply);
            else
                Apply();
        });

    private string? PromptProfileName()
    {
        using var form = new Form
        {
            Text = _loc.Get("msg.new_profile_title"),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(380, 120),
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            Font = Font
        };

        var lbl = new Label
        {
            Text = _loc.Get("msg.enter_name"),
            Location = new Point(12, 14),
            AutoSize = true
        };
        var txt = new TextBox
        {
            Location = new Point(12, 40),
            Width = 356
        };
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(212, 78),
            Width = 75
        };
        var cancel = new Button
        {
            Text = _loc.Get("wizard.cancel"),
            DialogResult = DialogResult.Cancel,
            Location = new Point(293, 78),
            Width = 75
        };

        form.Controls.Add(lbl);
        form.Controls.Add(txt);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
            return;

        void Write()
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        if (InvokeRequired)
            BeginInvoke(Write);
        else
            Write();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();
        base.OnFormClosed(e);
    }
}
