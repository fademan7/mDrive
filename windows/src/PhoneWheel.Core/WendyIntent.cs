using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

public enum DriverIntent { UNKNOWN, GET_GAP_AHEAD, GET_GAP_BEHIND, GET_TYRE_STATUS, GET_FUEL, GET_ERS, GET_DAMAGE, GET_POSITION, GET_LAP, GET_WEATHER, GET_FLAGS, GET_PENALTIES, GET_PIT_STATUS, GET_BRAKE_BIAS, GET_DIFFERENTIAL, DRIVER_FEEDBACK, CONFIRM, REJECT, CHANGE_SETTING, GET_PIT_ADVICE, PLAN_PIT, CANCEL_PIT, GET_LAP_REPORT, GET_COACHING, GET_TYRE_TEMPERATURE, GET_TYRE_PRESSURE, GET_TYRE_AGE, GET_BRAKE_TEMPERATURE, GET_ENGINE_TEMPERATURE, GET_LAPS_REMAINING,
    RADIO_CHECK, GET_HELP, GET_SPEED, GET_GEAR, GET_RPM, GET_DRS, GET_TYRE_COMPOUND, GET_TRACK_TEMPERATURE, GET_AIR_TEMPERATURE, GET_SESSION_TIME, GET_PIT_LIMITER, GET_PIT_STOPS, GET_WING_SETUP, GET_LAP_VALIDITY, GET_LAST_LAP_TIME, GET_SECTOR, GET_ERS_MODE, GET_WEATHER_FORECAST, GET_GAP_LEADER }
public enum DriverSymptom { NONE, UNDERSTEER, OVERSTEER, REAR_INSTABILITY, POOR_TRACTION, WHEELSPIN, FRONT_LOCKING, REAR_BRAKING_INSTABILITY, HIGH_SPEED_INSTABILITY, KERB_INSTABILITY, TYRE_OVERHEATING, UNEVEN_WEAR, EXCESSIVE_DEGRADATION, AERO_BALANCE, BOTTOMING, WEAK_ROTATION, STRAIGHT_LINE_SPEED }
public enum CornerPhase { UNKNOWN, CORNER_ENTRY, MID_CORNER, CORNER_EXIT, STRAIGHT }
public enum DrivingCondition { UNKNOWN, ON_THROTTLE, ON_BRAKE, COASTING, OVER_KERB, HIGH_SPEED }
public enum DriverWheel { ANY, FRONT_LEFT, FRONT_RIGHT, REAR_LEFT, REAR_RIGHT }

// The model cannot return telemetry, free-form speech, proposed values, or an
// executable command. Even CONFIRM is checked against the original utterance.
public sealed record WendyIntent(DriverIntent Intent, DriverSymptom Symptom = DriverSymptom.NONE,
    CornerPhase Phase = CornerPhase.UNKNOWN, DrivingCondition Condition = DrivingCondition.UNKNOWN,
    DriverWheel Wheel = DriverWheel.ANY)
{
    public static readonly WendyIntent Unknown = new(DriverIntent.UNKNOWN);
    public static object Schema => new {
        type = "object", additionalProperties = false,
        properties = new Dictionary<string, object> {
            ["intent"] = Choice<DriverIntent>(), ["symptom"] = Choice<DriverSymptom>(),
            ["phase"] = Choice<CornerPhase>(), ["condition"] = Choice<DrivingCondition>(), ["wheel"] = Choice<DriverWheel>()
        }, required = new[] { "intent", "symptom", "phase", "condition", "wheel" }
    };
    private static object Choice<T>() where T : struct, Enum => new { type = "string", @enum = Enum.GetNames<T>() };
    public static WendyIntent Parse(string json)
    {
        try {
            if (json.Length > 1024) return Unknown;
            using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
            var keys = root.EnumerateObject().Select(p => p.Name).ToArray();
            if (keys.Length != 5 || keys.Distinct().Count() != 5 || keys.Any(k => k is not ("intent" or "symptom" or "phase" or "condition" or "wheel"))) return Unknown;
            T Read<T>(string name) where T : struct, Enum {
                var raw = root.GetProperty(name).GetString();
                if (raw == null || !Enum.GetNames<T>().Contains(raw)) throw new JsonException();
                return Enum.Parse<T>(raw);
            }
            var value = new WendyIntent(Read<DriverIntent>("intent"), Read<DriverSymptom>("symptom"), Read<CornerPhase>("phase"), Read<DrivingCondition>("condition"), Read<DriverWheel>("wheel"));
            if (value.Intent != DriverIntent.DRIVER_FEEDBACK && (value.Symptom != DriverSymptom.NONE || value.Phase != CornerPhase.UNKNOWN || value.Condition != DrivingCondition.UNKNOWN)) return Unknown;
            if (value.Intent == DriverIntent.DRIVER_FEEDBACK && value.Symptom == DriverSymptom.NONE) return Unknown;
            return value;
        } catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) { return Unknown; }
    }
}

