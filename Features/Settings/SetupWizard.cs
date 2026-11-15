using System.Text;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static readonly string[] ThemeChoices = ["Midnight", "Purple", "Green", "Yellow", "Red", "AMOLED", "Material You"];

    static string NormalizeLocalName(string? name) => string.Concat((name ?? "").EnumerateRunes()
        .Where(rune => !Rune.IsControl(rune) && Rune.GetUnicodeCategory(rune) != System.Globalization.UnicodeCategory.Format)
        .Take(24).Select(rune => rune.ToString())).Trim();

    SoftSelector MakeAppearanceSelector()
    {
        var selector = MakeThemeCombo(220);
        selector.Items.AddRange([T("Windows theme", "Windows teması"), T("Light", "Aydınlık"), T("Dark", "Karanlık")]);
        selector.SelectedIndex = settings.FollowWindowsTheme ? 0 : settings.LightMode ? 1 : 2;
        return selector;
    }

    bool EffectiveLightMode() => settings.Theme != "AMOLED" &&
        (settings.FollowWindowsTheme ? WindowsUsesLightTheme() : settings.LightMode);

    void SaveAppearanceChoice(int index)
    {
        settings.FollowWindowsTheme = index == 0;
        settings.LightMode = index == 1;
        lightMode = EffectiveLightMode();
    }

    PopupToggle MakeShineToggle() => new(Accent, SurfaceAlt)
    {
        Text = T("Button glow", "Düğme parlaması"),
        Checked = settings.ShinyHomeIcons, Dock = DockStyle.Fill, Cursor = Cursors.Hand
    };

    PopupToggle MakeAnimationToggle(bool advanced) => new(Accent, SurfaceAlt)
    {
        Text = advanced ? T("Advanced animations", "Gelişmiş animasyonlar") : T("Interface animations", "Arayüz animasyonları"),
        Checked = advanced ? settings.AdvancedAnimationsEnabled : settings.AnimationsEnabled,
        Dock = DockStyle.Fill, Cursor = Cursors.Hand
    };

    void ShowSetupWizard() => ShowSetupWizard(beforeLauncher: false);

    void ShowSetupWizard(bool beforeLauncher)
    {
        Rectangle launcherBounds = beforeLauncher ? Bounds : RectangleToScreen(ClientRectangle);
        var customizationChoices = MakeCustomizationChoices(out var applyCustomization, previewMotion: true, styled: true);
        using var popup = new RoundedPopupForm(Accent)
        {
            Text = "BedrockLoader", StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = beforeLauncher,
            CornerRadius = 16, Bounds = launcherBounds, AnimateEntrance = false,
            TrackPopupProximity = shell => TrackProximity(shell, () => customizationChoices[0].Checked),
            BackColor = Surface, ForeColor = TextColor
        };
        var original = (settings.Theme, settings.MaterialYouColor, settings.FollowWindowsTheme, settings.LightMode, lightMode);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, popup.ClientSize.Width < 850 ? 172 : 220));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        popup.Controls.Add(shell);
        var brand = WrappedText("BedrockLoader\n" + T("Setup wizard", "Kurulum Sihirbazı"), 13, true);
        brand.Dock = DockStyle.Fill;
        shell.Controls.Add(brand, 0, 0);
        var wolf = new AboutLogo { Size = new Size(54, 54), Anchor = AnchorStyles.Top | AnchorStyles.Right,
            ForeColor = Accent, Margin = new Padding(0, 0, 4, 0) };
        shell.Controls.Add(wolf, 1, 0);
        string[] steps = [T("Welcome", "Hoş geldin"), T("Profile", "Profil"), T("Appearance", "Görünüm"),
            T("Customization", "Özelleştirme"), "Discord Rich Presence", T("Review", "Son kontrol")];
        string[] stepIcons = ["Home", "Skins", "display", "sliders", "discord", "check-mark"];
        int page = 0;
        var rail = new RoundedSettingsList(this) { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = steps.Length + 1,
            Padding = new Padding(8), Tag = "settings-card", Margin = new Padding(0, 0, 10, 0) };
        rail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var stepButtons = new AnimatedRoundButton[steps.Length];
        for (int index = 0; index < steps.Length; index++)
        {
            rail.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var step = MakeButton($"{index + 1}.  {steps[index]}", () => { });
            step.Dock = DockStyle.Fill;
            step.Margin = new Padding(1, 2, 1, 2);
            step.TextAlign = ContentAlignment.MiddleLeft;
            step.Padding = new Padding(8, 0, 4, 0);
            step.Font = LauncherFont.Create(9, FontStyle.Bold);
            stepButtons[index] = step;
            rail.Controls.Add(step, 0, index);
        }
        rail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(rail, 0, 1);
        var layout = new RoundedSettingsList(this) { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9,
            Tag = "settings-card", Padding = new Padding(18, 14, 18, 8), Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int height in new[] { 40, 62, 48, 48, 48, 132, 48, 44 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(layout, 1, 1);
        var heading = WrappedText("", 17, true);
        var body = WrappedText("", 11);
        heading.Dock = body.Dock = DockStyle.Fill;
        var accent = MakeThemeCombo(220);
        accent.Items.AddRange(ThemeChoices);
        accent.SelectedItem = settings.Theme;
        var appearance = MakeAppearanceSelector();
        var amoledAppearance = WrappedText(T("Dark · AMOLED", "Karanlık · AMOLED"), 10);
        amoledAppearance.Dock = DockStyle.Fill;
        amoledAppearance.Enabled = false;
        var customization = BuildChoiceGrid(customizationChoices, 1);
        customization.Tag = "settings-card";
        var name = MakeTextBox(settings.LocalName);
        name.MaxLength = 48;
        name.AccessibleName = name.PlaceholderText = T("Local name (optional)", "Yerel ad (isteğe bağlı)");
        var nameBox = WrapPopupInput(name);
        var profile = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, Tag = "settings-card" };
        profile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        profile.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        profile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nameBox.Dock = DockStyle.Fill;
        profile.Controls.Add(nameBox, 0, 0);
        var iconEditor = BuildAppIconEditor(out var saveIcon, out var refreshIcon);
        profile.Controls.Add(iconEditor, 0, 1);
        var discordChoices = MakeDiscordChoices(out var readDiscordChoices);
        var rpc = BuildChoiceGrid(discordChoices, 1);
        rpc.Tag = "settings-card";
        ApplyButtonPalette(accent);
        ApplyButtonPalette(appearance);
        var titleBar = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "settings-card" };
        var titleGlyphs = stepIcons.Select(icon => new HeadingGlyph(this, icon)
            { Dock = DockStyle.Left, Width = 32, Visible = false }).ToArray();
        titleBar.Controls.Add(heading);
        titleBar.Controls.AddRange(titleGlyphs);
        layout.Controls.Add(titleBar, 0, 0);
        layout.Controls.Add(body, 0, 1);
        layout.Controls.Add(accent, 0, 2);
        var appearanceHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "settings-card" };
        appearance.Dock = DockStyle.Top;
        appearanceHost.Controls.AddRange([appearance, amoledAppearance]);
        layout.Controls.Add(appearanceHost, 0, 3);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        var materials = BuildMaterialColorChoices(Preview);
        materials.Tag = "settings-card";
        layout.ClientSizeChanged += (_, _) => FitPalette();
        layout.Controls.Add(materials, 0, 4);
        layout.Controls.Add(customization, 0, 5);
        layout.Controls.Add(profile, 0, 6);
        layout.Controls.Add(rpc, 0, 7);
        var welcome = BuildWizardWelcome();
        var review = WrappedText("", 11);
        review.Dock = DockStyle.Fill;
        layout.Controls.Add(welcome, 0, 8);
        var bottom = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        shell.Controls.Add(bottom, 0, 2);
        shell.SetColumnSpan(bottom, 2);
        footer.Dock = DockStyle.Right;
        footer.Width = 255;
        bottom.Controls.Add(footer);
        var next = MakeButton("", () => { }, 126);
        var back = MakeButton(T("Back", "Geri"), () => { }, 105);
        var skip = MakeButton(T("Skip", "Atla"), () => popup.DialogResult = DialogResult.Cancel, 105);
        skip.Dock = DockStyle.Left;
        bottom.Controls.Add(skip);
        next.Tag = "primary";
        ApplyButtonPalette(next);
        back.Click += (_, _) => { page--; Render(); };
        next.Click += (_, _) =>
        {
            if (page < steps.Length - 1) { page++; Render(); }
            else popup.DialogResult = DialogResult.OK;
        };
        footer.Controls.AddRange([next, back]);
        popup.AcceptButton = next;
        popup.CancelButton = skip;
        accent.SelectedIndexChanged += (_, _) => { Preview(); Render(); };
        appearance.SelectedIndexChanged += (_, _) => Preview();
        for (int index = 0; index < stepButtons.Length; index++)
        {
            int target = index;
            stepButtons[index].Click += (_, _) => { page = target; Render(); };
        }
        Render();
        refreshIcon();
        var result = beforeLauncher ? popup.ShowDialog() : popup.ShowDialog(this);
        if (result != DialogResult.OK)
        {
            (settings.Theme, settings.MaterialYouColor, settings.FollowWindowsTheme, settings.LightMode, lightMode) = original;
            AnimatedRoundButton.AnimationsEnabled = settings.AnimationsEnabled;
            AnimatedRoundButton.AdvancedAnimationsEnabled = settings.AdvancedAnimationsEnabled;
            ApplyTheme();
            return;
        }
        applyCustomization();
        settings.LocalName = NormalizeLocalName(name.Text);
        saveIcon();
        settings.Discord = readDiscordChoices();
        settings.IntroSeen = true;
        SaveSettings();
        RefreshDiscordOptions();
        BuildShellRefresh();

        void Preview()
        {
            settings.Theme = accent.SelectedItem?.ToString() ?? settings.Theme;
            SaveAppearanceChoice(appearance.SelectedIndex);
            ApplyTheme();
            popup.BackColor = footer.BackColor = Surface;
            popup.BorderAccent = Accent;
            RecolorTree(popup);
            wolf.ForeColor = Accent;
            refreshIcon();
            UpdateStepColors();
            popup.Invalidate(true);
        }

        void Render()
        {
            heading.Text = page switch
            {
                0 => T("Welcome to BedrockLoader", "BedrockLoader’a hoş geldin"),
                1 => steps[1],
                2 => T("Appearance", "Görünüm"),
                3 => T("Customization", "Özelleştirme"),
                4 => "Discord Rich Presence",
                _ => steps[5]
            };
            body.Text = page switch
            {
                0 => T("Set up BedrockLoader in a few steps. You can change these choices later in Settings.",
                    "BedrockLoader’ı birkaç adımda hazırla. Seçimlerini daha sonra Ayarlar’dan değiştirebilirsin."),
                1 => T("Add a name for your Home greeting, or leave it blank.", "Ana ekran selamlaması için bir ad yazabilirsin. Bu alan isteğe bağlı."),
                2 => T("Choose an accent palette and a light or dark appearance.", "Renk paletini ve aydınlık ya da karanlık görünümü seç."),
                3 => T("Choose button effects, sound and window behavior.", "Düğme efektlerini, sesi ve pencere davranışını seç."),
                4 => T("Show your Minecraft activity and local name on Discord.", "Minecraft etkinliğini ve yerel adını Discord’da göster."),
                _ => T("Review your choices before saving.", "Kaydetmeden önce seçimlerini gözden geçir.")
            };
            accent.Visible = page == 2;
            bool amoled = accent.SelectedItem as string == "AMOLED";
            appearance.Visible = page == 2 && !amoled;
            appearance.Enabled = !amoled;
            amoledAppearance.Visible = page == 2 && amoled;
            bool showPalette = page == 2 && UsesAccentPalette(accent.SelectedItem?.ToString());
            materials.Visible = showPalette;
            RefreshBasicColors(materials);
            customization.Visible = page == 3;
            profile.Visible = page == 1;
            rpc.Visible = page == 4;
            for (int row = 2; row <= 7; row++)
                layout.RowStyles[row].Height = row switch
                {
                    2 or 3 when page == 2 => 48,
                    4 when showPalette => PaletteHeight(),
                    5 when page == 3 => Math.Min(44, Math.Max(32, (layout.ClientSize.Height - layout.Padding.Vertical - 102) / customizationChoices.Length))
                        * customizationChoices.Length,
                    6 when page == 1 => 176,
                    7 when page == 4 => 44 * discordChoices.Length,
                    _ => 0
                };
            back.Enabled = page > 0;
            layout.Controls.Remove(page == 5 ? welcome : review);
            if (page == 5)
            {
                review.Text = string.Join(Environment.NewLine + Environment.NewLine,
                    $"{steps[1]} · {(NormalizeLocalName(name.Text) is { Length: > 0 } localName ? localName : T("Optional", "İsteğe bağlı"))}",
                    $"{steps[2]} · {accent.SelectedItem} / {(amoled ? T("Dark", "Karanlık") : appearance.SelectedItem)}",
                    $"Discord Rich Presence · {(readDiscordChoices().Enabled ? T("On", "Açık") : T("Off", "Kapalı"))}");
                layout.Controls.Add(review, 0, 8);
            }
            else if (welcome.Parent == null) layout.Controls.Add(welcome, 0, 8);
            welcome.Visible = page == 0;
            next.Text = page == 5 ? T("Finish", "Bitir") : T("Next", "İleri");
            UpdateStepColors();
        }

        int PaletteHeight()
        {
            int count = accent.SelectedItem as string == "AMOLED" ? AmoledColorCount : MaterialColors.Length;
            int columns = Math.Max(1, (layout.ClientSize.Width - layout.Padding.Horizontal) / 50);
            return materials.Padding.Vertical + 46 * ((count + columns - 1) / columns);
        }

        void FitPalette()
        {
            if (page != 2 || !UsesAccentPalette(accent.SelectedItem as string)) return;
            int height = PaletteHeight();
            if (layout.RowStyles[4].Height != height) layout.RowStyles[4].Height = height;
        }

        void UpdateStepColors()
        {
            for (int index = 0; index < stepButtons.Length; index++)
            {
                var button = stepButtons[index];
                button.BackColor = index == page ? MixColors(SurfaceAlt, Accent, .28) : SurfaceAlt;
                button.ForeColor = TextColor;
                button.AccessibleDescription = index == page ? T("Current step", "Geçerli adım") : null;
                titleGlyphs[index].Visible = index == page;
            }
        }
    }

    TableLayoutPanel BuildWizardWelcome()
    {
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, Tag = "settings-card" };
        for (int i = 0; i < 2; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        }
        Add("game", T("Cinnabar and Minecraft", "Cinnabar ve Minecraft"),
            T("Launch Cinnabar or Minecraft from Home.", "Ana ekrandan Cinnabar’ı veya Minecraft’ı başlat."), 0, 0);
        Add("layers", T("Mods and packs", "Mod ve paket yönetimi"), T("Manage mods, packs and skins.", "Modları, paketleri ve skinleri yönet."), 1, 0);
        Add("backup", T("Backups", "Yedekleme"), T("Back up worlds and settings.", "Dünyalarını ve ayarlarını yedekle."), 0, 1);
        Add("discord", T("Discord integration", "Discord entegrasyonu"), T("Show what you’re playing.", "Oynadığın oyunu Discord’da göster."), 1, 1);
        return cards;

        void Add(string icon, string title, string description, int column, int row)
        {
            var card = new WizardFeatureCard(this, icon, title, description)
            {
                Dock = DockStyle.Fill, CornerRadius = 18, Outline = true, TabStop = false,
                Cursor = Cursors.Default, AccessibleName = title, AccessibleDescription = description,
                AccessibleRole = AccessibleRole.StaticText
            };
            ApplyButtonPalette(card);
            cards.Controls.Add(card, column, row);
        }
    }

    sealed class WizardFeatureCard(MainForm owner, string icon, string title, string description) : AnimatedRoundButton
    {
        readonly Font headingFont = LauncherFont.Create(11, FontStyle.Bold);
        readonly Font descriptionFont = LauncherFont.Create(9);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int width = Math.Max(1, Width - 20);
            int iconSize = Math.Clamp(Height / 3, 22, 48);
            int top = Math.Max(3, (Height - 58 - iconSize) / 2 + 3);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, title, headingFont, new Rectangle(10, top, width, 22),
                ForeColor, flags | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(e.Graphics, description, descriptionFont, new Rectangle(10, top + 24, width, 30),
                owner.Muted, flags | TextFormatFlags.WordBreak);
            SoftIcons.Draw(e.Graphics, icon, new RectangleF((Width - iconSize) / 2f, top + 56, iconSize, iconSize), owner.Accent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                headingFont.Dispose();
                descriptionFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
