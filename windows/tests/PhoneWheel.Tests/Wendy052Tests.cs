using System.Buffers.Binary;
using PhoneWheel.Core;

internal static class Wendy052Tests
{
    private static void Check(bool v, string why) { if (!v) throw new Exception(why); }
    private static byte[] Packet(int id, uint frame = 1, ulong uid = 1) {
        var p = new byte[id switch { 1 => 753, 2 => 1285, 3 => 45, 5 => 1133, 6 => 1352, 7 => 1239, 10 => 1041, _ => throw new Exception() }];
        BinaryPrimitives.WriteUInt16LittleEndian(p, 2025); p[5] = 1; p[6] = (byte)id; p[28] = 255;
        BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7), uid); BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23), frame);
        if (id == 1) { p[30] = 39; p[31] = 24; p[35] = 15; p[42] = 80; BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(38), 121); }
        if (id == 2) { p[61] = 1; p[62] = 1; p[64] = 2; p[65] = 2; p[73] = 4; p[74] = 2; BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(29), 91234); BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(37), 28000); BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(40), 30000); }
        if (id == 5) { p[29] = 30; p[30] = 28; }
        if (id == 6) { BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(29), 280); p[44] = 7; BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(45), 11500); p[47] = 1; }
        if (id == 7) { p[33] = 1; p[51] = 1; p[54] = 18; p[55] = 17; p[56] = 6; p[57] = 1; p[70] = 3; BinaryPrimitives.WriteSingleLittleEndian(p.AsSpan(34), 30); }
        if (id == 10) { p[59] = 12; p[60] = 7; }
        return p;
    }
    public static void Queries() {
        var r = new F1RaceState(); var e = new WendyEngineer();
        foreach (var id in new[] { 1, 2, 5, 6, 7, 10 }) Check(r.Ingest(Packet(id), 0), "packet " + id);
        var cases = new Dictionary<string, string> {
            ["radio check"] = "Loud and clear", ["what can I ask"] = "Ask about",
            ["how fast am I going"] = "280 kilometres", ["what gear am I in"] = "Gear 7", ["engine rpm"] = "11500 RPM",
            ["is DRS open"] = "DRS is open", ["what tyres am I on"] = "medium tyres, 6 laps",
            ["track temperature"] = "39 degrees", ["air temperature"] = "24 degrees", ["time left"] = "2 minutes and 1 seconds",
            ["pit limiter"] = "on. Pit speed limit is 80", ["how many pit stops have I made"] = "2 pit stops",
            ["wing settings"] = "Front wing setting 30, rear wing 28", ["is my lap valid"] = "currently valid",
            ["last lap time"] = "91.234 seconds", ["sector times"] = "Sector two: 30.000", ["ERS mode"] = "overtake",
            ["rear wing damage"] = "12 percent", ["floor damage"] = "7 percent"
        };
        foreach (var (q, expected) in cases) Check(e.Answer(q, 1, r, 0).Text.Contains(expected), q + " => " + e.Answer(q, 1, r, 0).Text);
        Check(WendyLanguage.Fallback("set wing settings to 40").Intent == DriverIntent.CHANGE_SETTING, "no mutation as query");
        Check(WendyLanguage.Fallback("speed and gear").Intent == DriverIntent.UNKNOWN, "mixed unknown");
        r.Ingest(Packet(1, 2), 2500);
        foreach (var q in new[] { "speed", "gear", "rpm", "drs", "tyre compound", "pit stops", "wing setup", "sector times", "floor damage" }) Check(e.Answer(q, 1, r, 2500).Text.Contains("unavailable"), "stale " + q);
        var invalid = Packet(6, 2); invalid[44] = 9; Check(!r.Ingest(invalid, 2500), "bad gear");
        invalid = Packet(7, 2); invalid[70] = 4; Check(!r.Ingest(invalid, 2500), "bad ERS mode");
    }
    public static void Greetings() {
        var r = new F1RaceState(); var g = new WendyGreetings();
        Check(g.Take(r, 0) == null, "no telemetry no greeting");
        void Feed(ulong uid, uint frame = 1) { foreach (var id in new[] { 1, 2, 7 }) r.Ingest(Packet(id, frame, uid), 0); }
        Feed(1); var first = g.Take(r, 0); Check(first != null, "race greeting");
        Check(g.Take(r, 1) == null, "no duplicate");
        r.Reset(); Feed(1); Check(g.Take(r, 1) == null, "flashback or reconnect no repeat");
        Feed(2); Check(g.Take(r, 0) is { } second && second != first, "new session variation");
        Feed(3); var p = Packet(1, 2, 3); p[43] = 1; r.Ingest(p, 0); Check(g.Take(r, 0) == null, "paused silent");
        p = Packet(1, 3, 3); p[35] = 18; r.Ingest(p, 0); Check(g.Take(r, 0) == null, "time trial not race");
        p = Packet(1, 4, 3); p[153] = 1; r.Ingest(p, 0); Check(g.Take(r, 0) == null, "safety car priority");
        Check(WendyGreetings.Phrases.Distinct().Count() >= 20, "varied greetings");
    }
    public static void ForecastAndLeader() {
        var r = new F1RaceState(); var e = new WendyEngineer();
        var p = Packet(1); p[29 + 126] = 3; p[29 + 639] = 1;
        var start = 29 + 127;
        // First sample belongs to another session and must not be used.
        p[start] = 5; p[start + 1] = 2; p[start + 2] = 5; p[start + 7] = 100;
        p[start + 8] = 15; p[start + 9] = 5; p[start + 10] = 1; p[start + 15] = 20;
        p[start + 16] = 15; p[start + 17] = 10; p[start + 18] = 3; p[start + 23] = 80;
        Check(r.Ingest(p, 0), "forecast fixture");
        var answer = e.Answer("Is it going to rain?", 1, r, 0).Text;
        Check(answer.Contains("Approximate game forecast in 5 minutes") && answer.Contains("Rain is indicated in 10 minutes"), "forecast session filtering and units");
        var lap = Packet(2); lap[29 + 32] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(lap.AsSpan(29 + 17), 3456);
        lap[29 + 57 + 32] = 1; lap[29 + 57 + 33] = 1; lap[29 + 57 + 45] = 2;
        Check(r.Ingest(lap, 0), "leader fixture");
        Check(e.Answer("What's the gap to the leader?", 1, r, 0).Text.Contains("3.5 seconds"), "leader gap");
        Check(e.Answer("forecast", 1, r, 3100).Text.Contains("unavailable"), "stale forecast");
        BinaryPrimitives.WriteUInt32LittleEndian(lap.AsSpan(23), 2); lap[29 + 57 + 33] = 2; r.Ingest(lap, 1);
        Check(e.Answer("gap to leader", 1, r, 1).Text.Contains("unavailable"), "lapped gap not fabricated");
        p = Packet(1, 2); p[29 + 126] = 1; p[start] = 15; p[start + 1] = 5; p[start + 2] = 6; p[start + 7] = 101; r.Ingest(p, 1);
        Check(e.Answer("forecast", 1, r, 1).Text.Contains("unavailable"), "invalid optional forecast ignored");
    }
}
