using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

// Bounded semantic slots before the optional CPU classifier. No telemetry,
// inference process, controller output, or command execution belongs here.
public static class WendyNaturalLanguage
{
    private static bool Has(string q, string pattern) => Regex.IsMatch(q, @"\b(?:" + pattern + @")\b");
    public static string Request(string text) {
        var q = WendyLanguage.Normalize(text);
        q = Regex.Replace(q, @"^(?:ok|okay|alright)\s+", "");
        q = Regex.Replace(q, @"\b(d r s|e r s|r p m)\b", m => m.Value.Replace(" ", ""));
        for (var i = 0; i < 4; i++) {
            q = Regex.Replace(q, @"^(?:(?:hey |hello |hi )?wendy[, ]*|please |(?:can|could|would|will) you (?:please )?|(?:tell|give|show) me (?:an? update on |about )?|let me know (?:about )?|id like to know |i want to know )", "");
        }
        q = Regex.Replace(q, @"(?: please| for me| right now| thanks| thank you)+$", "");
        return q.Trim();
    }

    public static WendyIntent? Match(string text) {
        var q = Request(text);
        if (q.Length == 0) return new(DriverIntent.RADIO_CHECK);
        if (q is "hear me" or "hear me clearly" or "read me" or "you there" or "radio check") return new(DriverIntent.RADIO_CHECK);
        if (Has(q, "(?:ignore|override|forget).*(?:rules|instructions|prompt)")) return WendyIntent.Unknown;
        if (Has(q, "(?:dont|do not|never).*go ahead")) return WendyIntent.Unknown;
        if (q is "say again" or "repeat that" or "repeat please" or "what did you say" or "say that again") return new(DriverIntent.REPEAT_QUERY);
        if (q is "race update" or "race summary" or "race status" or "race situation" or "how are we doing" or "give me a race update" or "whats the situation") return new(DriverIntent.GET_RACE_SUMMARY);
        if (q is "hello" or "hi" or "hey" or "hello there" or "how are you" or "how are you doing" or "thanks" or "thank you" or "thank you wendy" or "good job" or "nice work" or "lets chat" or "i want to chat" or "a joke" or "im nervous" or "im frustrated" or "wish me luck" or "who are you" or "are you a real person") return new(DriverIntent.SMALL_TALK);
        if (q is "and behind" or "what about behind" or "and the car behind") return new(DriverIntent.GET_GAP_BEHIND);
        if (q is "and ahead" or "what about ahead" or "and the car ahead") return new(DriverIntent.GET_GAP_AHEAD);
        if (Regex.IsMatch(q, @"^(?:and |what about )?(?:the )?(?:front left|front right|rear left|rear right|left front|right front|left rear|right rear)(?: one| tyre| tire)?$"))
            return new(DriverIntent.FOLLOW_UP_WHEEL, Wheel: WendyLanguage.ExplicitWheel(q));
        // Never turn a negated/conditional request into a pit plan or setting write.
        if (Has(q, "(?:dont|do not|never|not|maybe|might|if).*(?:set|change|increase|decrease|adjust|box|pit)")) return WendyIntent.Unknown;
        if (WendyLanguage.IsCommand(q)) return new(DriverIntent.CHANGE_SETTING);

        var wheel = WendyLanguage.ExplicitWheel(q);
        var tyre = Has(q, "tyres?|tires?|rubber|compound");
        var brake = Has(q, "brakes?"); var engine = Has(q, "engine|motor");
        var temp = Has(q, "temps?|temperatures?|hot|cold|heat");
        // Statements about handling stay feedback; do not collapse to a tyre query.
        if (!Regex.IsMatch(q, @"^(?:what|whats|how|is|are|do|does|which)\b")) {
            DriverSymptom symptom = Has(q, "understeer|pushing wide|(?:doesnt|does not|wont|will not) (?:want to )?turn|front.*wont turn") ? DriverSymptom.UNDERSTEER
                : Has(q, "locking.*fronts?|fronts?.*lock(?:ing)?") ? DriverSymptom.FRONT_LOCKING
                : Has(q, "rear.*(?:loose|unstable)") ? DriverSymptom.REAR_INSTABILITY
                : Has(q, "oversteer|rear.*slid(?:ing|es?)") ? DriverSymptom.OVERSTEER
                : Has(q, "wheelspin|wheels? spinning") ? DriverSymptom.WHEELSPIN
                : Has(q, "(?:poor|no|losing) traction") ? DriverSymptom.POOR_TRACTION
                : tyre && Has(q, "overheating|too hot") ? DriverSymptom.TYRE_OVERHEATING
                : DriverSymptom.NONE;
            if (symptom != DriverSymptom.NONE) {
                var phase = Has(q, "exit|back on.*power|accelerating out") ? CornerPhase.CORNER_EXIT
                    : Has(q, "entry|turn in|braking") ? CornerPhase.CORNER_ENTRY
                    : Has(q, "apex|mid corner|halfway") ? CornerPhase.MID_CORNER
                    : Has(q, "straight") ? CornerPhase.STRAIGHT : CornerPhase.UNKNOWN;
                var condition = Has(q, "power|throttle|accelerating") ? DrivingCondition.ON_THROTTLE
                    : Has(q, "braking|on.*brake") ? DrivingCondition.ON_BRAKE
                    : Has(q, "kerbs?|curbs?") ? DrivingCondition.OVER_KERB
                    : Has(q, "high speed") ? DrivingCondition.HIGH_SPEED : DrivingCondition.UNKNOWN;
                return new(DriverIntent.DRIVER_FEEDBACK, symptom, phase, condition);
            }
        }

        var hits = new HashSet<DriverIntent>();
        void Add(bool when, DriverIntent intent) { if (when) hits.Add(intent); }
        var gap = Has(q, "gaps?|interval|ahead|behind|in front|next car|on my tail|chasing me|following me");
        if (gap) {
            var behind = Has(q, "behind|on my tail|chasing me|following me");
            var ahead = Has(q, "ahead|in front|next car"); var leader = Has(q, "leader|leading car");
            if ((ahead && behind) || (leader && behind)) return WendyIntent.Unknown;
            hits.Add(leader ? DriverIntent.GET_GAP_LEADER : behind ? DriverIntent.GET_GAP_BEHIND : DriverIntent.GET_GAP_AHEAD);
        }
        var fuel = Has(q, "fuel|petrol|gas");
        var damage = Has(q, "damage|damaged|broken|fault");
        var pit = Has(q, "pit|pits|pitting|box|stop|stops");
        var tyreAge = tyre && Has(q, "age|old|how many laps");
        var tyreCompound = tyre && Has(q, "compound|which|what.*(?:am i on|are we on|using)");
        if (temp) {
            Add(tyre || (!brake && !engine && wheel != DriverWheel.ANY), DriverIntent.GET_TYRE_TEMPERATURE);
            Add(brake, DriverIntent.GET_BRAKE_TEMPERATURE); Add(engine, DriverIntent.GET_ENGINE_TEMPERATURE);
            Add(Has(q, "track|asphalt"), DriverIntent.GET_TRACK_TEMPERATURE);
            Add(Has(q, "air|ambient"), DriverIntent.GET_AIR_TEMPERATURE);
        }
        if (tyre && !temp && !damage) hits.Add(Has(q, "pressures?|psi") ? DriverIntent.GET_TYRE_PRESSURE
            : tyreAge ? DriverIntent.GET_TYRE_AGE : tyreCompound ? DriverIntent.GET_TYRE_COMPOUND : DriverIntent.GET_TYRE_STATUS);
        Add(!tyre && Has(q, "wear"), DriverIntent.GET_TYRE_STATUS);
        Add(fuel, DriverIntent.GET_FUEL); Add(damage, DriverIntent.GET_DAMAGE);
        if (!damage) {
            Add(Has(q, "ers|battery|energy|deployment"), Has(q, "mode|deployment") ? DriverIntent.GET_ERS_MODE : DriverIntent.GET_ERS);
            Add(Has(q, "drs"), DriverIntent.GET_DRS);
        }
        Add(Has(q, "position|race order|what place|where am i"), DriverIntent.GET_POSITION);
        Add(Has(q, "flag|flags|yellow|red flag|blue flag|safety car|vsc|checkered|chequered"), DriverIntent.GET_FLAGS);
        Add(Has(q, "penalt(?:y|ies)|warnings?|track limits|drive through|stop go"), DriverIntent.GET_PENALTIES);
        Add(Has(q, "brake (?:bias|balance)"), DriverIntent.GET_BRAKE_BIAS);
        Add(Has(q, "diff|differential"), DriverIntent.GET_DIFFERENTIAL);
        Add(!damage && Has(q, "wing.*(?:settings?|setup)|(?:settings?|setup).*wing"), DriverIntent.GET_WING_SETUP);
        Add(Has(q, "gear|gears"), DriverIntent.GET_GEAR);
        Add(Has(q, "rpm|revs|revolutions"), DriverIntent.GET_RPM);
        Add(!pit && !temp && Has(q, "speed|how fast am i|how fast is the car"), DriverIntent.GET_SPEED);
        var weather = Has(q, "weather|rain|raining|forecast|conditions");
        if (weather) hits.Add(Has(q, "forecast|chance|going to rain|will.*rain|when.*rain|rain.*(?:coming|start|later|soon)") ? DriverIntent.GET_WEATHER_FORECAST : DriverIntent.GET_WEATHER);
        if (pit) {
            if (Has(q, "limiter|speed limit")) hits.Add(DriverIntent.GET_PIT_LIMITER);
            else if (Has(q, "how many|stop count|pit stops|number.*stops")) hits.Add(DriverIntent.GET_PIT_STOPS);
            else if (Has(q, "should|need|recommend|advi(?:ce|sable)|strategy|window|when|good.*(?:lap|time)|time to")) hits.Add(DriverIntent.GET_PIT_ADVICE);
            else hits.Add(DriverIntent.GET_PIT_STATUS);
        }
        if (!gap && !fuel && !tyreAge && !pit && Has(q, "lap|laps|pace|sector")) {
            hits.Add(Has(q, "sector") ? DriverIntent.GET_SECTOR
                : Has(q, "laps?.*(?:left|remaining|to go)") ? DriverIntent.GET_LAPS_REMAINING
                : Has(q, "valid|invalid|invalidate") ? DriverIntent.GET_LAP_VALIDITY
                : Has(q, "last lap time") ? DriverIntent.GET_LAST_LAP_TIME
                : Has(q, "last|pace|faster|lap report") ? DriverIntent.GET_LAP_REPORT : DriverIntent.GET_LAP);
        }
        Add(!gap && !fuel && !pit && !Has(q, "lap|laps|sector") && Has(q, "time left|time remaining|session time|long.*left.*session"), DriverIntent.GET_SESSION_TIME);
        Add(!pit && Has(q, "coach|coaching|improve|advice|how was.*corner"), DriverIntent.GET_COACHING);
        if (hits.Count > 1) return WendyIntent.Unknown;
        return hits.Count == 1 ? new(hits.Single(), Wheel: wheel) : null;
    }

    public static string Chat(string text) => Request(text) switch {
        "thanks" or "thank you" or "thank you wendy" or "good job" or "nice work" => "You're welcome. Stay focused.",
        "how are you" or "how are you doing" => "Ready to help. How's the car?",
        "im nervous" or "im frustrated" => "One corner at a time. Keep it smooth.",
        "wish me luck" => "Good luck. Smooth inputs, eyes ahead.",
        "a joke" => "My favourite exercise? Pit stops.",
        "who are you" or "are you a real person" => "I'm Wendy, your virtual racing assistant.",
        "lets chat" or "i want to chat" => "I'm here. How's your race going?",
        "hello" or "hi" or "hey" or "hello there" => "Hi. I'm here when you need me.",
        _ => "I'm here. How's the drive?"
    };
}
