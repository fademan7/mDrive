namespace PhoneWheel.Core;

public readonly record struct InputDiagnostics(Controls Received, Controls Output, bool HasRecentInput,
    bool Armed, ReleaseReason Reason, double? AgeMs, long ReleaseCount = 0, ReleaseReason LastRelease = ReleaseReason.Normal, long RejectedChallenges = 0);

public sealed class SafetyGate
{
    private readonly object _sync = new();
    private readonly ulong _session;
    private readonly byte[] _key;
    private readonly double _timeoutMs;
    private readonly double _challengeMs;
    private readonly Dictionary<uint, double> _sentHistory = [];
    private uint? _lastDirectionSequence;
    private uint? _lastControlSequence;
    private double? _lastReceiveMs;
    private Controls _lastReceivedControls;
    private uint? _epoch;
    private bool _previousArm;
    private double? _neutralSinceMs;
    private long _releaseCount;
    private long _rejectedChallenges;
    private ReleaseReason _lastRelease = ReleaseReason.Normal;

    public SafetyGate(ulong session, ReadOnlySpan<byte> key, double timeoutMs = 150, double challengeMs = 100)
    {
        if (session == 0 || key.Length != 32 || timeoutMs <= 0 || challengeMs <= 0)
            throw new ArgumentException("Invalid safety gate configuration.");
        _session = session;
        _key = key.ToArray();
        _timeoutMs = timeoutMs;
        _challengeMs = challengeMs;
    }

    public bool Armed { get; private set; }
    public Controls Output { get; private set; }
    public ReleaseReason Reason { get; private set; } = ReleaseReason.User;
    public uint LastAcceptedSequence => _lastControlSequence ?? 0;

    public bool AcceptInitialHello(ReadOnlySpan<byte> packet, bool reconnect = false)
    {
        lock (_sync)
        {
            HelloFrame frame;
            try { frame = Pwr1Codec.DecodeHello(packet, _key, _session); }
            catch (ProtocolException) { return false; }
            if (_lastDirectionSequence is uint last && !IsNewer(frame.Header.Sequence, last)) return false;
            _lastDirectionSequence = frame.Header.Sequence;
            if (reconnect) {
                DisarmLocked(ReleaseReason.Timeout);
                _sentHistory.Clear();
                _lastReceiveMs = null;
            }
            return true;
        }
    }

    public void NoteServerSend(uint sequence, double nowMs)
    {
        lock (_sync)
        {
            foreach (var stale in _sentHistory.Where(x => nowMs - x.Value < 0 || nowMs - x.Value > _challengeMs).Select(x => x.Key).ToArray())
                _sentHistory.Remove(stale);
            if (_sentHistory.Count >= 512)
            {
                var oldest = _sentHistory.MinBy(x => x.Value).Key;
                _sentHistory.Remove(oldest);
            }
            _sentHistory[sequence] = nowMs;
        }
    }

    public bool Ingest(ReadOnlySpan<byte> packet, double nowMs)
    {
        lock (_sync)
        {
            TickLocked(nowMs);
            ControlFrame frame;
            try { frame = Pwr1Codec.DecodeControl(packet, _key, _session); }
            catch (ProtocolException) { return false; }
            if (_lastDirectionSequence is uint last && !IsNewer(frame.Header.Sequence, last)) return false;
            if (!_sentHistory.TryGetValue(frame.Header.AckSequence, out var challengeSent) || nowMs - challengeSent < 0 || nowMs - challengeSent > _challengeMs)
                { _rejectedChallenges++; return false; }

            _lastDirectionSequence = frame.Header.Sequence;
            _lastControlSequence = frame.Header.Sequence;
            _lastReceiveMs = nowMs;
            _lastReceivedControls = frame.Controls;
            if (frame.CalibrationEpoch != _epoch)
            {
                DisarmLocked(ReleaseReason.Calibration);
                _epoch = frame.CalibrationEpoch;
            }
            if ((frame.Flags & Pwr1Codec.Ready) != Pwr1Codec.Ready)
            {
                var reason = (frame.Flags & 0x2) == 0 ? ReleaseReason.Sensor : ReleaseReason.Inactive;
                DisarmLocked(reason);
                return true;
            }

            var intent = (frame.Flags & Pwr1Codec.Arm) != 0;
            var neutral = frame.Controls.IsNeutral;
            if (!intent)
            {
                if (Armed) { _releaseCount++; _lastRelease = ReleaseReason.User; }
                Armed = false;
                Output = Controls.Neutral;
                Reason = ReleaseReason.User;
                _neutralSinceMs = neutral ? _neutralSinceMs ?? nowMs : null;
            }
            else if (!_previousArm)
            {
                Armed = neutral && _neutralSinceMs is double since && nowMs - since >= 300;
                _neutralSinceMs = null;
                Reason = Armed ? ReleaseReason.Normal : ReleaseReason.User;
            }
            _previousArm = intent;
            Output = Armed ? frame.Controls : Controls.Neutral;
            return true;
        }
    }

    public Controls Tick(double nowMs)
    {
        lock (_sync) return TickLocked(nowMs);
    }

    public InputDiagnostics GetDiagnostics(double nowMs)
    {
        lock (_sync) {
            TickLocked(nowMs);
            var age = _lastReceiveMs is double received ? nowMs - received : (double?)null;
            var recent = age is >= 0 && age < _timeoutMs;
            return new(recent ? _lastReceivedControls : Controls.Neutral, Output, recent, Armed, Reason, age, _releaseCount, _lastRelease, _rejectedChallenges);
        }
    }

    public void OutputFailed()
    {
        lock (_sync) DisarmLocked(ReleaseReason.OutputError);
    }

    private Controls TickLocked(double nowMs)
    {
        if (_lastReceiveMs is null || nowMs - _lastReceiveMs >= _timeoutMs)
            DisarmLocked(ReleaseReason.Timeout);
        return Output;
    }

    private void DisarmLocked(ReleaseReason reason)
    {
        if (Armed) { _releaseCount++; _lastRelease = reason; }
        Armed = false;
        _previousArm = false;
        _neutralSinceMs = null;
        Output = Controls.Neutral;
        Reason = reason;
    }

    public static bool IsNewer(uint candidate, uint previous)
    {
        var distance = unchecked(candidate - previous);
        return distance != 0 && distance < 0x80000000U;
    }
}
