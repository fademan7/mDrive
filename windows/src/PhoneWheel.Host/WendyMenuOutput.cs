using System.Diagnostics;
using System.Runtime.InteropServices;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

// Test-only keyboard channel. No reference to SafetyGate, GamepadWorker or ViGEm.
internal sealed class WendyMenuOutput : IWendyMenuOutput
{
    private sealed record Profile(Dictionary<WendyPitCommand, WendyMenuKey[]> Recipes, long Expires);
    private Profile? profile;
    private long revision;
    private long telemetryLease;
    private int generation = -1;
    // A queued keyboard task must not wake up after telemetry has stopped.
    // Called only by the optional Wendy worker, never by controller receive/output.
    public void Observe(F1RaceState race, double now) {
        if (generation != race.Generation) { Invalidate(); generation = race.Generation; }
        Interlocked.Exchange(ref telemetryLease, WendyMenuActions.SafeTrial(race, now) ? Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4 : 0);
    }
    public long Revision => Interlocked.Read(ref revision);
    public bool Enabled => Volatile.Read(ref profile) is { } p && Stopwatch.GetTimestamp() < p.Expires;
    public void Invalidate() { Interlocked.Increment(ref revision); Volatile.Write(ref profile, null); }
    public static bool ParseRecipe(string text, out WendyMenuKey[] keys) {
        keys = [];
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 16) return false;
        var parsed = new List<WendyMenuKey>();
        foreach (var part in parts) {
            // Enum.TryParse alone also accepts arbitrary numeric values.
            if (!Enum.GetNames<WendyMenuKey>().Any(n => n.Equals(part, StringComparison.OrdinalIgnoreCase)) ||
                !Enum.TryParse<WendyMenuKey>(part, true, out var key)) return false;
            parsed.Add(key);
        }
        if (parsed[0] is not (WendyMenuKey.Radio or WendyMenuKey.Pits) || parsed.Skip(1).Any(k => k is WendyMenuKey.Radio or WendyMenuKey.Pits)) return false;
        keys = parsed.ToArray(); return true;
    }
    public void Configure(Dictionary<WendyPitCommand, WendyMenuKey[]> recipes) {
        Invalidate();
        var copy = new Dictionary<WendyPitCommand, WendyMenuKey[]>();
        foreach (var (command, keys) in recipes) {
            if (!Enum.IsDefined(command) || !ParseRecipe(string.Join(',', keys), out var parsed)) throw new ArgumentException("Invalid menu recipe.");
            copy.Add(command, parsed);
        }
        if (copy.Count > 0) Volatile.Write(ref profile, new(copy, Stopwatch.GetTimestamp() + 600 * Stopwatch.Frequency));
    }
    public WendyMenuKey[]? Recipe(WendyPitCommand command) => Volatile.Read(ref profile) is { } p && p.Recipes.TryGetValue(command, out var keys) ? (WendyMenuKey[])keys.Clone() : null;
    public Task<bool> Pulse(WendyMenuKey key, CancellationToken token) {
        var owner = Volatile.Read(ref profile); var ticket = Revision;
        return Task.Run(async () => {
            bool Valid() => owner != null && ReferenceEquals(owner, Volatile.Read(ref profile)) && ticket == Revision && Enabled &&
                Stopwatch.GetTimestamp() < Interlocked.Read(ref telemetryLease) && !token.IsCancellationRequested && F1Foreground();
            if (!Valid()) return false;
            ushort vk = key switch { WendyMenuKey.Radio => 0x54, WendyMenuKey.Pits => 0x71, WendyMenuKey.Up => 0x26, WendyMenuKey.Down => 0x28,
                WendyMenuKey.Left => 0x25, WendyMenuKey.Right => 0x27, WendyMenuKey.Enter => 0x0d, _ => 0 };
            if (vk == 0 || (GetAsyncKeyState(vk) & 0x8000) != 0) return false;
            var pressed = false; var released = false; var valid = false;
            try {
                if (!Valid()) return false;
                pressed = Send(vk, false); if (!pressed) return false;
                await Task.Delay(45, token); valid = Valid();
            } catch (OperationCanceledException) { }
            finally { if (pressed) released = Send(vk, true); }
            return valid && released;
        });
    }
    private static bool F1Foreground() {
        try { GetWindowThreadProcessId(GetForegroundWindow(), out var pid); using var p = Process.GetProcessById((int)pid); return p.ProcessName.Equals("F1_25", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    private static bool Send(ushort key, bool up) {
        var input = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = (up ? 2u : 0u) | (key is >= 0x25 and <= 0x28 ? 1u : 0u) } } };
        return SendInput(1, [input], Marshal.SizeOf<Input>()) == 1;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit, Size = 32)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
