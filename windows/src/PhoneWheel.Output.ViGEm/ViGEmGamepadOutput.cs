using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PhoneWheel.Core;
using System.Diagnostics;

namespace PhoneWheel.Output.ViGEm;

public sealed class ViGEmGamepadOutput : IGamepadOutput
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;
    private bool _disposed;
    private readonly RumbleMailbox _rumble = new();
    private readonly long _rumbleClock = Stopwatch.GetTimestamp();
    private double RumbleNow => Stopwatch.GetElapsedTime(_rumbleClock).TotalMilliseconds;
    public byte ReadRumbleLevel(bool armed) => _rumble.ReadLevel(RumbleNow, armed);

    public ViGEmGamepadOutput()
    {
        _client = new ViGEmClient();
        _controller = _client.CreateXbox360Controller();
        _controller.AutoSubmitReport = false;
        _controller.ResetReport();
        _controller.FeedbackReceived += (_, feedback) => _rumble.Update(feedback.LargeMotor, feedback.SmallMotor, RumbleNow);
        _controller.Connect();
        // On the first tested PC, an unchanged all-zero report did not advance
        // XInput's packet counter and the just-enumerated slot briefly exposed
        // a stale LX value. A one-count pulse followed immediately by neutral
        // forces an observable report transition without meaningful steering.
        Write(new Controls(1f / 32767f, 0, 0));
        Neutralize();
        Thread.Sleep(20);
    }

    public string Name => "vigem-xbox360";

    public void Write(Controls controls)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        controls.Validate();
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, controls.StickX);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, Controls.QuantizeStick(controls.LookX));
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, Controls.QuantizeStick(controls.LookY));
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, controls.LeftTrigger);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, controls.RightTrigger);
        _controller.SetButtonsFull(controls.Buttons);
        _controller.SubmitReport();
    }

    public void Neutralize() => Write(Controls.Neutral);

    public void Dispose()
    {
        if (_disposed) return;
        try { Neutralize(); } catch { }
        try { _controller.Disconnect(); } catch { }
        _client.Dispose();
        _disposed = true;
    }
}
