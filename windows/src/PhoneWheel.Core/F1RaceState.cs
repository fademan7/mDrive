using System.Buffers.Binary;
using System.Text;

namespace PhoneWheel.Core;

public sealed record F1Forecast(int Minutes, int Weather, int RainPercent);

// EA F1 25 UDP v3: packed LE, format 2025 / packet version 1 only.
// Mutable state is owned by the engineer service, never by the gamepad worker.
public sealed class F1RaceState
{
    public ulong Session { get; private set; }
    public byte Player { get; private set; }
    public int Generation { get; private set; }
    public double LastPacketMs { get; private set; } = double.NegativeInfinity;
    public double SessionMs { get; private set; } = double.NegativeInfinity;
    public double LapMs { get; private set; } = double.NegativeInfinity;
    public double StatusMs { get; private set; } = double.NegativeInfinity;
    public double DamageMs { get; private set; } = double.NegativeInfinity;
    public double SetupMs { get; private set; } = double.NegativeInfinity;
    public double TelemetryMs { get; private set; } = double.NegativeInfinity;
    public int Differential { get; private set; }
    public int OffThrottleDifferential { get; private set; }
    public int FrontWingSetup { get; private set; }
    public int RearWingSetup { get; private set; }
    public float[] SetupPressure { get; private set; } = new float[4];
    public byte[] InnerTemperature { get; private set; } = new byte[4];
    public byte[] SurfaceTemperature { get; private set; } = new byte[4];
    public ushort[] BrakeTemperature { get; private set; } = new ushort[4];
    public float[] TyrePressure { get; private set; } = new float[4];
    public int EngineTemperature { get; private set; }
    public int SpeedKph { get; private set; }
    public int Gear { get; private set; }
    public int Rpm { get; private set; }
    public bool DrsOpen { get; private set; }
    public bool DrsAllowed { get; private set; }
    public int DrsDistance { get; private set; }
    public bool PitLimiter { get; private set; }
    public int ErsMode { get; private set; }
    public int VisualCompound { get; private set; }
    public int SessionType { get; private set; }
    public int Formula { get; private set; }
    public int SessionSecondsLeft { get; private set; }
    public int TrackTemperature { get; private set; }
    public int AirTemperature { get; private set; }
    public int PitSpeedLimit { get; private set; }
    public int PitStops { get; private set; }
    public int Sector { get; private set; }
    public uint Sector1Ms { get; private set; }
    public uint Sector2Ms { get; private set; }
    public byte[] ComponentDamage { get; private set; } = new byte[18]; // payload offsets 28..45
    public byte[] WheelDamage { get; private set; } = new byte[12]; // tyre/brake/blister, RL RR FL FR
    public double RaceStartMs { get; private set; } = double.NegativeInfinity;
    public float Throttle { get; private set; }
    public float Brake { get; private set; }
    public float Steer { get; private set; }
    public int PenaltySeconds { get; private set; }
    public int TrackWarnings { get; private set; }
    public int DriveThrough { get; private set; }
    public int StopGo { get; private set; }
    public bool LapInvalid { get; private set; }
    public uint LastLapMs { get; private set; }
    public uint CurrentLapTimeMs { get; private set; }
    public float LapDistance { get; private set; }
    public int TrackLength { get; private set; }
    public int TrackId { get; private set; }
    public int TotalLaps { get; private set; }
    public int PitIdealLap { get; private set; }
    public int PitLatestLap { get; private set; }
    public int PitRejoinPosition { get; private set; }
    public int TyreCompound { get; private set; }
    public int TyreAge { get; private set; }
    public int DriverStatus { get; private set; }
    public int BehindCar { get; private set; } = -1;
    public uint SetupSignature { get; private set; }
    public int AheadCar { get; private set; } = -1;
    public int Weather { get; private set; }
    public F1Forecast[] Forecast { get; private set; } = [];
    public bool ApproximateForecast { get; private set; }
    public double? LeaderGap { get; private set; }
    public int SafetyCar { get; private set; }
    public bool Paused { get; private set; }
    public bool Spectating { get; private set; }
    public int Position { get; private set; }
    public int Lap { get; private set; }
    public int Pit { get; private set; }
    public int Result { get; private set; }
    public double? Ahead { get; private set; }
    public double? Behind { get; private set; }
    public float FuelKg { get; private set; }
    public float FuelMfdLaps { get; private set; }
    public float ErsJoules { get; private set; }
    public int BrakeBias { get; private set; }
    public int FiaFlag { get; private set; } = -1;
    public float[] Wear { get; private set; } = new float[4];
    public int FrontWing { get; private set; }
    public int OtherDamage { get; private set; }
    public string? EventFlag { get; private set; }
    public string? PitEvent { get; private set; }
    public double PitEventMs { get; private set; } = double.NegativeInfinity;
    private readonly Dictionary<int, uint> frames = [];
    private readonly HashSet<string> frameEvents = [];
    private float lastSessionTime;
    public static bool Fresh(double stamp, double now, double limit = 2000) => now >= stamp && now - stamp < limit;
    public bool Connected(double now) => Fresh(LastPacketMs, now);
    public bool Live(double now) => Connected(now) && Fresh(SessionMs, now, 3000) && !Paused && !Spectating;
    public string Flag(double now)
    {
        if (!Live(now)) return "UNKNOWN";
        if (EventFlag != null) return EventFlag;
        if (SafetyCar == 1) return "SC";
        if (SafetyCar == 2) return "VSC";
        if (!Fresh(StatusMs, now)) return "UNKNOWN";
        return FiaFlag switch { 0 or 1 => "GREEN", 2 => "BLUE", 3 => "YELLOW", _ => "UNKNOWN" };
    }

