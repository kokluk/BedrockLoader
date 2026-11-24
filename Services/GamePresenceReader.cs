using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace KoklukBedrockModloader;

internal sealed class GamePresenceReader : IAsyncDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);

    readonly CancellationTokenSource stop = new();
    readonly Task worker;
    GamePresenceFrame current = GamePresenceFrame.Unknown;
    internal GamePresenceFrame Current => Volatile.Read(ref current);

    internal GamePresenceReader(int gamePid, DateTime startedUtc, Action changed)
    {
        worker = Listen(gamePid, startedUtc, changed, stop.Token);
    }

    async Task Listen(int gamePid, DateTime startedUtc, Action changed, CancellationToken token)
    {
        void Set(GamePresenceFrame value)
        {
            if (Interlocked.Exchange(ref current, value) != value) changed();
        }
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeClientStream(".", GamePresenceFrame.PipeName(gamePid),
                        PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
                    await pipe.ConnectAsync(500, token).ConfigureAwait(false);
                    if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint peer) || peer != gamePid)
                        throw new InvalidDataException("Unexpected activity producer");
                    using var process = Process.GetProcessById(gamePid);
                    if (process.StartTime.ToUniversalTime() != startedUtc) return;
                    while (true) Set(await GamePresenceFrame.ReadAsync(pipe, token).ConfigureAwait(false));
                }
                catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException
                    or System.Text.Json.JsonException or InvalidOperationException or InvalidDataException
                    or ArgumentException or System.ComponentModel.Win32Exception) { }
                finally { Set(GamePresenceFrame.Unknown); }
                await Task.Delay(5000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        try { await worker.ConfigureAwait(false); }
        finally { stop.Dispose(); }
    }
}
