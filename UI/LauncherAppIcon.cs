using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly string[] AppIconSymbols = ["wolf", "Addons", "game", "status"];
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
            ShowInTaskbar = false, ClientSize = new Size(500, 266), BackColor = Surface, ForeColor = TextColor
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 8, 16, 4) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        header.Controls.Add(new AboutLogo { Size = new Size(36, 36), ForeColor = Accent });
        header.Controls.Add(WrappedText(title, 15, true));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(BuildAppIconEditor(out var saveIcon, out _), 0, 1);
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
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        saveIcon();
        SaveSettings();
        RefreshAppIcon();
    }

    void RefreshAppIcon()
    {
        string symbol = AppIconSymbols.Contains(settings.AppIconSymbol) ? settings.AppIconSymbol : "wolf";
        Color color = settings.AppIconColor is int rgb ? Color.FromArgb(rgb | unchecked((int)0xff000000)) : Accent;
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
        var profile = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
            Margin = Padding.Empty, Tag = "settings-card" };
        profile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        profile.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        foreach (int height in new[] { 25, 42, 25, 42 })
            profile.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var iconShape = MakeThemeCombo(220);
        iconShape.Items.AddRange([T("Wolf", "Kurt"), T("Box", "Kutu"), T("Controller", "Oyun kumandası"),
            T("Check mark", "Onay işareti")]);
        iconShape.SelectedIndex = Math.Max(0, Array.IndexOf(AppIconSymbols, settings.AppIconSymbol));
        iconShape.AccessibleName = T("Taskbar icon", "Görev çubuğu simgesi");
        iconShape.Dock = DockStyle.Fill;
        profile.Controls.Add(WrappedText(iconShape.AccessibleName, 10, true), 0, 0);
        profile.Controls.Add(iconShape, 0, 1);
        profile.Controls.Add(WrappedText(T("Icon color", "Simge rengi"), 10, true), 0, 2);
        var iconColorRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
            Margin = Padding.Empty, Tag = "settings-card" };
        profile.Controls.Add(iconColorRow, 0, 3);
        int? customIconColor = settings.AppIconColor;
        var iconPreview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.CenterImage };
        profile.Controls.Add(iconPreview, 1, 1);
        profile.SetRowSpan(iconPreview, 3);
        iconPreview.Disposed += (_, _) => iconPreview.Image?.Dispose();
        var themeColor = MakeButton(T("Theme color", "Tema rengi"), () => { }, 112);
        var customColor = MakeButton(T("Choose color", "Renk seç"), () => { }, 112);
        themeColor.Click += (_, _) => { customIconColor = null; RefreshIconPreview(); };
        customColor.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = PreviewIconColor(), FullOpen = true };
            if (picker.ShowDialog(profile.FindForm()) != DialogResult.OK) return;
            customIconColor = picker.Color.ToArgb();
            RefreshIconPreview();
        };
        iconColorRow.Controls.AddRange([themeColor, customColor]);
        iconShape.SelectedIndexChanged += (_, _) => RefreshIconPreview();
        save = () =>
        {
            settings.AppIconSymbol = AppIconSymbols[iconShape.SelectedIndex];
            settings.AppIconColor = customIconColor;
        };
        refresh = RefreshIconPreview;
        refresh();
        return profile;

        Color PreviewIconColor() => customIconColor is int rgb
            ? Color.FromArgb(rgb | unchecked((int)0xff000000)) : Accent;

        void RefreshIconPreview()
        {
            var previous = iconPreview.Image;
            iconPreview.Image = RenderAppIcon(AppIconSymbols[iconShape.SelectedIndex], PreviewIconColor());
            previous?.Dispose();
            themeColor.Tag = customIconColor is null ? "primary" : null;
            customColor.Tag = customIconColor is null ? null : "primary";
            ApplyButtonPalette(themeColor);
            ApplyButtonPalette(customColor);
        }

    }
}
