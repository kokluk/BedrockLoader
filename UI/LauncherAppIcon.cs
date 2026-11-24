using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly string[] AppIconSymbols = ["wolf", "game"];
    Icon? generatedAppIcon;
    (string Symbol, int Color) appliedAppIcon;

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(nint icon);

    void ShowLauncherIconSettings(Form owner)
    {
        string title = T("Launcher Settings", "Launcher Ayarları");
        using var dialog = new RoundedPopupForm(Accent)
        {
            Text = title, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false, ClientSize = new Size(500, 312), BackColor = Surface, ForeColor = TextColor
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 8, 16, 4) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        header.Controls.Add(new AboutLogo { Size = new Size(36, 36), ForeColor = Accent });
        header.Controls.Add(WrappedText(title, 15, true));
        layout.Controls.Add(header, 0, 0);
        var editor = BuildAppIconEditor(out var saveIcon, out _);
        var shape = MakeThemeCombo(220);
        shape.Dock = DockStyle.Fill;
        shape.AccessibleName = T("Button shape", "Düğme biçimi");
        shape.Items.AddRange([T("Round", "Yuvarlak"), T("Square", "Kare")]);
        bool originalShape = LauncherAppearance.SquareButtons;
        shape.SelectedIndex = originalShape ? 1 : 0;
        void PreviewShape(bool square)
        {
            if (LauncherAppearance.SquareButtons == square) return;
            LauncherAppearance.SquareButtons = square;
            Refresh();
            owner.Refresh();
            dialog.Refresh();
        }
        shape.SelectedIndexChanged += (_, _) => PreviewShape(shape.SelectedIndex == 1);
        editor.Controls.Add(WrappedText(shape.AccessibleName, 10, true), 0, 3);
        editor.Controls.Add(shape, 0, 4);
        layout.Controls.Add(editor, 0, 1);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        var save = MakeButton(T("Save", "Kaydet"), () => dialog.DialogResult = DialogResult.OK, 116, "check-mark");
        save.Tag = "primary";
        var cancel = MakeButton(T("Cancel", "Vazgeç"), () => dialog.DialogResult = DialogResult.Cancel, 116);
        footer.Controls.AddRange([save, cancel]);
        layout.Controls.Add(footer, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        RecolorTree(dialog);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            PreviewShape(originalShape);
            return;
        }
        saveIcon();
        settings.SquareButtons = LauncherAppearance.SquareButtons;
        SaveSettings();
        RefreshAppIcon();
    }

    void RefreshAppIcon()
    {
        string symbol = AppIconSymbols.Contains(settings.AppIconSymbol) ? settings.AppIconSymbol : "wolf";
        Color color = settings.AppIconColor == Color.White.ToArgb() ? Color.White : Accent;
        var key = (symbol, color.ToArgb());
        if (generatedAppIcon != null && appliedAppIcon == key) return;

        using var bitmap = RenderAppIcon(symbol, color);
        nint handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            var replacement = (Icon)borrowed.Clone();
            var previous = generatedAppIcon;
            Icon = generatedAppIcon = replacement;
            if (trayIcon != null) trayIcon.Icon = replacement;
            appliedAppIcon = key;
            previous?.Dispose();
        }
        finally { DestroyIcon(handle); }
    }

    static Bitmap RenderAppIcon(string symbol, Color color)
    {
        var bitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var bounds = new Rectangle(4, 4, 56, 56);
        if (symbol != "wolf")
        {
            SoftIcons.Draw(graphics, symbol, bounds, color, true);
            return bitmap;
        }

        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("BedrockLoader.WolfTransparent.png")!;
        using var wolf = Image.FromStream(stream);
        using var tint = new ImageAttributes();
        tint.SetColorMatrix(new ColorMatrix
        {
            Matrix00 = 0, Matrix11 = 0, Matrix22 = 0,
            Matrix40 = color.R / 255f, Matrix41 = color.G / 255f, Matrix42 = color.B / 255f
        });
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(wolf, bounds, 0, 0, wolf.Width, wolf.Height, GraphicsUnit.Pixel, tint);
        return bitmap;
    }

    TableLayoutPanel BuildAppIconEditor(out Action save, out Action refresh)
    {
        var profile = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5,
            Margin = Padding.Empty, Tag = "settings-card" };
        profile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        profile.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        foreach (int height in new[] { 25, 42, 38, 25, 42 })
            profile.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var iconShape = MakeThemeCombo(220);
        iconShape.Items.AddRange([T("Fox", "Tilki"), T("Controller", "Oyun kumandası")]);
        iconShape.SelectedIndex = Math.Max(0, Array.IndexOf(AppIconSymbols, settings.AppIconSymbol));
        iconShape.AccessibleName = T("Taskbar icon", "Görev çubuğu simgesi");
        iconShape.Dock = DockStyle.Fill;
        profile.Controls.Add(WrappedText(iconShape.AccessibleName, 10, true), 0, 0);
        profile.Controls.Add(iconShape, 0, 1);
        var tint = MakeChoice(T("Use theme icon color", "Simge rengi"), settings.AppIconColor != Color.White.ToArgb());
        profile.Controls.Add(tint, 0, 2);
        var iconPreview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.CenterImage };
        profile.Controls.Add(iconPreview, 1, 1);
        profile.SetRowSpan(iconPreview, 3);
        iconPreview.Disposed += (_, _) => iconPreview.Image?.Dispose();
        tint.CheckedChanged += (_,_) => RefreshIconPreview();
        iconShape.SelectedIndexChanged += (_, _) => RefreshIconPreview();
        save = () =>
        {
            settings.AppIconSymbol = AppIconSymbols[iconShape.SelectedIndex];
            settings.AppIconColor = tint.Checked ? null : Color.White.ToArgb();
        };
        refresh = RefreshIconPreview;
        refresh();
        return profile;

        Color PreviewIconColor() => tint.Checked ? Accent : Color.White;

        void RefreshIconPreview()
        {
            var previous = iconPreview.Image;
            iconPreview.Image = RenderAppIcon(AppIconSymbols[iconShape.SelectedIndex], PreviewIconColor());
            previous?.Dispose();
        }

    }
}
