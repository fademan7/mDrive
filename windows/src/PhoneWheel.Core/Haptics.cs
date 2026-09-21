namespace PhoneWheel.Core;

public sealed class HapticArbiter
{
    private static readonly IReadOnlyDictionary<HapticEvent, int> Priority = new Dictionary<HapticEvent, int>
    {
        [HapticEvent.Lock] = 100, [HapticEvent.Spin] = 90, [HapticEvent.Grip] = 80,
        [HapticEvent.Collision] = 60, [HapticEvent.Curb] = 30, [HapticEvent.Shift] = 10, [HapticEvent.Engine] = 0
    };
    private readonly HashSet<HapticEvent> _enabled;
    private double _deadlineMs;
    public HapticEvent Event { get; private set; }
    public byte Level { get; private set; }
    public double? PatternStartedMs { get; private set; }

    public HapticArbiter(IEnumerable<HapticEvent>? enabled = null)
        => _enabled = enabled?.ToHashSet() ?? [HapticEvent.Lock, HapticEvent.Spin];

    public string Offer(HapticEvent evt, byte level, double nowMs, double sourceAgeMs = 0, ushort leaseMs = 100)
    {
        Tick(nowMs);
        if (sourceAgeMs is < 0 or > 100 || leaseMs is < 1 or > 150) return "dropped";
        if (evt == HapticEvent.Stop) { Stop(); return "stopped"; }
        if (!_enabled.Contains(evt) || !Priority.ContainsKey(evt) || level is < 1 or > 3) return "dropped";
        if (Event != HapticEvent.Stop && Priority[evt] < Priority[Event]) return "dropped";
        var same = evt == Event;
        Event = evt; Level = level; _deadlineMs = nowMs + leaseMs;
        if (!same) PatternStartedMs = nowMs;
        return same ? "refreshed" : "started";
    }

    public HapticEvent Tick(double nowMs)
    {
        if (nowMs >= _deadlineMs) Stop();
        return Event;
    }

    public void Stop() { Event = HapticEvent.Stop; Level = 0; PatternStartedMs = null; }
}
