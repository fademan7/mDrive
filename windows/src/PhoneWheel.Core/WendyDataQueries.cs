using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

// Closed read-only phrases before the optional classifier. No guessed values,
// inferred game actions, or unbounded conversation/history on the input worker.
public static class WendyDataQueries
{
    private static readonly (DriverIntent Intent, string Pattern)[] Queries = [
        (DriverIntent.GET_WEATHER_FORECAST, @"^(weather forecast|forecast|rain forecast|is it going to rain|will it rain|when (will|does) (the )?rain (start|come)|rain coming|any rain coming|chance of rain|whats the forecast|what is the forecast)$"),
        (DriverIntent.GET_GAP_LEADER, @"^((what is|whats) (the |my )?)?gap to (the )?(race )?leader$|^how far (is|to) (the )?leader$"),
        (DriverIntent.RADIO_CHECK, @"^(radio check|can you hear me|are you there|are you working|wendy|hello wendy|check radio)$"),
        (DriverIntent.GET_HELP, @"^(help|what can i ask|what can you do|list commands|voice commands)$"),
        (DriverIntent.GET_SPEED, @"^(speed|current speed|how fast am i going|(what is|whats) (my |the |our )?(current )?speed)$"),
        (DriverIntent.GET_GEAR, @"^(gear|what gear( am i in| is this)?|(what is|whats) (my |the )?gear)$"),
        (DriverIntent.GET_RPM, @"^(rpm|engine rpm|revs|(what is|whats|what are) (my |the )?(engine )?(rpm|revs))$"),
        (DriverIntent.GET_DRS, @"^(drs|drs status|is drs (open|on|available|allowed)|can i use drs|when can i use drs)$"),
        (DriverIntent.GET_TYRE_COMPOUND, @"^(tyre compound|tire compound|what (tyres|tires|compound) am i on|which (tyres|tires) am i using|(what is|whats) (my |the )?(tyre |tire )?compound)$"),
        (DriverIntent.GET_TRACK_TEMPERATURE, @"^(track temp(erature)?|how hot is the track|(what is|whats) (the )?track temp(erature)?)$"),
        (DriverIntent.GET_AIR_TEMPERATURE, @"^((air|ambient) temp(erature)?|(what is|whats) (the )?(air|ambient) temp(erature)?)$"),
        (DriverIntent.GET_SESSION_TIME, @"^(time left|session time|how (much|long) time (is )?left|how long (is )?left( in (the )?session)?)$"),
        (DriverIntent.GET_PIT_LIMITER, @"^(pit limiter|pit speed limit|is (the |my )?(pit )?limiter (on|off)|(what is|whats) (the )?pit (lane )?speed limit)$"),
        (DriverIntent.GET_PIT_STOPS, @"^(pit stops|how many (pit )?stops (have i made|have we made|so far))$"),
        (DriverIntent.GET_WING_SETUP, @"^(wing settings|wing setup|front wing setting|rear wing setting|(what is|whats|what are) (my |the )?(front |rear )?wing (setting|settings|setup))$"),
        (DriverIntent.GET_LAP_VALIDITY, @"^(is (this|my|the) lap (valid|invalid)|lap validity|did i invalidate (this|my|the) lap)$"),
        (DriverIntent.GET_LAST_LAP_TIME, @"^(last lap time|what was my last lap time|tell me my last lap time)$"),
        (DriverIntent.GET_SECTOR, @"^(sector times|sector report|what sector am i in|current sector)$"),
        (DriverIntent.GET_ERS_MODE, @"^(ers mode|deployment mode|what (ers |deployment )?mode am i (in|using)|(what is|whats) (my |the )?(ers|deployment) mode)$")
    ];
    public static DriverIntent? Match(string normalized) {
        var q = Regex.Replace(normalized, @"\b(d r s|e r s|r p m)\b", m => m.Value.Replace(" ", ""));
        return Queries.Where(v => Regex.IsMatch(q, v.Pattern)).Select(v => (DriverIntent?)v.Intent).FirstOrDefault();
    }
    public static WendyReply? Answer(DriverIntent intent, F1RaceState r, double now) {
        bool Fresh(double t) => F1RaceState.Fresh(t, now);
        WendyReply Missing(string name) => new($"{name} is unavailable.");
        string Seconds(uint ms) => (ms / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);
        switch (intent) {
            case DriverIntent.GET_GAP_LEADER: return !Fresh(r.LapMs) ? Missing("Gap to leader") : r.Position == 1 ? new("You are leading the race.") : r.LeaderGap is double gap ? new($"Gap to the leader is {gap.ToString("0.0", CultureInfo.InvariantCulture)} seconds.") : Missing("Same-lap gap to leader");
            case DriverIntent.GET_WEATHER_FORECAST:
                if (!F1RaceState.Fresh(r.SessionMs, now, 3000) || r.Forecast.Length == 0) return Missing("Game weather forecast for this session");
                var first = r.Forecast[0];
                var rain = r.Forecast.FirstOrDefault(f => f.Weather >= 3);
                return new($"{(r.ApproximateForecast ? "Approximate game forecast" : "Game forecast")} in {first.Minutes} minutes: {WendyEngineer.WeatherName(first.Weather)}, {first.RainPercent} percent chance of rain." + (rain != null && rain != first ? $" Rain is indicated in {rain.Minutes} minutes." : ""));
            case DriverIntent.GET_SPEED: return Fresh(r.TelemetryMs) ? new($"Speed is {r.SpeedKph} kilometres per hour.") : Missing("Speed");
            case DriverIntent.GET_GEAR: return Fresh(r.TelemetryMs) ? new(r.Gear switch { -1 => "Reverse gear.", 0 => "Neutral.", _ => $"Gear {r.Gear}." }) : Missing("Gear");
            case DriverIntent.GET_RPM: return Fresh(r.TelemetryMs) ? new($"Engine speed is {r.Rpm} RPM.") : Missing("Engine speed");
            case DriverIntent.GET_DRS:
                if (!Fresh(r.TelemetryMs) || !Fresh(r.StatusMs)) return Missing("DRS");
                return new(r.DrsOpen ? "DRS is open." : r.DrsAllowed ? "DRS is available, currently closed." : r.DrsDistance > 0 ? $"DRS activation point is {r.DrsDistance} metres ahead." : "DRS is not available.");
            case DriverIntent.GET_TYRE_COMPOUND:
                if (!Fresh(r.StatusMs)) return Missing("Tyre compound");
                var compound = r.Formula is 0 or 3 or 4 or 6 or 8 or 9 ? r.VisualCompound switch { 16 => "soft", 17 => "medium", 18 => "hard", 7 => "intermediate", 8 => "wet", _ => null } : null;
                return compound != null ? new($"You are on {compound} tyres, {r.TyreAge} laps old.") : Missing("Named tyre compound for this formula");
            case DriverIntent.GET_TRACK_TEMPERATURE: return new($"Track temperature is {r.TrackTemperature} degrees Celsius.");
            case DriverIntent.GET_AIR_TEMPERATURE: return new($"Air temperature is {r.AirTemperature} degrees Celsius.");
            case DriverIntent.GET_SESSION_TIME: return new($"Session timer shows {r.SessionSecondsLeft / 60} minutes and {r.SessionSecondsLeft % 60} seconds remaining.");
            case DriverIntent.GET_PIT_LIMITER: return Fresh(r.StatusMs) ? new($"Pit limiter is {(r.PitLimiter ? "on" : "off")}. Pit speed limit is {r.PitSpeedLimit} kilometres per hour.") : Missing("Pit limiter");
            case DriverIntent.GET_PIT_STOPS: return Fresh(r.LapMs) ? new($"You have made {r.PitStops} pit stops.") : Missing("Pit stops");
            case DriverIntent.GET_WING_SETUP: return Fresh(r.SetupMs) ? new($"Front wing setting {r.FrontWingSetup}, rear wing {r.RearWingSetup}.") : Missing("Wing settings");
            case DriverIntent.GET_LAP_VALIDITY: return Fresh(r.LapMs) ? new(r.LapInvalid ? "This lap is invalid." : "This lap is currently valid.") : Missing("Lap validity");
            case DriverIntent.GET_LAST_LAP_TIME: return Fresh(r.LapMs) && r.LastLapMs > 0 ? new($"Last lap: {Seconds(r.LastLapMs)} seconds.") : Missing("Last lap time");
            case DriverIntent.GET_SECTOR:
                if (!Fresh(r.LapMs) || r.Sector > 2) return Missing("Sector data");
                return new($"Currently sector {r.Sector + 1}." + (r.Sector >= 1 && r.Sector1Ms > 0 ? $" Sector one: {Seconds(r.Sector1Ms)} seconds." : "") + (r.Sector >= 2 && r.Sector2Ms > 0 ? $" Sector two: {Seconds(r.Sector2Ms)} seconds." : ""));
            case DriverIntent.GET_ERS_MODE: return Fresh(r.StatusMs) ? new($"ERS deployment mode is {new[] { "none", "medium", "hotlap", "overtake" }[r.ErsMode]}.") : Missing("ERS mode");
            default: return null;
        }
    }
    public static WendyReply? Damage(string normalized, F1RaceState r) {
        var components = new[] { ("rear wing", 2), ("floor", 3), ("diffuser", 4), ("sidepod", 5), ("gearbox", 8), ("engine", 9) };
        var matches = components.Where(c => normalized.Contains(c.Item1)).ToArray();
        if (matches.Length > 1) return new("Please ask about one damage component at a time.");
        if (matches.Length == 1) return new($"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(matches[0].Item1)} damage is {r.ComponentDamage[matches[0].Item2]} percent.");
        if (normalized.Contains("front wing")) return new(normalized.Contains("left") ? $"Left front wing damage is {r.ComponentDamage[0]} percent." : normalized.Contains("right") ? $"Right front wing damage is {r.ComponentDamage[1]} percent." : $"Front wing damage is {r.FrontWing} percent.");
        if (normalized.Contains("drs")) return new(r.ComponentDamage[6] != 0 ? "DRS fault reported." : "No DRS fault reported.");
        if (normalized.Contains("ers")) return new(r.ComponentDamage[7] != 0 ? "ERS fault reported." : "No ERS fault reported.");
        return null;
    }
}
