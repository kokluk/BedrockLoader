namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    async void ShowBackupActions()
    {
        if (backupBusy) return;
        int selected = ShowShortcutChoices(T("Backups", "Yedekler"),
            T("Choose what to export or restore.", "Dışa aktarmak veya geri yüklemek istediğin işlemi seç."),
            [(T("Export settings", "Ayarları dışa aktar"), true),
             (T("Import settings", "Ayarları içe aktar"), true),
             (T("Export full backup", "Tam yedeği dışa aktar"), true),
             (T("Restore full backup", "Tam yedeği içe aktar"), true)]);
        // Finish closing the selector before opening the existing backup dialogs.
        if (selected < 0) return;
        if (selected is 0 or 2) await ExportBackupAsync(full: selected == 2, back: ShowBackupActions);
        else await ImportBackupAsync(full: selected == 3, back: ShowBackupActions);
    }

    void ShowInstallationFolders()
    {
        string?[] folders =
        [
            MinecraftInstallation.FindGameDirectories(settings.ContentPath).FirstOrDefault(),
            Path.GetDirectoryName(FindCinnabar(nightly: false)),
            Path.GetDirectoryName(FindCinnabar(nightly: true))
        ];
        string[] names = ["Minecraft", T("Cinnabar Stable", "Cinnabar Kararlı"), T("Cinnabar Nightly", "Cinnabar Gecelik")];
        int selected = ShowShortcutChoices(T("Installation folders", "Kurulum klasörleri"),
            T("Which installation folder would you like to open?", "Hangi kurulum klasörünü açmak istersin?"),
            names.Select((name, index) => (name, Directory.Exists(folders[index]))).ToArray(), folderActions: true);
        if (selected == folders.Length)
        {
            using var picker = new FolderBrowserDialog
            {
                Description = T("Choose the Minecraft Content folder", "Minecraft Content klasörünü seç"),
                SelectedPath = Directory.Exists(settings.ContentPath) ? settings.ContentPath : ""
            };
            if (picker.ShowDialog(this) == DialogResult.OK && Directory.Exists(picker.SelectedPath))
            {
                settings.ContentPath = picker.SelectedPath;
                SaveSettings();
                RefreshDiscordOptions();
                UpdateStatus(T("Content folder saved.", "Content klasörü kaydedildi."));
            }
            return;
        }
        if (selected >= 0 && folders[selected] is { } folder && Directory.Exists(folder)) OpenPath(folder);
    }

    int ShowShortcutChoices(string title, string description, (string Text, bool Enabled)[] choices, bool folderActions = false)
    {
        using var popup = new RoundedPopupForm(Accent)
        {
            Text = title, FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false, BackColor = Surface, ForeColor = TextColor, ClientSize = new Size(480, 342),
            AutoScaleMode = AutoScaleMode.Dpi
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(22), BackColor = Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        popup.Controls.Add(layout);
        void Span(Control control, int row, int height)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 2);
        }
        Span(WrappedText(title, 16, true), 0, 44);
        Span(WrappedText(description), 1, 42);
        int result = -1;
        for (int i = 0; i < choices.Length; i++)
        {
            int choice = i;
            var button = MakeButton(folderActions ? T("Open folder", "Klasörü aç") : choices[i].Text,
                () => { result = choice; popup.DialogResult = DialogResult.OK; }, icon: folderActions ? "Addons" : "backup");
            button.Dock = DockStyle.Fill;
            button.Enabled = choices[i].Enabled;
            if (folderActions)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                var label = MakeLabel(choices[i].Text + (choices[i].Enabled ? "" : T(" · Not installed", " · Kurulu değil")));
                label.Anchor = AnchorStyles.Left;
                layout.Controls.Add(label, 0, i + 2);
                layout.Controls.Add(button, 1, i + 2);
            }
            else
            {
                if (i % 2 == 0) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
                layout.Controls.Add(button, i % 2, 2 + i / 2);
            }
        }
        if (folderActions)
        {
            var change = MakeButton(T("Change Minecraft folder", "Minecraft klasörünü değiştir"),
                () => { result = choices.Length; popup.DialogResult = DialogResult.OK; });
            change.Dock = DockStyle.Fill;
            Span(change, choices.Length + 2, 46);
        }
        var cancel = MakeButton(T("Cancel", "Vazgeç"), () => popup.DialogResult = DialogResult.Cancel);
        cancel.Anchor = AnchorStyles.Right;
        Span(cancel, folderActions ? choices.Length + 3 : 2 + (choices.Length + 1) / 2, 44);
        popup.CancelButton = cancel;
        popup.ClientSize = folderActions ? new Size(480, 386) : new Size(565, 286);
        return popup.ShowDialog(this) == DialogResult.OK ? result : -1;
    }

}
