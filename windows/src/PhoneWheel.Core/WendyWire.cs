using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PhoneWheel.Core;

// WDY1 is separate from PWR1. Per-connection random challenge prevents replay
// after reconnect; independent directional counters prevent replay in a stream.
public static class WendyWire
{
    public const int MaxJson = 2048;
    public static byte[] Encode(byte[] json, byte[] key, byte[] nonce, byte direction, ulong sequence)
    {
        if (json.Length is < 2 or > MaxJson || key.Length != 32 || nonce.Length != 32 || direction > 1 || sequence == 0) throw new ProtocolException("Invalid Wendy envelope.");
        var body = new byte[8 + json.Length];
        BinaryPrimitives.WriteUInt64BigEndian(body, sequence); json.CopyTo(body, 8);
        return [..body, ..Tag(body, key, nonce, direction)];
    }
    public static byte[] Decode(byte[] packet, byte[] key, byte[] nonce, byte direction, ulong expected)
    {
        if (packet.Length is < 42 or > MaxJson + 40 || key.Length != 32 || nonce.Length != 32 || direction > 1 || expected == 0) throw new ProtocolException("Invalid Wendy frame.");
        if (!CryptographicOperations.FixedTimeEquals(packet.AsSpan(packet.Length - 32), Tag(packet[..^32], key, nonce, direction)) || BinaryPrimitives.ReadUInt64BigEndian(packet) != expected)
            throw new ProtocolException("Wendy authentication or sequence failed.");
        return packet[8..^32];
    }
    private static byte[] Tag(byte[] body, byte[] key, byte[] nonce, byte direction) => HMACSHA256.HashData(key, (byte[])[.."WDY1"u8, ..nonce, direction, ..body]);
}
