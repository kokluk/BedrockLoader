using System.IO.Pipes;
using System.Runtime.InteropServices;
using KoklukBedrockModloader;

var scratch = Directory.CreateTempSubdirectory("BedrockPresence-");
Environment.SetEnvironmentVariable("APPDATA", scratch.FullName);
try
{
    var world = Path.Combine(scratch.FullName, "Minecraft Bedrock", "Users", "player", "games", "com.mojang", "minecraftWorlds", "test");
    Directory.CreateDirectory(Path.Combine(world, "db"));
    File.WriteAllText(Path.Combine(world, "levelname.txt"), "Örnek Dünya");
    var lockPath = Path.Combine(world, "db", "LOCK");
    File.WriteAllBytes(lockPath, []);
    NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "BedrockLoaderPresence.dll"));
    using var pipe = new NamedPipeClientStream(".", GamePresenceFrame.PipeName(Environment.ProcessId), PipeDirection.InOut, PipeOptions.Asynchronous);
    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await pipe.ConnectAsync(limit.Token);
    async Task Expect(string kind, string? name)
    {
        var state = await GamePresenceFrame.ReadAsync(pipe, limit.Token);
        if (state.Kind != kind || state.World != name) throw new Exception($"Expected {kind}, received {state}");
        Console.WriteLine($"PASS {kind}: {name}");
    }
    await Expect("unknown", null);
    using (File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        await Expect("local", "Örnek Dünya");
    await Expect("unknown", null);
}
finally { scratch.Delete(true); }
