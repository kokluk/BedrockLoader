using System.IO.Pipes;
using System.Threading.Channels;
using KoklukBedrockModloader;

namespace BedrockLoader.Presence;

internal sealed class ActivityPublisher : IDisposable
{
    readonly CancellationTokenSource stop = new();
    readonly Channel<bool> changes = Channel.CreateBounded<bool>(1);
    readonly Task worker;
    GamePresenceFrame current = GamePresenceFrame.Unknown;

    internal ActivityPublisher() => worker = Task.Run(() => Serve(stop.Token));

    internal void Publish(GamePresenceFrame frame)
    {
        if (Interlocked.Exchange(ref current, frame) != frame) changes.Writer.TryWrite(true);
    }

    async Task Serve(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(GamePresenceFrame.PipeName(Environment.ProcessId),
                    PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token);
                using var connection = CancellationTokenSource.CreateLinkedTokenSource(token);
                var sending = SendChanges(pipe, connection.Token);
                // The launcher never writes. EOF also wakes an idle publisher on a launcher restart.
                var disconnected = pipe.ReadAsync(new byte[1], connection.Token).AsTask();
                await Task.WhenAny(sending, disconnected);
                connection.Cancel();
                try { await Task.WhenAll(sending, disconnected); }
                catch (OperationCanceledException) when (connection.IsCancellationRequested) { }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(2000, token);
            }
        }
    }

    async Task SendChanges(Stream pipe, CancellationToken token)
    {
        GamePresenceFrame? sent = null;
        do
        {
            var next = Volatile.Read(ref current);
            if (next != sent)
            {
                await next.WriteAsync(pipe, token);
                sent = next;
            }
        } while (await changes.Reader.ReadAsync(token));
    }

    public void Dispose()
    {
        stop.Cancel();
        try { worker.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { stop.Dispose(); }
    }
}
