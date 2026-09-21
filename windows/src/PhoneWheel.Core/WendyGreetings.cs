namespace PhoneWheel.Core;

// Receiver lifetime session deduplication survives flashbacks and phone reconnects.
// An audible greeting verifies the downlink/TTS, never falsely certifies the mic.
public sealed class WendyGreetings
{
    private readonly HashSet<ulong> greeted = [];
    private int previous = -1;
    public static IReadOnlyList<string> Phrases { get; } = Array.AsReadOnly(new[] {
        "Wendy here. Telemetry is live. Let's have a good race.",
        "Hello driver, Wendy on the radio. I'm receiving your race data.",
        "Wendy checking in. Race telemetry connected. Good luck out there.",
        "Your engineer is here. Wendy on the radio, telemetry coming through.",
        "Hello from the pit wall. Wendy here, race data is live.",
        "Wendy online. I'll keep you updated on the important changes.",
        "Radio check from Wendy. I'm receiving telemetry. Let's get racing.",
        "Wendy with you for this race. I'll keep the updates short.",
        "Hello driver. Telemetry connected, Wendy on the pit wall.",
        "Wendy reporting in. Your race data is reaching me.",
        "Ready on the pit wall. Wendy here with your telemetry.",
        "Wendy on comms. I'll report flags and key race updates.",
        "Race engineer Wendy here. Your telemetry feed is up.",
        "Wendy checking the downlink. Race data is coming through.",
        "Welcome to the race. Wendy here, telemetry connected.",
        "It's Wendy. I'll watch the data while you focus on driving.",
        "Wendy from the pit wall. I'll be here for your race updates.",
        "Hello again, driver. Wendy on the radio, receiving race data.",
        "Wendy here for the race. Telemetry is flowing to the pit wall.",
        "Engineer check-in. This is Wendy, your race data is connected."
    });
    public string? Take(F1RaceState r, double now)
    {
        if (!r.Live(now) || r.SessionType is < 15 or > 17 || !F1RaceState.Fresh(r.LapMs, now) || r.Result != 2 ||
            r.SafetyCar == 3 || r.Flag(now) != "GREEN" || greeted.Contains(r.Session)) return null;
        // Lights out, or a late-connected active racer (not the garage/grid).
        if (!F1RaceState.Fresh(r.RaceStartMs, now, 30000) && (r.Lap < 1 || r.DriverStatus != 4)) return null;
        // Bounded memory even if a receiver remains open across many sessions.
        if (greeted.Count >= 128) greeted.Remove(greeted.First());
        greeted.Add(r.Session);
        var index = Random.Shared.Next(Phrases.Count - 1);
        if (index >= previous) index++;
        previous = index;
        return Phrases[index];
    }
}
