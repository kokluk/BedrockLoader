using DiscordRPC;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    const string DefaultDiscordApplicationId = "1567637573680238662";
    readonly CancellationTokenSource presenceLifetime = new();
    readonly SemaphoreSlim presenceChanged = new(0, 1);
    DiscordOptions presenceOptions = new();
    bool presenceStarted;

    void StartDiscordPresence()
    {
        if (presenceStarted) return;
        presenceStarted = true;
        RefreshDiscordOptions();
        _ = RunDiscordPresence(presenceLifetime.Token, () => Volatile.Read(ref presenceOptions), id => new DiscordRpcClient(id));
    }

    void WakePresence()
    {
        try { presenceChanged.Release(); }
        catch (SemaphoreFullException) { } // One pending refresh already covers all changes.
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
        WakePresence();
    }

    internal Task RunDiscordPresence(CancellationToken cancel, Func<DiscordOptions> readOptions,
        Func<string, DiscordRpcClient> createClient) => Task.Run(async () =>
    {
        DiscordRpcClient? client = null;
        string? applicationId = null;
        PresenceSnapshot? published = null;
        MinecraftInstallation.RunningGame? game = null;
        GamePresenceReader? reader = null;
        long nextScan = 0;
        string? scannedPath = null;
        bool gameEnded = false;
        int readyGeneration = 0, sentGeneration = 0;
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                var options = readOptions();
                bool enabled = options.Enabled && ValidDiscordId(options.ApplicationId);
                try
                {
                    var lastGame = game;
                    if (!enabled) { game = null; nextScan = 0; gameEnded = false; }
                    else if (Environment.TickCount64 >= nextScan || scannedPath != options.ContentPath)
                    {
                        nextScan = Environment.TickCount64 + 5000;
                        game = MinecraftInstallation.FindRunningGame(options.ContentPath,
                            scannedPath == options.ContentPath ? game : null);
                        scannedPath = options.ContentPath;
                        if (game != null) gameEnded = false;
                        else if (lastGame != null) gameEnded = true;
                    }
                    if (reader != null && (!options.ShowGameState || game != lastGame || game == null))
                    {
                        await reader.DisposeAsync();
                        reader = null;
                    }
                    if (game != null && options.ShowGameState)
                        reader ??= new GamePresenceReader(game.Pid, game.StartedUtc, WakePresence);
                    if (!enabled || applicationId != options.ApplicationId)
                    {
                        if (client?.IsInitialized == true) client.ClearPresence();
                        client?.Dispose();
                        client = null;
                        published = null;
                        applicationId = options.ApplicationId;
                    }
                    if (enabled)
                    {
                        if (client == null)
                        {
                            Volatile.Write(ref readyGeneration, 0);
                            sentGeneration = 0;
                            client = createClient(options.ApplicationId);
                            client.SkipIdenticalPresence = false;
                            client.OnError += (_, _) => LogAction("Discord rejected a presence request.", "Discord etkinlik isteğini reddetti.");
                            client.OnReady += (_, _) => { Interlocked.Increment(ref readyGeneration); WakePresence(); };
                            client.OnPresenceUpdate += (_, _) => LogAction("Discord acknowledged a presence update.", "Discord etkinlik güncellemesini onayladı.");
                        }
                        if (!client.IsInitialized) client.Initialize();

                        var snapshot = MakePresence(options, game, reader?.Current, gameEnded);
                        int generation = Volatile.Read(ref readyGeneration);
                        if (client.IsInitialized && generation > 0 && (snapshot != published || generation != sentGeneration))
                        {
                            if (snapshot == null) client.ClearPresence();
                            else client.SetPresence(new RichPresence
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
                if (enabled)
                    await presenceChanged.WaitAsync(TimeSpan.FromMilliseconds(
                        Math.Clamp(nextScan - Environment.TickCount64, 250, 5000)), cancel);
                else await presenceChanged.WaitAsync(cancel);
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        finally
        {
            if (reader != null) await reader.DisposeAsync();
            client?.Dispose();
        }
    });

    static bool ValidDiscordId(string value) => ulong.TryParse(value, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out ulong id) && id > 0;

    internal sealed record PresenceSnapshot(string Details, string? State, DateTime? StartedUtc, string LargeImage, string SmallImage);

    internal static PresenceSnapshot? MakePresence(DiscordOptions options, MinecraftInstallation.RunningGame? game,
        GamePresenceFrame? activity, bool gameEnded)
    {
        string image = string.IsNullOrWhiteSpace(options.LargeImage) ? "bedrockloader" : options.LargeImage;
        bool en = options.English;
        if (!options.Enabled || (game == null && gameEnded)) return null;
        if (game == null) return new(en ? "Main Menu" : "Ana Menü", en ? "Idle 🌙" : "Boşta 🌙", null, image, options.SmallImage);
        var info = options.ShowGameState ? activity : null;
        string? state = info?.Kind switch
        {
            "menu" => en ? "Main Menu" : "Ana Menü",
            "loading" => en ? "Loading a world" : "Dünya yükleniyor",
            "solo" => en ? "Playing solo" : "Tek başına oynuyor",
            "local" => en ? "Hosting a world" : "Yerel dünyada",
            "world" => en ? "In a world" : "Dünyada",
            "server" => en ? "On a multiplayer server" : "Çok oyunculu sunucuda",
            "realm" => en ? "On a Realm" : "Realm’de",
            _ => null
        };
        string? name = info?.Kind switch
        {
            "local" or "solo" when options.ShowWorldName => info.World,
            "server" or "realm" when options.ShowServer => info.Server,
            _ => null
        };
        var parts = new[] { state, name, info?.Dimension, options.LocalName };
        return new(options.ShowVersion && game.Version is { } version ? $"Minecraft {version}" : "Minecraft",
            GamePresenceFrame.Text(string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p))), 128),
            options.ShowTime ? game.StartedUtc : null, image, options.SmallImage);
    }

    PopupToggle[] MakeDiscordChoices(out Func<DiscordOptions> readChoices)
    {
        var options = settings.Discord ?? new DiscordOptions();
        var enabled = MakeChoice(T("Enable Rich Presence", "Rich Presence’ı etkinleştir"), options.Enabled);
        var state = MakeChoice(T("Share in-game activity", "Oyun durumunu paylaş"), options.ShowGameState);
        state.AccessibleDescription = T("Detailed activity requires the optional Onix presence plugin.",
            "Ayrıntılı oyun durumu için isteğe bağlı Onix etkinlik eklentisi gerekir.");
        var server = MakeChoice(T("Share server address or Realm name", "Sunucu adresini veya Realm adını paylaş"), options.ShowServer);
        var world = MakeChoice(T("Share local world name", "Yerel dünya adını paylaş"), options.ShowWorldName);
        var version = MakeChoice(T("Share Minecraft version", "Minecraft sürümünü paylaş"), options.ShowVersion);
        var time = MakeChoice(T("Share elapsed time", "Oyun süresini paylaş"), options.ShowTime);
        StyleChoiceRow(enabled, "discord");
        StyleChoiceRow(state, "display");
        StyleChoiceRow(server, "Servers");
        StyleChoiceRow(world, "Worlds");
        StyleChoiceRow(version, "game");
        StyleChoiceRow(time, "status");
        void RefreshAvailability()
        {
            state.Enabled = version.Enabled = time.Enabled = enabled.Checked;
            world.Enabled = server.Enabled = enabled.Checked && state.Checked;
        }
        enabled.CheckedChanged += (_, _) => RefreshAvailability();
        state.CheckedChanged += (_, _) => RefreshAvailability();
        RefreshAvailability();
        readChoices = () => options with { Enabled = enabled.Checked, ShowVersion = version.Checked, ShowTime = time.Checked,
            ShowGameState = state.Checked, ShowWorldName = world.Checked, ShowServer = server.Checked };
        return [enabled, state, version, server, world, time];
    }

    void ConfigureDiscordPresence()
    {
        var choices = MakeDiscordChoices(out var readChoices);
        var native = MakeButton(T("Manage native activity mod", "Native etkinlik modunu yönet"), () => { }, 260, "discord");
        native.Height = 42;
        using var dialog = MakeChoiceDialog("Discord Rich Presence",
            T("A compatible activity mod can share more detail.", "Uyumlu bir etkinlik modu oyundan daha fazla ayrıntı paylaşabilir."),
            choices, T("Save", "Kaydet"), headingIcon: "discord", extra: native);
        native.Click += (_,_) => ConfigureNativeActivity(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings.Discord = readChoices();
        SaveSettings();
        RefreshDiscordOptions();
    }

    internal sealed record DiscordOptions
    {
        public bool Enabled { get; init; } = true;
        public bool ShowVersion { get; init; } = true;
        public bool ShowTime { get; init; } = true;
        public bool ShowGameState { get; init; } = true;
        public bool ShowServer { get; init; }
        public bool ShowWorldName { get; init; }
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
