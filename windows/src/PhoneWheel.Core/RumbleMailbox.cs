namespace PhoneWheel.Core;

// Native feedback only replaces a tiny snapshot; it never sends packets.
// A conservative 500 ms source limit prevents indefinite stale motor output.
public sealed class RumbleMailbox
{
    private readonly object _sync = new();
    private byte _strength;
    private double _receivedMs;
    public void Update(byte large, byte small, double nowMs)
    {
        lock (_sync) { _strength = Math.Max(large, small); _receivedMs = nowMs; }
    }
    public byte ReadLevel(double nowMs, bool armed)
    {
        lock (_sync)
        {
            if (!armed || nowMs < _receivedMs || nowMs - _receivedMs >= 500) { _strength = 0; return 0; }
            return _strength == 0 ? (byte)0 : (byte)(_strength <= 85 ? 1 : _strength <= 170 ? 2 : 3);
        }
    }
}
