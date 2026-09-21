using PhoneWheel.Core;
using System.Diagnostics;

internal static class OutputRecoveryTests
{
    private sealed class FlakyOutput : IGamepadOutput {
        public string Name => "injected-fault";
        public volatile bool Fail;
        private readonly object sync = new();
        private Controls last;
        public Controls Last { get { lock (sync) return last; } }
        public readonly HashSet<int> Threads = [];
        public void Write(Controls c) {
            lock (sync) { Threads.Add(Environment.CurrentManagedThreadId); if (Fail) throw new IOException("Injected output failure"); last = c; }
        }
        public void Neutralize() => Write(Controls.Neutral);
        public void Dispose() { }
    }
    private static void Until(Func<bool> condition, string label, int timeout = 2000) {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < timeout) Thread.Sleep(5);
        if (!condition()) throw new Exception(label);
    }
    public static void Recovery() {
        var output = new FlakyOutput(); var worker = new GamepadWorker(output);
        var callbacks = 0;
        worker.OutputError += _ => Interlocked.Increment(ref callbacks);
        worker.OutputError += _ => throw new IOException("Broken diagnostic observer must not kill output recovery");
        try {
            worker.Publish(new Controls(.5f, .7f, .2f));
            Until(() => output.Last.Throttle > .6f, "initial gamepad report");
            output.Fail = true;
            worker.Publish(new Controls(.5f, .7f, .2f));
            Until(() => worker.Faulted && callbacks > 0, "fault observed and callback");
            var held = Stopwatch.StartNew();
            while (held.ElapsedMilliseconds < 600) { worker.Publish(new Controls(.5f, .9f, .2f)); Thread.Sleep(5); }
            if (worker.FailureCount < 2) throw new Exception("retry worker died after first error");
            output.Fail = false;
            Until(() => !worker.Faulted, "neutral recovery");
            if (!output.Last.IsNeutral) throw new Exception("buffered throttle replayed after recovery");
            worker.Publish(new Controls(.1f, .3f, 0));
            Until(() => output.Last.Throttle > .2f, "fresh report after recovery");
            Until(() => output.Last.IsNeutral, "producer stall expires stale output", 500);
            if (output.Threads.Count != 1) throw new Exception("native writes not serialized on one thread");
        } finally { worker.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        if (worker.Publish(Controls.Neutral)) throw new Exception("publish after shutdown");
    }
    public static void DedicatedLoops() {
        using var cancel = new CancellationTokenSource(); var ticks = 0; var poolThread = false;
        var loop = CriticalLoop.Start("test control clock", 4, cancel.Token, () => { poolThread |= Thread.CurrentThread.IsThreadPoolThread; Interlocked.Increment(ref ticks); });
        Until(() => ticks >= 5, "dedicated timer ticks"); cancel.Cancel(); loop.GetAwaiter().GetResult();
        if (poolThread) throw new Exception("control loop uses shared thread pool");
        using var faultCancel = new CancellationTokenSource();
        var failure = CriticalLoop.Start("test error", 4, faultCancel.Token, () => throw new IOException("test"));
        try { failure.GetAwaiter().GetResult(); throw new Exception("missing failure propagation"); } catch (IOException) { }
    }
}
