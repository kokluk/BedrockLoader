using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BedrockLoader.Presence;
using KoklukBedrockModloader;

int passed = 0;
void Check(bool result, string description)
{
    if (!result) throw new Exception(description);
    Console.WriteLine("PASS " + description);
    passed++;
}
using var process = Process.GetCurrentProcess();
var game = new MinecraftInstallation.RunningGame(process.Id, process.StartTime.ToUniversalTime(), "1.26.20");
var options = JsonSerializer.Deserialize<MainForm.DiscordOptions>("{}")! with { English = true };
var server = new GamePresenceFrame("server", "Nether", Server: "private.example");
var local = new GamePresenceFrame("solo", "Overworld", World: "My World");
MainForm.PresenceSnapshot? Project(GamePresenceFrame? frame, MainForm.DiscordOptions? choices = null)
    => MainForm.MakePresence(choices ?? options, game, frame, false);

Check(Project(null) is { Details: "Minecraft 1.26.20", State: null } basic && basic.StartedUtc == game.StartedUtc, "process-only fallback");
Check(Project(new("menu"))?.State == "Main Menu" && Project(new("loading"), options with { English = false })?.State == "Dünya yükleniyor", "observed menu and loading localized");
Check(Project(local)?.State == "Playing solo · Overworld" && Project(server)?.State == "On a multiplayer server · Nether", "legacy settings keep names private");
Check(Project(local, options with { ShowWorldName = true })?.State?.Contains("My World") == true, "world name opt-in");
Check(Project(server, options with { ShowServer = true })?.State?.Contains("private.example") == true, "server opt-in");
Check(Project(new("realm", "End", Server: "My realm"), options with { ShowWorldName = true })?.State == "On a Realm · End", "Realm name follows server privacy");
Check(Project(server, options with { ShowGameState = false, ShowServer = true, ShowTime = false, ShowVersion = false })
    is { Details: "Minecraft", State: null, StartedUtc: null }, "independent sharing switches");
Check(MainForm.MakePresence(options, null, server, true) == null &&
    MainForm.MakePresence(options, null, null, false)?.Details == "Main Menu", "game exit clears activity, startup idle preserved");
Check(Project(local, options with { Enabled = false }) == null, "RPC off");
Check(Encoding.UTF8.GetByteCount(GamePresenceFrame.Text(new string('界', 100), 64)!) <= 64 &&
    GamePresenceFrame.Text("a\n\u202Eb", 64) == "ab", "Unicode byte limit and formatting controls");

// Stand-in process with Windows version resources, entirely inside a temporary install folder.
var folder = Directory.CreateTempSubdirectory("BedrockPresence-");
try
{
    string image = Path.Combine(folder.FullName, "Minecraft.Windows.exe");
    File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), image);
    var start = new ProcessStartInfo(image) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
    start.ArgumentList.Add("-n"); start.ArgumentList.Add("3"); start.ArgumentList.Add(IPAddress.Loopback.ToString());
    using var fixture = Process.Start(start)!;
    try
    {
        Check(MinecraftInstallation.FindRunningGame("")?.Pid != fixture.Id, "unregistered namesake ignored");
        var found = MinecraftInstallation.FindRunningGame(folder.FullName);
        Check(found?.Pid == fixture.Id && found.Version != null && found.StartedUtc == fixture.StartTime.ToUniversalTime(), "process start and file version detected");
        Check(ReferenceEquals(found, MinecraftInstallation.FindRunningGame(folder.FullName, found)), "stable identity reuses metadata");
    }
    finally { await fixture.WaitForExitAsync(); }
    Check(MinecraftInstallation.FindRunningGame(folder.FullName)?.Pid != fixture.Id, "process exit observed");
}
finally { folder.Delete(true); }

async Task<GamePresenceFrame> RoundTrip(GamePresenceFrame frame)
{
    using var stream = new MemoryStream();
    await frame.WriteAsync(stream, default);
    stream.Position = 0;
    return await GamePresenceFrame.ReadAsync(stream, default);
}
Check(await RoundTrip(new("menu", "End", "stale", "old")) == new GamePresenceFrame("menu"), "menu drops leftover fields");
Check(await RoundTrip(new("other", "Nether")) == GamePresenceFrame.Unknown, "unknown activity is generic");
foreach (var body in new[] { "{}", "{", "{\"Kind\":\"menu\",\"Protocol\":2}", new string('a', 4097) })
{
    using var stream = new MemoryStream();
    var data = Encoding.UTF8.GetBytes(body);
    stream.Write(BitConverter.GetBytes(data.Length)); stream.Write(data); stream.Position = 0;
    bool rejected = false;
    try { await GamePresenceFrame.ReadAsync(stream, default); }
    catch (Exception ex) when (ex is InvalidDataException or JsonException) { rejected = true; }
    Check(rejected, "invalid frame rejected");
}

using var changed = new SemaphoreSlim(0);
await using (var reader = new GamePresenceReader(game.Pid, game.StartedUtc, () => changed.Release()))
{
    async Task Until(GamePresenceFrame expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        while (reader.Current != expected) await changed.WaitAsync(timeout.Token);
    }
    using (var provider = new ActivityPublisher())
    {
        provider.Publish(local);
        await Until(local); Check(true, "same-user pipe and OS producer identity");
        provider.Publish(server);
        await Until(server); Check(reader.Current.World == null, "world to server replaces entire frame");
    }
    await Until(GamePresenceFrame.Unknown); Check(true, "disconnect clears last world");
    using (var next = new ActivityPublisher())
    {
        next.Publish(new("menu"));
        await Until(new("menu")); Check(true, "provider reconnect sends current snapshot");
    }
}
using (var next = new ActivityPublisher())
{
    next.Publish(local);
    await using var wrongStart = new GamePresenceReader(game.Pid, game.StartedUtc.AddSeconds(-1), () => { });
    await Task.Delay(700);
    Check(wrongStart.Current == GamePresenceFrame.Unknown, "stale process identity rejected");
}
await DiscordReconnect.Verify();
Check(true, "launcher republishes activity after simulated Discord restart");
Console.WriteLine($"{passed} checks passed.");
