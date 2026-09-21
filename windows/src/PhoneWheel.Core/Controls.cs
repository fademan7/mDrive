namespace PhoneWheel.Core;

public readonly record struct Controls(float Steer = 0, float Throttle = 0, float Brake = 0, ushort Buttons = 0, float LookX = 0, float LookY = 0)
{
    public static Controls Neutral => new();

    public void Validate()
    {
        if (!float.IsFinite(Steer) || !float.IsFinite(Throttle) || !float.IsFinite(Brake))
            throw new ProtocolException("Control contains NaN or infinity.");
        if (Steer is < -1 or > 1 || Throttle is < 0 or > 1 || Brake is < 0 or > 1)
            throw new ProtocolException("Control is outside its normalized range.");
        if ((Buttons & ~0xF3FF) != 0)
            throw new ProtocolException("Reserved XInput button bits are set.");
        if (!float.IsFinite(LookX) || !float.IsFinite(LookY) || LookX is < -1 or > 1 || LookY is < -1 or > 1)
            throw new ProtocolException("Invalid right stick.");
    }

    public bool IsNeutral => MathF.Abs(Steer) <= 0.05f && Throttle == 0 && Brake == 0 && Buttons == 0 && LookX == 0 && LookY == 0;

    public short StickX => QuantizeStick(Steer);
    public byte LeftTrigger => QuantizeTrigger(Brake);
    public byte RightTrigger => QuantizeTrigger(Throttle);

    public static short QuantizeStick(float value)
    {
        if (!float.IsFinite(value)) throw new ProtocolException("Non-finite stick value.");
        value = Math.Clamp(value, -1, 1);
        var scale = value < 0 ? 32768f : 32767f;
        var magnitude = (int)MathF.Floor(MathF.Abs(value) * scale + 0.5f);
        return checked((short)(value < 0 ? -magnitude : magnitude));
    }

    public static byte QuantizeTrigger(float value)
    {
        if (!float.IsFinite(value)) throw new ProtocolException("Non-finite trigger value.");
        return checked((byte)MathF.Floor(Math.Clamp(value, 0, 1) * 255f + 0.5f));
    }
}

public sealed class ProtocolException(string message) : Exception(message);