public static class WendyLanguage
{
    public static DriverWheel ExplicitWheel(string text) {
        var q = Normalize(text);
        return q.Contains("front left") ? DriverWheel.FRONT_LEFT : q.Contains("front right") ? DriverWheel.FRONT_RIGHT : q.Contains("rear left") ? DriverWheel.REAR_LEFT : q.Contains("rear right") ? DriverWheel.REAR_RIGHT : DriverWheel.ANY;
    }
    public static string Normalize(string text) => Regex.Replace(Regex.Replace(text.ToLowerInvariant().Replace('’', '\''), "[^a-z0-9 ]", ""), " +", " ").Trim();
    public static bool IsConfirm(string text) => Normalize(text) is "yes" or "agree" or "agreed" or "yeah do it" or "sounds good" or "go ahead" or "yes do it" or "do it";
    public static bool IsReject(string text) => Normalize(text) is "no" or "no leave it" or "dont change it" or "leave it" or "reject" or "cancel";
    public static bool IsCommand(string text) => Regex.IsMatch(Normalize(text), @"^(set|increase|decrease|change|adjust|box|pit this lap)\b") || Normalize(text).Contains("next stop");
    // Closed phrases for plans; negative/ambiguous phrases never become a plan.
    public static WendyIntent? RaceRequest(string text) {
        var q = Regex.Replace(Normalize(text), @"^(hey )?wendy +", "");
        if (WendyDataQueries.Match(q) is { } dataQuery) return new(dataQuery);
        if (TelemetryQuery(q) is { } telemetry) return telemetry;
        if (Regex.IsMatch(q, @"^(what is|whats|tell me) (my |the |current )?brake (bias|balance)( value)?$")) return new(DriverIntent.GET_BRAKE_BIAS);
        if (Regex.IsMatch(q, @"^((what is|whats|tell me) (my |the |current )?)?(diff|differential)( value| setting| set to)?$")) return new(DriverIntent.GET_DIFFERENTIAL);
        if (q is "box" or "box box" or "box this lap" or "pit this lap" or "lets box" or "lets pit" or "i want to pit" or "request pit stop") return new(DriverIntent.PLAN_PIT);
        if (q is "stay out" or "cancel pit" or "cancel pit stop" or "cancel that" or "dont box" or "dont pit") return new(DriverIntent.CANCEL_PIT);
        if (Regex.IsMatch(q, @"^(do (i|we) need (to )?(pit|box)( in)?|should (i|we) (pit|box)( now| this lap)?|when should (i|we) pit|is it time to pit|pit strategy|pit window)$")) return new(DriverIntent.GET_PIT_ADVICE);
        if (Regex.IsMatch(q, @"^(how was (my |the )?last lap|whats (my |the )?(last lap time|lap time|pace)|lap report|last lap|am i getting faster)$")) return new(DriverIntent.GET_LAP_REPORT);
        if (q is "any advice" or "how can i improve" or "coach me" or "coaching status" or "how was that corner") return new(DriverIntent.GET_COACHING);
        return null;
    }
    private static WendyIntent? TelemetryQuery(string q) {
        if (IsCommand(q)) return null;
        var brake = Regex.IsMatch(q, @"\bbrakes?\b");
        var engine = Regex.IsMatch(q, @"\bengine\b");
        var tyre = Regex.IsMatch(q, @"\b(tyres?|tires?)\b") || (!brake && !engine && ExplicitWheel(q) != DriverWheel.ANY);
        var temperature = Regex.IsMatch(q, @"\b(temps?|temperatures?|hot|cold)\b");
        if (temperature && (new[] { tyre, brake, engine }.Count(v => v) > 1)) return WendyIntent.Unknown;
        if (temperature && (tyre || brake || engine)) return new(tyre ? DriverIntent.GET_TYRE_TEMPERATURE : brake ? DriverIntent.GET_BRAKE_TEMPERATURE : DriverIntent.GET_ENGINE_TEMPERATURE, Wheel: ExplicitWheel(q));
        if (tyre && Regex.IsMatch(q, @"\b(pressures?|psi)\b")) return new(DriverIntent.GET_TYRE_PRESSURE, Wheel: ExplicitWheel(q));
        if (tyre && Regex.IsMatch(q, @"\b(age|old|how many laps)\b")) return new(DriverIntent.GET_TYRE_AGE);
        if (Regex.IsMatch(q, @"\b(laps? (left|remaining|to go)|how (many|much) laps? (left|remain))\b") && !q.Contains("fuel")) return new(DriverIntent.GET_LAPS_REMAINING);
        return null;
    }

