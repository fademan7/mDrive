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
    private bool _supportsRecovery;
    private double? _recoverySince;
    private double? _neutralSinceMs;
    private long _releaseCount;
    private long _rejectedChallenges;
    private ReleaseReason _lastRelease = ReleaseReason.Normal;
    private readonly ControllerFlightRecorder? _flight;
    private double? _lastPacketMs;
    public double MaxReceiveGapMs { get; private set; }

    public SafetyGate(ulong session, ReadOnlySpan<byte> key, double timeoutMs = 150, double challengeMs = 100, ControllerFlightRecorder? flight = null)
    {
        if (session == 0 || key.Length != 32 || timeoutMs <= 0 || challengeMs <= 0)
            throw new ArgumentException("Invalid safety gate configuration.");
        _session = session;
        _key = key.ToArray();
        _timeoutMs = timeoutMs;
        _challengeMs = challengeMs;
        _flight = flight;
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
            _flight?.Record(new(ControlEvent.Hello, Sequence: frame.Header.Sequence, Armed: Armed), reconnect);
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
            // Keep bounded recent history for precise stale-vs-unknown diagnosis.
            // Validation below STILL rejects challenges older than 100ms.
            foreach (var stale in _sentHistory.Where(x => nowMs - x.Value > 10000).Select(x => x.Key).ToArray())
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
            var gap = _lastPacketMs is double previous ? nowMs - previous : (double?)null;
            _lastPacketMs = nowMs;
            if (gap is double g) MaxReceiveGapMs = Math.Max(MaxReceiveGapMs, g);
            ControlFrame frame;
            try { frame = Pwr1Codec.DecodeControl(packet, _key, _session); }
            catch (ProtocolException ex) { _flight?.Record(new(ControlEvent.Reject, Accepted: false, Rejection: Classify(ex), ReceiveGapMs: gap, Armed: Armed)); return false; }
            var known = _sentHistory.TryGetValue(frame.Header.AckSequence, out var challengeSent);
            double? age = known ? nowMs - challengeSent : null;
            bool Reject(PacketRejection reason) {
                _flight?.Record(new(ControlEvent.Reject, Sequence: frame.Header.Sequence, Ack: frame.Header.AckSequence,
                    Accepted: false, Rejection: reason, ReceiveGapMs: gap, ChallengeAgeMs: age, Flags: frame.Flags, Armed: Armed,
                    AcceptedAgeMs: _lastReceiveMs is double received ? nowMs - received : null));
                return false;
            }
            if (_lastDirectionSequence is uint last && !IsNewer(frame.Header.Sequence, last)) return Reject(PacketRejection.Sequence);
            if (!known || age < 0 || age > _challengeMs) {
                _rejectedChallenges++;
                return Reject(!known ? PacketRejection.UnknownChallenge : age < 0 ? PacketRejection.FutureChallenge : PacketRejection.StaleChallenge);
            }
            // A held value queued before expiry cannot revive output. Require a
            // round trip begun AFTER expiry, in the same live control epoch.
            if (_recoverySince is double cutoff) {
                if (frame.CalibrationEpoch != _epoch ||
                    (frame.Flags & (Pwr1Codec.Ready | Pwr1Codec.Arm | Pwr1Codec.FastRecovery)) !=
                    (Pwr1Codec.Ready | Pwr1Codec.Arm | Pwr1Codec.FastRecovery)) {
                    DisarmLocked(frame.CalibrationEpoch != _epoch ? ReleaseReason.Calibration :
                        (frame.Flags & 2) == 0 ? ReleaseReason.Sensor :
                        (frame.Flags & Pwr1Codec.Ready) != Pwr1Codec.Ready ? ReleaseReason.Inactive : ReleaseReason.User);
                } else if (challengeSent < cutoff) return Reject(PacketRejection.RecoveryProof);
                else {
                    _recoverySince = null;
                    Armed = true;
                    Reason = ReleaseReason.Normal;
                    _flight?.Record(new(ControlEvent.RecoveryResume, Sequence: frame.Header.Sequence,
                        ChallengeAgeMs: age, Armed: true, ReleaseCount: _releaseCount));
                }
            }
            _flight?.Record(new(ControlEvent.Receive, Sequence: frame.Header.Sequence, Ack: frame.Header.AckSequence,
                Accepted: true, ReceiveGapMs: gap, ChallengeAgeMs: age, Flags: frame.Flags, Armed: Armed, NonNeutral: !frame.Controls.IsNeutral));

            _lastDirectionSequence = frame.Header.Sequence;
            _lastControlSequence = frame.Header.Sequence;
            _lastReceiveMs = nowMs;
            _lastReceivedControls = frame.Controls;
            _supportsRecovery = (frame.Flags & Pwr1Codec.FastRecovery) != 0;
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
                if (Armed) RecordRelease(ReleaseReason.User);
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
                if (Armed) _flight?.Record(new(ControlEvent.Arm, Armed: true, ReleaseCount: _releaseCount));
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
        if (_recoverySince is double cutoff) {
            if (nowMs >= cutoff + _challengeMs) DisarmLocked(ReleaseReason.Timeout);
        } else if (_lastReceiveMs is null || nowMs - _lastReceiveMs >= _timeoutMs) {
            var expiry = (_lastReceiveMs ?? nowMs) + _timeoutMs;
            if (Armed && _supportsRecovery && nowMs < expiry + _challengeMs) {
                RecordRelease(ReleaseReason.Timeout);
                Armed = false;
                Output = Controls.Neutral;
                Reason = ReleaseReason.Recovering;
                _recoverySince = expiry;
                _flight?.Record(new(ControlEvent.RecoveryHold, Armed: false, Release: Reason));
            } else DisarmLocked(ReleaseReason.Timeout);
        }
        return Output;
    }

    private void DisarmLocked(ReleaseReason reason)
    {
        if (Armed) RecordRelease(reason);
        Armed = false;
        _recoverySince = null;
        _previousArm = false;
        _neutralSinceMs = null;
        Output = Controls.Neutral;
        Reason = reason;
    }

    private void RecordRelease(ReleaseReason reason) {
        _releaseCount++; _lastRelease = reason;
        _flight?.Record(new(ControlEvent.Release, Armed: false, Release: reason, ReleaseCount: _releaseCount), true);
    }
    public static PacketRejection Classify(ProtocolException ex) => ex.Message switch {
        "Authentication failed." => PacketRejection.Authentication,
        "Wrong session." => PacketRejection.Session,
        _ => PacketRejection.Protocol
    };

    public static bool IsNewer(uint candidate, uint previous)
    {
        var distance = unchecked(candidate - previous);
        return distance != 0 && distance < 0x80000000U;
    }
}
