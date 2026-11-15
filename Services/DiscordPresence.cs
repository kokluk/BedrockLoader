using DiscordRPC;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string DefaultDiscordApplicationId = "1567637573680238662";
    readonly CancellationTokenSource presenceLifetime = new();
    DiscordOptions presenceOptions = new();

    void StartDiscordPresence()
    {
        RefreshDiscordOptions();
        _ = RunDiscordPresence(presenceLifetime.Token);
    }

    void RefreshDiscordOptions()
    {
        var options = settings.Discord ?? new DiscordOptions();
        Volatile.Write(ref presenceOptions, options with
        {
            ContentPath = settings.ContentPath,
            LocalName = NormalizeLocalName(settings.LocalName),
            English = settings.Language == "EN",
            ApplicationId = string.IsNullOrWhiteSpace(options.ApplicationId) ? DefaultDiscordApplicationId : options.ApplicationId
        });
    }

    Task RunDiscordPresence(CancellationToken cancel) => Task.Run(async () =>
    {
        DiscordRpcClient? client = null;
        string? applicationId = null;
        PresenceSnapshot? published = null;
        int readyGeneration = 0, sentGeneration = 0;
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var options = Volatile.Read(ref presenceOptions);
                    bool enabled = options.Enabled && ValidDiscordId(options.ApplicationId);
                    var game = enabled ? MinecraftInstallation.FindRunningGame(options.ContentPath) : null;
                    bool showPresence = enabled;
                    if (!showPresence || applicationId != options.ApplicationId)
                    {
                        client?.Dispose();
                        client = null;
                        published = null;
                        applicationId = options.ApplicationId;
                    }
                    if (showPresence)
                    {
                        if (client == null)
                        {
                            Volatile.Write(ref readyGeneration, 0);
                            sentGeneration = 0;
                            client = new DiscordRpcClient(options.ApplicationId);
                            client.OnError += (_, _) => LogAction("Discord rejected a presence request.", "Discord etkinlik isteğini reddetti.");
                            client.OnReady += (_, _) => Interlocked.Increment(ref readyGeneration);
                            client.OnPresenceUpdate += (_, _) => LogAction("Discord acknowledged a presence update.", "Discord etkinlik güncellemesini onayladı.");
                        }
                        if (!client.IsInitialized) client.Initialize();

                        string largeImage = string.IsNullOrWhiteSpace(options.LargeImage) ? "bedrockloader" : options.LargeImage;
                        var snapshot = game is null
                            ? new PresenceSnapshot(options.English ? "Main Menu" : "Ana Menü",
                                options.English ? "Idle 🌙" : "Boşta 🌙", null, largeImage, options.SmallImage)
                            : new PresenceSnapshot(options.ShowVersion && game.Version is { } version
                                    ? $"Minecraft {version}" : "Minecraft",
                                string.IsNullOrEmpty(options.LocalName) ? null : options.LocalName,
                                options.ShowTime ? game.StartedUtc : null, largeImage, options.SmallImage);
                        int generation = Volatile.Read(ref readyGeneration);
                        if (client.IsInitialized && generation > 0 && (snapshot != published || generation != sentGeneration))
                        {
                            client.SetPresence(new RichPresence
                            {
                                Details = snapshot.Details,
                                State = snapshot.State,
                                Timestamps = snapshot.StartedUtc is { } started ? new Timestamps { Start = started } : null,
                                Assets = new Assets { LargeImageKey = snapshot.LargeImage, SmallImageKey = snapshot.SmallImage }
                            });
                            published = snapshot;
                            sentGeneration = generation;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    client?.Dispose();
                    client = null;
                    published = null;
                }
                await Task.Delay(TimeSpan.FromSeconds(3), cancel);
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        finally { client?.Dispose(); }
    });

    static bool ValidDiscordId(string value) => ulong.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out ulong id) && id > 0;

    sealed record PresenceSnapshot(string Details, string? State, DateTime? StartedUtc, string LargeImage, string SmallImage);

    PopupToggle[] MakeDiscordChoices(out Func<DiscordOptions> readChoices)
    {
        var options = settings.Discord ?? new DiscordOptions();
        var enabled = MakeChoice(T("Enable Rich Presence", "Rich Presence’ı etkinleştir"), options.Enabled);
        var version = MakeChoice(T("Share Minecraft version", "Minecraft sürümünü paylaş"), options.ShowVersion);
        var time = MakeChoice(T("Share elapsed time", "Oyun süresini paylaş"), options.ShowTime);
        StyleChoiceRow(enabled, "discord");
        StyleChoiceRow(version, "game");
        StyleChoiceRow(time, "status");
        void RefreshAvailability() => version.Enabled = time.Enabled = enabled.Checked;
        enabled.CheckedChanged += (_, _) => RefreshAvailability();
        RefreshAvailability();
        readChoices = () => options with { Enabled = enabled.Checked, ShowVersion = version.Checked, ShowTime = time.Checked };
        return [enabled, version, time];
    }

    void ConfigureDiscordPresence()
    {
        var choices = MakeDiscordChoices(out var readChoices);
        using var dialog = MakeChoiceDialog("Discord Rich Presence",
            T("Choose which game details to show on your Discord profile.", "Discord profilinde hangi oyun bilgilerinin görüneceğini seç."),
            choices, T("Save", "Kaydet"), headingIcon: "discord");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings.Discord = readChoices();
        SaveSettings();
        RefreshDiscordOptions();
    }

    sealed record DiscordOptions
    {
        public bool Enabled { get; init; } = true;
        public bool ShowVersion { get; init; } = true;
        public bool ShowTime { get; init; } = true;
        public string ApplicationId { get; init; } = DefaultDiscordApplicationId;
        public string LargeImage { get; init; } = "bedrockloader";
        public string SmallImage { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string ContentPath { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string LocalName { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public bool English { get; init; }
    }
}