    public bool Ingest(ReadOnlySpan<byte> data, double now)
    {
        if (data.Length < 29 || U16(data, 0) != 2025 || data[5] != 1 || data[27] >= 22 || (data[28] != 255 && data[28] >= 22)) return false;
        var id = data[6];
        var size = id switch { 1 => 753, 2 => 1285, 3 => 45, 5 => 1133, 6 => 1352, 7 => 1239, 10 => 1041, _ => 0 };
        if (size == 0 || data.Length != size) return false;
        var uid = BinaryPrimitives.ReadUInt64LittleEndian(data[7..]);
        var time = F(data, 15);
        if (uid == 0 || !float.IsFinite(time) || time < 0) return false;
        var player = data[27];
        var frame = BinaryPrimitives.ReadUInt32LittleEndian(data[23..]);
        // Validate before mutating state, including malformed data from our own UDP port.
        if (!Validate(data, id, player)) return false;
        if (uid != Session || player != Player) { Reset(); Session = uid; Player = player; }
        if (frames.TryGetValue(id, out var previous) && !SafetyGate.IsNewer(frame, previous))
        {
            if (id != 3 || frame != previous || frameEvents.Contains(Encoding.ASCII.GetString(data.Slice(29, 4)))) return false;
        }
        if (id == 3) { if (!frames.TryGetValue(id, out var priorEventFrame) || frame != priorEventFrame) frameEvents.Clear(); frameEvents.Add(Encoding.ASCII.GetString(data.Slice(29, 4))); }
        if (time + 1 < lastSessionTime) { Reset(); Session = uid; Player = player; }
        lastSessionTime = Math.Max(time, lastSessionTime); frames[id] = frame; LastPacketMs = now;
        var p = data[29..];
        switch (id)
        {
            case 1:
                Weather = p[0]; Paused = p[14] != 0; Spectating = p[15] != 0; SafetyCar = p[124]; SessionMs = now;
                TotalLaps = p[3]; TrackLength = U16(p, 4); TrackId = unchecked((sbyte)p[7]);
                SessionType = p[6]; Formula = p[8]; SessionSecondsLeft = U16(p, 9); PitSpeedLimit = p[13];
                TrackTemperature = unchecked((sbyte)p[1]); AirTemperature = unchecked((sbyte)p[2]);
                var forecasts = new List<F1Forecast>();
                for (var i = 0; i < p[126]; i++) {
                    var sample = p.Slice(127 + i * 8, 8);
                    if (SessionType != 0 && sample[0] == SessionType && sample[1] > 0 && sample[2] <= 5 && sample[7] <= 100)
                        forecasts.Add(new(sample[1], sample[2], sample[7]));
                }
                Forecast = forecasts.OrderBy(f => f.Minutes).ToArray(); ApproximateForecast = p[639] != 0;
                // 127 + 64*8 forecast bytes + accuracy/difficulty + three uint32 IDs.
                PitIdealLap = p[653]; PitLatestLap = p[654]; PitRejoinPosition = p[655];
                break;
            case 2:
                var l = p.Slice(player * 57, 57);
                Position = l[32]; Lap = l[33]; Pit = l[34]; Result = l[45]; Ahead = Behind = LeaderGap = null; AheadCar = -1;
                LapInvalid = l[37] != 0; PenaltySeconds = l[38]; TrackWarnings = l[40]; DriveThrough = l[41]; StopGo = l[42];
                LastLapMs = BinaryPrimitives.ReadUInt32LittleEndian(l);
                PitStops = l[35]; Sector = l[36]; Sector1Ms = (uint)(l[10] * 60000 + U16(l, 8)); Sector2Ms = (uint)(l[13] * 60000 + U16(l, 11));
                CurrentLapTimeMs = BinaryPrimitives.ReadUInt32LittleEndian(l[4..]); LapDistance = F(l, 20); DriverStatus = l[44]; BehindCar = -1;
                // Position-neighbour gaps only for active cars on the same lap.
                if (Result == 2 && Position > 0)
                    for (var i = 0; i < 22; i++)
                    {
                        var other = p.Slice(i * 57, 57);
                        if (i == player || other[45] != 2 || other[33] != Lap) continue;
                        if (other[32] == Position - 1) { Ahead = Gap(l); AheadCar = i; }
                        if (other[32] == Position + 1) { Behind = Gap(other); BehindCar = i; }
                        if (other[32] == 1) LeaderGap = l[19] * 60 + U16(l, 17) / 1000.0;
                    }
                LapMs = now;
                break;
            case 5:
                var setup = p.Slice(player * 50, 50);
                FrontWingSetup = setup[0]; RearWingSetup = setup[1]; Differential = setup[2]; OffThrottleDifferential = setup[3];
                for (var i = 0; i < 4; i++) SetupPressure[i] = F(setup, 29 + i * 4);
                SetupSignature = 2166136261;
                foreach (var value in setup[..46]) SetupSignature = unchecked((SetupSignature ^ value) * 16777619); // Excludes fuel load.
                SetupMs = now;
                break;
            case 6:
                var telemetry = p.Slice(player * 60, 60);
                SpeedKph = U16(telemetry, 0); Throttle = F(telemetry, 2); Steer = F(telemetry, 6); Brake = F(telemetry, 10);
                Gear = unchecked((sbyte)telemetry[15]); Rpm = U16(telemetry, 16); DrsOpen = telemetry[18] != 0;
                for (var i = 0; i < 4; i++) { InnerTemperature[i] = telemetry[34 + i]; SurfaceTemperature[i] = telemetry[30 + i]; BrakeTemperature[i] = U16(telemetry, 22 + i * 2); TyrePressure[i] = F(telemetry, 40 + i * 4); }
                EngineTemperature = U16(telemetry, 38);
                TelemetryMs = now;
                break;
            case 7:
                var s = p.Slice(player * 55, 55);
                BrakeBias = s[3]; FuelKg = F(s, 5); FuelMfdLaps = F(s, 13); FiaFlag = unchecked((sbyte)s[28]); ErsJoules = F(s, 37); StatusMs = now;
                TyreCompound = s[25]; TyreAge = s[27];
                VisualCompound = s[26]; PitLimiter = s[4] != 0; DrsAllowed = s[22] != 0; DrsDistance = U16(s, 23); ErsMode = s[41];
                break;
            case 10:
                var d = p.Slice(player * 46, 46);
                Wear = new float[4];
                for (var i = 0; i < 4; i++) Wear[i] = F(d, i * 4);
                FrontWing = Math.Max(d[28], d[29]);
                ComponentDamage = d.Slice(28, 18).ToArray();
                WheelDamage = d.Slice(16, 12).ToArray();
                OtherDamage = 0;
                for (var i = 16; i < 24; i++) OtherDamage = Math.Max(OtherDamage, d[i]);
                for (var i = 30; i < 34; i++) OtherDamage = Math.Max(OtherDamage, d[i]);
                OtherDamage = Math.Max(OtherDamage, Math.Max(d[36], d[37]));
                if (d[34] != 0 || d[35] != 0 || d[44] != 0 || d[45] != 0) OtherDamage = 100;
                DamageMs = now;
                break;
            case 3:
                switch (Encoding.ASCII.GetString(p[..4]))
                {
                    case "RDFL": EventFlag = "RED"; break;
                    case "CHQF": EventFlag = "CHECKERED"; break;
                    case "LGOT": EventFlag = null; RaceStartMs = now; break;
                    case "SSTA": Reset(); Session = uid; Player = player; LastPacketMs = now; break;
                    case "SEND": Reset(); Session = uid; Player = player; break;
                    case "FLBK": Reset(); Session = uid; Player = player; break;
                    case "TMPT": PitEvent = "Your teammate is in the pits."; PitEventMs = now; break;
                }
                break;
        }
        return true;
    }

