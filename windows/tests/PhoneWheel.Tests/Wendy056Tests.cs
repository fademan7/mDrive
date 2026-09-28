using System.Buffers.Binary;
using PhoneWheel.Core;

internal static class Wendy056Tests
{
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    private sealed class Fixture {
        public readonly F1RaceState Race = new();
        public readonly WendyEngineer Wendy = new();
        public readonly Dictionary<int, byte[]> Packets = new();
        private uint frame;
        public Fixture() {
            foreach (var (id, length) in new[] { (1, 753), (2, 1285), (6, 1352), (7, 1239), (10, 1041) }) {
                var p = new byte[length]; BinaryPrimitives.WriteUInt16LittleEndian(p, 2025);
                p[5] = 1; p[6] = (byte)id; p[28] = 255; BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7), 1);
                Packets[id] = p;
            }
            Packets[2][61] = 1; Packets[2][62] = 3; Packets[2][74] = 2;
            Packets[7][57] = 1; F(7, 5, 20);
            BinaryPrimitives.WriteUInt16LittleEndian(Packets[6].AsSpan(29), 200);
        }
        public void F(int id, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(Packets[id].AsSpan(29 + offset), value);
        public void Feed(double now) {
            foreach (var p in Packets.Values) { BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23), ++frame); Check(Race.Ingest(p, now), "fixture"); }
        }
    }
    public static void DamageAndRadio() {
        var f = new Fixture(); var d = f.Packets[10];
        d[29 + 28] = 25; d[29 + 31] = 32; d[29 + 18] = 35; d[29 + 34] = 1; d[29 + 38] = 60;
        f.Feed(0);
        var text = f.Wendy.Answer("any damage", 1, f.Race, 0).Text;
        foreach (var part in new[] { "Left front wing damage is 25", "Floor damage is 32", "Front left tyre damage is 35", "DRS fault", "MGU H wear is 60", "Consider boxing" }) Check(text.Contains(part), part);
        Check(!text.Contains("panel"), "panel referral remains");
        Check(f.Wendy.Answer("do i need pit in", 1, f.Race, 0).Text.Contains("front wing change"), "damage advice");
        Check(f.Wendy.Answer("front wing damage", 1, f.Race, 0).Text.Contains("25 percent"), "targeted damage");
        Check(f.Wendy.Answer("any damage", 1, f.Race, 4000).Text.Contains("unavailable"), "stale damage");
        Check(WendyLanguage.Fallback("OK, box box").Intent == DriverIntent.PLAN_PIT, "polite plan");
        Check(WendyLanguage.Fallback("OK, don't box").Intent == DriverIntent.CANCEL_PIT, "negative plan");
        Check(WendyLanguage.Fallback("OK, box box or stay out").Intent != DriverIntent.PLAN_PIT, "ambiguous plan");
        Check(f.Wendy.Answer("OK, box box", 1, f.Race, 0).Text.Contains("cannot send"), "do not pretend game command");

        var alert = new Fixture(); alert.Packets[10][57] = 15; alert.Feed(0);
        Check(alert.Wendy.Alert(alert.Race, 0)?.Text.Contains("15 percent") == true, "initial component warning");
        alert.Feed(9000); Check(alert.Wendy.Alert(alert.Race, 9000) == null, "no repeated damage");
        alert.Packets[10][57] = 25; alert.Feed(31000);
        Check(alert.Wendy.Alert(alert.Race, 31000)?.Text.Contains("25 percent") == true, "escalation warning");
        alert.Packets[10][57] = 0; alert.Feed(62000); alert.Wendy.Alert(alert.Race, 62000);
        alert.Packets[10][57] = 15; alert.Feed(93000);
        Check(alert.Wendy.Alert(alert.Race, 93000)?.Text.Contains("15 percent") == true, "repair resets crossing");

        var radio = new Fixture(); radio.Feed(0); Check(radio.Wendy.Alert(radio.Race, 0) == null, "no immediate filler");
        radio.Feed(61000); radio.F(6, 6, .5f); radio.Feed(61001);
        Check(radio.Wendy.Alert(radio.Race, 61001) == null, "no quiet briefing in corner");
        radio.F(6, 6, 0); radio.Feed(61002);
        Check(radio.Wendy.Alert(radio.Race, 61002)?.Text.Contains("Highest tyre wear") == true, "useful quiet interval");
        radio.Feed(62000); Check(radio.Wendy.Alert(radio.Race, 62000) == null, "briefing cooldown");
        radio.Wendy.Answer("radio check", 1, radio.Race, 120000); radio.Feed(122000);
        Check(radio.Wendy.Alert(radio.Race, 122000) == null, "no briefing right after conversation");
        Check(radio.Wendy.Alert(radio.Race, 200000) == null, "no stale briefing");
    }
}
