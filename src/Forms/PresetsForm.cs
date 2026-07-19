using QBittorrentProfileManager.Models;
using QBittorrentProfileManager.Services;

namespace QBittorrentProfileManager.Forms;

/// <summary>
/// Separate menu for applying recommended qBittorrent.ini presets.
/// Not part of the first-run wizard.
/// </summary>
public sealed class PresetsForm : Form
{
    private readonly AppPaths _paths;
    private readonly ConfigService _configService;
    private readonly LocalizationService _loc;
    private readonly IniPresetService _iniPresets = new();
    private readonly QBittorrentProcessService _processService = new();

    private readonly RadioButton _rbLive = new();
    private readonly RadioButton _rbProfile = new();
    private readonly ComboBox _cmbProfile = new();
    private readonly ComboBox _cmbConnection = new();
    private readonly ComboBox _cmbRole = new();
    private readonly Label _lblNote = new();
    private readonly Button _btnApply = new();
    private readonly Button _btnCancel = new();

    private bool _warningHandled;

    public PresetsForm(AppPaths paths, ConfigService configService, LocalizationService loc)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        InitializeComponent();
        PopulateControls();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = _loc.Get("presets.title");
        ClientSize = new Size(480, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(248, 249, 252);
        UiAssets.ApplyFormIcon(this);
        Padding = new Padding(12);

        var lblTarget = new Label
        {
            AutoSize = true,
            Location = new Point(16, 16),
            Text = _loc.Get("presets.target")
        };

        _rbLive.AutoSize = true;
        _rbLive.Location = new Point(20, 44);
        _rbLive.Text = _loc.Get("presets.target_live");
        _rbLive.Checked = true;
        _rbLive.CheckedChanged += (_, _) => UpdateTargetEnabled();

        _rbProfile.AutoSize = true;
        _rbProfile.Location = new Point(20, 72);
        _rbProfile.Text = _loc.Get("presets.target_profile");
        _rbProfile.CheckedChanged += (_, _) => UpdateTargetEnabled();

        _cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbProfile.Location = new Point(48, 100);
        _cmbProfile.Size = new Size(400, 28);
        _cmbProfile.DisplayMember = nameof(ProfileInfo.Name);
        _cmbProfile.ValueMember = nameof(ProfileInfo.Id);
        _cmbProfile.Enabled = false;

        var lblConnection = new Label
        {
            AutoSize = true,
            Location = new Point(16, 144),
            Text = _loc.Get("presets.connection")
        };

        _cmbConnection.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbConnection.Location = new Point(20, 168);
        _cmbConnection.Size = new Size(428, 28);

        var lblRole = new Label
        {
            AutoSize = true,
            Location = new Point(16, 208),
            Text = _loc.Get("presets.role")
        };

        _cmbRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbRole.Location = new Point(20, 232);
        _cmbRole.Size = new Size(428, 28);

        _lblNote.Location = new Point(16, 272);
        _lblNote.Size = new Size(448, 36);
        _lblNote.ForeColor = SystemColors.GrayText;
        _lblNote.Text = _loc.Get("presets.note");

        _btnApply.Text = _loc.Get("common.apply");
        _btnApply.Location = new Point(252, 316);
        _btnApply.Size = new Size(100, 30);
        _btnApply.Click += async (_, _) => await ApplyAsync();

        _btnCancel.Text = _loc.Get("common.cancel");
        _btnCancel.Location = new Point(364, 316);
        _btnCancel.Size = new Size(100, 30);
        _btnCancel.DialogResult = DialogResult.Cancel;

        AcceptButton = _btnApply;
        CancelButton = _btnCancel;

        Controls.Add(lblTarget);
        Controls.Add(_rbLive);
        Controls.Add(_rbProfile);
        Controls.Add(_cmbProfile);
        Controls.Add(lblConnection);
        Controls.Add(_cmbConnection);
        Controls.Add(lblRole);
        Controls.Add(_cmbRole);
        Controls.Add(_lblNote);
        Controls.Add(_btnApply);
        Controls.Add(_btnCancel);

        Shown += PresetsForm_Shown;

        ResumeLayout(performLayout: true);
    }