    public void Reset()
    {
        Generation++; frames.Clear(); frameEvents.Clear(); lastSessionTime = 0; EventFlag = null; PitEvent = null;
        SessionMs = LapMs = StatusMs = DamageMs = LastPacketMs = PitEventMs = SetupMs = TelemetryMs = double.NegativeInfinity;
        Ahead = Behind = null; AheadCar = BehindCar = -1; FiaFlag = -1; SafetyCar = 0;
        RaceStartMs = double.NegativeInfinity;
        Forecast = []; LeaderGap = null;
    }
    private static bool Validate(ReadOnlySpan<byte> d, byte id, byte player)
    {
        var p = d[29..];
        if (id == 1) return p[0] <= 5 && p[14] <= 1 && p[15] <= 1 && p[18] <= 21 && p[124] <= 3 && p[126] <= 64 && p[655] <= 22;
        if (id == 2)
        {
            for (var i = 0; i < 22; i++) { var l = p.Slice(i * 57, 57); if (l[32] > 22 || l[34] > 2 || l[45] > 7 || U16(l, 14) >= 60000 || U16(l, 17) >= 60000) return false; }
            if (!float.IsFinite(F(p, player * 57 + 20)) || Math.Abs(F(p, player * 57 + 20)) > 100000) return false;
        }
        if (id == 7)
        {
            var s = p.Slice(player * 55, 55);
            return s[3] <= 100 && s[4] <= 1 && s[22] <= 1 && s[41] <= 3 && float.IsFinite(F(s, 5)) && F(s, 5) >= 0 && float.IsFinite(F(s, 13)) &&
                float.IsFinite(F(s, 37)) && F(s, 37) >= 0 && unchecked((sbyte)s[28]) is >= -1 and <= 3;
        }
        if (id == 5) {
            var s = p.Slice(player * 50, 50);
            if (s[2] > 100 || s[3] > 100) return false;
            for (var i = 0; i < 4; i++) if (!float.IsFinite(F(s, 29 + 4 * i)) || F(s, 29 + 4 * i) is < 0 or > 100) return false;
        }
        if (id == 6) {
            var s = p.Slice(player * 60, 60);
            if (unchecked((sbyte)s[15]) is < -1 or > 8 || s[18] > 1) return false;
            if (U16(s, 0) > 500 || !float.IsFinite(F(s, 2)) || F(s, 2) is < 0 or > 1 || !float.IsFinite(F(s, 6)) || F(s, 6) is < -1 or > 1 || !float.IsFinite(F(s, 10)) || F(s, 10) is < 0 or > 1) return false;
            for (var i = 0; i < 4; i++) if (!float.IsFinite(F(s, 40 + i * 4)) || F(s, 40 + i * 4) is < 0 or > 100) return false;
        }
        if (id == 10)
        {
            var s = p.Slice(player * 46, 46);
            for (var i = 0; i < 4; i++) if (!float.IsFinite(F(s, i * 4)) || F(s, i * 4) is < 0 or > 100) return false;
            for (var i = 16; i < 46; i++) if (s[i] > 100) return false;
        }
        if (id == 3) return Encoding.ASCII.GetString(p[..4]) is "RDFL" or "CHQF" or "LGOT" or "SSTA" or "SEND" or "FLBK" or "TMPT";
        return true;
    }
    private static double Gap(ReadOnlySpan<byte> d) => d[16] * 60 + U16(d, 14) / 1000.0;
    private static ushort U16(ReadOnlySpan<byte> d, int o) => BinaryPrimitives.ReadUInt16LittleEndian(d[o..]);
    private static float F(ReadOnlySpan<byte> d, int o) => BinaryPrimitives.ReadSingleLittleEndian(d[o..]);
}
