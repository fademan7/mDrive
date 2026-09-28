namespace PhoneWheel.Core;

// Observed percentages/fault bits only. Pit thresholds are conservative advice,
// not a simulator repairability model or an optimal pit strategy.
public static class WendyDamage
{
    private static readonly string[] Parts = ["Left front wing", "Right front wing", "Rear wing", "Floor", "Diffuser", "Sidepod", "DRS", "ERS", "Gearbox", "Engine", "MGU H", "Energy store", "Control electronics", "Internal combustion engine", "MGU K", "Turbocharger", "Engine blown", "Engine seized"];
    private static readonly string[] Wheels = ["Rear left", "Rear right", "Front left", "Front right"];
    public sealed record Finding(string Key, int Value, string Text, bool Fault = false);
    public static Finding[] Findings(F1RaceState r) {
        var found = new List<Finding>();
        for (var i = 0; i < r.ComponentDamage.Length; i++) {
            var value = r.ComponentDamage[i];
            if (value == 0) continue;
            var fault = i is 6 or 7 or 16 or 17;
            found.Add(new($"part:{i}", value, fault ? $"{Parts[i]} fault reported." :
                $"{Parts[i]} {(i is >= 10 and <= 15 ? "wear" : "damage")} is {value} percent.", fault));
        }
        for (var i = 0; i < r.WheelDamage.Length; i++) {
            var value = r.WheelDamage[i]; if (value == 0) continue;
            found.Add(new($"wheel:{i}", value, $"{Wheels[i % 4]} {new[] { "tyre damage", "brake damage", "tyre blistering" }[i / 4]} is {value} percent."));
        }
        return found.OrderByDescending(f => f.Fault).ThenByDescending(f => f.Value).ToArray();
    }
    public static string Summary(F1RaceState r) {
        var found = Findings(r);
        if (found.Length == 0) return "No damage or component wear reported.";
        // A spoken request replaces having to inspect the MFD. Include every
        // nonzero monitored component; proactive alerts use a shorter subset.
        return string.Join(" ", found.Select(f => f.Text)) + " " + PitGuidance(r);
    }
    public static string PitGuidance(F1RaceState r) {
        if (r.ComponentDamage[16] != 0 || r.ComponentDamage[17] != 0)
            return "Terminal engine fault reported. A normal pit stop may not resolve it.";
        if (r.FrontWing >= 20)
            return "Consider boxing for a front wing change; the damage may affect handling. This is advice, not a pit request.";
        if (r.WheelDamage.Take(4).Any(v => v >= 30) || r.Wear.Max() >= 70)
            return "Consider boxing for fresh tyres. This is advice, not a pit request.";
        if (r.OtherDamage >= 10)
            return "Repair availability is not reported by telemetry. I cannot promise a pit stop will fix this damage.";
        return r.FrontWing > 0 ? "Monitor the handling; this damage alone does not establish an urgent pit stop." :
            "No urgent damage-related pit stop is indicated by these thresholds.";
    }
}
