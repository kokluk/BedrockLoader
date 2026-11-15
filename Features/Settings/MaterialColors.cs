using System.Drawing.Drawing2D;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly (string Id, string English, string Turkish, Color Color)[] MaterialColors =
    {
        ("Lavender", "Lavender", "Lavanta", Color.FromArgb(190, 170, 255)),
        ("Rose", "Rose", "Gül", Color.FromArgb(246, 177, 200)),
        ("Peach", "Peach", "Şeftali", Color.FromArgb(255, 190, 160)),
        ("Sand", "Sand", "Kum", Color.FromArgb(231, 208, 157)),
        ("Sage", "Sage", "Adaçayı", Color.FromArgb(185, 210, 163)),
        ("Mint", "Mint", "Nane", Color.FromArgb(157, 221, 196)),
        ("Sky", "Sky", "Gökyüzü", Color.FromArgb(162, 205, 247)),
        ("Slate", "Slate", "Taş", Color.FromArgb(190, 199, 218))
    };
    static readonly (string Id, string English, string Turkish, Color Color)[] BasicColors =
    [
        ("Red", "Red", "Kırmızı", Color.FromArgb(255, 91, 107)),
        ("Blue", "Blue", "Mavi", Color.FromArgb(90, 157, 255)),
        ("Yellow", "Yellow", "Sarı", Color.FromArgb(249, 209, 80)),
        ("Green", "Green", "Yeşil", Color.FromArgb(79, 205, 145))
    ];

    static bool IsAccentColor(string id) => MaterialColors.Concat(BasicColors).Any(color => color.Id == id);
    static bool IsAmoledColor(string id) => id is "Lavender" or "Rose" || BasicColors.Any(color => color.Id == id);
    static int AmoledColorCount => BasicColors.Length + 2;

    static bool UsesAccentPalette(string? theme) => theme is "AMOLED" or "Material You";

    Color MaterialAccent()
    {
        var palette = settings.Theme == "AMOLED" ? MaterialColors.Concat(BasicColors).Where(color => IsAmoledColor(color.Id)) : MaterialColors;
        var color = palette.FirstOrDefault(choice => choice.Id == settings.MaterialYouColor).Color;
        return color.IsEmpty ? MaterialColors[0].Color : color;
    }

    FlowLayoutPanel BuildMaterialColorChoices(Action? preview = null)
    {
        var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 2, 0, 6) };
        var tips = new ToolTip();
        choices.Disposed += (_, _) => tips.Dispose();
        foreach (var color in MaterialColors.Concat(BasicColors))
        {
            string name = T(color.English, color.Turkish);
            var button = new MaterialColorButton
            {
                SwatchColor = color.Color, IsSelected = color.Id == settings.MaterialYouColor,
                Width = 44, Height = 40, CornerRadius = 20, Margin = new Padding(0, 0, 6, 6),
                AccessibleName = name, Cursor = Cursors.Hand, Tag = color.Id
            };
            ApplyButtonPalette(button);
            tips.SetToolTip(button, name);
            button.Click += (_, _) =>
            {
                settings.MaterialYouColor = color.Id;
                foreach (MaterialColorButton swatch in choices.Controls)
                    swatch.IsSelected = Equals(swatch.Tag, button.Tag);
                if (preview != null) preview();
                else
                {
                    SaveSettings(); ApplyTheme();
                    UpdateStatus(T($"{settings.Theme} color: {name}", $"{settings.Theme} rengi: {name}"));
                }
            };
            choices.Controls.Add(button);
        }
        return choices;
    }

    void RefreshBasicColors(FlowLayoutPanel palette)
    {
        foreach (Control swatch in palette.Controls)
        {
            if (swatch.Tag is string id)
                swatch.Visible = settings.Theme == "AMOLED" ? IsAmoledColor(id) : MaterialColors.Any(color => color.Id == id);
            ((MaterialColorButton)swatch).IsSelected = ((MaterialColorButton)swatch).SwatchColor == MaterialAccent();
        }
    }

    sealed class MaterialColorButton : AnimatedRoundButton
    {
        public Color SwatchColor { get; init; }
        bool selected;
        public bool IsSelected { get => selected; set { selected = value; AccessibleDescription = value ? "✓" : ""; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!IsSelected) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            SoftIcons.Draw(e.Graphics, "check-mark", new RectangleF((Width - 20) / 2f, (Height - 20) / 2f, 20, 20), ForeColor);
        }
    }
}
