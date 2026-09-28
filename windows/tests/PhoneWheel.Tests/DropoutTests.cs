using PhoneWheel.Core;
using System.Diagnostics;
using System.Text.Json;

internal static class DropoutTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static void Check(bool b, string why) { if (!b) throw new Exception(why); }
    private sealed class Link {
        public readonly SafetyGate Gate;
        public uint Seq, Ack;
        public double Now;
        public Link(ControllerFlightRecorder? recorder = null) { Gate = new(42, Key, flight: recorder); }
        public byte[] Frame(Controls controls, ushort flags = 15) => Pwr1Codec.EncodeControl(
            new(new(PacketKind.Control, 42, ++Seq, (ulong)(Now * 1000), Ack), controls, flags, 1), Key);
        public bool Send(double time, Controls c, ushort flags = 15, bool challenge = true) {
            Now = time; if (challenge) Gate.NoteServerSend(++Ack, time);
            return Gate.Ingest(Frame(c, flags), time);
        }
        public void Arm() { Send(0, Controls.Neutral, 14); Send(100, Controls.Neutral, 14); Send(200, Controls.Neutral, 14); Send(300, Controls.Neutral, 14); Send(310, Controls.Neutral); Check(Gate.Armed, "arm"); }
    }
    public static void JitterAndRejections() {
        foreach (var held in new[] { new Controls(.8f, .7f, .3f), new Controls(-.8f, 0, 0) }) {
            var x = new Link(); x.Arm();
            // Normal stream with 1-2 lost frames, 20/40/80ms jitter and 90ms gap.
            double t = 310;
            for (var i = 0; i < 1000; i++) {
                t += i % 13 == 0 ? 90 : i % 7 == 0 ? 40 : 8;
                Check(x.Send(t, held) && x.Gate.Armed, "recoverable jitter disarmed");
                var packet = x.Frame(held); Check(x.Gate.Ingest(packet, t + 1), "first packet");
                Check(!x.Gate.Ingest(packet, t + 2) && x.Gate.Armed, "duplicate disarmed");
                var older = x.Frame(held); var newer = x.Frame(held);
                Check(x.Gate.Ingest(newer, t + 3), "newer frame rejected");
                Check(!x.Gate.Ingest(older, t + 4) && x.Gate.Armed, "out-of-order packet disarmed");
            }
            // A challenge aged 101ms is rejected, but isolated rejection does not disarm.
            x.Gate.NoteServerSend(++x.Ack, t - 101);
            Check(!x.Send(t + 2, held, challenge: false) && x.Gate.Armed, "stale ACK caused immediate release");
            Check(x.Send(t + 10, held) && x.Gate.Armed, "fresh ACK did not recover");
            Check(x.Gate.GetDiagnostics(t + 10).ReleaseCount == 0, "jitter release count");
            Console.WriteLine($"  jitter/loss: max receive gap {x.Gate.MaxReceiveGapMs:F0}ms, releases 0, ACK rejects {x.Gate.GetDiagnostics(t + 10).RejectedChallenges}");
        }
    }
    public static void LossAndHeldRecovery() {
        foreach (var held in new[] { new Controls(.6f, 1, 0), new Controls(-.9f, 0, 0), new Controls(0, 0, 1) }) {
            var x = new Link(); x.Arm(); x.Send(320, held);
            Check(x.Gate.Tick(469) == held, "deadline changed before 150ms");
            Check(x.Gate.Tick(470).IsNeutral && !x.Gate.Armed, "150ms loss not neutral");
            Check(x.Gate.GetDiagnostics(470).LastRelease == ReleaseReason.Timeout, "loss reason");
            x.Send(480, held); Check(!x.Gate.Armed && x.Gate.Output.IsNeutral, "held input replayed on reconnect");
            for (var t = 500; t <= 1000; t += 50) x.Send(t, Controls.Neutral, 14);
            x.Send(1010, Controls.Neutral); Check(x.Gate.Armed, "neutral rearm failed");
            x.Send(1020, held); Check(x.Gate.Output == held, "fresh output after rearm");
        }
    }
    public static void ProvenFastRecovery() {
        foreach (var held in new[] { new Controls(.8f, 1, 0), new Controls(-.9f, 0, 1) }) {
            var x = new Link(); x.Arm(); x.Send(320, held, 31);
            Check(x.Gate.Tick(470).IsNeutral && !x.Gate.Armed, "deadline must still neutralize at 150ms");
            Check(x.Gate.Reason == ReleaseReason.Recovering, "capability did not enter recovery");
            x.Gate.NoteServerSend(++x.Ack, 469);
            Check(!x.Send(480, held, 31, false) && x.Gate.Output.IsNeutral, "pre-cutoff proof replayed throttle");
            var fresh = new Controls(.3f, .4f, .1f);
            Check(x.Send(490, fresh, 31) && x.Gate.Armed && x.Gate.Output == fresh, "fresh post-cutoff proof failed");
            Check(x.Gate.GetDiagnostics(490).ReleaseCount == 1, "release accounting");
            Console.WriteLine("  170ms outage: neutral at 150ms, new-input recovery at 170ms; no dwell or stale replay");
        }
        foreach (var failure in new[] { "deadline", "late-watchdog", "output", "sensor", "focus", "arm", "epoch", "hello" }) {
            var x = new Link(); x.Arm(); x.Send(320, new(.8f, 1, 0), 31);
            x.Gate.Tick(failure == "late-watchdog" ? 570 : 470);
            if (failure == "deadline") x.Gate.Tick(570);
            if (failure == "output") x.Gate.OutputFailed();
            if (failure == "sensor") x.Send(480, Controls.Neutral, 29);
            if (failure == "focus") x.Send(480, Controls.Neutral, 27);
            if (failure == "arm") x.Send(480, Controls.Neutral, 30);
            if (failure == "epoch") {
                x.Gate.NoteServerSend(++x.Ack, 480);
                x.Gate.Ingest(Pwr1Codec.EncodeControl(new(new(PacketKind.Control, 42, ++x.Seq, 480000, x.Ack), Controls.Neutral, 31, 2), Key), 480);
            }
            if (failure == "hello") x.Gate.AcceptInitialHello(Pwr1Codec.EncodeHello(new(new(PacketKind.Hello, 42, ++x.Seq, 480000, 0)), Key), true);
            x.Send(failure is "deadline" or "late-watchdog" ? 580 : 490, new(.8f, 1, 0), 31);
            Check(!x.Gate.Armed && x.Gate.Output.IsNeutral, failure + " bypassed hard disarm");
        }
        var repeated = new Link(); repeated.Arm(); repeated.Send(320, new(.8f, 1, 0), 31);
        repeated.Gate.Tick(470);
        repeated.Gate.NoteServerSend(++repeated.Ack, 469);
        for (var t = 480; t < 570; t += 10) repeated.Send(t, new(.8f, 1, 0), 31, false);
        repeated.Send(570, new(.8f, 1, 0), 31);
        Check(!repeated.Gate.Armed && repeated.Gate.Output.IsNeutral, "rejected proof extended recovery deadline");
    }
    public static void DistinctFaults() {
        foreach (var (flags, reason) in new[] { ((ushort)13, ReleaseReason.Sensor), ((ushort)11, ReleaseReason.Inactive), ((ushort)14, ReleaseReason.User) }) {
            var x = new Link(); x.Arm(); x.Send(320, new(.5f, .8f, 0));
            x.Send(330, Controls.Neutral, flags); Check(!x.Gate.Armed && x.Gate.Output.IsNeutral, "READY/ARM fault ignored");
            Check(x.Gate.GetDiagnostics(330).LastRelease == reason, "READY cause incorrect");
        }
        var ack = new Link(); ack.Arm(); ack.Send(320, new(.5f, .8f, 0));
        for (var t = 430; t <= 470; t += 10) ack.Send(t, new(.5f, .8f, 0), challenge: false);
        Check(!ack.Gate.Armed && ack.Gate.GetDiagnostics(470).LastRelease == ReleaseReason.Timeout, "prolonged ACK failure did not release");
        var output = new Link(); output.Arm(); output.Gate.OutputFailed();
        output.Send(330, new(.5f, .8f, 0)); Check(!output.Gate.Armed && output.Gate.Output.IsNeutral, "output recovery replayed held input");
        Check(output.Gate.GetDiagnostics(330).LastRelease == ReleaseReason.OutputError, "output failure cause overwritten");
    }
    public static void Recorder() {
        var dir = Path.Combine(Path.GetTempPath(), "mdrive-flight-test-" + Guid.NewGuid().ToString("N"));
        double now = 0;
        using var r = new ControllerFlightRecorder(dir, () => Volatile.Read(ref now));
        for (var i = 0; i <= 12; i++) { Volatile.Write(ref now, i * 1000); r.Record(new(ControlEvent.Receive, Sequence: (uint)i)); }
        Thread.Sleep(150); Check(!Directory.Exists(dir), "normal operation wrote a file");
        r.Record(new(ControlEvent.Release, Release: ReleaseReason.Timeout), true);
        Volatile.Write(ref now, 14000); r.Record(new(ControlEvent.Hello));
        Volatile.Write(ref now, 15000); r.Record(new(ControlEvent.Arm));
        var wait = Stopwatch.StartNew(); while (r.LastFile == null && wait.ElapsedMilliseconds < 3000) Thread.Sleep(10);
        Check(r.LastFile != null, "incident not saved");
        using var json = JsonDocument.Parse(File.ReadAllText(r.LastFile!));
        var events = json.RootElement.GetProperty("Events").EnumerateArray().ToArray();
        Check(events.All(e => e.GetProperty("AtMs").GetDouble() >= 2000), "unbounded history");
        Check(events.Any(e => e.GetProperty("Event").GetString() == "Hello"), "post incident history absent");
        Check(r.SaveFailures == 0, "save failure");
        Console.WriteLine($"  recorder: {events.Length} records, normal disk writes 0, pre/post incident PASS");
    }
    public static void ReceiveIsolation() {
        // Separate test process only: simulate a saturated shared pool. This is
        // a scheduling-risk reproduction, NOT evidence of real F1 pool starvation.
        ThreadPool.GetMinThreads(out var min, out var minIo);
        ThreadPool.GetMaxThreads(out var max, out var maxIo);
        using var occupied = new ManualResetEventSlim(); using var unblock = new ManualResetEventSlim();
        using var cancel = new CancellationTokenSource();
        using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
        udp.Dispose();
        using var transport = new PhoneWheel.Host.WifiTransport(System.Net.IPAddress.Loopback, port);
        using var received = new ManualResetEventSlim();
        Task? blocker = null, pooled = null, dedicated = null;
        try {
            Check(ThreadPool.SetMinThreads(1, minIo) && ThreadPool.SetMaxThreads(1, maxIo), "pool setup");
            blocker = Task.Run(() => { occupied.Set(); unblock.Wait(); }); Check(occupied.Wait(2000), "pool blocker start");
            var pooledRan = false;
            pooled = Task.Run(() => pooledRan = true);
            dedicated = CriticalLoop.Run("receive isolation test", () => {
                var packet = transport.Receive(cancel.Token);
                Check(packet.Buffer.Length == 1 && packet.Buffer[0] == 7, "UDP bytes"); received.Set();
            });
            using var sender = new System.Net.Sockets.UdpClient();
            sender.Send(new byte[] { 7 }, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port));
            Check(received.Wait(1000), "dedicated receive stalled with pool");
            Thread.Sleep(160);
            Check(!pooledRan, "test did not actually saturate shared pool");
            Console.WriteLine("  shared-pool continuation blocked >160ms; dedicated UDP receive completed");
        } finally {
            unblock.Set(); cancel.Cancel();
            ThreadPool.SetMaxThreads(max, maxIo); ThreadPool.SetMinThreads(min, minIo);
            blocker?.GetAwaiter().GetResult(); pooled?.GetAwaiter().GetResult(); dedicated?.GetAwaiter().GetResult();
        }
    }
}
