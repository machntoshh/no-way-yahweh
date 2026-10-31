using System.Buffers.Binary;
using System.Text.Json;

namespace NoWayYahweh.Core;

/// <summary>Native Messaging framing: 4-byte little-endian length, then UTF-8 JSON.</summary>
public static class NativeMessaging
{
    /// <summary>Our extension never sends more than a few hundred bytes; anything larger is refused.</summary>
    public const int MaxIncomingBytes = 64 * 1024;

    /// <summary>Browser limit for host → extension messages.</summary>
    public const int MaxOutgoingBytes = 1024 * 1024;

    /// <summary>Returns null on a clean end of stream (the browser closed the connection).</summary>
    public static byte[]? Read(Stream input)
    {
        Span<byte> header = stackalloc byte[4];
        var read = ReadFully(input, header);
        if (read == 0) return null;
        if (read < 4) throw new InvalidDataException("Cabeçalho incompleto.");

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length > MaxIncomingBytes) throw new InvalidDataException($"Mensagem grande demais ({length} bytes).");

        var body = new byte[length];
        if (ReadFully(input, body) < length) throw new InvalidDataException("Mensagem incompleta.");
        return body;
    }

    public static void Write(Stream output, object message)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        if (body.Length > MaxOutgoingBytes) throw new InvalidOperationException("Resposta excede 1 MB.");
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)body.Length);
        output.Write(header);
        output.Write(body);
        output.Flush();
    }

    private static int ReadFully(Stream input, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = input.Read(buffer[total..]);
            if (n == 0) break;
            total += n;
        }
        return total;
    }
}
