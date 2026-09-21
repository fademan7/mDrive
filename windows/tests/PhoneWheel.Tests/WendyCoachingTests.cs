using System.Buffers.Binary;
using PhoneWheel.Core;

internal static class WendyCoachingTests
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private sealed class Drive
    {
        public readonly F1RaceState Race = new();
        public readonly WendyEngineer Engineer = new();
        public double Now;
        public uint Frame;
        public bool Invalid, Yellow, Pit, Pause;
        public int Weather, Compound = 18;
        public float Wear;
        public void Feed(int lap, int bin, uint last = 60000, bool slower = false)
        {
            Now += 600;
            foreach (var id in new[] { 1, 2, 5, 6, 7, 10 }) {
                var p = new byte[id switch { 1 => 753, 2 => 1285, 5 => 1133, 6 => 1352, 7 => 1239, _ => 1041 }];
                BinaryPrimitives.WriteUInt16LittleEndian(p, 2025); p[5] = 1; p[6] = (byte)id; p[28] = 255;
                BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7), 1); BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23), ++Frame);
                void F(int o, float v) => BinaryPrimitives.WriteSingleLittleEndian(p.AsSpan(29 + o), v);
                if (id == 1) { p[29] = (byte)Weather; p[32] = 20; BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(33), 2000); p[43] = (byte)(Pause ? 1 : 0); p[682] = 8; p[683] = 10; p[684] = 5; }
                if (id == 2) {
                    BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(29), last);
                    BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(33), (uint)(bin * 600 + (slower && bin >= 20 ? 1000 : 0)));
                    F(20, bin * 20); p[61] = 2; p[62] = (byte)lap; p[63] = (byte)(Pit ? 1 : 0); p[66] = (byte)(Invalid ? 1 : 0); p[73] = 1; p[74] = 2;
                    var other = 29 + 57; p[other + 32] = 3; p[other + 33] = (byte)lap; p[other + 45] = 2;
                    BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(other + 14), 1200);
                }
                if (id == 6) {
                    BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(29), (ushort)(bin is >= 10 and <= 17 ? 110 : slower ? 225 : 200));
                    F(2, bin is >= 10 and <= 17 ? .2f : 1); F(6, bin is >= 11 and <= 18 ? .3f : 0); F(10, bin is >= 10 and <= 12 ? .7f : 0);
                }
                if (id == 7) { p[57] = (byte)(Yellow ? 3 : 1); p[54] = (byte)Compound; F(5, 20); }
                if (id == 10) F(8, Wear);
                Check(Race.Ingest(p, Now), "fixture ingest");
            }
            Engineer.Observe(Race, Now);
        }
        public void Lap(int number, bool invalid = false, int from = 0) {
            for (var i = from; i < 100; i++) { Invalid = invalid && i == 30; Feed(number, i); } Invalid = false;
        }
    }
    public static void PitAndLanguage()
    {
        foreach (var text in new[] { "Do I need pit in?", "Do I need to pit?", "Should we box this lap?", "When should I pit?", "Wendy, pit window" })
            Check(WendyLanguage.Fallback(text).Intent == DriverIntent.GET_PIT_ADVICE, text);
        Check(WendyLanguage.Fallback("box box").Intent == DriverIntent.PLAN_PIT, "box plan");
        Check(WendyLanguage.Fallback("don't box").Intent == DriverIntent.CANCEL_PIT, "negative no box");
        Check(WendyLanguage.Fallback("box box or stay out").Intent != DriverIntent.PLAN_PIT, "ambiguous no plan");
        Check(WendyLanguage.Fallback("How was my last lap?").Intent == DriverIntent.GET_LAP_REPORT, "lap report intent");
        var d = new Drive(); d.Feed(1, 0);
        Check(d.Race.PitIdealLap == 8 && d.Race.PitLatestLap == 10 && d.Race.PitRejoinPosition == 5 && d.Race.TrackLength == 2000, "official offsets");
        Check(d.Engineer.Answer("Do I need pit in?", 1, d.Race, d.Now).Text.Contains("in 7 laps"), "pit timing");
        Check(d.Engineer.Answer("box box", .3, d.Race, d.Now).Kind == "rejected", "pit low confidence");
        Check(d.Engineer.Answer("box box", 1, d.Race, d.Now).Text.Contains("cannot send"), "not false game command");
        Check(d.Engineer.Answer("Pit status", 1, d.Race, d.Now).Text.Contains("not confirmed"), "plan state");
        d.Engineer.Answer("Stay out", 1, d.Race, d.Now);
        Check(d.Engineer.Answer("Pit status", 1, d.Race, d.Now).Text.Contains("out on track"), "cancel plan");
        d.Wear = 75; d.Feed(1, 1);
        Check(d.Engineer.Answer("Should I pit?", 1, d.Race, d.Now).Text.Contains("70 percent"), "wear reason");
        Check(d.Engineer.Answer("Should I pit?", 1, d.Race, d.Now + 4000).Text.Contains("unavailable"), "no stale advice");
        Check(d.Engineer.Answer(new(DriverIntent.PLAN_PIT), "maybe not", 1, d.Race, d.Now).Kind == "rejected", "model cannot invent plans");
    }
    public static void Laps()
    {
        var d = new Drive(); d.Lap(1); Check(!d.Engineer.Coaching.HasReference, "no reference before finish");
        d.Feed(2, 0); Check(d.Engineer.Coaching.ReferenceLap == 1, "first clean lap used immediately");
        Check(d.Engineer.Coaching.TakeReport(d.Now)?.Contains("Reference learned") == true, "first report");
        Check(d.Engineer.Coaching.TakeReport(d.Now) == null, "report consumed");
        for (var i = 1; i < 25; i++) d.Feed(2, i, slower: true);
        Check(d.Engineer.Coaching.LastAdvice.Contains("lost 1.0 seconds"), "second lap zone comparison");
        Check(d.Engineer.Coaching.TakeAdvice(d.Race, d.Now)?.Contains("25 kilometres") == true, "measured entry difference");
        Check(d.Engineer.Coaching.TakeAdvice(d.Race, d.Now) == null, "no backlog");
        for (var i = 25; i < 100; i++) d.Feed(2, i);
        d.Feed(3, 0, 61000);
        Check(d.Engineer.Coaching.LastReport.Contains("1.0 seconds slower"), "lap delta");
        d.Weather = 3; d.Feed(3, 1); Check(!d.Engineer.Coaching.HasReference, "weather invalidates reference");
        var bad = new Drive(); bad.Lap(1, true); bad.Feed(2, 0); Check(!bad.Engineer.Coaching.HasReference, "invalid lap excluded");
        var partial = new Drive(); partial.Lap(1, from: 40); partial.Feed(2, 0); Check(!partial.Engineer.Coaching.HasReference, "partial connection excluded");
        var gap = new Drive(); gap.Lap(1); gap.Now += 3000; gap.Feed(2, 0); Check(!gap.Engineer.Coaching.HasReference, "gap excludes lap");
        foreach (var kind in new[] { "yellow", "pit", "pause" }) {
            var guarded = new Drive();
            for (var i = 0; i < 100; i++) { guarded.Yellow = kind == "yellow" && i == 30; guarded.Pit = kind == "pit" && i == 30; guarded.Pause = kind == "pause" && i == 30; guarded.Feed(1, i); }
            guarded.Feed(2, 0); Check(!guarded.Engineer.Coaching.HasReference, kind + " excluded");
        }
        d.Race.Reset(); d.Engineer.Observe(d.Race, d.Now); Check(!d.Engineer.Coaching.HasReference, "session clears reference");
    }
    public static void Traffic()
    {
        var d = new Drive(); d.Feed(1, 0); Check(d.Engineer.Alert(d.Race, d.Now) == null, "traffic persistence");
        for (var i = 1; i < 5; i++) { d.Feed(1, i); Check(d.Engineer.Alert(d.Race, d.Now) == null, "traffic no instant alarm"); }
        d.Feed(1, 5); Check(d.Engineer.Alert(d.Race, d.Now)?.Text.Contains("1.2 seconds back") == true, "following gap alert");
        for (var i = 6; i < 30; i++) { d.Feed(1, i); Check(d.Engineer.Alert(d.Race, d.Now) == null, "no repeated traffic alert"); }
    }
}
