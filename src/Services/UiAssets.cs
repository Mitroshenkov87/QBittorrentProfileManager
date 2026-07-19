using System.Drawing.Drawing2D;

namespace QBittorrentProfileManager.Services;

/// <summary>Loads app graphics (icon, header, toolbar images) from Assets next to the exe.</summary>
public static class UiAssets
{
    private static string? _assetsDir;
    private static Icon? _appIcon;
    private static Image? _header;
    private static readonly Dictionary<string, Image> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string AssetsDir
    {
        get
        {
            if (_assetsDir is not null)
                return _assetsDir;

            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "Assets"),
                Path.Combine(baseDir, "..", "Assets"),
                Path.Combine(baseDir, "..", "..", "..", "Assets"),
            };

            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (Directory.Exists(full))
                {
                    _assetsDir = full;
                    return _assetsDir;
                }
            }

            _assetsDir = Path.Combine(baseDir, "Assets");
            return _assetsDir;
        }
    }

    public static Icon? AppIcon
    {
        get
        {
            if (_appIcon is not null)
                return _appIcon;

            var ico = Path.Combine(AssetsDir, "app.ico");
            if (File.Exists(ico))
            {
                // Clone so file is not locked.
                using var tmp = new Icon(ico);
                _appIcon = (Icon)tmp.Clone();
                return _appIcon;
            }

            var png = Path.Combine(AssetsDir, "app-icon-32.png");
            if (File.Exists(png))
            {
                using var bmp = new Bitmap(png);
                _appIcon = Icon.FromHandle(bmp.GetHicon());
                return _appIcon;
            }

            return null;
        }
    }

    public static Image? HeaderImage
    {
        get
        {
            if (_header is not null)
                return _header;

            var path = Path.Combine(AssetsDir, "header.png");
            if (!File.Exists(path))
                return null;

            _header = Image.FromFile(path);
            return _header;
        }
    }

    public static Image? GetUi(string name)
    {
        if (Cache.TryGetValue(name, out var img))
            return img;

        var path = Path.Combine(AssetsDir, "ui", name + ".png");
        if (!File.Exists(path))
            return null;

        img = Image.FromFile(path);
        Cache[name] = img;
        return img;
    }

    public static ImageList CreateRoleImageList()
    {
        var list = new ImageList
        {
            ImageSize = new Size(16, 16),
            ColorDepth = ColorDepth.Depth32Bit
        };

        list.Images.Add("general", DrawRoleIcon(Color.FromArgb(70, 130, 180)));
        list.Images.Add("media", DrawRoleIcon(Color.FromArgb(155, 89, 182)));
        list.Images.Add("software", DrawRoleIcon(Color.FromArgb(39, 174, 96)));
        list.Images.Add("seed", DrawRoleIcon(Color.FromArgb(241, 196, 15)));
        list.Images.Add("custom", DrawRoleIcon(Color.FromArgb(127, 140, 141)));
        list.Images.Add("active", DrawRoleIcon(Color.FromArgb(46, 204, 113), check: true));
        return list;
    }

    private static Bitmap DrawRoleIcon(Color color, bool check = false)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using (var brush = new SolidBrush(color))
            g.FillEllipse(brush, 1, 1, 13, 13);
        using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1f))
            g.DrawEllipse(pen, 1, 1, 13, 13);

        if (check)
        {
            using var pen = new Pen(Color.White, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLines(pen, new[]
            {
                new Point(4, 8),
                new Point(7, 11),
                new Point(12, 5)
            });
        }

        return bmp;
    }

    public static void ApplyButtonIcon(Button button, string uiName)
    {
        var img = GetUi(uiName);
        if (img is null)
            return;

        button.Image = img;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Padding = new Padding(6, 0, 6, 0);
    }

    public static void ApplyFormIcon(Form form)
    {
        try
        {
            if (AppIcon is not null)
                form.Icon = AppIcon;
        }
        catch
        {
            // ignore icon failures
        }
    }
}
