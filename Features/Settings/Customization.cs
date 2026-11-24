namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    RoundedPopupForm MakeChoiceDialog(string title, string description, IReadOnlyList<PopupToggle> choices, string accept, int columns = 1, bool showBack = false, string? headingIcon = null, Control? extra = null)
    {
        int rows = (choices.Count + columns - 1) / columns;
        int extraHeight = extra?.Height ?? 0;
        bool useCards = headingIcon != null;
        var dialog = new RoundedPopupForm(Accent)
        {
            Text = title, BackColor = Surface, ForeColor = TextColor,
            ClientSize = new Size(useCards ? 638 : columns == 2 ? 585 : 515, (useCards ? 154 : 170) + 44 * rows + extraHeight),
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false
        };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14, 7, 14, 7) };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, useCards ? 32 : 52));
        var heading = WrappedText(title, 15, true);
        heading.Dock = DockStyle.Fill;
        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        header.Controls.Add(heading);
        if (headingIcon != null)
        {
            heading.Padding = new Padding(37, 0, 0, 0);
            var glyph = new HeadingGlyph(this, headingIcon) { Location = new Point(1, 6), Size = new Size(30, 30) };
            header.Controls.Add(glyph);
            glyph.BringToFront();
        }
        grid.Controls.Add(header, 0, 0);
        var subtitle = WrappedText(description);
        if (useCards) subtitle.Padding = new Padding(37, 0, 0, 0);
        grid.Controls.Add(subtitle, 0, 1);
        grid.GetControlFromPosition(0, 1)!.Dock = DockStyle.Fill;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44 * rows));
        grid.Controls.Add(BuildChoiceGrid(choices, columns), 0, 2);
        grid.RowStyles.Add(extra == null ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, extraHeight));
        if (extra != null) grid.Controls.Add(extra, 0, 3);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var ok = MakeButton(accept, () => dialog.DialogResult = DialogResult.OK, useCards ? 140 : 120,
            useCards ? "check-mark" : null);
        ok.Tag = "primary";
        var cancel = MakeButton(showBack ? T("Back", "Geri") : T("Cancel", "Vazgeç"), () => dialog.DialogResult = DialogResult.Cancel, 120);
        ok.Margin = cancel.Margin = new Padding(5, 2, 5, 2);
        ok.Height = cancel.Height = 36;
        footer.Controls.AddRange([ok, cancel]);
        grid.Controls.Add(footer, 0, 4);
        dialog.Controls.Add(grid);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        ok.Enabled = choices.Any(choice => choice.Checked) || accept == T("Save", "Kaydet");
        foreach (var choice in choices)
            choice.CheckedChanged += (_, _) => ok.Enabled = choices.Any(item => item.Checked) || accept == T("Save", "Kaydet");
        RecolorTree(dialog);
        return dialog;
    }

    static TableLayoutPanel BuildChoiceGrid(IReadOnlyList<PopupToggle> choices, int columns)
    {
        int rows = (choices.Count + columns - 1) / columns;
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty };
        for (int column = 0; column < columns; column++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        for (int row = 0; row < rows; row++) panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        var tips = new ToolTip { InitialDelay = 400, ShowAlways = true };
        panel.Disposed += (_, _) => tips.Dispose();
        for (int index = 0; index < choices.Count; index++)
        {
            var choice = choices[index];
            if (!string.IsNullOrWhiteSpace(choice.AccessibleDescription)) tips.SetToolTip(choice, choice.AccessibleDescription);
            panel.Controls.Add(choice, index % columns, index / columns);
        }
        return panel;
    }

    PopupToggle MakeChoice(string text, bool value) => new(Accent, SurfaceAlt)
    {
        Text = text, Checked = value, Dock = DockStyle.Fill, Cursor = Cursors.Hand
    };

    PopupToggle[] MakeCustomizationChoices(out Action apply, bool previewMotion = false, bool styled = false)
    {
        var glow = MakeShineToggle();
        var animation = MakeAnimationToggle(false);
        var advanced = MakeAnimationToggle(true);
        var sounds = MakeChoice(T("Click sound", "Tıklama sesi"), settings.ClickSounds);
        var usage = MakeChoice(T("Show RAM usage", "RAM kullanımını göster"), settings.ShowUsage);
        var tray = MakeChoice(T("Minimize to system tray on close", "Kapatınca sistem tepsisine indir"), settings.CloseToTray);
        bool startupEnabled = WindowsStartup.IsEnabled();
        var startup = MakeChoice(T("Run at startup", "Başlangıçta çalıştır"), startupEnabled);
        startup.AccessibleDescription = T("Starts BedrockLoader in the system tray when you sign in to Windows.",
            "Windows oturumu açılınca BedrockLoader’ı sistem tepsisinde başlatır.");
        glow.AccessibleDescription = T("Highlights buttons as the pointer approaches.", "Fare yaklaşınca düğmeleri aydınlatır.");
        animation.AccessibleDescription = T("Animates presses and interface transitions.", "Düğme basışlarını ve arayüz geçişlerini hareketlendirir.");
        advanced.AccessibleDescription = T("Adds extra movement to icons.", "Simgelere ek hareket efektleri verir.");
        sounds.AccessibleDescription = T("Plays a short sound on clicks. Turning it on previews the sound.", "Tıklamalarda kısa bir ses çalar. Açtığında sesi hemen duyarsın.");
        usage.AccessibleDescription = T("Shows the launcher's RAM usage in the header.", "Üst çubukta launcher’ın RAM kullanımını gösterir.");
        tray.AccessibleDescription = T("Keeps the launcher running in the system tray after closing.", "Kapatınca launcher’ı sistem tepsisinde çalışır durumda tutar.");
        sounds.PlaySoundOnClick = false;
        sounds.CheckedChanged += (_, _) => { if (sounds.Checked) PlayClickSample(); };
        advanced.Enabled = animation.Checked;
        animation.CheckedChanged += (_, _) => advanced.Enabled = animation.Checked;
        if (previewMotion)
        {
            animation.CheckedChanged += (_, _) => AnimatedRoundButton.AnimationsEnabled = animation.Checked;
            advanced.CheckedChanged += (_, _) => AnimatedRoundButton.AdvancedAnimationsEnabled = advanced.Checked;
        }
        apply = () =>
        {
            settings.ShinyHomeIcons = glow.Checked;
            AnimatedRoundButton.AnimationsEnabled = settings.AnimationsEnabled = animation.Checked;
            AnimatedRoundButton.AdvancedAnimationsEnabled = settings.AdvancedAnimationsEnabled = advanced.Checked;
            settings.ClickSounds = sounds.Checked;
            settings.ShowUsage = usage.Checked;
            if (usageBadge != null) usageBadge.Visible = settings.ShowUsage;
            settings.CloseToTray = tray.Checked;
            if (startup.Checked != startupEnabled)
            {
                try { WindowsStartup.SetEnabled(startup.Checked); }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                { ShowError(ex, T("Could not change Windows startup", "Windows başlangıç ayarı değiştirilemedi")); }
            }
        };
        PopupToggle[] choices = [glow, animation, advanced, sounds, usage, tray, startup];
        if (styled)
        {
            string[] icons = ["sun", "motion", "layers", "sound", "memory", "download", "update"];
            for (int i = 0; i < choices.Length; i++) StyleChoiceRow(choices[i], icons[i]);
        }
        return choices;
    }

    static void StyleChoiceRow(PopupToggle choice, string icon)
    {
        choice.RowIcon = icon;
        choice.Margin = new Padding(0, 2, 0, 3);
    }

    void ShowCustomization()
    {
        var choices = MakeCustomizationChoices(out var apply, styled: true);
        var launcherSettings = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 48, WrapContents = false };
        launcherSettings.Controls.Add(new AboutLogo { Size = new Size(32, 36), ForeColor = Accent });
        launcherSettings.Controls.Add(MakeButton(T("Launcher Settings", "Launcher Ayarları"),
            () => ShowLauncherIconSettings(launcherSettings.FindForm() ?? this), 175));
        using var dialog = MakeChoiceDialog(T("Customization", "Özelleştirme"),
            T("Customize the look and behavior of the launcher.", "Launcher’ın görünümünü ve davranışını özelleştir."),
            choices, T("Save", "Kaydet"), headingIcon: "display", extra: launcherSettings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        apply();
        SaveSettings();
        ApplyTheme();
    }

    BackupParts? ChooseBackupParts(bool full, bool importing, Action? back = null)
    {
        var available = new List<(BackupParts Part, PopupToggle Choice)>
        {
            (BackupParts.Settings, MakeChoice(T("Launcher settings", "Launcher ayarları"), true)),
            (BackupParts.Servers, MakeChoice(T("Saved servers", "Kayıtlı sunucular"), true))
        };
        if (full)
        {
            available.Add((BackupParts.Worlds, MakeChoice(T("Minecraft worlds", "Minecraft dünyaları"), false)));
            available.Add((BackupParts.Packs, MakeChoice(T("Resource and behavior packs", "Kaynak ve davranış paketleri"), false)));
            available.Add((BackupParts.Mods, MakeChoice(T("Local mods", "Yerel modlar"), false)));
            available.Add((BackupParts.Skins, MakeChoice(T("Skins and skin packs", "Skinler ve skin paketleri"), false)));
            available.Add((BackupParts.Cinnabar, MakeChoice(T("Cinnabar data and shaders", "Cinnabar verileri ve shader’lar"), false)));
        }
        using var dialog = MakeChoiceDialog(importing ? T("Choose what to import", "İçe aktarılacakları seç") : T("Choose what to export", "Dışa aktarılacakları seç"),
            importing ? T("Only selected items are read. Existing game files are kept.", "Yalnızca seçilenler okunur. Mevcut oyun dosyaları korunur.")
                : T("Only selected items are included in the backup.", "Yedeğe yalnızca seçtiğin öğeler eklenir."),
            available.Select(item => item.Choice).ToArray(), T("Continue", "Devam et"), columns: 2, showBack: back != null);
        var result = dialog.ShowDialog(this);
        if (result != DialogResult.OK)
        {
            if (result == DialogResult.Cancel && back != null) BeginInvoke(back);
            return null;
        }
        BackupParts parts = 0;
        foreach (var item in available)
            if (item.Choice.Checked) parts |= item.Part;
        return parts == 0 ? null : parts;
    }
}
