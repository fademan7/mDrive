using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

public enum WendyCommandState { Requested, Executing, Confirmed, Unconfirmed, Rejected }
public enum WendySetting { BrakeBias, Differential }
public interface IWendyActionOutput
{
    bool Enabled { get; }
    Task<bool> Pulse(WendySetting setting, int direction, CancellationToken cancellation);
}

// Optional, stop-and-verify direct shortcuts. Never owns controller state/buttons.
// A failed step is never retried. Every further step requires new game telemetry.
public sealed class WendyActions(IWendyActionOutput? output = null)
{
    public WendyCommandState State { get; private set; } = WendyCommandState.Rejected;
    public bool Busy => State is WendyCommandState.Requested or WendyCommandState.Executing;
    public string Detail { get; private set; } = "No command requested.";
    private WendySetting setting;
    private int target, expected, current, generation;
    private double started, verifyAfter;
    private Task<bool>? pulse;
    private CancellationTokenSource? cancellation;
    private bool verifying;
    private WendyReply? completion;
    public string Label => setting == WendySetting.BrakeBias ? "Brake bias" : "Differential";
    private static int Value(F1RaceState r, WendySetting s) => s == WendySetting.BrakeBias ? r.BrakeBias : r.Differential;
    private static double Stamp(F1RaceState r, WendySetting s) => s == WendySetting.BrakeBias ? r.StatusMs : r.SetupMs;
    private static bool Ready(F1RaceState r, double now) => r.Live(now) && F1RaceState.Fresh(r.LapMs, now, 500) && r.Pit == 0 && r.Result == 2;

    public static bool Parse(string text, out WendySetting setting, out int value, out bool relative)
    {
        setting = WendySetting.BrakeBias; value = 0; relative = false;
        if (text.Length > 240 || Regex.IsMatch(text, @"-\s*\d")) return false;
        var q = WendyNaturalLanguage.Request(text);
        q = Regex.Replace(q, @"\bthe (brake bias|diff|differential)\b", "$1");
        q = Regex.Replace(q, @"\bdiff\b", "differential");
        q = Regex.Replace(q, @"\b(twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)(?: (one|two|three|four|five|six|seven|eight|nine))?\b", m =>
            ((Array.IndexOf(new[] {"twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"},m.Groups[1].Value)+2)*10 +
             (m.Groups[2].Success ? Array.IndexOf(new[] {"one","two","three","four","five","six","seven","eight","nine"},m.Groups[2].Value)+1 : 0)).ToString());
        q = Regex.Replace(q, @"\b(one|two|three|four|five|six|seven|eight|nine|ten)\b", m =>
            (Array.IndexOf(new[] {"one","two","three","four","five","six","seven","eight","nine","ten"}, m.Value)+1).ToString());
        q = Regex.Replace(q, @"(?: percent| per cent)$", "");
        var exact = Regex.Match(q, @"^set (brake bias|differential) to ([0-9]{1,3})$");
        if (exact.Success) { setting = exact.Groups[1].Value == "brake bias" ? WendySetting.BrakeBias : WendySetting.Differential; value = int.Parse(exact.Groups[2].Value); return value <= 100; }
        var change = Regex.Match(q, @"^(increase|decrease|reduce) (brake bias|differential) by ([0-9]{1,2})$");
        var forward = Regex.Match(q, @"^move brake bias (forward|back|backward|rearward) (?:by )?([0-9]{1,2})$");
        if (change.Success) { setting = change.Groups[2].Value == "brake bias" ? WendySetting.BrakeBias : WendySetting.Differential; value = int.Parse(change.Groups[3].Value) * (change.Groups[1].Value == "increase" ? 1 : -1); }
        else if (forward.Success) value = int.Parse(forward.Groups[2].Value) * (forward.Groups[1].Value == "forward" ? 1 : -1);
        else return false;
        relative = true; return value != 0 && Math.Abs(value) <= 20;
    }
    public WendyReply Request(string text, double confidence, F1RaceState r, double now)
    {
        if (Busy) return new("A setting change is already pending. Please wait.", "rejected");
        completion = null; State = WendyCommandState.Requested;
        if (!double.IsFinite(confidence) || confidence < .75 || !Parse(text, out setting, out var number, out var relative))
            return Reject("I couldn't confirm the exact setting and number. No change made.");
        if (!Ready(r, now) || !F1RaceState.Fresh(Stamp(r, setting), now, 1000)) return Reject("Fresh driving telemetry is required. No change made.");
        target = relative ? Value(r, setting) + number : number;
        var step = setting == WendySetting.BrakeBias ? 1 : 5;
        if (target is < 0 or > 100 || Math.Abs(target - Value(r, setting)) > 20 || (target - Value(r, setting)) % step != 0)
            return Reject("That target is outside the verified step or twenty-point change limit. No change made.");
        if (output?.Enabled != true) return Reject("Direct setting shortcuts are not enabled in the receiver. No change made.");
        if (Value(r, setting) == target) { State = WendyCommandState.Confirmed; Detail = $"{Label} is already {target}."; return new(Detail, "confirmed"); }
        generation = r.Generation; started = now; verifying = false; pulse = null; current = Value(r, setting);
        cancellation?.Dispose(); cancellation = new(); State = WendyCommandState.Executing;
        Detail = $"Changing {Label.ToLowerInvariant()} to {target}.";
        return new(Detail, "executing");
    }
    public WendyReply Reject(string reason) { cancellation?.Cancel(); pulse = null; completion = null; State = WendyCommandState.Rejected; Detail = reason; return new(reason, "rejected"); }
    public void Invalidate()
    {
        if (Busy) Finish(false, "Command cancelled after connection or session change. No retry.");
        cancellation?.Cancel(); completion = null;
    }
    public void Tick(F1RaceState r, double now)
    {
        if (!Busy) { if (generation != r.Generation) completion = null; return; }
        if (generation != r.Generation || !Ready(r, now) || now - started > 8000 || output?.Enabled != true) { Finish(false); return; }
        if (pulse != null) {
            if (!pulse.IsCompleted) return;
            if (!pulse.IsCompletedSuccessfully || !pulse.Result) { Finish(false); return; }
            pulse = null; verifying = true; verifyAfter = now; return;
        }
        var stamp = Stamp(r, setting);
        if (!F1RaceState.Fresh(stamp, now, 1000)) { Finish(false); return; }
        if (verifying) {
            if (now - verifyAfter > 1500) { Finish(false); return; }
            if (stamp <= verifyAfter) return;
            if (Value(r, setting) != expected) return; // no guessed success on an intermediate/clamped value
            verifying = false; current = expected;
        }
        if (Value(r, setting) != current) { Finish(false, "Setting changed outside this command. No further input sent."); return; }
        if (Value(r, setting) == target) { Finish(true); return; }
        var direction = Math.Sign(target - Value(r, setting));
        expected = Value(r, setting) + direction * (setting == WendySetting.BrakeBias ? 1 : 5);
        pulse = output!.Pulse(setting, direction, cancellation!.Token);
    }
    private void Finish(bool success, string? reason = null)
    {
        cancellation?.Cancel(); State = success ? WendyCommandState.Confirmed : WendyCommandState.Unconfirmed;
        Detail = reason ?? (success ? $"{Label} set to {target}." : $"I couldn't confirm the {Label.ToLowerInvariant()} change. No retry.");
        completion = new(Detail, success ? "confirmed" : "unconfirmed"); pulse = null;
    }
    public WendyReply? TakeCompletion() { var value = completion; completion = null; return value; }
}
