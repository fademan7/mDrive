using System.Globalization;

namespace PhoneWheel.Core;

// Bounded, distance-aligned observations; not an optimal racing line or physics model.
// Owned exclusively by the optional engineer worker. No controller references or disk I/O.
public sealed class WendyCoaching
{
    private const int Step = 20, Capacity = 1501;
    private sealed record Point(float Distance, uint Time, int Speed, float Brake, float Steer, float Throttle);
    private sealed record Zone(int Start, int End);
    private Point?[] current = new Point?[Capacity], reference = new Point?[Capacity];
    private readonly List<Zone> zones = [];
    private int generation = -1, track = -1, length, weather = -1, compound = -1, age, lap = -1, lastBin = -1;
    private uint setup, referenceTime, previousTime, bestTime;
    private bool invalid = true;
    private double lastSample = double.NegativeInfinity, reportAt, adviceAt, lastAdvice = double.NegativeInfinity;
    private int zoneIndex, adviceCount;
    private string? report, advice;
    public bool HasReference => referenceTime > 0;
    public string LastReport { get; private set; } = "Complete a clean lap to start lap comparisons.";
    public string LastAdvice { get; private set; } = "Learning your first clean lap. Coaching starts on the following lap.";
    public int ReferenceLap { get; private set; }
    private static string N(double n) => n.ToString("0.0", CultureInfo.InvariantCulture);

