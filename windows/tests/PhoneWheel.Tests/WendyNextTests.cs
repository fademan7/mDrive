using System.Buffers.Binary;
using System.Text.Json;
using PhoneWheel.Core;

internal static class WendyNextTests
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    public static void Intent()
    {
        var valid = """{"intent":"GET_GAP_AHEAD","symptom":"NONE","phase":"UNKNOWN","condition":"UNKNOWN","wheel":"ANY"}""";
        Check(WendyIntent.Parse(valid).Intent == DriverIntent.GET_GAP_AHEAD, "JSON schema");
        Check(WendyIntent.Parse(valid.Replace("GET_GAP_AHEAD", "12")).Intent == DriverIntent.UNKNOWN, "numeric enum rejected");
        Check(WendyIntent.Parse(valid.Replace("\"wheel\":\"ANY\"", "\"wheel\":\"ANY\",\"fuel\":42")).Intent == DriverIntent.UNKNOWN, "invented field rejected");
        Check(WendyIntent.Parse(valid.Replace("NONE", "UNDERSTEER")).Intent == DriverIntent.UNKNOWN, "query cannot carry feedback");
        Check(WendyLanguage.Fallback("How far is the guy in front?").Intent == DriverIntent.GET_GAP_AHEAD, "gap fallback");
        Check(WendyLanguage.Fallback("fuel and tyre wear").Intent == DriverIntent.UNKNOWN, "ambiguous not tyres");
        Check(WendyLanguage.Fallback("hello there").Intent == DriverIntent.UNKNOWN, "unknown");
        Check(!WendyLanguage.IsConfirm("Don't go ahead"), "negative is not approval");
        var engineer = new WendyEngineer(); var race = new F1RaceState();
        Check(engineer.Answer("hello there", 1, race, 0).Text == "I didn't understand that.", "unknown response");
        Check(engineer.Answer("Go ahead", 1, race, 0).Text.Contains("No change"), "no pending approval");
        Check(engineer.Answer("set brake bias to 54 or 55", 1, race, 0).Kind == "rejected", "ambiguous mutation");
    }
    private static byte[] Packet(int id, uint frame, int lap = 3)
    {
        var p = new byte[id switch { 1 => 753, 2 => 1285, 5 => 1133, 6 => 1352, 7 => 1239, 10 => 1041, _ => throw new ArgumentException() }];
        BinaryPrimitives.WriteUInt16LittleEndian(p, 2025); p[5] = 1; p[6] = (byte)id; p[28] = 255;
        BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7), 1); BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23), frame);
        if (id == 2) { p[29 + 32] = 1; p[29 + 33] = (byte)lap; p[29 + 45] = 2; p[29 + 38] = 5; p[29 + 40] = 2; }
        if (id == 5) { p[29 + 2] = 55; p[29 + 3] = 40; for (var i = 0; i < 4; i++) BinaryPrimitives.WriteSingleLittleEndian(p.AsSpan(29 + 29 + 4 * i), 23); }
        if (id == 6) { BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(29), 150); for (var i = 0; i < 4; i++) p[29 + 34 + i] = (byte)(i == 2 ? 115 : 90); }
        if (id == 7) { p[29 + 28] = 1; BinaryPrimitives.WriteSingleLittleEndian(p.AsSpan(29 + 5), 20); }
        return p;
    }
    public static void Telemetry()
    {
        var race = new F1RaceState(); var engineer = new WendyEngineer(); uint frame = 0;
        void Feed(double time, int lap = 3) { foreach (var id in new[] { 1, 2, 5, 6, 7, 10 }) Check(race.Ingest(Packet(id, ++frame, lap), time), "fixture ingest"); engineer.Patterns.Observe(race, time); }
        Feed(0);
        Check(race.Differential == 55 && race.OffThrottleDifferential == 40 && race.InnerTemperature[2] == 115, "packed offsets");
        Check(engineer.Answer("What's the diff?", 1, race, 0).Text.Contains("55 percent"), "diff query");
        Check(engineer.Answer("Any penalties?", 1, race, 0).Text.Contains("5 seconds"), "penalty query");
        Check(engineer.Answer("Pit status?", 1, race, 0).Text.Contains("out on track"), "pit query");
        Check(engineer.Patterns.SustainedHotWheel == -1, "one sample is not trend");
        for (var i = 1; i <= 31; i++) Feed(i * 1000, i < 20 ? 3 : 4);
        Check(engineer.Patterns.SustainedHotWheel == 2 && engineer.Patterns.RepeatedHeat, "sustained multi lap heat");
        var feedback = new WendyIntent(DriverIntent.DRIVER_FEEDBACK, DriverSymptom.TYRE_OVERHEATING);
        Check(engineer.Answer(feedback, "My tyres are overheating", 1, race, 31000).Kind == "suggestion", "corroborated review only");
        Check(engineer.Recommendations.Count == 0, "no implicit consent");
        Check(engineer.Answer("Sounds good", .3, race, 31001).Kind == "rejected", "low confidence not approval");
        Check(engineer.Answer("Sounds good", 1, race, 31002).Kind == "saved" && engineer.Recommendations.Count == 1, "confirmed saved not applied");
        Check(engineer.Answer("Sounds good", 1, race, 31003).Kind != "saved", "confirmation consumed");
        Check(engineer.Answer(new(DriverIntent.DRIVER_FEEDBACK, DriverSymptom.UNDERSTEER, CornerPhase.CORNER_ENTRY), "car won't turn in", 1, race, 31004).Text.Contains("don't have enough"), "no unsupported dynamics claim");
        engineer.Answer(feedback, "hot tyres", 1, race, 31005);
        Check(engineer.Answer("No leave it", 1, race, 31006).Text.Contains("No change"), "reject");
        Check(engineer.Answer("Go ahead", 1, race, 31007).Kind != "saved", "rejected cannot confirm");
        engineer.Answer(feedback, "hot tyres", 1, race, 31008); race.Reset();
        Check(engineer.Answer("Go ahead", 1, race, 31009).Kind != "saved", "new session expires pending");
        var invalid = Packet(6, ++frame); BinaryPrimitives.WriteSingleLittleEndian(invalid.AsSpan(31), float.NaN);
        Check(!race.Ingest(invalid, 32000), "nonfinite throttle");
        Check(!race.Ingest(Packet(5, ++frame)[..^1], 32000), "truncated setup");
    }
    public static void TemperatureQueries()
    {
        var race = new F1RaceState(); var engineer = new WendyEngineer();
        race.Ingest(Packet(1, 1), 0); race.Ingest(Packet(2, 1), 0); race.Ingest(Packet(7, 1), 0);
        var telemetry = Packet(6, 1);
        for (var i = 0; i < 4; i++) {
            telemetry[29 + 34 + i] = (byte)(90 + i); telemetry[29 + 30 + i] = (byte)(100 + i);
            BinaryPrimitives.WriteUInt16LittleEndian(telemetry.AsSpan(29 + 22 + i * 2), (ushort)(600 + i));
            BinaryPrimitives.WriteSingleLittleEndian(telemetry.AsSpan(29 + 40 + i * 4), 23 + i * .5f);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(telemetry.AsSpan(29 + 38), 110);
        Check(race.Ingest(telemetry, 0), "temperature packet");
        foreach (var text in new[] { "tyre temperature", "What are my tire temperatures?", "How hot are my tyres?", "Wendy tyre temps" })
            Check(WendyLanguage.Fallback(text).Intent == DriverIntent.GET_TYRE_TEMPERATURE, text);
        Check(engineer.Answer("Front left tyre temperature", 1, race, 0).Text.Contains("92, surface 102"), "specific tyre inner/surface");
        Check(engineer.Answer("Tyre temperatures", 1, race, 0).Text.Contains("Front left 92.0, front right 93.0, rear left 90.0"), "wheel order");
        Check(engineer.Answer("Front right tyre pressure", 1, race, 0).Text.Contains("24.5 PSI"), "actual pressure not setup");
        Check(engineer.Answer("Front left brake temperature", 1, race, 0).Text.Contains("602.0"), "brake wheel identity");
        Check(engineer.Answer("Engine temperature", 1, race, 0).Text.Contains("110 degrees Celsius"), "engine temperature");
        Check(engineer.Answer("How old are my tyres", 1, race, 0).Text.Contains("laps old"), "age query");
        Check(WendyLanguage.Fallback("How many laps left?").Intent == DriverIntent.GET_LAPS_REMAINING, "remaining laps");
        Check(WendyLanguage.Fallback("Set tyre pressure to 24").Intent == DriverIntent.CHANGE_SETTING, "write not converted to query");
        Check(WendyLanguage.Fallback("engine and tyre temperature").Intent == DriverIntent.UNKNOWN, "ambiguous components");
        race.Ingest(Packet(1, 2), 2500);
        Check(engineer.Answer("tyre temperature", 1, race, 2500).Text.Contains("unavailable"), "stale values never spoken");
        var bad = Packet(6, 2); BinaryPrimitives.WriteSingleLittleEndian(bad.AsSpan(69), float.NaN);
        Check(!race.Ingest(bad, 2500), "nonfinite pressure rejected");
    }
}
