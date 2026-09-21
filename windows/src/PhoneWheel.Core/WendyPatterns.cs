namespace PhoneWheel.Core;

public sealed record SetupRecommendation(string Id, ulong Session, string Observation, string Recommendation, string Evidence, DateTimeOffset CreatedAt);

// Conservative bounded observations, not a vehicle dynamics estimator. Thresholds
// below are product heuristics, not universal F1 tyre operating windows.
public sealed class WendyPatterns
{
    private int generation = -1, lastLap, lastAhead = -1, lastWeather = -1;
    private double lastSample = double.NegativeInfinity, heatStart = double.NaN, wearStart = double.NaN;
    private int hotWheel = -1, heatLap, wearLap;
    private readonly Queue<double> gaps = new();
    private readonly Queue<uint> laps = new();
    private double lastGapAt = double.NegativeInfinity;
    private bool dirtyLap;
    public int SustainedHotWheel { get; private set; } = -1;
    public bool RepeatedHeat { get; private set; }
    public bool UnevenWear { get; private set; }
    public string? GapTrend { get; private set; }
    public bool PaceLoss { get; private set; }
    public void Observe(F1RaceState race, double now)
    {
        if (generation != race.Generation) { generation = race.Generation; Reset(); }
        if (!race.Live(now) || !F1RaceState.Fresh(race.LapMs, now) || race.Pit != 0 || race.Result != 2 || race.Flag(now) != "GREEN") { Reset(); return; }
        if (now - lastSample < 100) return;
        if (now - lastSample > 2000 || (lastWeather >= 0 && lastWeather != race.Weather)) Reset();
        lastSample = now; lastWeather = race.Weather;
        if (race.Lap != lastLap) {
            if (lastLap > 0 && race.Lap == lastLap + 1 && !dirtyLap && race.LastLapMs is > 20000 and < 600000) {
                laps.Enqueue(race.LastLapMs); while (laps.Count > 4) laps.Dequeue();
                var a = laps.ToArray(); PaceLoss = a.Length == 4 && a[1] > a[0] + 700 && a[2] > a[1] + 700 && a[3] > a[2] + 700;
            } else { laps.Clear(); PaceLoss = false; }
            dirtyLap = false; lastLap = race.Lap;
        }
        dirtyLap |= race.LapInvalid;
        if (race.AheadCar != lastAhead || race.Ahead == null) { gaps.Clear(); GapTrend = null; lastAhead = race.AheadCar; }
        if (race.Ahead is double gap && now - lastGapAt >= 5000) {
            lastGapAt = now; gaps.Enqueue(gap); while (gaps.Count > 4) gaps.Dequeue();
            var a = gaps.ToArray(); GapTrend = a.Length == 4 && Enumerable.Range(1, 3).All(i => a[i] < a[i - 1] - .25) ? "closing" :
                a.Length == 4 && Enumerable.Range(1, 3).All(i => a[i] > a[i - 1] + .25) ? "opening" : null;
        }
        if (F1RaceState.Fresh(race.TelemetryMs, now) && race.SpeedKph > 60) {
            var wheel = Array.IndexOf(race.InnerTemperature, race.InnerTemperature.Max());
            if (race.InnerTemperature[wheel] >= 110) {
                if (hotWheel != wheel || double.IsNaN(heatStart)) { heatStart = now; heatLap = race.Lap; hotWheel = wheel; }
                SustainedHotWheel = now - heatStart >= 10000 ? wheel : -1;
                RepeatedHeat = now - heatStart >= 30000 && race.Lap > heatLap;
            } else ClearHeat();
        } else ClearHeat();
        if (F1RaceState.Fresh(race.DamageMs, now) && race.Wear.Max() - race.Wear.Min() >= 12 && race.Wear.Max() >= 20) {
            if (double.IsNaN(wearStart)) { wearStart = now; wearLap = race.Lap; }
            UnevenWear = race.Lap >= wearLap + 2 && now - wearStart >= 30000;
        } else { wearStart = double.NaN; UnevenWear = false; }
    }
    private void ClearHeat() { heatStart = double.NaN; hotWheel = SustainedHotWheel = -1; RepeatedHeat = false; }
    private void Reset() { ClearHeat(); wearStart = double.NaN; UnevenWear = false; gaps.Clear(); laps.Clear(); GapTrend = null; PaceLoss = false; lastLap = 0; lastAhead = lastWeather = -1; lastSample = lastGapAt = double.NegativeInfinity; dirtyLap = true; }

    public SetupRecommendation? Suggest(WendyIntent feedback, F1RaceState race, double now)
    {
        if (!race.Live(now) || !F1RaceState.Fresh(race.SetupMs, now) || !F1RaceState.Fresh(race.DamageMs, now) || race.FrontWing > 0 || race.OtherDamage > 0) return null;
        if (feedback.Symptom == DriverSymptom.TYRE_OVERHEATING && RepeatedHeat && SustainedHotWheel >= 0 && race.SetupPressure.All(p => p > 0))
            return new("tyre-temperature", race.Session, "Sustained high inner tyre temperature across multiple laps.",
                "Review tyre pressures and driving inputs in the garage next session; no numeric pressure change is verified.",
                $"Wheel {SustainedHotWheel}; inner temperature {race.InnerTemperature[SustainedHotWheel]} C; setup pressure {race.SetupPressure[SustainedHotWheel]:0.0} PSI.", DateTimeOffset.UtcNow);
        if (feedback.Symptom is DriverSymptom.UNEVEN_WEAR or DriverSymptom.EXCESSIVE_DEGRADATION && UnevenWear)
            return new("uneven-wear", race.Session, "Wear spread above 12 percentage points across at least three laps.",
                "Review camber, pressures and corner balance in the garage next session; no numeric setup change is verified.",
                $"Wear range {race.Wear.Min():0.0} to {race.Wear.Max():0.0} percent; wing setup {race.FrontWingSetup}/{race.RearWingSetup}.", DateTimeOffset.UtcNow);
        return null;
    }
}
