using PhoneWheel.Core;
using PhoneWheel.Output.ViGEm;

var backend = GetOption(args, "--backend") ?? "vigem";
var seconds = double.TryParse(GetOption(args, "--step-seconds"), out var parsed) ? parsed : 2.0;
if (seconds <= 0) return Fail("--step-seconds must be positive.");

IGamepadOutput output;
try
{
    output = backend switch
    {
        "vigem" => new ViGEmGamepadOutput(),
        "null" => new NullGamepadOutput(),
        "csv" => new CsvGamepadOutput(GetOption(args, "--csv") ?? "probe-output.csv"),
        _ => throw new ArgumentException("Backend must be vigem, null, or csv.")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"NOT RUN — could not create {backend} output: {ex.GetType().Name}: {ex.Message}");
    if (backend == "vigem")
        Console.Error.WriteLine("Install the official ViGEmBus 1.22.0 release, then rerun. Driver installation alone does not create a pad.");
    return 2;
}

using (output)
{
    Console.WriteLine($"Backend={output.Name}. Observe XInput now; Ctrl+C always attempts neutralization.");
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    var steps = new (string Label, Controls Value)[]
    {
        ("neutral", new()),
        ("LX -50%", new(-0.5f, 0, 0)),
        ("LX +50%", new(0.5f, 0, 0)),
        ("LT brake 50%", new(0, 0, 0.5f)),
        ("RT throttle 50%", new(0, 0.5f, 0)),
        ("LT+RT 100% simultaneous", new(0, 1, 1)),
        ("neutral", new())
    };
    try
    {
        foreach (var step in steps)
        {
            output.Write(step.Value);
            Console.WriteLine($"{step.Label}: LX={step.Value.StickX}, LT={step.Value.LeftTrigger}, RT={step.Value.RightTrigger}");
            await Task.Delay(TimeSpan.FromSeconds(seconds), cancel.Token);
        }
    }
    catch (OperationCanceledException) { }
    finally { output.Neutralize(); }
}
return 0;

static string? GetOption(string[] values, string name)
{
    var index = Array.IndexOf(values, name);
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}
static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
