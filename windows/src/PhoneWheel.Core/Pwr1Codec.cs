using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PhoneWheel.Core;

public enum PacketKind : byte { Hello = 0, Control = 1, Haptic = 2, Status = 3, ControlLook = 4 }
public enum HostState : byte { Released = 0, Active = 1 }
public enum ReleaseReason : byte
{
    Normal = 0, Timeout = 1, Sensor = 2, Inactive = 3, Calibration = 4,
    User = 5, Session = 6, OutputError = 7, Permission = 8, Recovering = 9
}
public enum HapticEvent : byte
{
    Stop = 0, Lock = 1, Spin = 2, Grip = 3, Collision = 4, Curb = 5, Shift = 6, Engine = 7,
    GamepadRumble = 8
}

public readonly record struct PacketHeader(PacketKind Kind, ulong Session, uint Sequence, ulong SentMonotonicUs, uint AckSequence);
public readonly record struct HelloFrame(PacketHeader Header);
public readonly record struct ControlFrame(PacketHeader Header, Controls Controls, ushort Flags, uint CalibrationEpoch);
public readonly record struct StatusFrame(PacketHeader Header, HostState State, ReleaseReason Reason);
public readonly record struct HapticFrame(PacketHeader Header, HapticEvent Event, byte Level, ushort LeaseMs);

public static class Pwr1Codec
{
    public const byte Version = 1;
    public const int HeaderSize = 32;
    public const int TagSize = 16;
    public const ushort Arm = 0x1;
    public const ushort Ready = 0xE;
    public const ushort FastRecovery = 0x10;
    public const ushort AllFlags = 0x1F;

    public static byte[] EncodeHello(HelloFrame frame, ReadOnlySpan<byte> key) => Encode(frame.Header, ReadOnlySpan<byte>.Empty, key);

