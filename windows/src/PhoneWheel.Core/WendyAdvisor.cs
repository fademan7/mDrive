namespace PhoneWheel.Core;

public sealed record AdvisorProposal(SetupRecommendation Recommendation, string? Command, string Hud);

// Repeated DRIVER-REPORTED symptoms, not unvalidated slip/lock detection.
// Each report must be backed by a clean completed lap with the relevant input
// phase. Numeric proposals are a single reversible trial, never a diagnosis.
public sealed class WendyAdvisor
{
    private int generation = -1, lap, compound, age, weather, lastTrialLap = -100;
    private uint setup;
    private double last = double.NegativeInfinity, exitTime, entryTime, lastTrial = double.NegativeInfinity;
    private bool dirty = true;
    private float maxDistance;
    private readonly Queue<(int Lap, DriverSymptom Symptom)> reports = new();
    private DriverSymptom reported;
    private AdvisorProposal? pending;
    private double pendingAt;
    private double lastOffer = double.NegativeInfinity;
    private uint pendingSetup;
    private static bool Suitable(F1RaceState r, double now) => r.Live(now) && r.Result == 2 && r.Pit == 0 && !r.LapInvalid && r.Flag(now) == "GREEN" &&
        F1RaceState.Fresh(r.LapMs, now) && F1RaceState.Fresh(r.StatusMs, now) && F1RaceState.Fresh(r.SetupMs, now) && F1RaceState.Fresh(r.DamageMs, now) && F1RaceState.Fresh(r.TelemetryMs, now) &&
        r.FrontWing == 0 && r.OtherDamage == 0 && r.Wear.Max() < 55 && r.InnerTemperature.All(t => t is >= 75 and <= 110) && r.TrackLength > 500;
    public void Observe(F1RaceState r, double now)
    {
        if (generation != r.Generation || setup != r.SetupSignature || compound != r.TyreCompound || age > r.TyreAge || weather != r.Weather) {
            reports.Clear(); pending = null; reported = DriverSymptom.NONE; lap = 0; dirty = true;
            generation = r.Generation; setup = r.SetupSignature; compound = r.TyreCompound; weather = r.Weather;
        }
        age = r.TyreAge;
        if (!Suitable(r, now)) { dirty = true; pending = null; return; }
        if (now - last < 100) return;
        var dt = now - last; last = now;
        if (dt > 1000) { dirty = true; reports.Clear(); pending = null; }
        if (r.Lap != lap) {
            if (r.Lap == lap + 1 && !dirty && maxDistance >= r.TrackLength * .85 && r.LapDistance < r.TrackLength * .05 && reported != DriverSymptom.NONE &&
                (IsExit(reported) ? exitTime >= 3000 : entryTime >= 3000)) {
                reports.Enqueue((lap, reported)); while(reports.Count > 4) reports.Dequeue();
            } else reports.Clear();
            lap = r.Lap; reported = DriverSymptom.NONE; entryTime = exitTime = 0; maxDistance = 0;
            dirty = r.LapDistance > r.TrackLength * .05;
        }
        maxDistance = Math.Max(maxDistance, r.LapDistance);
        if (dt is > 0 and <= 250 && Math.Abs(r.TelemetryMs-r.LapMs) < 150 && r.SpeedKph is >= 40 and <= 200) {
            if (r.Throttle > .5 && r.Brake < .05 && Math.Abs(r.Steer) > .1) exitTime += dt;
            if (r.Brake > .3) entryTime += dt;
        }
    }
    private static bool IsExit(DriverSymptom s) => s is DriverSymptom.REAR_INSTABILITY or DriverSymptom.OVERSTEER or DriverSymptom.POOR_TRACTION or DriverSymptom.WHEELSPIN;
    public string? Feedback(WendyIntent intent, F1RaceState r, double now) => Propose(intent,r,now,true);
    public string? Proactive(F1RaceState r, double now) {
        if (pending != null || reports.Count < 3 || now-lastOffer < 180000 || r.SpeedKph < 80 || Math.Abs(r.Steer) > .1 || r.Brake > .05) return null;
        var symptom=reports.Last().Symptom;
        return Propose(new(DriverIntent.DRIVER_FEEDBACK,symptom,IsExit(symptom)?CornerPhase.CORNER_EXIT:CornerPhase.CORNER_ENTRY),r,now,false);
    }
    private string? Propose(WendyIntent intent, F1RaceState r, double now, bool record)
    {
        if (!Suitable(r, now) || dirty || now - lastTrial < 180000 || r.Lap - lastTrialLap < 3) return null;
        var symptom = intent.Symptom;
        if (IsExit(symptom) && intent.Phase != CornerPhase.CORNER_EXIT) return "Does the rear problem happen on corner exit? No setup change proposed yet.";
        if (symptom is not (DriverSymptom.UNDERSTEER or DriverSymptom.FRONT_LOCKING) && !IsExit(symptom)) return null;
        if (symptom == DriverSymptom.UNDERSTEER && intent.Phase != CornerPhase.CORNER_ENTRY) return "Is the turning problem on entry, mid corner, or exit?";
        if(record) reported = symptom;
        if (reports.Count(x => x.Symptom == symptom || (IsExit(x.Symptom) && IsExit(symptom))) < 3)
            return "Feedback noted. I need the same problem across three clean completed laps before suggesting a setup trial.";
        var evidence = "Repeated driver reports over three clean laps; matching braking or throttle phase; stable setup, tyres and weather. This is not a measured slip diagnosis.";
        var observation = IsExit(symptom) ? "You repeatedly reported rear traction or balance trouble on exit." : symptom == DriverSymptom.FRONT_LOCKING ? "You repeatedly reported front locking during braking." : "You repeatedly reported poor turn-in on entry.";
        string? command = null;
        string recommendation, hud;
        if (IsExit(symptom) && r.Differential >= 15 && r.Differential % 5 == 0) {
            command = $"Set differential to {r.Differential - 5}";
            recommendation = "Try reducing on-throttle differential by five percentage points, then compare. This may not cure the cause.";
            hud = "REAR EXIT · DIFF -5%";
        } else {
            recommendation = symptom == DriverSymptom.FRONT_LOCKING ? "Review braking pressure and front brake balance in the garage; try easing initial brake pressure first. No numeric balance change is justified yet."
                : "Review entry balance and off-throttle differential in the garage. No numeric geometry or aero change is justified yet.";
            hud = symptom == DriverSymptom.FRONT_LOCKING ? "FRONT LOCK · REVIEW" : "TURN-IN · REVIEW";
        }
        var saved = new SetupRecommendation("driver-pattern", r.Session, observation, recommendation, evidence, DateTimeOffset.UtcNow);
        pending = new(saved, command, hud); pendingAt = lastOffer = now; pendingSetup = r.SetupSignature;
        return observation + " " + recommendation + (command == null ? " Save this garage review?" : " Agree?");
    }
    private bool Valid(F1RaceState r, double now) => pending != null && generation == r.Generation && pendingSetup == r.SetupSignature && now >= pendingAt && now - pendingAt <= 30000 && Suitable(r, now);
    public AdvisorProposal? Approve(F1RaceState r, double now) {
        var value = Valid(r, now) ? pending : null; pending = null;
        if(value != null) { lastTrial = now; lastTrialLap = r.Lap; reports.Clear(); }
        return value;
    }
    public string Summary(F1RaceState r, double now) => Valid(r, now) ? pending!.Hud : "";
    public void ClearApproval() => pending = null;
    public void ResetConversation() { pending = null; reports.Clear(); reported = DriverSymptom.NONE; }
}
