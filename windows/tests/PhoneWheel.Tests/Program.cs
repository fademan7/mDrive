using System.Buffers.Binary;
using System.Security.Cryptography;
using PhoneWheel.Core;

internal static class Program
{
static int Main(string[] args)
{
    if (args.Length == 2 && args[0] == "--model") return WendyModelTests.Run(args[1]).GetAwaiter().GetResult();
    var tests = new (string Name, Action Test)[]
    {
        ("control fixed vector", ControlFixedVector),
        ("Android pairing URI fixture", PairingUri),
        ("all packet codecs", AllPacketCodecs),
        ("invalid packets", InvalidPackets),
        ("xinput independent triggers", IndependentTriggers),
        ("sequence wrap", SequenceWrap),
        ("safety dwell timeout and rearm", SafetyTransitions),
        ("authenticated reconnect releases and rejects replay", AuthenticatedReconnect),
        ("received preview versus safe output", DiagnosticPreview),
        ("haptic no queue and refresh", HapticTransitions),
        ("game rumble expiry and disarm", RumbleTransitions),
        ("F1 2025 synthetic layout", F1Layout),
        ("Wendy telemetry/query/alerts/freshness", WendyTests.Race),
        ("Wendy proactive event thresholds and cooldowns", WendyTests.Alerts),
        ("Wendy OFF/ON socket lifecycle", WendyServiceTests.Run),
        ("Wendy authenticated envelope", WendyTests.Wire),
        ("right stick codec and safety", WendyTests.Look)
        ,("Wendy intent schema and approvals", WendyNextTests.Intent)
        ,("Wendy extended telemetry and conservative patterns", WendyNextTests.Telemetry)
        ,("Wendy pit strategy intents and no game mutation", WendyCoachingTests.PitAndLanguage)
        ,("Wendy first lap reference and second lap coaching", WendyCoachingTests.Laps)
        ,("Wendy following traffic persistence and cooldown", WendyCoachingTests.Traffic)
        ,("Wendy tyre/brake/engine temperature and pressure queries", WendyNextTests.TemperatureQueries)
        ,("Wendy expanded read-only queries and freshness", Wendy052Tests.Queries)
        ,("Wendy race greeting lifecycle and variation", Wendy052Tests.Greetings)
        ,("gamepad output exception recovery and stale mailbox expiry", OutputRecoveryTests.Recovery)
        ,("dedicated controller clocks and failure propagation", OutputRecoveryTests.DedicatedLoops)
        ,("Wendy game weather forecast and same-lap leader gap", Wendy052Tests.ForecastAndLeader)
        ,("controller jitter, packet loss and isolated stale ACK", DropoutTests.JitterAndRejections)
        ,("controller loss and held throttle/steering/brake recovery", DropoutTests.LossAndHeldRecovery)
        ,("controller bounded post-expiry proof recovery and hard-fault isolation", DropoutTests.ProvenFastRecovery)
        ,("controller distinct READY, ACK and output failure transitions", DropoutTests.DistinctFaults)
        ,("controller flight recorder bounded RAM and triggered disk", DropoutTests.Recorder)
        ,("controller UDP independent of saturated shared pool", DropoutTests.ReceiveIsolation)
        ,("Wendy natural phrases, ambiguity and short conversation", WendyNaturalLanguageTests.Phrases)
        ,("Wendy fresh follow-up context and race summary", WendyNaturalLanguageTests.Context)
        ,("Wendy known phrases bypass the CPU model", WendyNaturalLanguageTests.FastPath)
        ,("Wendy authenticated sentence conversation round trip", WendyServiceTests.Conversation)
        ,("Wendy spoken damage, pit guidance and quiet interval radio", Wendy056Tests.DamageAndRadio)
    };
    var failures = 0;
    foreach (var test in tests)
    {
        try { test.Test(); Console.WriteLine($"PASS {test.Name}"); }
        catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
    }
    Console.WriteLine($"{tests.Length - failures}/{tests.Length} groups passed");
    return failures == 0 ? 0 : 1;
}

static readonly byte[] Key = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
static void PairingUri()
{
    Equal("phonewheel://pair?v=1&host=192.168.1.25&port=26760&session=0102030405060708&key=AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8",
        PairingPayload.Create(System.Net.IPAddress.Parse("192.168.1.25"), 26760, Session, Key));
}
const ulong Session = 0x0102030405060708;

static PacketHeader Header(PacketKind kind, uint seq = 0x01020304, uint ack = 0x0A0B0C0D)
    => new(kind, Session, seq, 0x0102030405060708, ack);

static void ControlFixedVector()
{
    var frame = new ControlFrame(Header(PacketKind.Control), new(-0.5f, 1, 0.5f, 0x1000), Pwr1Codec.Ready, 1);
    var packet = Pwr1Codec.EncodeControl(frame, Key);
    var expected = Convert.FromHexString("505752310101140008070605040302010403020108070605040302010D0C0B0A000000BF0000803F0000003F00100E0001000000");
    Equal(68, packet.Length);
    True(packet.AsSpan(0, 52).SequenceEqual(expected));
    Equal(frame, Pwr1Codec.DecodeControl(packet, Key, Session));
}

static void AllPacketCodecs()
{
    var hello = new HelloFrame(Header(PacketKind.Hello));
    Equal(hello, Pwr1Codec.DecodeHello(Pwr1Codec.EncodeHello(hello, Key), Key, Session));
    var status = new StatusFrame(Header(PacketKind.Status), HostState.Active, ReleaseReason.Normal);
    Equal(status, Pwr1Codec.DecodeStatus(Pwr1Codec.EncodeStatus(status, Key), Key, Session));
    var haptic = new HapticFrame(Header(PacketKind.Haptic), HapticEvent.Lock, 3, 100);
    Equal(haptic, Pwr1Codec.DecodeHaptic(Pwr1Codec.EncodeHaptic(haptic, Key), Key, Session));
    Equal(48, Pwr1Codec.EncodeHello(hello, Key).Length);
    Equal(52, Pwr1Codec.EncodeStatus(status, Key).Length);
    Equal(56, Pwr1Codec.EncodeHaptic(haptic, Key).Length);
}

static void InvalidPackets()
{
    var packet = Pwr1Codec.EncodeControl(new(Header(PacketKind.Control), new(-0.5f, 1, 0.5f), Pwr1Codec.Ready, 1), Key);
    for (var i = 0; i < packet.Length * 8; i++)
    {
        var changed = packet.ToArray(); changed[i / 8] ^= (byte)(1 << (i % 8));
        Throws(() => Pwr1Codec.DecodeControl(changed, Key, Session));
    }
    for (var i = 0; i < packet.Length; i++)
    {
        var truncated = packet[..i];
        Throws(() => Pwr1Codec.DecodeControl(truncated, Key, Session));
    }
    var nan = packet.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(nan.AsSpan(32), BitConverter.SingleToInt32Bits(float.NaN));
    Resign(nan);
    Throws(() => Pwr1Codec.DecodeControl(nan, Key, Session));
}

static void IndependentTriggers()
{
    Equal((short)-32768, new Controls(-1, 1, 0.5f).StickX);
    Equal((byte)128, new Controls(-1, 1, 0.5f).LeftTrigger);
    Equal((byte)255, new Controls(-1, 1, 0.5f).RightTrigger);
    var both = new Controls(1, 1, 1);
    Equal((byte)255, both.LeftTrigger); Equal((byte)255, both.RightTrigger);
}

static void SequenceWrap()
{
    True(SafetyGate.IsNewer(0, uint.MaxValue));
    False(SafetyGate.IsNewer(9, 9)); False(SafetyGate.IsNewer(8, 9));
    False(SafetyGate.IsNewer(0x80000000, 0));
}

static void SafetyTransitions()
{
    var gate = new SafetyGate(Session, Key); uint seq = 0;
    var hello = Pwr1Codec.EncodeHello(new(new(PacketKind.Hello, Session, ++seq, 1, 0)), Key);
    True(gate.AcceptInitialHello(hello)); False(gate.AcceptInitialHello(hello)); Equal(0u, gate.LastAcceptedSequence);
    bool Send(double now, Controls c = default, bool arm = false, bool ready = true, uint epoch = 1)
    {
        seq++; gate.NoteServerSend(seq, now - 1);
        var flags = (ushort)((ready ? Pwr1Codec.Ready : 0) | (arm ? Pwr1Codec.Arm : 0));
        return gate.Ingest(Pwr1Codec.EncodeControl(new(new(PacketKind.Control, Session, seq, 1, seq), c, flags, epoch), Key), now);
    }
    foreach (var t in new[] { 0d, 50, 100, 150, 200, 250, 300 }) True(Send(t));
    True(Send(310, arm: true)); True(gate.Armed);
    True(Send(320, new(0.5f, 1, 1), arm: true)); Equal(1f, gate.Output.Throttle);
    Equal(1f, gate.Tick(469).Throttle); Equal(Controls.Neutral, gate.Tick(470)); False(gate.Armed);
    Equal(1L, gate.GetDiagnostics(470).ReleaseCount); Equal(ReleaseReason.Timeout, gate.GetDiagnostics(470).LastRelease);
    True(Send(480, new(0, 1, 0), arm: true)); False(gate.Armed);
    foreach (var t in new[] { 500d, 550, 600, 650, 700, 750, 800 }) True(Send(t));
    True(Send(810, arm: true)); True(gate.Armed);
    gate.OutputFailed(); False(gate.Armed); Equal(Controls.Neutral, gate.Output);
    Equal(ReleaseReason.OutputError, gate.GetDiagnostics(811).LastRelease);
    True(Send(815, new(0, 1, 0), arm: true)); False(gate.Armed);
    True(Send(820, arm: true, epoch: 2)); False(gate.Armed);
    foreach (var t in new[] { 830d, 880, 930, 980, 1030, 1080, 1130 }) True(Send(t, epoch: 2));
    True(Send(1140, arm: true, epoch: 2)); True(gate.Armed);
}

static void AuthenticatedReconnect()
{
    var gate = new SafetyGate(Session, Key); uint seq = 0;
    byte[] Hello(uint n) => Pwr1Codec.EncodeHello(new(new(PacketKind.Hello, Session, n, 1, 0)), Key);
    bool Send(double now, bool arm = false, float throttle = 0) {
        seq++; gate.NoteServerSend(seq, now);
        return gate.Ingest(Pwr1Codec.EncodeControl(new(new(PacketKind.Control, Session, seq, 1, seq),
            new(0, throttle, 0), (ushort)(Pwr1Codec.Ready | (arm ? Pwr1Codec.Arm : 0)), 1), Key), now);
    }
    True(gate.AcceptInitialHello(Hello(++seq)));
    for (var t = 0; t <= 300; t += 50) True(Send(t));
    True(Send(310, true)); True(gate.Armed);
    True(Send(320, true, 1));
    var reconnect = Hello(++seq);
    var tampered = reconnect.ToArray(); tampered[^1] ^= 1;
    False(gate.AcceptInitialHello(tampered, true)); True(gate.Armed);
    True(gate.AcceptInitialHello(reconnect, true)); False(gate.Armed); Equal(Controls.Neutral, gate.Output);
    False(gate.AcceptInitialHello(reconnect, true));
    True(Send(330, true, 1)); False(gate.Armed);
    for (var t = 350; t <= 650; t += 50) True(Send(t));
    True(Send(660, true)); True(gate.Armed);
}

static void DiagnosticPreview()
{
    var gate = new SafetyGate(Session, Key);
    False(gate.GetDiagnostics(0).HasRecentInput);
    gate.NoteServerSend(10, 0);
    var packet = Pwr1Codec.EncodeControl(new(new(PacketKind.Control, Session, 2, 1, 10),
        new(0, .3f, .2f), Pwr1Codec.Ready, 1), Key);
    True(gate.Ingest(packet, 1));
    var d = gate.GetDiagnostics(2);
    True(d.HasRecentInput); Equal(.3f, d.Received.Throttle); Equal(.2f, d.Received.Brake);
    Equal(Controls.Neutral, d.Output); False(d.Armed);
    packet[^1] ^= 1;
    False(gate.Ingest(packet, 3));
    Equal(.3f, gate.GetDiagnostics(3).Received.Throttle);
    d = gate.GetDiagnostics(151);
    False(d.HasRecentInput); Equal(Controls.Neutral, d.Received); Equal(Controls.Neutral, d.Output);
    Equal(ReleaseReason.Timeout, d.Reason);
}

static void HapticTransitions()
{
    var a = new HapticArbiter([HapticEvent.Lock, HapticEvent.Spin, HapticEvent.Curb]);
    Equal("started", a.Offer(HapticEvent.Curb, 1, 0));
    Equal("started", a.Offer(HapticEvent.Lock, 3, 10));
    Equal("dropped", a.Offer(HapticEvent.Curb, 1, 20));
    var started = a.PatternStartedMs;
    Equal("refreshed", a.Offer(HapticEvent.Lock, 2, 30)); Equal(started, a.PatternStartedMs);
    Equal(HapticEvent.Stop, a.Tick(130));
}

static void RumbleTransitions()
{
    var r = new RumbleMailbox();
    r.Update(85, 0, 0); Equal((byte)1, r.ReadLevel(0, true));
    r.Update(0, 170, 1); Equal((byte)2, r.ReadLevel(1, true));
    r.Update(255, 0, 2); Equal((byte)3, r.ReadLevel(501, true));
    Equal((byte)0, r.ReadLevel(502, true));
    r.Update(255, 0, 503); Equal((byte)0, r.ReadLevel(504, false));
    Equal((byte)0, r.ReadLevel(505, true));
    r.Update(255, 0, 506); r.Update(0, 0, 507); Equal((byte)0, r.ReadLevel(507, true));
    var haptic = new HapticFrame(Header(PacketKind.Haptic), HapticEvent.GamepadRumble, 3, 100);
    Equal(haptic, Pwr1Codec.DecodeHaptic(Pwr1Codec.EncodeHaptic(haptic, Key), Key, Session));
}

static void F1Layout()
{
    var packet = new byte[273];
    Convert.FromHexString("E907190100010D01000000000000000000803F020000000300000000FF").CopyTo(packet, 0);
    WriteFloats(packet, 77, 11, 22, 33, 44); WriteFloats(packet, 93, -0.5f, 0.25f, -1, 0);
    var parsed = F1MotionEx2025Parser.Parse(packet);
    Equal(33f, parsed.WheelSpeedRaw["FL"]); Equal(-0.5f, parsed.SlipRatioRaw["RL"]);
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 2026); Throws(() => F1MotionEx2025Parser.Parse(packet));
}

static void WriteFloats(byte[] data, int offset, params float[] values)
{
    foreach (var value in values) { BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), BitConverter.SingleToInt32Bits(value)); offset += 4; }
}
static void Resign(byte[] packet)
{
    var tag = HMACSHA256.HashData(Key, packet.AsSpan(0, packet.Length - 16)); tag.AsSpan(0, 16).CopyTo(packet.AsSpan(packet.Length - 16));
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}");
}
static void True(bool value) { if (!value) throw new Exception("expected true"); }
static void False(bool value) { if (value) throw new Exception("expected false"); }
static void Throws(Action action) { try { action(); } catch (ProtocolException) { return; } throw new Exception("expected ProtocolException"); }
}
