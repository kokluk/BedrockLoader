using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KoklukBedrockModloader;

// Shared by the optional in-game producer and the launcher. Each frame is a
// complete replacement so joining another world never inherits the last name.
internal sealed record GamePresenceFrame(string Kind, string? Dimension = null, string? World = null, string? Server = null,
    [property: System.Text.Json.Serialization.JsonRequired] int Protocol = 1)
{
    internal static readonly GamePresenceFrame Unknown = new("unknown");
    internal static string PipeName(int pid) => $@"LOCAL\BedrockLoader.Presence.v1.{pid}";
    const int MaxBytes = 4096;

    internal async Task WriteAsync(Stream stream, CancellationToken token)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(this);
        if (data.Length > MaxBytes) throw new InvalidDataException("Oversized activity");
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(data, token);
    }

    internal static async Task<GamePresenceFrame> ReadAsync(Stream stream, CancellationToken token)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        int count = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (count is <= 0 or > MaxBytes) throw new InvalidDataException("Invalid activity size");
        byte[] bytes = new byte[count];
        await stream.ReadExactlyAsync(bytes, token);
        var frame = JsonSerializer.Deserialize<GamePresenceFrame>(bytes);
        // Don't silently interpret a future or unversioned producer as v1.
        if (frame is not { Protocol: 1 })
            throw new InvalidDataException("Unsupported activity protocol");
        return frame.Kind switch
        {
            "menu" or "loading" => new(frame.Kind),
            "local" or "solo" or "world" or "server" or "realm" => frame with
            {
                Dimension = frame.Dimension is "Overworld" or "Nether" or "End" ? frame.Dimension : null,
                World = frame.Kind is "local" or "solo" ? Text(frame.World, 64) : null,
                Server = frame.Kind is "server" or "realm" ? Text(frame.Server, 64) : null
            },
            _ => Unknown
        };
    }

    internal static string? Text(string? value, int byteLimit)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = new StringBuilder();
        int bytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator) continue;
            if (bytes + rune.Utf8SequenceLength > byteLimit) break;
            result.Append(rune);
            bytes += rune.Utf8SequenceLength;
        }
        var text = result.ToString().Trim();
        return text.Length == 0 ? null : text;
    }
}
