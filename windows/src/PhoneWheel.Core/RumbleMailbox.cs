namespace PhoneWheel.Core;

// Native feedback only replaces a tiny snapshot; it never sends packets.
// XInput motor state is latched, not a heartbeat: unchanged motors may produce
// no more callbacks. Network leases/disarm stop phone output independently.
public sealed class RumbleMailbox
{
    private readonly object _sync = new();
    private byte _strength;
    private double _receivedMs;
    private byte _pendingPeak;
    private double _peakMs;
    public void Update(byte large, byte small, double nowMs)
    {
        lock (_sync) {
            _strength = Math.Max(large, small); _receivedMs = nowMs;
            if (_strength != 0) {
                if (nowMs - _peakMs > 100 || nowMs < _peakMs) _pendingPeak = 0;
                _pendingPeak = Math.Max(_pendingPeak, _strength); _peakMs = nowMs;
            }
        }
    }
    public byte ReadLevel(double nowMs, bool armed)
    {
        lock (_sync)
        {
            if (!armed || nowMs < _receivedMs) { _strength = _pendingPeak = 0; return 0; }
            // At most one bounded peak, never a queue. This preserves a brief
            // rumble that starts/stops between packet sends, not stale events.
            var value = nowMs - _peakMs is >= 0 and <= 100 ? Math.Max(_strength, _pendingPeak) : _strength;
            _pendingPeak = 0;
            return value == 0 ? (byte)0 : (byte)(value <= 85 ? 1 : value <= 170 ? 2 : 3);
        }
    }
}