    private void PopulateControls()
    {
        var config = _configService.Load();

        _cmbProfile.Items.Clear();
        foreach (var profile in config.Profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            _cmbProfile.Items.Add(profile);

        if (_cmbProfile.Items.Count > 0)
        {
            var activeIndex = -1;
            if (!string.IsNullOrWhiteSpace(config.ActiveProfileId))
            {
                for (var i = 0; i < _cmbProfile.Items.Count; i++)
                {
                    if (_cmbProfile.Items[i] is ProfileInfo p
                        && string.Equals(p.Id, config.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
                    {
                        activeIndex = i;
                        break;
                    }
                }
            }

            _cmbProfile.SelectedIndex = activeIndex >= 0 ? activeIndex : 0;
        }
        else
        {
            _rbProfile.Enabled = false;
            _cmbProfile.Enabled = false;
        }

        _cmbConnection.Items.Clear();
        foreach (var key in new[] { "slow", "average", "fast", "gigabit" })
            _cmbConnection.Items.Add(new LabeledValue(key, _loc.Get($"wizard.conn.{key}")));

        SelectByValue(_cmbConnection, string.IsNullOrWhiteSpace(config.ConnectionClass)
            ? "average"
            : config.ConnectionClass);

        _cmbRole.Items.Clear();
        foreach (var key in new[] { "general", "media", "software", "seed" })
            _cmbRole.Items.Add(new LabeledValue(key, _loc.Get($"presets.role.{key}")));

        var defaultRole = "general";
        if (!string.IsNullOrWhiteSpace(config.ActiveProfileId))
        {
            var active = config.Profiles.FirstOrDefault(p =>
                string.Equals(p.Id, config.ActiveProfileId, StringComparison.OrdinalIgnoreCase));
            if (active is not null && !string.IsNullOrWhiteSpace(active.Role)
                && active.Role is "general" or "media" or "software" or "seed")
            {
                defaultRole = active.Role;
            }
        }

        SelectByValue(_cmbRole, defaultRole);
        UpdateTargetEnabled();
    }

    private void PresetsForm_Shown(object? sender, EventArgs e)
    {
        if (_warningHandled)
            return;

        _warningHandled = true;

        var result = MessageBox.Show(
            this,
            _loc.Get("presets.warning"),
            _loc.Get("presets.warning_title"),
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (result != DialogResult.OK)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void UpdateTargetEnabled()
    {
        _cmbProfile.Enabled = _rbProfile.Checked && _cmbProfile.Items.Count > 0;
    }

    private async Task ApplyAsync()
    {
        _btnApply.Enabled = false;
        try
        {
            var config = _configService.Load();
            var iniPath = ResolveIniPath(config);
            if (iniPath is null)
                return;

            if (!File.Exists(iniPath))
            {
                MessageBox.Show(
                    this,
                    _loc.Get("presets.no_ini"),
                    _loc.Get("presets.warning_title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (!await EnsureQBittorrentClosedAsync())
                return;

            var connectionClass = GetSelectedValue(_cmbConnection) ?? "average";
            var role = GetSelectedValue(_cmbRole) ?? "general";

            // ApplyPreset backs up the existing INI (.bak-yyyyMMdd-HHmmss) before writing.
            var backupPath = _iniPresets.ApplyPreset(iniPath, connectionClass, role);

            var msg = _loc.Get("presets.applied");
            if (!string.IsNullOrEmpty(backupPath))
                msg += Environment.NewLine + _loc.Get("presets.backup_ini") + Environment.NewLine + backupPath;

            MessageBox.Show(
                this,
                msg,
                _loc.Get("presets.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                _loc.Get("common.error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
                _btnApply.Enabled = true;
        }
    }

    private string? ResolveIniPath(AppConfig config)
    {
        if (_rbLive.Checked)
            return Path.Combine(_paths.LiveRoaming, "qBittorrent.ini");

        if (_cmbProfile.SelectedItem is not ProfileInfo profile
            || string.IsNullOrWhiteSpace(profile.Id))
        {
            MessageBox.Show(
                this,
                _loc.Get("presets.no_profile"),
                _loc.Get("presets.warning_title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return null;
        }

        return Path.Combine(_paths.ProfileRoaming(config, profile.Id), "qBittorrent.ini");
    }

    private async Task<bool> EnsureQBittorrentClosedAsync()
    {
        if (!_processService.IsRunning(out _))
            return true;

        var answer = MessageBox.Show(
            this,
            _loc.Get("msg.qbt_must_close"),
            _loc.Get("presets.warning_title"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
            return false;

        UseWaitCursor = true;
        try
        {
            var closed = await _processService.TryCloseGracefullyAsync(TimeSpan.FromSeconds(12));
            if (!closed)
            {
                _processService.ForceKill();
                closed = !_processService.IsRunning(out _);
            }

            if (!closed)
            {
                MessageBox.Show(
                    this,
                    _loc.Get("msg.qbt_must_close"),
                    _loc.Get("common.error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            return true;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static void SelectByValue(ComboBox combo, string value)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is LabeledValue item
                && string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string? GetSelectedValue(ComboBox combo) =>
        combo.SelectedItem is LabeledValue item ? item.Value : null;

    private sealed class LabeledValue
    {
        public LabeledValue(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }
}
