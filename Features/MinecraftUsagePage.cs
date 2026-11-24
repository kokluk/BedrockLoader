using System.ComponentModel;
using System.Diagnostics;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    System.Windows.Forms.Timer? minecraftUsageTimer;
    Label? minecraftCpuReading, minecraftRamReading, minecraftUsageState;
    MinecraftReading? minecraftReading;
    bool samplingMinecraftUsage;
    bool minecraftUsageAlerted;

    sealed record MinecraftReading(MinecraftInstallation.RunningGame Game, TimeSpan CpuTime, long Timestamp,
        long WorkingSet, double? CpuPercent);

    void RefreshMinecraftUsageMonitoring()
    {
        if (!settings.ShowMinecraftUsage)
        {
            StopMinecraftUsageMonitoring();
            return;
        }
        if (minecraftUsageTimer != null) return;
        minecraftUsageTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        minecraftUsageTimer.Tick += (_, _) => _ = SampleMinecraftUsage();
        minecraftUsageTimer.Start();
        _ = SampleMinecraftUsage();
    }

    void StopMinecraftUsageMonitoring()
    {
        minecraftUsageTimer?.Dispose();
        minecraftUsageTimer = null;
        minecraftReading = null;
        minecraftUsageAlerted = false;
    }

    async Task SampleMinecraftUsage()
    {
        if (samplingMinecraftUsage || minecraftUsageTimer == null) return;
        samplingMinecraftUsage = true;
        var timer = minecraftUsageTimer;
        var previous = minecraftReading;
        string path = settings.ContentPath;
        try
        {
            var reading = await Task.Run(() => ReadMinecraftUsage(path, previous));
            if (IsDisposed || Disposing || minecraftUsageTimer != timer) return;
            if (reading?.Game != previous?.Game) minecraftUsageAlerted = false;
            minecraftReading = reading;
            RenderMinecraftUsage();
            bool high = reading != null && ExceedsMinecraftUsageLimit(reading.CpuPercent, reading.WorkingSet,
                settings.MinecraftCpuLimit, settings.MinecraftRamLimitMiB);
            if (high && !minecraftUsageAlerted && reading != null)
            {
                ShowTrayIcon();
                var cpu = reading.CpuPercent is double percent ? $"{percent:F1}%" : "—";
                trayIcon!.ShowBalloonTip(4000, T("High resource usage", "Yüksek kaynak kullanımı"),
                    $"Minecraft · CPU {cpu} · RAM {reading.WorkingSet / 1048576d:F0} MiB", ToolTipIcon.Warning);
            }
            minecraftUsageAlerted = high;
        }
        finally { samplingMinecraftUsage = false; }
    }

    internal static bool ExceedsMinecraftUsageLimit(double? cpu, long ramBytes, int cpuLimit, int ramLimitMiB) =>
        (cpuLimit > 0 && cpu > cpuLimit) || (ramLimitMiB > 0 && ramBytes > ramLimitMiB * 1048576L);

    static MinecraftReading? ReadMinecraftUsage(string path, MinecraftReading? previous)
    {
        try
        {
            var game = MinecraftInstallation.FindRunningGame(path, previous?.Game);
            if (game == null) return null;
            using var process = Process.GetProcessById(game.Pid);
            if (process.StartTime.ToUniversalTime() != game.StartedUtc) return null;
            long memory = process.WorkingSet64;
            var cpu = process.TotalProcessorTime;
            long now = Stopwatch.GetTimestamp();
            double? percent = null;
            if (previous?.Game == game)
            {
                double elapsed = Stopwatch.GetElapsedTime(previous.Timestamp, now).TotalSeconds;
                if (elapsed > 0) percent = Math.Clamp(100 * (cpu - previous.CpuTime).TotalSeconds /
                    (elapsed * Environment.ProcessorCount), 0, 100);
            }
            return new(game, cpu, now, memory, percent);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or
            IOException or UnauthorizedAccessException) { return null; }
    }

    void RenderMinecraftUsage()
    {
        if (minecraftCpuReading is not { IsDisposed: false } cpu ||
            minecraftRamReading is not { IsDisposed: false } ram ||
            minecraftUsageState is not { IsDisposed: false } state) return;
        cpu.Text = minecraftReading?.CpuPercent is double percent ? $"CPU  {percent:F1}%" : "CPU  —";
        ram.Text = minecraftReading is { } reading ? $"RAM  {reading.WorkingSet / 1048576d:N0} MiB" : "RAM  —";
        state.Text = minecraftReading == null
            ? T("No running Minecraft session to measure.", "Ölçülebilen bir Minecraft oturumu yok.")
            : T("Live Minecraft usage", "Canlı Minecraft kullanımı");
    }

    void BuildMinecraftUsagePage()
    {
        AddPageHeading(T("Minecraft resource usage", "Minecraft kaynak kullanımı"),
            T("CPU is shown as a percentage of total system capacity.", "CPU, toplam sistem kapasitesinin yüzdesi olarak gösterilir."));
        var layout = new RoundedSettingsList(this) { Height = 338, ColumnCount = 2, RowCount = 7,
            Padding = new Padding(16), Tag = "settings-card" };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foreach (int height in new[] { 56, 31, 34, 45, 45, 46, 44 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        Label AddMetric(int column)
        {
            var tile = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 14,
                Margin = new Padding(column == 0 ? 0 : 5, 0, column == 0 ? 5 : 0, 4), Tag = "usage-metric" };
            var value = WrappedText("", 15, true);
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.MiddleCenter;
            tile.Controls.Add(value);
            ApplyLatencyPalette(tile, value);
            layout.Controls.Add(tile, column, 0);
            return value;
        }
        minecraftCpuReading = AddMetric(0);
        minecraftRamReading = AddMetric(1);
        minecraftUsageState = WrappedText("", 9);
        layout.Controls.Add(minecraftUsageState, 0, 1);
        layout.SetColumnSpan(minecraftUsageState, 2);
        RenderMinecraftUsage();

        var limitsTitle = WrappedText(T("Notification thresholds", "Bildirim eşikleri"), 11, true);
        layout.Controls.Add(limitsTitle, 0, 2);
        layout.SetColumnSpan(limitsTitle, 2);
        TextBox AddLimit(string title, int row, int value)
        {
            var label = WrappedText(title, 10);
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(label, 0, row);
            var input = MakeTextBox(value.ToString());
            input.MaxLength = 7;
            input.AccessibleName = title;
            var field = WrapPopupInput(input);
            field.Dock = DockStyle.Fill;
            layout.Controls.Add(field, 1, row);
            return input;
        }
        var cpu = AddLimit(T("CPU alert threshold (%)", "CPU bildirim eşiği (%)"), 3, settings.MinecraftCpuLimit);
        var ram = AddLimit(T("RAM alert threshold (MiB)", "RAM bildirim eşiği (MiB)"), 4, settings.MinecraftRamLimitMiB);
        string helpText = T("A threshold of 0 turns off that notification.", "Eşik 0 ise o kaynak için bildirim gönderilmez.");
        var help = WrappedText(helpText, 9);
        help.Dock = DockStyle.Fill;
        layout.Controls.Add(help, 0, 5);
        layout.SetColumnSpan(help, 2);
        var save = MakeButton(T("Save", "Kaydet"), () =>
        {
            if (!int.TryParse(cpu.Text, out int cpuLimit) || cpuLimit is < 0 or > 100 ||
                !int.TryParse(ram.Text, out int ramLimit) || ramLimit is < 0 or > 1048576)
            {
                help.Text = T("Enter a whole number from 0 to 100 for CPU and from 0 to 1048576 for RAM.",
                    "CPU için 0–100, RAM için 0–1048576 arasında tam sayı gir.");
                return;
            }
            if (settings.MinecraftCpuLimit != cpuLimit || settings.MinecraftRamLimitMiB != ramLimit)
                minecraftUsageAlerted = false;
            settings.MinecraftCpuLimit = cpuLimit;
            settings.MinecraftRamLimitMiB = ramLimit;
            help.Text = helpText;
            SaveSettings();
            UpdateStatus(T("Resource alerts saved.", "Kaynak bildirim ayarları kaydedildi."));
        }, 145);
        save.Tag = "primary";
        save.Anchor = AnchorStyles.Right;
        layout.Controls.Add(save, 1, 6);
        contentHost.Controls.Add(new ThemedScrollPanel(this, layout) { Dock = DockStyle.Fill });
    }
}
