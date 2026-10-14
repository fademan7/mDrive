using System.Text.RegularExpressions;

namespace PhoneWheel.Core;

public enum WendyPitCommand { Box, Soft, Medium, Hard, Intermediate, Wet }
public enum WendyMenuKey { Radio, Pits, Up, Down, Left, Right, Enter }
public interface IWendyMenuOutput
{
    long Revision { get; }
    bool Enabled { get; }
    WendyMenuKey[]? Recipe(WendyPitCommand command);
    Task<bool> Pulse(WendyMenuKey key, CancellationToken token);
}

// Experimental, operator-calibrated menu recipes. No controller report ownership.
// This first trial is deliberately restricted to stationary single-player practice.
public sealed class WendyMenuActions(IWendyMenuOutput? output = null)
{
    public WendyCommandState State { get; private set; } = WendyCommandState.Rejected;
    public bool Busy => State is WendyCommandState.Requested or WendyCommandState.Executing;
    private WendyMenuKey[] steps = [];
    private Task<bool>? pulse;
    private CancellationTokenSource? cancel;
    private int index, generation;
    private long revision;
    private double started, nextAt, panelAfter, lastRequest = double.NegativeInfinity;
    private bool waitPits;
    private WendyReply? completion;
    public string Detail { get; private set; } = "Menu trial disabled.";

    public static bool TryParse(string text, out WendyPitCommand command)
    {
        command = WendyPitCommand.Box;
        if (text.Length is 0 or > 240) return false;
        var q = WendyNaturalLanguage.Request(text);
        if (Regex.IsMatch(q, @"^(?:boxbox|box(?: box){0,2}|box this lap|pit this lap|lets box|lets pit|i want to pit|request (?:a )?pit stop)$")) return true;
        var match = Regex.Match(q, @"^(?:prepare|fit|select|put on|set|give me)(?: the| a set of)? (softs?|mediums?|hards?|inters?|intermediates?|wets?)(?: tyres?| tires?)?(?: (?:for |at )?(?:the )?next (?:stop|pit stop))?$");
        if (!match.Success) return false;
        command = match.Groups[1].Value switch {
            "soft" or "softs" => WendyPitCommand.Soft,
            "medium" or "mediums" => WendyPitCommand.Medium,
            "hard" or "hards" => WendyPitCommand.Hard,
            "inter" or "inters" or "intermediate" or "intermediates" => WendyPitCommand.Intermediate,
            _ => WendyPitCommand.Wet
        };
        return true;
    }
    public static bool LooksLikeRequest(string text) => Regex.IsMatch(WendyNaturalLanguage.Request(text),
        @"\b(?:boxbox|prepare|fit|select|put on)\b|\b(?:softs?|mediums?|hards?|inters?|intermediates?|wets?)\b.*\bnext (?:stop|pit stop)\b");

    public static bool SafeTrial(F1RaceState r, double now) => r.Live(now) && !r.NetworkGame && r.SessionType is >= 1 and <= 4 &&
        F1RaceState.Fresh(r.LapMs, now, 500) && F1RaceState.Fresh(r.TelemetryMs, now, 500) &&
        r.Result == 2 && r.Pit == 0 && r.SpeedKph <= 1 && r.Throttle < .01f;

    public WendyReply Request(string text, double confidence, F1RaceState r, double now)
    {
        if (Busy) return new("A menu command is already running. No command queued.", "rejected");
        completion = null; State = WendyCommandState.Requested;
        if (!TryParse(text, out var command) || !double.IsFinite(confidence) || confidence < .75)
            return Reject("Please name one clear pit command or tyre compound. No input sent.");
        if (output?.Enabled != true) return Reject("The practice menu trial is off. No game input sent.");
        if (!SafeTrial(r, now) || r.MfdPanel != 255) return Reject("Stop off the racing line in solo practice, release throttle and close all menus first.");
        var recipeRevision = output.Revision;
        var recipe = output.Recipe(command);
        if (recipe is not { Length: >= 2 and <= 16 } || recipe[0] is not (WendyMenuKey.Radio or WendyMenuKey.Pits) ||
            recipe.Skip(1).Any(k => k is WendyMenuKey.Radio or WendyMenuKey.Pits || !Enum.IsDefined(k)))
            return Reject("That command has no calibrated menu sequence. No input sent.");
        if (!output.Enabled || output.Revision != recipeRevision) return Reject("Menu configuration changed. No input sent.");
        if (now - lastRequest < 5000) return Reject("Please wait before another menu trial. No input sent.");
        lastRequest = now; steps = (WendyMenuKey[])recipe.Clone(); index = 0; generation = r.Generation; revision = recipeRevision;
        started = nextAt = now; waitPits = false; pulse = null;
        cancel?.Dispose(); cancel = new(); cancel.CancelAfter(8000);
        State = WendyCommandState.Executing;
        Detail = $"Trying {(command == WendyPitCommand.Box ? "pit request" : command.ToString().ToLowerInvariant() + " tyre selection")}.";
        return new(Detail, "executing");
    }
    private WendyReply Reject(string reason) { State = WendyCommandState.Rejected; Detail = reason; return new(reason, "rejected"); }
    public void Tick(F1RaceState r, double now)
    {
        if (!Busy) { if (r.Generation != generation) completion = null; return; }
        if (r.Generation != generation || !SafeTrial(r, now) || now - started > 8000 || output?.Enabled != true || output.Revision != revision || cancel!.IsCancellationRequested) {
            Finish("Menu trial stopped after a state change. Result unconfirmed; no retry."); return;
        }
        if (pulse != null) {
            if (!pulse.IsCompleted) return;
            if (!pulse.IsCompletedSuccessfully || !pulse.Result) { Finish("Menu input failed or game focus changed. Result unconfirmed; no retry."); return; }
            pulse = null; waitPits = steps[index] == WendyMenuKey.Pits; panelAfter = now; index++; nextAt = now + 250;
        }
        if (waitPits) {
            if (now - panelAfter > 1500) { Finish("The pit menu did not open. No further input sent."); return; }
            if (r.TelemetryMs <= panelAfter || r.MfdPanel != 1) return;
            waitPits = false;
        }
        if (index > 0 && steps[0] == WendyMenuKey.Pits && r.MfdPanel != 1) { Finish("The pit menu changed. No further input sent."); return; }
        if (now < nextAt) return;
        if (index == steps.Length) {
            Finish("Menu inputs sent. I cannot confirm the game's pit request or next tyre selection. Please check the game."); return;
        }
        try { pulse = output!.Pulse(steps[index], cancel.Token); }
        catch (Exception) { Finish("Menu output error. Result unconfirmed; no retry."); }
    }
    private void Finish(string reason) {
        cancel?.Cancel(); pulse = null; State = WendyCommandState.Unconfirmed; Detail = reason; completion = new(reason, "unconfirmed");
    }
    public void Invalidate() { if (Busy) Finish("Menu command cancelled. No retry."); cancel?.Cancel(); completion = null; }
    public WendyReply? TakeCompletion() { var result = completion; completion = null; return result; }
}