    public static byte[] EncodeControl(ControlFrame frame, ReadOnlySpan<byte> key)
    {
        frame.Controls.Validate();
        if ((frame.Flags & ~AllFlags) != 0) throw new ProtocolException("Reserved control flags are set.");
        var look = frame.Header.Kind == PacketKind.ControlLook;
        if (!look && (frame.Controls.LookX != 0 || frame.Controls.LookY != 0)) throw new ProtocolException("Right stick requires ControlLook.");
        Span<byte> payload = stackalloc byte[look ? 28 : 20];
        WriteFloat(payload, 0, frame.Controls.Steer);
        WriteFloat(payload, 4, frame.Controls.Throttle);
        WriteFloat(payload, 8, frame.Controls.Brake);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[12..], frame.Controls.Buttons);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[14..], frame.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[16..], frame.CalibrationEpoch);
        if (look) { WriteFloat(payload, 20, frame.Controls.LookX); WriteFloat(payload, 24, frame.Controls.LookY); }
        return Encode(frame.Header, payload, key);
    }

    public static byte[] EncodeStatus(StatusFrame frame, ReadOnlySpan<byte> key)
    {
        if (!Enum.IsDefined(frame.State) || !Enum.IsDefined(frame.Reason)) throw new ProtocolException("Unknown status value.");
        Span<byte> payload = stackalloc byte[4];
        payload[0] = (byte)frame.State;
        payload[1] = (byte)frame.Reason;
        return Encode(frame.Header, payload, key);
    }

    public static byte[] EncodeHaptic(HapticFrame frame, ReadOnlySpan<byte> key)
    {
        ValidateHaptic(frame.Event, frame.Level, frame.LeaseMs);
        Span<byte> payload = stackalloc byte[8];
        payload[0] = (byte)frame.Event;
        payload[1] = frame.Level;
        BinaryPrimitives.WriteUInt16LittleEndian(payload[2..], frame.LeaseMs);
        return Encode(frame.Header, payload, key);
    }

    public static HelloFrame DecodeHello(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, ulong session)
        => new(DecodeAndAuthenticate(packet, key, session, PacketKind.Hello, 0).Header);

    public static ControlFrame DecodeControl(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, ulong session)
    {
        var look = packet.Length > 5 && packet[5] == (byte)PacketKind.ControlLook;
        var parsed = DecodeAndAuthenticate(packet, key, session, look ? PacketKind.ControlLook : PacketKind.Control, look ? 28 : 20);
        var p = parsed.Payload;
        var controls = new Controls(ReadFloat(p, 0), ReadFloat(p, 4), ReadFloat(p, 8), BinaryPrimitives.ReadUInt16LittleEndian(p[12..]), look ? ReadFloat(p, 20) : 0, look ? ReadFloat(p, 24) : 0);
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(p[14..]);
        if ((flags & ~AllFlags) != 0) throw new ProtocolException("Reserved control flags are set.");
        controls.Validate();
        return new(parsed.Header, controls, flags, BinaryPrimitives.ReadUInt32LittleEndian(p[16..]));
    }

    public static StatusFrame DecodeStatus(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, ulong session)
    {
        var parsed = DecodeAndAuthenticate(packet, key, session, PacketKind.Status, 4);
        if (parsed.Payload[2] != 0 || parsed.Payload[3] != 0) throw new ProtocolException("Status reserved field is nonzero.");
        var state = (HostState)parsed.Payload[0];
        var reason = (ReleaseReason)parsed.Payload[1];
        if (!Enum.IsDefined(state) || !Enum.IsDefined(reason)) throw new ProtocolException("Unknown status value.");
        return new(parsed.Header, state, reason);
    }

    public static HapticFrame DecodeHaptic(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, ulong session)
    {
        var parsed = DecodeAndAuthenticate(packet, key, session, PacketKind.Haptic, 8);
        if (BinaryPrimitives.ReadUInt32LittleEndian(parsed.Payload[4..]) != 0) throw new ProtocolException("Haptic reserved field is nonzero.");
        var evt = (HapticEvent)parsed.Payload[0];
        var level = parsed.Payload[1];
        var lease = BinaryPrimitives.ReadUInt16LittleEndian(parsed.Payload[2..]);
        ValidateHaptic(evt, level, lease);
        return new(parsed.Header, evt, level, lease);
    }

    public static PacketHeader PeekAuthenticatedHeader(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, ulong session)
    {
        if (packet.Length < HeaderSize + TagSize) throw new ProtocolException("Datagram is too short.");
        var kind = (PacketKind)packet[5];
        var payloadLength = kind switch
        {
            PacketKind.Hello => 0,
            PacketKind.Control => 20,
            PacketKind.ControlLook => 28,
            PacketKind.Haptic => 8,
            PacketKind.Status => 4,
            _ => throw new ProtocolException("Unknown packet kind.")
        };
        return DecodeAndAuthenticate(packet, key, session, kind, payloadLength).Header;
    }

    private static byte[] Encode(PacketHeader header, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> key)
    {
        ValidateKey(key);
        if (!Enum.IsDefined(header.Kind)) throw new ProtocolException("Unknown packet kind.");
        var data = new byte[HeaderSize + payload.Length + TagSize];
        "PWR1"u8.CopyTo(data);
        data[4] = Version;
        data[5] = (byte)header.Kind;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), checked((ushort)payload.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), header.Session);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), header.Sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(20), header.SentMonotonicUs);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(28), header.AckSequence);
        payload.CopyTo(data.AsSpan(HeaderSize));
        var tag = HMACSHA256.HashData(key, data.AsSpan(0, HeaderSize + payload.Length));
        tag.AsSpan(0, TagSize).CopyTo(data.AsSpan(HeaderSize + payload.Length));
        return data;
    }

    private sealed record ParsedPacket(PacketHeader Header, byte[] Payload);

    private static ParsedPacket DecodeAndAuthenticate(
        ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key, ulong session, PacketKind expectedKind, int expectedPayload)
    {
        ValidateKey(key);
        var expectedSize = HeaderSize + expectedPayload + TagSize;
        if (packet.Length != expectedSize) throw new ProtocolException("Unexpected datagram size.");
        var body = packet[..^TagSize];
        var actualTag = packet[^TagSize..];
        Span<byte> fullTag = stackalloc byte[32];
        HMACSHA256.HashData(key, body, fullTag);
        if (!CryptographicOperations.FixedTimeEquals(actualTag, fullTag[..TagSize])) throw new ProtocolException("Authentication failed.");
        if (!packet[..4].SequenceEqual("PWR1"u8) || packet[4] != Version || packet[5] != (byte)expectedKind)
            throw new ProtocolException("Unknown protocol, version, or packet kind.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(packet[6..]) != expectedPayload) throw new ProtocolException("Unexpected payload length.");
        var packetSession = BinaryPrimitives.ReadUInt64LittleEndian(packet[8..]);
        if (packetSession != session) throw new ProtocolException("Wrong session.");
        var header = new PacketHeader(expectedKind, packetSession,
            BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(packet[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[28..]));
        return new ParsedPacket(header, packet.Slice(HeaderSize, expectedPayload).ToArray());
    }

    private static void ValidateHaptic(HapticEvent evt, byte level, ushort lease)
    {
        if (!Enum.IsDefined(evt)) throw new ProtocolException("Unknown haptic event.");
        if (evt == HapticEvent.Stop)
        {
            if (level != 0 || lease != 0) throw new ProtocolException("Stop haptic must have zero level and lease.");
        }
        else if (level is < 1 or > 3 || lease is < 1 or > 150)
            throw new ProtocolException("Invalid haptic level or lease.");
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new ProtocolException("PWR1 key must be 32 bytes.");
    }

    private static void WriteFloat(Span<byte> destination, int offset, float value)
        => BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], BitConverter.SingleToInt32Bits(value));
    private static float ReadFloat(ReadOnlySpan<byte> source, int offset)
        => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
}
