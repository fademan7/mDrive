using System.Buffers.Binary;

namespace PhoneWheel.Core;

public sealed record F1MotionEx2025(ulong SessionUid, uint Frame, uint OverallFrame, byte Player,
    IReadOnlyDictionary<string, float> WheelSpeedRaw, IReadOnlyDictionary<string, float> SlipRatioRaw);

public static class F1MotionEx2025Parser
{
    private static readonly string[] Wheels = ["RL", "RR", "FL", "FR"];
    public static F1MotionEx2025 Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != 273) throw new ProtocolException("Unexpected F1 2025 Motion Ex length.");
        var format = BinaryPrimitives.ReadUInt16LittleEndian(data);
        var version = data[5];
        var packetId = data[6];
        var player = data[27];
        var secondary = data[28];
        if (format != 2025 || version != 1 || packetId != 13 || player >= 22 || (secondary != 255 && secondary >= 22))
            throw new ProtocolException("Unsupported F1 format/version/id/player.");
        var sessionTime = ReadFloat(data, 15);
        if (!float.IsFinite(sessionTime) || sessionTime < 0) throw new ProtocolException("Invalid F1 session time.");
        var speeds = new Dictionary<string, float>(4);
        var slips = new Dictionary<string, float>(4);
        for (var i = 0; i < 4; i++)
        {
            var speed = ReadFloat(data, 77 + i * 4);
            var slip = ReadFloat(data, 93 + i * 4);
            if (!float.IsFinite(speed) || !float.IsFinite(slip)) throw new ProtocolException("Non-finite F1 telemetry.");
            speeds[Wheels[i]] = speed; slips[Wheels[i]] = slip;
        }
        return new(BinaryPrimitives.ReadUInt64LittleEndian(data[7..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[19..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[23..]), player, speeds, slips);
    }
    private static float ReadFloat(ReadOnlySpan<byte> data, int offset)
        => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data[offset..]));
}
