using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

public sealed record WendyReply(string Text, string Kind = "answer");

// No model, no network calls, and no reference to the controller output API.
public sealed class WendyEngineer(IWendyActionOutput? output = null, IWendyMenuOutput? menuOutput = null)
{
    private readonly Dictionary<string, double> spoken = [];
    private double lastAlert = double.NegativeInfinity;
    private double lastReply = double.NegativeInfinity;
    private double sessionObserved;
    private int briefingTopic;
    private double lastUnknown = double.NegativeInfinity;
    private readonly Dictionary<string, int> damageLevels = [];
    private int generation = -1;
    private string lastFlag = "UNKNOWN";
    private int? weather;
    private int? pit;
    private int lastPenalty, lastWarnings, lastDriveThrough, lastStopGo;
    private readonly HashSet<string> crossed = [];
    private static string N(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    public WendyPatterns Patterns { get; } = new();
    public WendyCoaching Coaching { get; } = new();
    public WendyTyreLife TyreLife { get; } = new();
    public WendyActions Actions { get; } = new(output);
    public WendyMenuActions MenuActions { get; } = new(menuOutput);
    private bool menuCommand;
    public WendyCommandState CommandState => menuCommand ? MenuActions.State : Actions.State;
    public void TickActions(F1RaceState race, double now) { Actions.Tick(race, now); MenuActions.Tick(race, now); }
    public WendyAdvisor Advisor { get; } = new();
    public string Summary(F1RaceState race, double now) {
        var value = TyreLife.Summary(race, now);
        if (value == "PIT THIS LAP") return value;
        if (race.Live(now) && F1RaceState.Fresh(race.LapMs, now) && race.Pit == 0 && pitPlanGeneration == race.Generation && pitPlanLap == race.Lap) return "PIT THIS LAP";
        return value.Length > 0 ? value : Advisor.Summary(race, now);
    }
    private readonly WendyGreetings greetings = new();
    private int pitPlanLap = -1, pitPlanGeneration = -1;
    private int followingCar = -1;
    private double followingSince = double.NegativeInfinity;
    private bool followingReported;
    public void Observe(F1RaceState race, double now) { Patterns.Observe(race, now); Coaching.Observe(race, now); TyreLife.Observe(race, now); Advisor.Observe(race, now); TickActions(race, now); }
    private SetupRecommendation? pending;
    private int pendingGeneration;
    private uint pendingSetup;
    private double pendingAt;
    public List<SetupRecommendation> Recommendations { get; } = [];
    private WendyIntent? lastQuery;
    private string lastQueryText = "";
    private double lastQueryAt;
    private int lastQueryGeneration;
    public void ResetConversation() { pending = null; lastQuery = null; lastQueryText = ""; Actions.Invalidate(); MenuActions.Invalidate(); Advisor.ResetConversation(); pitPlanLap = -1; }
    public WendyReply Answer(string text, double confidence, F1RaceState race, double now)
        => Answer(WendyLanguage.Fallback(text), text, confidence, race, now);
    public WendyReply Answer(WendyIntent intent, string text, double confidence, F1RaceState race, double now)
    {
        // Repeat re-queries current telemetry, never replays a setting/plan/approval
        // or an old numeric answer. Context is bounded to one topic for 30 seconds.
        if (intent.Intent is DriverIntent.REPEAT_QUERY or DriverIntent.FOLLOW_UP_WHEEL) {
            if (lastQuery == null || lastQueryGeneration != race.Generation || now - lastQueryAt is < 0 or > 30000)
                return new("Which race information would you like?");
            if (intent.Intent == DriverIntent.FOLLOW_UP_WHEEL) {
                if (lastQuery.Intent is not (DriverIntent.GET_TYRE_STATUS or DriverIntent.GET_TYRE_TEMPERATURE or DriverIntent.GET_TYRE_PRESSURE or DriverIntent.GET_BRAKE_TEMPERATURE))
                    return new("Tyre wear, temperature, or pressure?");
                intent = lastQuery with { Wheel = intent.Wheel };
            } else { intent = lastQuery; text = lastQueryText; }
        }
        if (intent.Intent.ToString().StartsWith("GET_", StringComparison.Ordinal)) {
            lastQuery = intent; lastQueryText = text; lastQueryAt = now; lastQueryGeneration = race.Generation;
        } else { lastQuery = null; lastQueryText = ""; }
        var reply = AnswerCore(intent, text, confidence, race, now);
        // Incidental AUTO noise that produces no answer must not postpone radio traffic.
        if (reply.Text.Length > 0) lastReply = now;
        return reply;
    }
    private WendyReply AnswerCore(WendyIntent intent, string text, double confidence, F1RaceState race, double now)
    {
        if (text.Length > 240) return new("Please use a short English request.");
        // Social/radio phrases override classifier mistakes, with or without telemetry.
        if (WendyConversation.RadioCheck(text)) return new("Loud and clear. I can hear you.");
        if (WendyConversation.Reply(text) is {} chat)
            return new(race.Live(now) ? "I'm with you. Let's focus on the race." : chat);
        if (intent.Intent == DriverIntent.UNKNOWN) {
            // AUTO can hear incidental speech: never keep advertising race questions.
            if (!Regex.IsMatch(WendyLanguage.Normalize(text), @"^(?:hey wendy|wendy|what|why|how|can|could|tell|please)\b") || now-lastUnknown<30000) return new("", "silent");
            lastUnknown=now;
            return new(race.Live(now) ? "I didn't catch that." : "I didn't quite catch that. I can have a short chat, but not answer every topic.");
        }
        var q = WendyNaturalLanguage.Request(text);
        // Original utterance gates all experimental input, never a model-created recipe.
        if (menuOutput?.Enabled == true && (WendyMenuActions.TryParse(text, out _) || WendyMenuActions.LooksLikeRequest(text))) {
            pending = null; Advisor.ClearApproval(); menuCommand = true;
            if (Actions.Busy) return new("A setting change is pending. No menu command queued.", "rejected");
            return MenuActions.Request(text, confidence, race, now);
        }
        if (intent.Intent is DriverIntent.REJECT or DriverIntent.CANCEL_PIT) {
            var wasRunning = MenuActions.Busy; MenuActions.Invalidate();
            if (wasRunning) { pending = null; Advisor.ClearApproval(); return new("Menu command stopped. Earlier inputs may have applied; please check the game.", "unconfirmed"); }
        }
        if (MenuActions.Busy && (intent.Intent == DriverIntent.CONFIRM || intent.Intent == DriverIntent.CHANGE_SETTING))
            return new("A menu command is running. No setting change queued.", "rejected");
        if (intent.Intent == DriverIntent.REJECT) { var busy = Actions.Busy; pending = null; Advisor.ResetConversation(); Actions.Invalidate(); return new(busy ? "Understood. No further change will be sent." : "Understood. No change made."); }
        if (intent.Intent == DriverIntent.CONFIRM) {
            if (!WendyLanguage.IsConfirm(text) || !double.IsFinite(confidence) || confidence < .75) return new("Please clearly confirm or reject the recommendation.", "rejected");
            if (Advisor.Approve(race, now) is { } proposal) {
                if (proposal.Command != null && output?.Enabled == true) { menuCommand = false; return Actions.Request(proposal.Command, confidence, race, now); }
                Recommendations.Add(proposal.Recommendation); if (Recommendations.Count > 100) Recommendations.RemoveAt(0);
                return new("Saved for garage review. No game setting changed.", "saved");
            }
            if (pending == null || pendingGeneration != race.Generation || now - pendingAt > 30000 || !race.Live(now) || !F1RaceState.Fresh(race.SetupMs, now) || pendingSetup != race.SetupSignature) { pending = null; return new("There is no current recommendation to confirm. No change made."); }
            Recommendations.Add(pending); if (Recommendations.Count > 100) Recommendations.RemoveAt(0);
            pending = null; return new("Saved for next session. No game setting has been changed.", "saved");
        }
        pending = null; // A different question ends the approval context.
        Advisor.ClearApproval();
        if (intent.Intent is DriverIntent.PLAN_PIT or DriverIntent.CANCEL_PIT) {
            if (WendyLanguage.RaceRequest(text)?.Intent != intent.Intent || !double.IsFinite(confidence) || confidence < .75)
                return new("Please clearly repeat the pit plan. No game command sent.", "rejected");
            if (intent.Intent == DriverIntent.CANCEL_PIT) { pitPlanLap = -1; var busy = Actions.Busy; Actions.Invalidate(); return new(busy ? "Pit reminder cancelled. No further setting input will be sent." : "Pit reminder cancelled. No game command sent.", "plan"); }
            if (!race.Live(now) || !F1RaceState.Fresh(race.LapMs, now)) return Missing("Pit planning telemetry");
            pitPlanLap = race.Lap; pitPlanGeneration = race.Generation;
            menuCommand = false;
            Actions.Reject("Pit request unsupported: no verified direct binding or pit-request acknowledgement.");
            return new("Box this lap noted as a reminder. Select the pit request in the game; I cannot send it yet.", "plan");
        }
        if (intent.Intent == DriverIntent.CHANGE_SETTING || WendyLanguage.IsCommand(text))
        {
            if (WendyMenuActions.TryParse(text, out _) || WendyMenuActions.LooksLikeRequest(text)) { menuCommand = true; return MenuActions.Request(text, confidence, race, now); }
            menuCommand = false;
            return output?.Enabled == true ? Actions.Request(text, confidence, race, now) : new("Automatic game setting changes are not supported. No change made.", "rejected");
        }
        if (intent.Intent == DriverIntent.UNKNOWN) return new("Please ask one topic: gaps, tyres, fuel, damage, or pit advice.");
        if (intent.Intent == DriverIntent.SMALL_TALK) return new(race.Live(now) ? "Let\'s keep the radio for racing." : WendyConversation.Reply(text) ?? "I\'m here and listening.");
        if (intent.Intent == DriverIntent.RADIO_CHECK) return new("Loud and clear. Wendy received your request.");
        if (intent.Intent == DriverIntent.GET_HELP && !race.Live(now)) return new("We can have a short chat or a radio check. Race information is unavailable until F1 telemetry is live.");
        if (intent.Intent == DriverIntent.GET_HELP) return new(menuOutput?.Enabled == true
            ? "Practice menu trial is on. While stopped, say box box or prepare soft, medium, hard, inters or wets. Only calibrated commands run; results are unconfirmed. Say cancel to stop."
            : "Ask about tyres, temperatures, gaps, fuel, ERS, damage, DRS, laps, penalties, weather or pit advice. Box box sets a reminder, not a game command.");
        if (!race.Live(now)) return new("Race information is unavailable right now. We can chat while you are off track.");
        if (intent.Intent == DriverIntent.GET_RACE_SUMMARY) {
            if (!F1RaceState.Fresh(race.LapMs, now)) return Missing("Race summary");
            var summary = $"P {race.Position}, lap {race.Lap}. {FlagText(race.Flag(now))}";
            if (race.Ahead is double gap) summary += $" Ahead {N(gap)} seconds.";
            if (race.Behind is double behind) summary += $" Behind {N(behind)} seconds.";
            if (q.Contains("full race", StringComparison.Ordinal)) {
                if (F1RaceState.Fresh(race.DamageMs, now)) summary += $" Highest tyre wear {Math.Round(race.Wear.Max())} percent.";
                if (F1RaceState.Fresh(race.StatusMs, now)) summary += $" Fuel {N(race.FuelKg)} kilograms.";
                if (race.PenaltySeconds > 0) summary += $" Time penalties {race.PenaltySeconds} seconds.";
            }
            return new(summary);
        }
        if (WendyDataQueries.Answer(intent.Intent, race, now) is { } dataReply) return dataReply;
        bool Fresh(double time) => F1RaceState.Fresh(time, now);
        if (intent.Intent == DriverIntent.GET_PIT_ADVICE) return PitAdvice(race, now);
        if (intent.Intent == DriverIntent.GET_LAP_REPORT) return new(Coaching.LastReport);
        if (intent.Intent == DriverIntent.GET_COACHING) return new(Coaching.LastAdvice);
        if (intent.Intent == DriverIntent.GET_TYRE_AGE) return Fresh(race.StatusMs) ? new($"This tyre set is {race.TyreAge} laps old.") : Missing("Tyre age");
        if (intent.Intent == DriverIntent.GET_LAPS_REMAINING) return Fresh(race.LapMs) && race.TotalLaps > 0 && race.Lap > 0 ? new($"{Math.Max(0, race.TotalLaps - race.Lap + 1)} laps remaining, including this lap.") : Missing("Laps remaining");
        if (intent.Intent is DriverIntent.GET_TYRE_TEMPERATURE or DriverIntent.GET_TYRE_PRESSURE or DriverIntent.GET_BRAKE_TEMPERATURE or DriverIntent.GET_ENGINE_TEMPERATURE) {
            if (!Fresh(race.TelemetryMs)) return Missing("Temperature or pressure telemetry");
            if (intent.Intent == DriverIntent.GET_ENGINE_TEMPERATURE) return new($"Engine temperature is {race.EngineTemperature} degrees Celsius.");
            var wheel = intent.Wheel switch { DriverWheel.REAR_LEFT => 0, DriverWheel.REAR_RIGHT => 1, DriverWheel.FRONT_LEFT => 2, DriverWheel.FRONT_RIGHT => 3, _ => -1 };
            var names = new[] { "Rear left", "Rear right", "Front left", "Front right" };
            if (intent.Intent == DriverIntent.GET_TYRE_TEMPERATURE && wheel >= 0) return new($"{names[wheel]} inner temperature is {race.InnerTemperature[wheel]}, surface {race.SurfaceTemperature[wheel]} degrees Celsius.");
            var values = intent.Intent switch {
                DriverIntent.GET_TYRE_TEMPERATURE => race.InnerTemperature.Select(v => N(v)).ToArray(),
                DriverIntent.GET_BRAKE_TEMPERATURE => race.BrakeTemperature.Select(v => N(v)).ToArray(),
                _ => race.TyrePressure.Select(v => N(v)).ToArray()
            };
            var unit = intent.Intent == DriverIntent.GET_TYRE_PRESSURE ? "PSI" : "degrees Celsius";
            var label = intent.Intent == DriverIntent.GET_TYRE_TEMPERATURE ? "Inner tyre temperatures" : intent.Intent == DriverIntent.GET_BRAKE_TEMPERATURE ? "Brake temperatures" : "Tyre pressures";
            return wheel >= 0 ? new($"{names[wheel]}: {values[wheel]} {unit}.") : new($"{label}, {unit}. Front left {values[2]}, front right {values[3]}, rear left {values[0]}, rear right {values[1]}.");
        }
        if (intent.Intent == DriverIntent.DRIVER_FEEDBACK) {
            if (Advisor.Feedback(intent, race, now) is { } advice) return new(output?.Enabled == true ? advice : advice.Replace(" Agree?", " Save this recommendation for manual adjustment?"), "suggestion");
            var suggestion = Patterns.Suggest(intent, race, now);
            if (suggestion == null) return new("Feedback noted. I don't have enough verified telemetry evidence for a setup change.");
            pending = suggestion; pendingAt = now; pendingGeneration = race.Generation; pendingSetup = race.SetupSignature;
            return new(suggestion.Observation + " Save a garage review recommendation for next session?", "suggestion");
        }
        if (intent.Intent == DriverIntent.GET_TYRE_STATUS)
        {
            if (!Fresh(race.DamageMs)) return Missing("Tyre wear");
            if (Regex.IsMatch(q, @"\b(life|remaining|last|left)\b") && intent.Wheel == DriverWheel.ANY)
                return TyreLife.Estimate(race, now) is { } life ? new($"Approximately {life.Low} to {life.High} laps to the tyre wear planning limit. Not a puncture prediction.") : new("Not enough stable clean laps for a tyre life estimate yet.");
            var names = new[] { "Rear left", "Rear right", "Front left", "Front right" };
            var wheel = intent.Wheel switch { DriverWheel.REAR_LEFT => 0, DriverWheel.REAR_RIGHT => 1, DriverWheel.FRONT_LEFT => 2, DriverWheel.FRONT_RIGHT => 3, _ => -1 };
            if (wheel < 0) wheel = Array.IndexOf(race.Wear, race.Wear.Max());
            return new($"{names[wheel]} wear is {Math.Round(race.Wear[wheel])} percent.");
        }
        if (intent.Intent == DriverIntent.GET_GAP_AHEAD) return Fresh(race.LapMs) && race.Ahead is double ahead ? new($"Gap ahead is {N(ahead)} seconds.") : Missing("Gap ahead");
        if (intent.Intent == DriverIntent.GET_GAP_BEHIND) return Fresh(race.LapMs) && race.Behind is double behind ? new($"Gap behind is {N(behind)} seconds.") : Missing("Gap behind");
        if (intent.Intent == DriverIntent.GET_FUEL) return Fresh(race.StatusMs) ? new($"Fuel is {N(race.FuelKg)} kilograms. MFD fuel estimate is {N(race.FuelMfdLaps)} laps.") : Missing("Fuel");
        // UDP provides joules, not a universal battery capacity across game seasons.
        if (intent.Intent == DriverIntent.GET_ERS) return Fresh(race.StatusMs) ? new($"ERS store is {N(race.ErsJoules / 1_000_000)} megajoules.") : Missing("ERS");
        if (intent.Intent == DriverIntent.GET_DAMAGE) {
            if (!Fresh(race.DamageMs)) return Missing("Damage");
            if (WendyDataQueries.Damage(q, race) is { } detail) return detail;
            if (Regex.IsMatch(q, @"\b(?:wing|front|rear)\b")) return new("Front wing or rear wing damage?");
            return new(Regex.IsMatch(q, @"\b(?:all|full|every|complete)\b") ? WendyDamage.Summary(race) : WendyDamage.Brief(race));
        }
        if (intent.Intent == DriverIntent.GET_WEATHER) return new($"Current weather: {WeatherName(race.Weather)}.");
        if (intent.Intent == DriverIntent.GET_FLAGS) return race.Flag(now) == "UNKNOWN" ? Missing("Flag status") : new(FlagText(race.Flag(now)));
        if (intent.Intent == DriverIntent.GET_LAP) return Fresh(race.LapMs) && race.Lap > 0 ? new($"You are on lap {race.Lap}.") : Missing("Lap");
        if (intent.Intent == DriverIntent.GET_POSITION) return Fresh(race.LapMs) && race.Position > 0 ? new($"You are P {race.Position}.") : Missing("Position");
        if (intent.Intent == DriverIntent.GET_BRAKE_BIAS) return Fresh(race.StatusMs) ? new($"Brake bias is {race.BrakeBias} percent.") : Missing("Brake bias");
        if (intent.Intent == DriverIntent.GET_DIFFERENTIAL) return Fresh(race.SetupMs) ? new($"On throttle differential is {race.Differential} percent. Off throttle is {race.OffThrottleDifferential} percent.") : Missing("Differential");
        if (intent.Intent == DriverIntent.GET_PENALTIES) return Fresh(race.LapMs) ? new($"Time penalties: {race.PenaltySeconds} seconds. Track warnings: {race.TrackWarnings}. Unserved drive through: {race.DriveThrough}. Stop go: {race.StopGo}.") : Missing("Penalties");
        if (intent.Intent == DriverIntent.GET_PIT_STATUS) return Fresh(race.LapMs) ? new(race.Pit switch { 1 => "You are pitting.", 2 => "You are in the pit area.", _ => pitPlanGeneration == race.Generation && pitPlanLap == race.Lap ? "You planned to box this lap. The game pit request is not confirmed." : "You are out on track." }) : Missing("Pit status");
        return new("I didn't understand that.");
    }
    private static WendyReply Missing(string name) => new($"{name} is unavailable.");
    private static WendyReply PitAdvice(F1RaceState r, double now) {
        if (!F1RaceState.Fresh(r.LapMs, now)) return Missing("Pit strategy");
        if (r.Pit != 0) return new("You are already pitting.");
        if (F1RaceState.Fresh(r.DamageMs, now)) {
            if (r.FrontWing >= 20 || r.OtherDamage >= 30 || r.ComponentDamage[16] != 0 || r.ComponentDamage[17] != 0)
                return new(WendyDamage.Brief(r) + " " + WendyDamage.PitGuidance(r));
            if (r.Wear.Max() >= 70) return new("Tyre wear is above 70 percent. Consider pitting soon.");
        }
        if (F1RaceState.Fresh(r.StatusMs, now) && r.Weather >= 3 && r.TyreCompound is >= 16 and <= 22)
            return new("Rain with dry tyres fitted. Consider a tyre change; I cannot judge track grip yet.");
        if (r.PitIdealLap > 0 && r.PitLatestLap >= r.PitIdealLap && r.PitLatestLap <= r.TotalLaps) {
            var rejoin = r.PitRejoinPosition > 0 ? $" Game predicts rejoining P {r.PitRejoinPosition}." : "";
            if (r.Lap >= r.PitIdealLap) return new($"The game's pit target is lap {r.PitIdealLap}, latest lap {r.PitLatestLap}. You are in or past that window." + rejoin);
            return new($"The game's pit target is lap {r.PitIdealLap}, in {r.PitIdealLap - r.Lap} laps." + rejoin);
        }
        return F1RaceState.Fresh(r.DamageMs, now)
            ? new($"No urgent stop indicated by damage or tyre wear. Highest wear is {Math.Round(r.Wear.Max())} percent. The best strategy window is unavailable.")
            : new("Damage and tyre wear are unavailable, so I cannot judge whether you need to pit.");
    }
    public static string WeatherName(int weather) => new[] { "clear", "light cloud", "overcast", "light rain", "heavy rain", "storm" }[Math.Clamp(weather, 0, 5)];
    public static string FlagText(string flag) => flag switch { "SC" => "Safety Car deployed.", "VSC" => "Virtual Safety Car deployed.", "CHECKERED" => "Checkered flag.", _ => $"{char.ToUpperInvariant(flag[0])}{flag[1..].ToLowerInvariant()} flag." };

    // No speech backlog: evaluate current conditions only when Android is idle.
    public WendyReply? Alert(F1RaceState race, double now)
    {
        if (generation != race.Generation) { generation = race.Generation; spoken.Clear(); crossed.Clear(); damageLevels.Clear(); sessionObserved = now; briefingTopic = 0; weather = pit = null; lastFlag = "UNKNOWN"; lastAlert = double.NegativeInfinity; lastPenalty = lastWarnings = lastDriveThrough = lastStopGo = 0; followingCar = -1; followingReported = false; followingSince = double.NegativeInfinity; }
        if (!race.Live(now)) return null;
        if (MenuActions.TakeCompletion() is { } menuCompleted) return menuCompleted with { Kind = "alert" };
        if (Actions.TakeCompletion() is { } completed) return completed with { Kind = "alert" };
        if (pending != null && (pendingGeneration != race.Generation || now - pendingAt > 30000)) pending = null;
        string? key = null, text = null;
        var flag = race.Flag(now);
        var closeBehind = flag == "GREEN" && race.Pit == 0 && F1RaceState.Fresh(race.LapMs, now) && race.Behind is > 0 and <= 1.5 && race.BehindCar >= 0;
        if (!closeBehind || followingCar != race.BehindCar) { followingSince = now; followingReported = false; }
        followingCar = race.BehindCar;
        if (flag != lastFlag && flag is "RED" or "YELLOW" or "BLUE" or "SC" or "VSC" or "CHECKERED") { key = "flag:" + flag; text = FlagText(flag); }
        if (flag != lastFlag && key == null) lastFlag = flag;
        if (now - lastAlert < 8000) return null;
        if (key != null && Sendable(key, now, 30000)) { lastFlag = flag; return Speak(key, text!, now); }
        if (TyreLife.PitNeeded(race, now) && Sendable("pit-wear-plan", now, 120000)) return Speak("pit-wear-plan", "Pit this lap recommended. Tyre wear is near the planning limit. Pit request is not automatic.", now);
        if (F1RaceState.Fresh(race.LapMs, now)) {
            if ((race.PenaltySeconds > lastPenalty || race.DriveThrough > lastDriveThrough || race.StopGo > lastStopGo) && Sendable("penalty", now, 15000)) {
                lastPenalty = race.PenaltySeconds; lastDriveThrough = race.DriveThrough; lastStopGo = race.StopGo;
                return Speak("penalty", $"Penalty update. {race.PenaltySeconds} seconds, {race.DriveThrough} drive through, {race.StopGo} stop go outstanding.", now);
            }
            if (race.TrackWarnings > lastWarnings && Sendable("track-warning", now, 30000)) { lastWarnings = race.TrackWarnings; return Speak("track-warning", $"Track limits warning. {race.TrackWarnings} warnings recorded.", now); }
            lastPenalty = Math.Min(lastPenalty, race.PenaltySeconds); lastDriveThrough = Math.Min(lastDriveThrough, race.DriveThrough); lastStopGo = Math.Min(lastStopGo, race.StopGo); lastWarnings = Math.Min(lastWarnings, race.TrackWarnings);
        }
        if (Patterns.SustainedHotWheel >= 0 && F1RaceState.Fresh(race.TelemetryMs, now) && Sendable("temperature", now, 120000)) {
            var wheel = Patterns.SustainedHotWheel;
            return Speak("temperature", $"{new[] { "Rear left", "Rear right", "Front left", "Front right" }[wheel]} inner tyre temperature has stayed above 110 degrees.", now);
        }
        if (F1RaceState.Fresh(race.DamageMs, now))
        {
            var max = race.Wear.Max(); var wheel = Array.IndexOf(race.Wear, max);
            foreach (var threshold in new[] { 70, 55, 40 })
            {
                key = $"wear:{threshold}";
                if (max < threshold - 5) crossed.Remove(key);
                if (max >= threshold && !crossed.Contains(key) && Sendable(key, now, 120000))
                {
                    foreach (var lower in new[] {40, 55, 70}.Where(v => v <= threshold)) crossed.Add($"wear:{lower}");
                    return Speak(key, $"{new[] {"Rear left", "Rear right", "Front left", "Front right"}[wheel]} tyre wear is above {threshold} percent.", now);
                }
            }
            var findings = WendyDamage.Findings(race);
            int Level(WendyDamage.Finding f) => f.Fault ? 3 : f.Key is "part:10" or "part:11" or "part:12" or "part:13" or "part:14" or "part:15"
                ? f.Value >= 95 ? 3 : f.Value >= 85 ? 2 : f.Value >= 70 ? 1 : 0
                : f.Value >= 40 ? 3 : f.Value >= 20 ? 2 : f.Value >= 10 ? 1 : 0;
            foreach (var oldKey in damageLevels.Keys.ToArray()) {
                var current = findings.FirstOrDefault(f => f.Key == oldKey);
                if (current == null || Level(current) < damageLevels[oldKey]) damageLevels[oldKey] = current == null ? 0 : Level(current);
            }
            var worsening = findings.Where(f => Level(f) > damageLevels.GetValueOrDefault(f.Key)).ToArray();
            if (worsening.Length > 0 && Sendable("damage", now, 30000)) {
                foreach (var f in worsening.Take(3)) damageLevels[f.Key] = Level(f);
                return Speak("damage", string.Join(" ", worsening.Take(3).Select(f => f.Text)) + " " + WendyDamage.PitGuidance(race), now);
            }
        }
        if (F1RaceState.Fresh(race.StatusMs, now))
        {
            if (race.FuelKg > 4) crossed.Remove("fuel");
            if (race.FuelKg < 3 && !crossed.Contains("fuel") && Sendable("fuel", now, 120000)) { crossed.Add("fuel"); return Speak("fuel", "Low fuel. Less than three kilograms remaining.", now); }
        }
        if (weather is int oldWeather && oldWeather != race.Weather && Sendable("weather", now, 60000)) { weather = race.Weather; return Speak("weather", $"Weather changed to {WeatherName(race.Weather)}.", now); }
        weather ??= race.Weather;
        if (F1RaceState.Fresh(race.LapMs, now))
        {
            if (pit is int oldPit && oldPit != race.Pit && race.Pit == 2 && Sendable("pit", now, 60000)) { pit = race.Pit; return Speak("pit", "You are in the pit lane.", now); }
            pit = race.Pit;
        }
        if (race.PitEvent != null && F1RaceState.Fresh(race.PitEventMs, now, 8000) && Sendable("teammate-pit", now, 60000)) return Speak("teammate-pit", race.PitEvent, now);
        if (greetings.Take(race, now) is { } greeting) return Speak("greeting", greeting, now);
        if (closeBehind && !followingReported && now - followingSince >= 3000 && Sendable("close-behind", now, 120000)) {
            followingReported = true; return Speak("close-behind", $"Car behind is {N(race.Behind!.Value)} seconds back.", now);
        }
        if (flag == "GREEN" && race.Pit == 0 && F1RaceState.Fresh(race.LapMs, now)) {
            if (pitPlanGeneration == race.Generation && pitPlanLap == race.Lap && race.TrackLength > 0 && race.LapDistance > race.TrackLength * .7 && Sendable($"pit-plan:{race.Lap}", now, 120000))
                return Speak($"pit-plan:{race.Lap}", "Pit reminder: you planned to box this lap. Make the pit request in the game.", now);
            if (Coaching.TakeReport(now) is { } lapReport) return Speak("lap-report", lapReport, now);
            if (Coaching.TakeAdvice(race, now) is { } advice) return Speak("corner-coach", advice, now);
        }
        if (Patterns.GapTrend is string trend && F1RaceState.Fresh(race.LapMs, now) && Sendable("gap-trend", now, 90000)) return Speak("gap-trend", trend == "closing" ? "You are steadily closing on the car ahead." : "The gap to the car ahead is steadily increasing.", now);
        if (Patterns.PaceLoss && Sendable("pace", now, 180000)) return Speak("pace", "Your last three clean laps have each been slower. The cause is not confirmed.", now);
        if (pending == null && !Actions.Busy && Advisor.Proactive(race,now) is { } proposal) return Speak("driver-pattern", output?.Enabled == true ? proposal : proposal.Replace(" Agree?", " Save this recommendation for manual adjustment?"), now);
        if (pending == null && Sendable("setup-review", now, 300000)) {
            var suggestion = Patterns.Suggest(new(DriverIntent.DRIVER_FEEDBACK, Patterns.RepeatedHeat ? DriverSymptom.TYRE_OVERHEATING : DriverSymptom.UNEVEN_WEAR), race, now);
            if (suggestion != null) { pending = suggestion; pendingAt = now; pendingGeneration = race.Generation; pendingSetup = race.SetupSignature; return Speak("setup-review", suggestion.Observation + " Save a garage review recommendation for next session?", now); }
        }
        // Optional useful radio traffic, never filler, stale facts, or during a
        // corner/braking/flags. Existing urgent events retain priority above.
        if (flag == "GREEN" && race.Pit == 0 && race.Result == 2 &&
            F1RaceState.Fresh(race.LapMs, now) && F1RaceState.Fresh(race.TelemetryMs, now) &&
            race.SpeedKph >= 60 && Math.Abs(race.Steer) < .2 && race.Brake < .1 &&
            now - Math.Max(sessionObserved, lastAlert) >= 45000 && now - lastReply >= 12000) {
            for (var attempt = 0; attempt < 7; attempt++) {
                var topic = briefingTopic++ % 7;
                var briefing = topic switch {
                    0 when race.Ahead is double ahead => $"Gap ahead {N(ahead)} seconds." + (race.Behind is double behind ? $" Behind {N(behind)} seconds." : ""),
                    1 when F1RaceState.Fresh(race.DamageMs, now) => $"Highest tyre wear {Math.Round(race.Wear.Max())} percent." + (race.Wear.Max() >= 55 ? " Plan your next stop around tyre condition." : ""),
                    2 when F1RaceState.Fresh(race.StatusMs, now) => $"Fuel {N(race.FuelKg)} kilograms. MFD fuel estimate {N(race.FuelMfdLaps)} laps.",
                    3 => $"Weather is {WeatherName(race.Weather)}. Track temperature {race.TrackTemperature} degrees.",
                    4 when F1RaceState.Fresh(race.DamageMs,now) => WendyDamage.Brief(race),
                    5 when F1RaceState.Fresh(race.TelemetryMs,now) => $"Inner tyre temperatures range from {race.InnerTemperature.Min()} to {race.InnerTemperature.Max()} degrees.",
                    6 when race.Forecast.Length>0 => WendyDataQueries.Answer(DriverIntent.GET_WEATHER_FORECAST,race,now)?.Text,
                    _ => null
                };
                if (briefing != null && Sendable($"briefing:{topic}", now, 180000)) return Speak($"briefing:{topic}", briefing, now);
            }
        }
        return null;
    }
    private bool Sendable(string key, double now, double cooldown) => !spoken.TryGetValue(key, out var previous) || now - previous >= cooldown;
    private WendyReply Speak(string key, string text, double now) { spoken[key] = now; lastAlert = now; return new(text, "alert"); }
}