    public void Observe(F1RaceState r, double now)
    {
        if (generation != r.Generation) { Reset(); generation = r.Generation; }
        if (!r.Live(now) || !F1RaceState.Fresh(r.LapMs, now, 300) || r.Pit != 0 || r.LapInvalid || r.Result != 2 || r.Flag(now) != "GREEN" || r.SafetyCar != 0 || r.DriverStatus is 0 or 2 or 3 ||
            (F1RaceState.Fresh(r.DamageMs, now) && (r.FrontWing > 0 || r.OtherDamage > 0))) {
            invalid = true; advice = report = null; return;
        }
        if (!F1RaceState.Fresh(r.TelemetryMs, now, 250) || Math.Abs(r.TelemetryMs - r.LapMs) > 100 ||
            !F1RaceState.Fresh(r.StatusMs, now) || !F1RaceState.Fresh(r.SetupMs, now) ||
            !F1RaceState.Fresh(r.DamageMs, now) || r.FrontWing > 0 || r.OtherDamage > 0) {
            if (now - lastSample > 1000) invalid = true;
            return;
        }
        if (r.TrackLength is < 1000 or > 30000 || r.Lap < 1 || r.TyreCompound == 0) return;
        if (track != r.TrackId || length != r.TrackLength || weather != r.Weather || compound != r.TyreCompound || setup != r.SetupSignature || r.TyreAge < age) {
            Reset(); generation = r.Generation; track = r.TrackId; length = r.TrackLength; weather = r.Weather; compound = r.TyreCompound; setup = r.SetupSignature;
        }
        age = r.TyreAge;
        if (now - lastSample < 100) return; // At most 10 Hz, independent of game UDP rate.
        if (now - lastSample > 1000) invalid = true;
        lastSample = now;
        if (r.Lap != lap) {
            if (r.Lap == lap + 1) Finish(r, now);
            current = new Point?[Capacity]; lastBin = -1; zoneIndex = adviceCount = 0; advice = null;
            lap = r.Lap; invalid = r.LapDistance > 80 || r.LapDistance < -100;
        }
        if (r.LapDistance < 0) return; // Grid before start line.
        var bin = (int)(r.LapDistance / Step);
        if (bin >= Capacity || r.LapDistance > length + 30) { invalid = true; return; }
        if (lastBin >= 0 && (bin < lastBin || bin - lastBin > 5)) invalid = true;
        if (bin == lastBin) return;
        current[bin] = new(r.LapDistance, r.CurrentLapTimeMs, r.SpeedKph, r.Brake, r.Steer, r.Throttle);
        lastBin = bin;
        if (invalid || !HasReference) return;
        while (zoneIndex < zones.Count && bin >= zones[zoneIndex].End + 2) {
            var zone = zones[zoneIndex++];
            if (adviceCount >= 2 || now - lastAdvice < 30000 || r.Brake > .05 || Math.Abs(r.Steer) > .12 || r.Throttle < .5 || (r.Ahead is double gap && gap < 2)) continue;
            if (current[zone.Start] is not { } entry || current[zone.End] is not { } exit || reference[zone.Start] is not { } oldEntry || reference[zone.End] is not { } oldExit) continue;
            if (Math.Abs(entry.Distance - oldEntry.Distance) > 8 || Math.Abs(exit.Distance - oldExit.Distance) > 8 || exit.Time <= entry.Time) continue;
            var loss = ((double)exit.Time - entry.Time - ((double)oldExit.Time - oldEntry.Time)) / 1000;
            var speed = entry.Speed - oldEntry.Speed;
            if (loss < .3 || Math.Abs(speed) < 10) continue;
            advice = $"Compared with your reference, you entered the last braking zone {Math.Abs(speed)} kilometres per hour {(speed > 0 ? "faster" : "slower")}, but lost {N(loss)} seconds through it. " +
                (speed > 0 ? "Try a more controlled entry." : "Review that braking point.");
            LastAdvice = advice; adviceAt = now; adviceCount++; lastAdvice = now;
        }
    }
    private void Finish(F1RaceState r, double now)
    {
        var end = Math.Min(Capacity - 1, length / Step);
        var count = current.Take(end).Count(p => p != null);
        var complete = !invalid && lastBin * Step >= length - 100 && count >= end * .85 && r.LastLapMs is >= 20000 and <= 1800000;
        if (!complete) { LastReport = "Last lap was incomplete or unsuitable for a clean pace comparison."; return; }
        var delta = previousTime == 0 ? "" : $" {N(Math.Abs((double)r.LastLapMs - previousTime) / 1000)} seconds {(r.LastLapMs < previousTime ? "faster" : "slower")} than your previous clean lap.";
        LastReport = $"Lap {lap}: {r.LastLapMs / 60000} minutes {N(r.LastLapMs % 60000 / 1000.0)} seconds." + delta;
        if (bestTime != 0 && r.LastLapMs < bestTime) LastReport += " New clean session best.";
        bestTime = bestTime == 0 ? r.LastLapMs : Math.Min(bestTime, r.LastLapMs); previousTime = r.LastLapMs;
        if (!HasReference || r.LastLapMs < referenceTime) {
            var first = !HasReference;
            reference = current; referenceTime = r.LastLapMs; ReferenceLap = lap; BuildZones();
            LastAdvice = $"Reference is lap {ReferenceLap}. Comparing braking zones on this lap.";
            if (first) LastReport += " Reference learned. Comparisons start now.";
        }
        report = LastReport; reportAt = now;
    }
    private void BuildZones()
    {
        zones.Clear(); var start = -1; var straight = 0;
        for (var i = 0; i < Math.Min(Capacity, length / Step); i++) {
            if (reference[i] is not { } p) { start = -1; straight = 0; continue; }
            if (start < 0) { if (p.Brake >= .2 && p.Speed > 80) start = Math.Max(0, i - 1); continue; }
            straight = p.Brake < .05 && Math.Abs(p.Steer) < .12 && p.Throttle > .7 ? straight + 1 : 0;
            if (i - start > 30) { start = -1; straight = 0; }
            else if (straight >= 3 && i - start >= 4) { zones.Add(new(start, i)); start = -1; straight = 0; }
        }
    }
    public string? TakeReport(double now) { var value = now - reportAt <= 15000 ? report : null; report = null; return value; }
    public string? TakeAdvice(F1RaceState r, double now) {
        if (now - adviceAt > 5000) advice = null;
        if (invalid || r.Brake > .05 || Math.Abs(r.Steer) > .12) return null;
        var value = advice; advice = null; return value;
    }
    private void Reset() {
        current = new Point?[Capacity]; reference = new Point?[Capacity]; zones.Clear();
        referenceTime = previousTime = bestTime = 0; lap = lastBin = -1; invalid = true; report = advice = null;
        lastSample = lastAdvice = double.NegativeInfinity; ReferenceLap = 0;
        LastReport = "Complete a clean lap to start lap comparisons.";
        LastAdvice = "Learning your first clean lap. Coaching starts on the following lap.";
    }
}
