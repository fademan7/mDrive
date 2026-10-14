using System.Buffers.Binary;
using PhoneWheel.Core;
using PhoneWheel.Host;

internal static class WendyMenuTests
{
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    private sealed class Output : IWendyMenuOutput {
        public long Revision { get; set; }
        public bool Enabled { get; set; } = true;
        public WendyMenuKey[]? Steps = [WendyMenuKey.Radio, WendyMenuKey.Right];
        public int Count; public bool Fail, Throw;
        public WendyMenuKey[]? Recipe(WendyPitCommand command) => Steps;
        public Task<bool> Pulse(WendyMenuKey key, CancellationToken token) {
            Count++; if (Throw) throw new IOException("injected");
            return Fail ? Task.FromException<bool>(new IOException("injected")) : Task.FromResult(!token.IsCancellationRequested);
        }
    }
    private sealed class Fixture {
        public F1RaceState Race = new();
        public int Panel = 255, Speed, SessionType = 1, Network, Result = 2, Pit;
        public float Throttle; public bool Paused;
        private uint frame;
        public void Feed(double now) {
            foreach (var (id, size) in new[] { (1, 753), (2, 1285), (6, 1352) }) {
                var b = new byte[size]; BinaryPrimitives.WriteUInt16LittleEndian(b, 2025); b[5] = 1; b[6] = (byte)id; b[28] = 255;
                BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(7), 1); BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(23), ++frame);
                if (id == 1) { b[35] = (byte)SessionType; b[154] = (byte)Network; b[43] = (byte)(Paused ? 1 : 0); }
                if (id == 2) { b[74] = (byte)Result; b[63] = (byte)Pit; }
                if (id == 6) { b[1349] = (byte)Panel; BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(29), (ushort)Speed); BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(31), Throttle); }
                Check(Race.Ingest(b, now), "fixture");
            }
        }
    }
    public static void Language() {
        var radio = new (string Text, DriverIntent Intent)[] {
            ("Box this lap", DriverIntent.PLAN_PIT), ("Full race update", DriverIntent.GET_RACE_SUMMARY),
            ("Driver in front", DriverIntent.GET_GAP_AHEAD), ("Driver behind", DriverIntent.GET_GAP_BEHIND),
            ("Tyre status", DriverIntent.GET_TYRE_STATUS), ("Vehicle condition", DriverIntent.GET_DAMAGE),
            ("Weather report", DriverIntent.GET_WEATHER), ("Pit stop information", DriverIntent.GET_PIT_INFORMATION),
            ("Set hard tyres", DriverIntent.CHANGE_SETTING)
        };
        using (var model = new CpuIntentModel(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))) {
            foreach (var (phrase, expected) in radio) foreach (var text in new[] { phrase, "Wendy, could you tell me " + phrase + " please?" }) {
                var result = model.Classify(text, CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Value.Intent == expected && result.Source == "Race rules", "radio label " + text);
            }
            Check(model.ProcessId == null, "radio labels do not start inference");
        }
        Check(WendyLanguage.Fallback("Give me a full race update").Intent == DriverIntent.GET_RACE_SUMMARY, "article wrapper");
        Check(WendyLanguage.Fallback("Vehicle condition and weather report").Intent == DriverIntent.UNKNOWN, "mixed radio topics");
        foreach (var phrase in new[] { "boxbox", "OK box box", "box this lap", "Wendy, could you prepare medium tyres for the next stop please", "prepare soft", "select intermediates", "fit wets", "prepare hard tires" })
            Check(WendyMenuActions.TryParse(phrase, out _) && WendyLanguage.Fallback(phrase).Intent is DriverIntent.CHANGE_SETTING or DriverIntent.PLAN_PIT, phrase);
        Check(WendyMenuActions.TryParse("prepare medium", out var c) && c == WendyPitCommand.Medium, "compound");
        foreach (var phrase in new[] { "don't prepare soft", "maybe box box", "prepare medium or soft", "box box and prepare medium", "if rain prepare wets", "prepare soft tomorrow", "what are soft tyres" })
            Check(!WendyMenuActions.TryParse(phrase, out _), "ambiguous " + phrase);
        foreach (var recipe in new[] { "Radio, Down, Right", "Pits, Down, Left" }) Check(WendyMenuOutput.ParseRecipe(recipe, out _), recipe);
        foreach (var recipe in new[] { "T,Right", "Radio", "Radio,0", "Radio,Escape", "Pits,Radio,Enter", string.Join(',', Enumerable.Repeat("Down", 17)) })
            Check(!WendyMenuOutput.ParseRecipe(recipe, out _), "invalid recipe");
        var host = new WendyMenuOutput(); Check(!host.Enabled, "default off");
        host.Configure(new() { [WendyPitCommand.Box] = [WendyMenuKey.Radio, WendyMenuKey.Right] });
        Check(host.Enabled, "opt in"); host.Invalidate(); Check(!host.Enabled, "disable");
        Check(!host.Pulse(WendyMenuKey.Radio, CancellationToken.None).GetAwaiter().GetResult(), "disabled path sends no native keys");
    }
    public static void Execution() {
        var f = new Fixture(); f.Feed(0); var o = new Output(); var a = new WendyMenuActions(o);
        Check(a.Request("box box", 1, f.Race, 0).Kind == "executing", "execute");
        a.Tick(f.Race, 0); Check(o.Count == 1, "first pulse");
        Check(a.Request("prepare soft", 1, f.Race, 1).Kind == "rejected", "no queue");
        f.Feed(50); a.Tick(f.Race, 50); f.Feed(300); a.Tick(f.Race, 300); Check(o.Count == 2, "second pulse");
        f.Feed(350); a.Tick(f.Race, 350); f.Feed(600); a.Tick(f.Race, 600);
        Check(a.State == WendyCommandState.Unconfirmed && a.TakeCompletion()?.Text.Contains("cannot confirm") == true && a.TakeCompletion() == null, "never false success");
        Check(a.Request("box box", 1, f.Race, 600).Kind == "rejected" && o.Count == 2, "deduplicate cooldown");

        foreach (var cause in new[] { "stale", "speed", "throttle", "session", "cancel", "revision", "disable", "pause", "network", "race", "pit", "fault", "throw", "timeout" }) {
            f = new(); f.Feed(0); o = new(); a = new(o); a.Request("prepare medium", 1, f.Race, 0); a.Tick(f.Race, 0);
            switch (cause) {
                case "speed": f.Speed = 2; break; case "throttle": f.Throttle = .1f; break;
                case "session": f.Race.Reset(); break; case "cancel": a.Invalidate(); break;
                case "revision": o.Revision++; break; case "disable": o.Enabled = false; break;
                case "pause": f.Paused = true; break; case "network": f.Network = 1; break;
                case "race": f.SessionType = 10; break; case "pit": f.Pit = 1; break;
                case "fault": o.Fail = true; break; case "throw": o.Throw = true; break;
            }
            if (cause != "stale") f.Feed(100);
            a.Tick(f.Race, cause == "stale" ? 600 : cause == "timeout" ? 9000 : 100);
            if (cause is "fault" or "throw") { f.Feed(400); a.Tick(f.Race, 400); f.Feed(450); a.Tick(f.Race, 450); }
            Check(a.State == WendyCommandState.Unconfirmed, "stopped: " + cause);
            var count = o.Count; f.Feed(10000); a.Tick(f.Race, 10000); Check(o.Count == count, "no replay " + cause);
        }
        foreach (var bad in new[] { "low confidence", "no recipe", "menus open", "not practice" }) {
            f = new(); o = new(); a = new(o);
            if (bad == "no recipe") o.Steps = null;
            if (bad == "menus open") f.Panel = 1;
            if (bad == "not practice") f.SessionType = 10;
            f.Feed(0); Check(a.Request("box box", bad == "low confidence" ? .2 : 1, f.Race, 0).Kind == "rejected" && o.Count == 0, bad);
        }
        // Pits recipes cannot progress without a NEW observed pit page.
        f = new(); f.Feed(0); o = new() { Steps = [WendyMenuKey.Pits, WendyMenuKey.Right] }; a = new(o);
        a.Request("prepare soft", 1, f.Race, 0); a.Tick(f.Race, 0); f.Feed(50); a.Tick(f.Race, 50);
        f.Feed(350); a.Tick(f.Race, 350); Check(o.Count == 1, "wait for pit page");
        f.Panel = 1; f.Feed(400); a.Tick(f.Race, 400); Check(o.Count == 2, "new page permits next key");
        f.Panel = 2; f.Feed(500); a.Tick(f.Race, 500); Check(a.State == WendyCommandState.Unconfirmed, "page change cancels");
        f = new(); f.Feed(0); o = new() { Steps = [WendyMenuKey.Pits, WendyMenuKey.Right] }; a = new(o);
        a.Request("prepare soft", 1, f.Race, 0); a.Tick(f.Race, 0); f.Feed(50); a.Tick(f.Race, 50); f.Feed(1600); a.Tick(f.Race, 1600);
        Check(o.Count == 1 && a.State == WendyCommandState.Unconfirmed, "page timeout no retry");
        // Engineer integration: original text must match, and reconnect/cancel stop it.
        f = new(); f.Feed(0); o = new(); var e = new WendyEngineer(menuOutput: o);
        foreach (var query in new[] { "Full race update", "Driver in front", "Driver behind", "Tyre status", "Vehicle condition", "Weather report", "Pit stop information" }) {
            Check(e.Answer(query, 1, f.Race, 0).Kind == "answer", "read-only radio query " + query);
            e.Observe(f.Race, 0);
            Check(o.Count == 0 && !e.MenuActions.Busy, "query never sends menu input " + query);
        }
        Check(e.Answer("prepare medium", 1, f.Race, 0).Kind == "executing", "voice route");
        Check(e.Answer("cancel", 1, f.Race, 1).Kind == "unconfirmed", "honest cancel");
        e.ResetConversation(); e.Observe(f.Race, 1); Check(o.Count == 0, "cancel before any write");
        Check(e.Answer(new(DriverIntent.PLAN_PIT), "maybe box this lap", 1, f.Race, 2).Kind == "rejected", "model cannot authorize");
    }
}
