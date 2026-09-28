using System.Diagnostics;
using System.Text.Json;

namespace PhoneWheel.Core;

// Only bounded categories and numeric control-path metadata. Never payloads,
// addresses, session IDs, keys, voice text, or exception messages.
public enum ControlEvent { Receive, Reject, Release, Arm, Hello, StatusSend, Output, OutputError, OutputStall, TransportError, RecoveryHold, RecoveryResume }
public enum PacketRejection { None, Authentication, Session, Protocol, Sequence, UnknownChallenge, StaleChallenge, FutureChallenge, Peer, RecoveryProof }
public readonly record struct ControlTrace(ControlEvent Event, double AtMs = 0, uint? Sequence = null,
    uint? Ack = null, bool? Accepted = null, PacketRejection Rejection = PacketRejection.None,
    double? ReceiveGapMs = null, double? ChallengeAgeMs = null, ushort? Flags = null,
    bool? Armed = null, ReleaseReason? Release = null, double? WriteMs = null, double? WriteAgeMs = null,
    bool? NonNeutral = null, long? ReleaseCount = null, long? ErrorCount = null, int? ErrorCode = null, double? AcceptedAgeMs = null);

public sealed class ControllerFlightRecorder : IDisposable
{
    private readonly object sync = new();
    private readonly ControlTrace[] ring = new ControlTrace[32768];
    private int head, count;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Func<double>? testClock;
    private sealed record Incident(double At, ControlTrace Cause);
    private Incident? trigger;
    private double nextTrigger;
    private readonly string directory;
    private readonly CancellationTokenSource stop = new();
    private readonly Thread writer;
    private long dropped, failures;
    public long Dropped => Interlocked.Read(ref dropped);
    public long SaveFailures => Interlocked.Read(ref failures);
    public string? LastFile { get; private set; }
    public double NowMs => testClock?.Invoke() ?? clock.Elapsed.TotalMilliseconds;

    public ControllerFlightRecorder(string directory, Func<double>? testClock = null) {
        this.directory = directory;
        this.testClock = testClock;
        writer = new Thread(WriteLoop) { IsBackground = true, Name = "mDrive diagnostic writer", Priority = ThreadPriority.BelowNormal };
        writer.Start();
    }
    public void Record(ControlTrace entry, bool incident = false) {
        var now = NowMs;
        // Incident latch independent of ring contention: never lose the trigger.
        if (incident && now >= Volatile.Read(ref nextTrigger)) Interlocked.CompareExchange(ref trigger, new Incident(now, entry with { AtMs = now }), null);
        if (!Monitor.TryEnter(sync)) { Interlocked.Increment(ref dropped); return; }
        try {
            var pending = Volatile.Read(ref trigger);
            var cutoff = (pending?.At ?? now) - 10000;
            while (count > 0 && ring[head].AtMs < cutoff) { head = (head + 1) % ring.Length; count--; }
            if (count == ring.Length) { head = (head + 1) % ring.Length; count--; Interlocked.Increment(ref dropped); }
            ring[(head + count++) % ring.Length] = entry with { AtMs = now };
        } finally { Monitor.Exit(sync); }
    }
    private void WriteLoop() {
        while (true) {
            stop.Token.WaitHandle.WaitOne(100);
            var incident = Volatile.Read(ref trigger);
            if (incident == null) { if (stop.IsCancellationRequested) break; continue; }
            var at = incident.At;
            if (!stop.IsCancellationRequested && NowMs - at < 3000) continue;
            ControlTrace[] captured;
            lock (sync) {
                captured = Enumerable.Range(0, count).Select(i => ring[(head + i) % ring.Length])
                    .Where(e => e.AtMs >= at - 10000 && e.AtMs <= at + 3000).ToArray();
                Volatile.Write(ref nextTrigger, NowMs); Interlocked.Exchange(ref trigger, null);
            }
            try {
                Directory.CreateDirectory(directory);
                var file = Path.Combine(directory, $"controller-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.json");
                using (var stream = File.Create(file)) JsonSerializer.Serialize(stream, new { Schema = 1, Platform = "PC", CapturedUtc = DateTime.UtcNow,
                    TriggerMs = at, Cause = incident.Cause, Dropped, Events = captured }, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
                LastFile = file;
                // Bounded retention; only our own incident files in this directory.
                foreach (var old in new DirectoryInfo(directory).GetFiles("controller-*.json").OrderByDescending(f => f.LastWriteTimeUtc).Skip(8)) old.Delete();
            } catch { Interlocked.Increment(ref failures); }
            if (stop.IsCancellationRequested) break;
        }
    }
    public void Dispose() { stop.Cancel(); writer.Join(2000); /* A pending incident is flushed by the writer on shutdown. */ }
}
