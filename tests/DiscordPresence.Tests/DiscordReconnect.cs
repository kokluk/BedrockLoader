using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using DiscordRPC;
using DiscordRPC.IO;
using DiscordRPC.Logging;
using KoklukBedrockModloader;

// Exercise the real RPC client and launcher loop. The transport is private to
// this test so even a running desktop Discord never receives a test activity.
internal sealed class DiscordReconnect : INamedPipeClient
{
    readonly ConcurrentQueue<PipeFrame> incoming = new();
    readonly Channel<(int Connection, string Details)> sent = Channel.CreateUnbounded<(int, string)>();
    volatile bool online = true, connected;
    int generation;
    public ILogger Logger { get; set; } = new NullLogger();
    public int ConnectedPipe => 0;
    public bool IsConnected => connected;
    public bool Connect(int pipe)
    {
        if (!online) return false;
        Interlocked.Increment(ref generation);
        return connected = true;
    }
    public bool ReadFrame(out PipeFrame frame) => incoming.TryDequeue(out frame);
    public bool WriteFrame(PipeFrame frame)
    {
        if (!connected) return false;
        if (frame.Opcode == Opcode.Handshake)
        {
            incoming.Enqueue(new PipeFrame(Opcode.Frame, new
            {
                cmd = "DISPATCH", evt = "READY",
                data = new { v = 1, config = new { api_endpoint = "local", environment = "test" },
                    user = new { id = "100000000000000001", username = "Fixture", discriminator = "0" } }
            }));
        }
        else if (frame.Opcode == Opcode.Frame)
        {
            using var json = JsonDocument.Parse(frame.Data);
            var root = json.RootElement;
            if (root.GetProperty("cmd").GetString() == "SET_ACTIVITY" &&
                root.GetProperty("args").TryGetProperty("activity", out var activity) &&
                activity.ValueKind == JsonValueKind.Object && activity.TryGetProperty("details", out var details))
                sent.Writer.TryWrite((generation, details.GetString()!));
        }
        return true;
    }
    public void Close() => connected = false;
    public void Dispose() => Close();

    internal static Task Verify()
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm();
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var pipe = new DiscordReconnect();
                var options = new MainForm.DiscordOptions { English = true, ShowGameState = false };
                var worker = form.RunDiscordPresence(stop.Token, () => options,
                    id => new DiscordRpcClient(id, client: pipe));
                try
                {
                    var first = pipe.sent.Reader.ReadAsync(stop.Token).AsTask().GetAwaiter().GetResult();
                    pipe.online = false;
                    pipe.Close();
                    Thread.Sleep(400);
                    pipe.online = true;
                    (int Connection, string Details) next;
                    do { next = pipe.sent.Reader.ReadAsync(stop.Token).AsTask().GetAwaiter().GetResult(); }
                    while (next.Connection <= first.Connection);
                    if (next.Details != first.Details) throw new InvalidDataException("Presence was not restored.");
                }
                finally { stop.Cancel(); worker.GetAwaiter().GetResult(); }
                finished.SetResult();
            }
            catch (Exception ex) { finished.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return finished.Task;
    }
}
