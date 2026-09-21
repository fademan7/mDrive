using System.Globalization;
using System.Diagnostics;

namespace PhoneWheel.Core;

public interface IGamepadOutput : IDisposable
{
    string Name { get; }
    void Write(Controls controls);
    void Neutralize();
}

public sealed class NullGamepadOutput : IGamepadOutput
{
    public string Name => "null";
    public Controls Last { get; private set; }
    public void Write(Controls controls) { controls.Validate(); Last = controls; }
    public void Neutralize() => Last = Controls.Neutral;
    public void Dispose() => Neutralize();
}

public sealed class CsvGamepadOutput : IGamepadOutput
{
    private readonly StreamWriter _writer;
    private readonly long _started = System.Diagnostics.Stopwatch.GetTimestamp();
    public CsvGamepadOutput(string path)
    {
        _writer = new(path, append: false);
        _writer.WriteLine("pc_monotonic_ns,elapsed_ms,steer,lx,brake,lt,throttle,rt,buttons");
    }
    public string Name => "csv";
    public void Write(Controls c)
    {
        c.Validate();
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_started, now).TotalMilliseconds;
        var monotonicNs = checked((long)(now * (1_000_000_000.0 / System.Diagnostics.Stopwatch.Frequency)));
        _writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{monotonicNs},{elapsed:F3},{c.Steer:F6},{c.StickX},{c.Brake:F6},{c.LeftTrigger},{c.Throttle:F6},{c.RightTrigger},{c.Buttons}"));
        _writer.Flush();
    }
    public void Neutralize() => Write(Controls.Neutral);
    public void Dispose() { Neutralize(); _writer.Dispose(); }
}

public sealed class GamepadWorker : IAsyncDisposable
{
    private readonly IGamepadOutput _output;
    private sealed record Sample(Controls Controls, long At);
    private Sample? _latest;
    private readonly AutoResetEvent _wake = new(false);
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private volatile bool _stopping;
    private volatile bool _faulted;
    private long _failures, _writes, _lastSuccess;
    private readonly long _started = Stopwatch.GetTimestamp();
    private string _lastError = "None";
    public bool Faulted => _faulted;
    public long FailureCount => Interlocked.Read(ref _failures);
    public long WriteCount => Interlocked.Read(ref _writes);
    public string LastError => Volatile.Read(ref _lastError);
    public double LastWriteAgeMs => Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastSuccess) is var stamp && stamp != 0 ? stamp : _started).TotalMilliseconds;
    public event Action<Exception>? OutputError;

    public GamepadWorker(IGamepadOutput output)
    {
        _output = output;
        // The critical native gamepad call must not share .NET thread-pool
        // continuations with UI, speech/model work or telemetry processing.
        _thread = new Thread(Run) { IsBackground = true, Name = "mDrive gamepad output", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public bool Publish(Controls controls) {
        if (_stopping) return false;
        Interlocked.Exchange(ref _latest, new Sample(controls, Stopwatch.GetTimestamp()));
        try { _wake.Set(); } catch (ObjectDisposedException) { return false; }
        return true;
    }

    private void Run()
    {
        var last = Controls.Neutral;
        long lastPublished = 0, nextRetry = 0;
        try
        {
            while (!_stopping) {
                _wake.WaitOne(10);
                if (_stopping) break;
                var sample = Interlocked.Exchange(ref _latest, null);
                if (sample != null) { last = sample.Controls; lastPublished = sample.At; }
                if (_faulted && Stopwatch.GetTimestamp() < nextRetry) continue;
                // Independent freshness guard even if the producer/watchdog
                // stalls. Never replay buffered throttle when output recovers.
                var controls = _faulted || lastPublished == 0 || Stopwatch.GetElapsedTime(lastPublished).TotalMilliseconds >= 150 ? Controls.Neutral : last;
                try {
                    _output.Write(controls);
                    Interlocked.Increment(ref _writes);
                    Interlocked.Exchange(ref _lastSuccess, Stopwatch.GetTimestamp());
                    if (_faulted) {
                        last = Controls.Neutral; lastPublished = 0;
                        Interlocked.Exchange(ref _latest, null);
                        _faulted = false; // Only after a successful neutral report.
                    }
                }
                catch (Exception ex) {
                    Interlocked.Increment(ref _failures);
                    Volatile.Write(ref _lastError, ex.GetType().Name);
                    var first = !_faulted;
                    _faulted = true; last = Controls.Neutral; lastPublished = 0;
                    Interlocked.Exchange(ref _latest, null);
                    // Disarm before any retry; keep the same virtual controller
                    // so games do not lose their XInput device assignment.
                    if (first) try { OutputError?.Invoke(ex); } catch { /* Diagnostics cannot kill output recovery. */ }
                    try { _output.Neutralize(); } catch { }
                    nextRetry = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4;
                }
            }
        }
        finally
        {
            try { _output.Neutralize(); } catch { }
            try { _output.Dispose(); } catch { }
            _done.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stopping = true; _wake.Set();
        await _done.Task;
        _wake.Dispose();
    }
}
