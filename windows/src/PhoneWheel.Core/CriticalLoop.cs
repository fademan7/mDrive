using System.Diagnostics;

namespace PhoneWheel.Core;

// Short control-plane callbacks on dedicated threads, not the shared async
// pool used by voice/telemetry. No catch-up burst after an OS scheduling delay.
public static class CriticalLoop
{
    public static Task Run(string name, Action body) {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() => { try { body(); done.TrySetResult(); } catch (Exception ex) { done.TrySetException(ex); } })
            { IsBackground = true, Name = name, Priority = ThreadPriority.AboveNormal }.Start();
        return done.Task;
    }
    public static Task Start(string name, int intervalMs, CancellationToken token, Action tick) {
        if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs));
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => {
            try {
                while (!token.IsCancellationRequested) {
                    var at = Stopwatch.GetTimestamp();
                    tick();
                    var wait = Math.Max(1, intervalMs - (int)Stopwatch.GetElapsedTime(at).TotalMilliseconds);
                    if (token.WaitHandle.WaitOne(wait)) break;
                }
                done.TrySetResult();
            } catch (OperationCanceledException) when (token.IsCancellationRequested) { done.TrySetResult(); }
            catch (Exception ex) { done.TrySetException(ex); }
        }) { IsBackground = true, Name = name, Priority = ThreadPriority.AboveNormal };
        thread.Start();
        return done.Task;
    }
}
