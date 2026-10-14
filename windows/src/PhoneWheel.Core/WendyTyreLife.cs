namespace PhoneWheel.Core;

public sealed record TyreRemaining(int Low, int High, int Samples);

// Empirical wear-to-70% planning range, NOT a puncture or optimum-strategy model.
// No compound-specific degradation constant is invented: a new set/compound resets it.
public sealed class WendyTyreLife
{
    private int generation = -1, compound = -1, age, weather = -1, lap;
    private uint setup;
    private double last = double.NegativeInfinity;
    private float[] start = new float[4];
    private float maximumDistance;
    private bool dirty = true;
    private readonly Queue<double[]> rates = new();
    public void Observe(F1RaceState r, double now)
    {
        if (generation != r.Generation || compound != r.TyreCompound || r.TyreAge < age || weather != r.Weather || setup != r.SetupSignature) {
            rates.Clear(); lap = 0; dirty = true; generation = r.Generation; compound = r.TyreCompound; weather = r.Weather; setup = r.SetupSignature;
        }
        age = r.TyreAge;
        if (!Ready(r, now)) { dirty = true; return; }
        if (now - last < 100) return;
        if (now - last > 2000) { dirty = true; rates.Clear(); }
        last = now;
        if (r.Lap != lap) {
            if (r.Lap == lap + 1 && !dirty && maximumDistance >= r.TrackLength * .85 && r.LapDistance < r.TrackLength * .05) {
                var rate = Enumerable.Range(0, 4).Select(i => (double)r.Wear[i] - start[i]).ToArray();
                if (rate.All(v => v is >= .2 and <= 25)) { rates.Enqueue(rate); while(rates.Count > 5) rates.Dequeue(); }
                else rates.Clear();
            } else rates.Clear();
            lap = r.Lap; start = (float[])r.Wear.Clone(); maximumDistance = r.LapDistance;
            dirty = r.LapDistance > r.TrackLength * .05;
        }
        maximumDistance = Math.Max(maximumDistance, r.LapDistance);
        dirty |= r.LapInvalid || r.Pit != 0 || r.Flag(now) != "GREEN" || r.Wear.Where((v, i) => v < start[i] - .5).Any();
    }
    private static bool Ready(F1RaceState r, double now) => r.Live(now) && r.Result == 2 && r.TrackLength > 500 &&
        F1RaceState.Fresh(r.LapMs, now) && F1RaceState.Fresh(r.StatusMs, now) && F1RaceState.Fresh(r.DamageMs, now) && F1RaceState.Fresh(r.TelemetryMs, now);
    public TyreRemaining? Estimate(F1RaceState r, double now)
    {
        if (generation != r.Generation || rates.Count < 3 || dirty || !Ready(r, now) || r.Pit != 0 || r.TyreAge < 3) return null;
        double low = 999, high = 999;
        for (var i = 0; i < 4; i++) {
            var a = rates.Select(v => v[i]).Order().ToArray(); var median = a[a.Length/2];
            if ((a[^1] - a[0]) / median > .65) return null;
            // Wider bounds outside a conservative temperature band; not a heat/wear physics model.
            var uncertainty = r.InnerTemperature[i] is < 75 or > 110 ? .45 : .25;
            var remaining = Math.Max(0, 70 - r.Wear[i]);
            low = Math.Min(low, remaining / (a[^1] * (1 + uncertainty)));
            high = Math.Min(high, remaining / (a[0] * (1 - uncertainty)));
        }
        if (high > 60) return null; // too little measured degradation for a useful prediction
        var lo = (int)Math.Floor(low); var hi = Math.Max(lo + 2, (int)Math.Ceiling(high));
        return new(lo, hi, rates.Count);
    }
    public bool PitNeeded(F1RaceState r, double now) => Ready(r, now) && r.Pit == 0 && r.TotalLaps > r.Lap &&
        (r.Wear.Max() >= 75 || (Estimate(r, now) is { Low: 0, High: <= 2 } && r.Wear.Max() >= 65));
    public string Summary(F1RaceState r, double now)
    {
        if (!r.Live(now) || !F1RaceState.Fresh(r.LapMs, now) || r.Result != 2) return "";
        if (PitNeeded(r, now)) return "PIT THIS LAP";
        if (F1RaceState.Fresh(r.DamageMs, now) && r.FrontWing >= 10) return $"FW DAMAGE {r.FrontWing}%";
        var rain = r.Forecast.FirstOrDefault(f => f.Weather >= 3 && f.Minutes <= 15 && f.RainPercent >= 50);
        if (r.Weather < 3 && rain != null) return $"RAIN ~{rain.Minutes} MIN";
        return Estimate(r, now) is { } estimate ? $"TYRE REMAIN {estimate.Low}–{estimate.High} LAPS" : "";
    }
}
