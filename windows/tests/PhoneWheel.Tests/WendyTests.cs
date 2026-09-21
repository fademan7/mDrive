using System.Buffers.Binary;
using System.Text;
using PhoneWheel.Core;

internal static class WendyTests
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    static byte[] Packet(int id, uint frame = 1)
    {
        var p = new byte[id switch { 1 => 753, 2 => 1285, 3 => 45, 7 => 1239, 10 => 1041, _ => throw new Exception() }];
        BinaryPrimitives.WriteUInt16LittleEndian(p, 2025); p[2] = 25; p[5] = 1; p[6] = (byte)id;
        BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7), 1); p[27] = 2; p[28] = 255;
        BinaryPrimitives.WriteSingleLittleEndian(p.AsSpan(15), 10); BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23), frame);
        return p;
    }
    static void Float(byte[] p, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(p.AsSpan(offset), value);
    public static void Race()
    {
        var race = new F1RaceState(); var wendy = new WendyEngineer();
        Check(!race.Connected(0), "empty not connected");
        var session = Packet(1); session[29] = 3; Check(race.Ingest(session, 0), "session");
        var status = Packet(7); var s = 29 + 2 * 55;
        status[s + 3] = 54; status[s + 28] = 3; Float(status, s + 5, 21); Float(status, s + 13, -1.5f); Float(status, s + 37, 2_000_000);
        Check(race.Ingest(status, 0), "status");
        var damage = Packet(10); var d = 29 + 2 * 46;
        Float(damage, d + 8, 42); damage[d + 28] = 15; Check(race.Ingest(damage, 0), "damage");
        var lap = Packet(2); var l = 29 + 2 * 57;
        lap[l + 32] = 2; lap[l + 33] = 5; lap[l + 45] = 2; BinaryPrimitives.WriteUInt16LittleEndian(lap.AsSpan(l + 14), 1800);
        var a = 29; lap[a + 32] = 1; lap[a + 33] = 5; lap[a + 45] = 2;
        var b = 29 + 57; lap[b + 32] = 3; lap[b + 33] = 5; lap[b + 45] = 2; lap[b + 16] = 1; BinaryPrimitives.WriteUInt16LittleEndian(lap.AsSpan(b + 14), 2500);
        Check(race.Ingest(lap, 0), "lap"); Check(race.Ahead == 1.8 && race.Behind == 62.5, "minute gaps / actual following car");
        var expected = new Dictionary<string, string> {
            ["How are my tyres?"] = "Front left wear is 42", ["What's the gap ahead?"] = "1.8 seconds", ["What's the gap behind?"] = "62.5 seconds",
            ["How much fuel do I have?"] = "MFD fuel estimate is -1.5", ["What's my ERS?"] = "2.0 megajoules", ["Any damage?"] = "15 percent",
            ["What's the weather?"] = "light rain", ["What flag is out?"] = "Yellow flag", ["What lap am I on?"] = "lap 5", ["What's my position?"] = "P 2"
        };
        foreach (var (query, result) in expected) Check(wendy.Answer(query, 1, race, 100).Text.Contains(result), query);
        Check(wendy.Alert(race, 100)?.Text == "Yellow flag.", "flag alert priority"); Check(wendy.Alert(race, 200) == null, "global cooldown");
        Check(wendy.Answer("Set brake bias to 54", 1, race, 100).Kind == "unsupported", "no unsafe injection");
        Check(wendy.Answer("Set brake bias to 54 or 55", 1, race, 100).Kind == "rejected", "ambiguous number");
        Check(wendy.Answer("Box this lap", -1, race, 100).Kind == "rejected", "unknown confidence");
        Check(wendy.Answer("Box this lap", .99, race, 100).Text.Contains("cannot send"), "pit request not falsely confirmed");
        Check(wendy.Answer("What lap am I on?", 1, race, 3001).Text.Contains("unavailable"), "stale guard");
        Check(race.Flag(3001) == "UNKNOWN", "never stale green");
        var invalid = (byte[])damage.Clone(); Float(invalid, d, float.NaN); Check(!race.Ingest(invalid, 1), "NaN");
        Check(!race.Ingest(damage[..^1], 1), "truncated packet");
        invalid = (byte[])status.Clone(); invalid[5] = 2; Check(!race.Ingest(invalid, 1), "packet version");
        invalid = (byte[])status.Clone(); BinaryPrimitives.WriteUInt16LittleEndian(invalid, 2026); Check(!race.Ingest(invalid, 1), "unsupported 2026 format");
        Check(!race.Ingest(status, 1500), "replay cannot refresh"); Check(race.StatusMs == 0, "freshness unchanged");
        var evt = Packet(3); Encoding.ASCII.GetBytes("RDFL").CopyTo(evt, 29); Check(race.Ingest(evt, 500), "red event"); Check(race.Flag(500) == "RED", "red priority");
        Encoding.ASCII.GetBytes("CHQF").CopyTo(evt, 29); Check(race.Ingest(evt, 500), "two event codes one frame"); Check(race.Flag(500) == "CHECKERED", "checkered");
        Check(!race.Ingest(evt, 501), "event duplicate");
        var newer = Packet(1, 2); newer[29 + 124] = 2; BinaryPrimitives.WriteUInt64LittleEndian(newer.AsSpan(7), 2);
        Check(race.Ingest(newer, 1000), "new session"); Check(race.Flag(1000) == "VSC" && !F1RaceState.Fresh(race.DamageMs, 1000), "session clears stale data");
        var paused = (byte[])newer.Clone(); paused[29 + 14] = 1; BinaryPrimitives.WriteUInt32LittleEndian(paused.AsSpan(23), 3);
        Check(race.Ingest(paused, 1001) && !race.Live(1001), "pause guard");
    }
    public static void Wire()
    {
        var nonce = Enumerable.Repeat((byte)7, 32).ToArray(); var json = Encoding.UTF8.GetBytes("{\"state\":\"IDLE\"}");
        var encoded = WendyWire.Encode(json, Key, nonce, 0, 1);
        Check(Convert.ToHexString(encoded).ToLowerInvariant() == "00000000000000017b227374617465223a2249444c45227d7824d12b9ccd917168d40c8ee8da1b39ade0950145ac4cfe64bb6db754ae9ea6", "independent Node crypto fixture");
        Check(WendyWire.Decode(encoded, Key, nonce, 0, 1).SequenceEqual(json), "decode");
        void Reject(Action action) { try { action(); } catch (ProtocolException) { return; } throw new Exception("accepted invalid wire"); }
        Reject(() => WendyWire.Decode(encoded, Key, nonce, 0, 2)); Reject(() => WendyWire.Decode(encoded, Key, nonce, 1, 1));
        Reject(() => WendyWire.Decode(encoded, Key, new byte[32], 0, 1)); encoded[10] ^= 1; Reject(() => WendyWire.Decode(encoded, Key, nonce, 0, 1));
        Reject(() => WendyWire.Encode(new byte[2049], Key, nonce, 0, 1));
    }
    public static void Alerts()
    {
        var race = new F1RaceState(); var engineer = new WendyEngineer(); uint frame = 0;
        var session = Packet(1); var status = Packet(7); var damage = Packet(10); var lap = Packet(2);
        var s = 29 + 2 * 55; var d = 29 + 2 * 46; var l = 29 + 2 * 57;
        status[s + 28] = 1; Float(status, s + 5, 20); lap[l + 45] = 2;
        void Feed(double now) { frame++; foreach (var p in new[] { session, status, damage, lap }) { BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23), frame); Check(race.Ingest(p, now), "feed"); } }
        Feed(0); Check(engineer.Alert(race, 0) == null, "quiet healthy baseline");
        Float(damage, d + 8, 42); Feed(100); Check(engineer.Alert(race, 100)?.Text.Contains("above 40") == true, "wear crossing");
        Feed(9000); Check(engineer.Alert(race, 9000) == null, "wear doesn't repeat after global cooldown");
        damage[d + 28] = 15; Feed(10000); Check(engineer.Alert(race, 10000)?.Text == "Front wing damage detected.", "damage crossing");
        Feed(19000); Check(engineer.Alert(race, 19000) == null, "damage doesn't repeat");
        Float(status, s + 5, 2); Feed(20000); Check(engineer.Alert(race, 20000)?.Text.StartsWith("Low fuel") == true, "fuel threshold");
        session[29] = 4; Feed(29000); Check(engineer.Alert(race, 29000)?.Text.Contains("heavy rain") == true, "weather transition");
        lap[l + 34] = 2; Feed(38000); Check(engineer.Alert(race, 38000)?.Text.Contains("pit lane") == true, "pit entry");
        Feed(47000); Check(engineer.Alert(race, 47000) == null, "no repeated announcements");
        session[29 + 124] = 1; Feed(48000); Check(engineer.Alert(race, 48000)?.Text == "Safety Car deployed.", "SC");
        session[29 + 124] = 2; Feed(57000); Check(engineer.Alert(race, 57000)?.Text == "Virtual Safety Car deployed.", "VSC");
        var evt = Packet(3, ++frame); Encoding.ASCII.GetBytes("RDFL").CopyTo(evt, 29); Check(race.Ingest(evt, 57100), "red");
        Check(engineer.Alert(race, 57100) == null, "red visible but speech cooldown"); Check(race.Flag(57100) == "RED", "UI flag immediate");
        Feed(66000); Check(engineer.Alert(race, 66000)?.Text == "Red flag.", "red speech");
        Check(engineer.Alert(race, 70000) == null, "stale telemetry cannot speak");
        var newSession = Packet(1, ++frame); Check(race.Ingest(newSession, 70001), "fresh session only");
        Check(engineer.Answer("How are my tyres?", 1, race, 70001).Text.Contains("unavailable"), "individual field expiry");
    }
    public static void Look()
    {
        var h = new PacketHeader(PacketKind.ControlLook, 1, 1, 1, 1);
        var c = new Controls(.5f, .3f, .7f, 0x1000, -1, .25f);
        var p = Pwr1Codec.EncodeControl(new(h, c, 0xE, 1), Key);
        Check(p.Length == 76 && Pwr1Codec.DecodeControl(p, Key, 1).Controls == c, "look roundtrip");
        Check(!new Controls(LookX: .1f).IsNeutral, "held right stick blocks rearm");
        var gate = new SafetyGate(1, Key);
        for (uint seq = 1; seq <= 5; seq++) { gate.NoteServerSend(seq, seq * 100); gate.Ingest(Pwr1Codec.EncodeControl(new(h with { Sequence = seq, AckSequence = seq }, Controls.Neutral, 0xE, 1), Key), seq * 100); }
        gate.NoteServerSend(6, 501); gate.Ingest(Pwr1Codec.EncodeControl(new(h with { Sequence = 6, AckSequence = 6 }, Controls.Neutral, 0xF, 1), Key), 501);
        gate.NoteServerSend(7, 502); gate.Ingest(Pwr1Codec.EncodeControl(new(h with { Sequence = 7, AckSequence = 7 }, c, 0xF, 1), Key), 502);
        Check(gate.Output == c, "all axes active"); Check(gate.Tick(652) == Controls.Neutral, "all axes timeout neutral");
    }
}