    // Limited offline fallback, explicitly reported as Rules in the diagnostics.
    // No default-to-tyres path; mixed categories are intentionally UNKNOWN.
    public static WendyIntent Fallback(string text)
    {
        if (text.Length is 0 or > 240) return WendyIntent.Unknown;
        var q = Normalize(text);
        if (RaceRequest(q) is { } raceRequest) return raceRequest;
        if (IsConfirm(q)) return new(DriverIntent.CONFIRM);
        if (IsReject(q)) return new(DriverIntent.REJECT);
        if (IsCommand(q)) return new(DriverIntent.CHANGE_SETTING);
        var hits = new HashSet<DriverIntent>();
        void Add(DriverIntent intent, string pattern) { if (Regex.IsMatch(q, pattern)) hits.Add(intent); }
        Add(DriverIntent.GET_GAP_BEHIND, @"\b(behind|chasing|following me|on my tail)\b");
        Add(DriverIntent.GET_GAP_AHEAD, @"\b(gap|ahead|in front|next car)\b");
        if (hits.Contains(DriverIntent.GET_GAP_BEHIND)) hits.Remove(DriverIntent.GET_GAP_AHEAD);
        Add(DriverIntent.GET_TYRE_STATUS, @"\b(tyres?|tires?|wear)\b");
        Add(DriverIntent.GET_FUEL, @"\b(fuel|petrol|gas)\b");
        Add(DriverIntent.GET_ERS, @"\b(ers|battery|energy)\b");
        Add(DriverIntent.GET_DAMAGE, @"\b(damage|broken)\b");
        Add(DriverIntent.GET_POSITION, @"\b(position|place|where am i)\b");
        Add(DriverIntent.GET_LAP, @"\b(lap|laps)\b");
        Add(DriverIntent.GET_WEATHER, @"\b(weather|rain|raining)\b");
        Add(DriverIntent.GET_FLAGS, @"\b(flag|flags|safety car|vsc)\b");
        Add(DriverIntent.GET_PENALTIES, @"\b(penalties|penalty|warnings|track limits)\b");
        Add(DriverIntent.GET_PIT_STATUS, @"\b(pit|pits|pitting)\b");
        Add(DriverIntent.GET_BRAKE_BIAS, @"\bbrake bias\b");
        Add(DriverIntent.GET_DIFFERENTIAL, @"\b(differential|diff)\b");
        if (hits.Contains(DriverIntent.GET_FUEL)) hits.Remove(DriverIntent.GET_LAP);
        if (hits.Count != 1) return WendyIntent.Unknown;
        var wheel = q.Contains("front left") ? DriverWheel.FRONT_LEFT : q.Contains("front right") ? DriverWheel.FRONT_RIGHT : q.Contains("rear left") ? DriverWheel.REAR_LEFT : q.Contains("rear right") ? DriverWheel.REAR_RIGHT : DriverWheel.ANY;
        return new(hits.Single(), Wheel: wheel);
    }
}
