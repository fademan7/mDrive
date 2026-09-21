using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using PhoneWheel.Core;
using PhoneWheel.Output.ViGEm;
using PhoneWheel.Host;

Console.OutputEncoding = Encoding.UTF8;
var interactive = args.Length == 0;
int exitCode;
try { exitCode = await RunAsync(args, interactive); }
catch (Exception ex)
{
    Console.Error.WriteLine($"Unable to start receiver: {ex.Message}");
    Console.Error.WriteLine("Close any running receiver and try again.");
    exitCode = 1;
}
if (interactive && exitCode != 0 && !Console.IsInputRedirected)
{
    Console.WriteLine("Press Enter to close after reviewing the error.");
    Console.ReadLine();
}
return exitCode;

static async Task<int> RunAsync(string[] args, bool interactive)
{
UsbConnector? usbConnector = null;
if (!args.Contains("--usb-test") && (interactive || args.Contains("--usb"))) usbConnector = await UsbConnector.FindAsync();
var usb = args.Contains("--usb") || interactive && usbConnector != null;
if (usb && usbConnector is null && !args.Contains("--usb-test")) {
    Console.Error.WriteLine("No USB phone. Check cable and USB debugging authorization."); return 3;
}
var port = int.TryParse(GetOption(args, "--port"), out var p) ? p : usb ? 26761 : 26760;
var backend = GetOption(args, "--backend") ?? "vigem";
var monitor = interactive || args.Contains("--monitor") || args.Contains("--qr");
var session = ulong.TryParse(GetOption(args, "--session-hex"), System.Globalization.NumberStyles.HexNumber, null, out var configuredSession)
    ? configuredSession : RandomNonZeroUInt64();
var key = GetOption(args, "--key-base64") is string configuredKey ? Convert.FromBase64String(configuredKey) : RandomNumberGenerator.GetBytes(32);
if (session == 0 || key.Length != 32) { Console.Error.WriteLine("Session must be nonzero and key must decode to 32 bytes."); return 1; }
var runSeconds = double.TryParse(GetOption(args, "--run-seconds"), out var configuredSeconds) ? configuredSeconds : 0;
var bindAddress = usb ? IPAddress.Loopback : IPAddress.TryParse(GetOption(args, "--bind"), out var parsedAddress) ? parsedAddress : FindLocalAddress();
// Bind before creating the virtual controller so a second instance fails cleanly.
using IReceiverTransport socket = usb ? new UsbTransport(port) : new WifiTransport(bindAddress, port);
IGamepadOutput output;
try
{
    output = backend switch
    {
        "null" => new NullGamepadOutput(),
        "csv" => new CsvGamepadOutput(GetOption(args, "--csv") ?? "host-output.csv"),
        "vigem" => new ViGEmGamepadOutput(),
        _ => throw new ArgumentException("Backend must be null, csv, or vigem.")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"OUTPUT ERROR: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine("Virtual gamepad driver (ViGEmBus) required. See README.md.");
    return 2;
}

if (!monitor)
    Console.WriteLine($"protocol=PWR1 pcIp={bindAddress} pcPort={port} sessionIdHex={session:X16} keyBase64={Convert.ToBase64String(key)}");
Console.WriteLine("PhoneWheel receiver | Keep this window open and connect the phone app.");
Console.WriteLine($"Output: {backend} | Transport: {(usb ? "USB cable" : "Wi-Fi UDP")} | Port: {port}");
if (!usb) {
Console.WriteLine($"PC IP       : {bindAddress}");
Console.WriteLine($"Session hex : {session:X16}");
Console.WriteLine($"Key Base64  : {Convert.ToBase64String(key)}");
Console.WriteLine("Enter these values in the phone connection settings. Never share or log the key.");
Console.WriteLine("Use the same Wi-Fi or USB tethering. Allow only private networks in Windows Firewall.");
} else Console.WriteLine("USB auto pairing · no QR/Wi-Fi needed. Unlock the phone and authorize USB debugging.");
Console.WriteLine("Hold comfortably and release controls to center/start. After recovery, release and center. Exit: Q or Ctrl+C.");
Console.WriteLine(usb ? "Cable loss reconnects automatically. Restart receiver if the app was closed." : "If the app was closed, restart receiver and scan its new QR in Options.");
if (!usb && bindAddress.Equals(IPAddress.Loopback)) Console.WriteLine("No LAN address. Connect Wi-Fi and restart receiver.");
using var stop = new CancellationTokenSource();
using var wendy = new WendyService(bindAddress, key,
    int.TryParse(GetOption(args, "--engineer-port"), out var ep) ? ep : 26762,
    int.TryParse(GetOption(args, "--telemetry-port"), out var tp) ? tp : 20777);
if (args.Contains("--engineer")) wendy.SetEnabled(true);
using var pairingWindow = interactive || args.Contains("--qr")
    ? new PairingWindow(PairingPayload.Create(bindAddress, port, session, key), $"{bindAddress}:{port}", () => { if (!stop.IsCancellationRequested) stop.Cancel(); }, usb, wendy) : null;
if (runSeconds > 0) stop.CancelAfter(TimeSpan.FromSeconds(runSeconds));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var gate = new SafetyGate(session, key);
await using var worker = new GamepadWorker(output);
worker.OutputError += ex => { gate.OutputFailed(); Console.Error.WriteLine($"OUTPUT ERROR: {ex.Message}"); };
IPEndPoint? phone = null;
long receivedPackets = 0;
long rejectedPackets = 0;
uint outboundSequence = 0;
var started = Stopwatch.GetTimestamp();
double NowMs() => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
ulong NowUs() => checked((ulong)(NowMs() * 1000));

var usbSetup = Task.Run(async () => {
    if (usbConnector is null || !usb || args.Contains("--usb-test")) return;
    try {
        var payload = PairingPayload.Create(IPAddress.Loopback, port, session, key).Replace("phonewheel://pair?", "phonewheel://usb?");
        await usbConnector.ConnectAsync(port, payload);
        Console.WriteLine("USB pairing delivered · hold comfortably and release controls to prepare.");
        var unavailable = false;
        while (!stop.IsCancellationRequested) {
            await Task.Delay(2000, stop.Token);
            try {
                await usbConnector.EnsureReverseAsync(port);
                // Optional side-channel mapping failure must not stop driving.
                if (wendy.Enabled) try { await usbConnector.EnsureReverseAsync(wendy.Port); } catch (IOException) { }
                if (unavailable) Console.WriteLine("USB restored · release controls and center to prepare.");
                unavailable = false;
            } catch (IOException) {
                if (!unavailable) Console.WriteLine("USB device not responding · waiting for the same phone");
                unavailable = true;
            }
        }
    } catch { stop.Cancel(); throw; }
});

var receive = Task.Run(async () =>
{
    while (!stop.IsCancellationRequested)
    {
        UdpReceiveResult result;
        try { result = await socket.ReceiveAsync(stop.Token); }
        catch (OperationCanceledException) { break; }
        catch (SocketException) when (!stop.IsCancellationRequested)
        {
            // Windows can surface an ICMP port-unreachable from a departed
            // phone as WSAECONNRESET. It is a disconnect, not a host crash;
            // the independent watchdog releases the controls.
            await Task.Delay(5, stop.Token);
            continue;
        }
        try
        {
            Interlocked.Increment(ref receivedPackets);
            var kind = Pwr1Codec.PeekAuthenticatedHeader(result.Buffer, key, session).Kind;
            if (kind == PacketKind.Hello)
            {
                if ((phone is null || usb || !gate.GetDiagnostics(NowMs()).HasRecentInput) && gate.AcceptInitialHello(result.Buffer, reconnect: phone != null))
                {
                    phone = result.RemoteEndPoint;
                    Console.WriteLine(usb ? "USB authenticated · waiting for neutral" : "Wi-Fi authenticated · waiting for neutral");
                }
                continue;
            }
            if (kind is not (PacketKind.Control or PacketKind.ControlLook) || phone is null || !result.RemoteEndPoint.Equals(phone)) continue;
            gate.Ingest(result.Buffer, NowMs());
        }
        catch (ProtocolException) { Interlocked.Increment(ref rejectedPackets); }
    }
});

var watchdog = CriticalLoop.Start("mDrive safety watchdog", 4, stop.Token, () => {
    if (worker.Faulted || worker.LastWriteAgeMs is > 150) gate.OutputFailed();
    worker.Publish(gate.Tick(NowMs()));
});

var rumbleActive = false;
var lastHapticMs = double.NegativeInfinity;
    // More frequent ACK challenges tolerate isolated lost Wi-Fi replies without
    // increasing the 100 ms freshness window or the 150 ms safety timeout.
var status = CriticalLoop.Start("mDrive ACK and haptics", 20, stop.Token, () =>
    {
        if (phone is null) return;
        var seq = unchecked(++outboundSequence);
        var header = new PacketHeader(PacketKind.Status, session, seq, NowUs(), gate.LastAcceptedSequence);
        var health = gate.GetDiagnostics(NowMs());
        var outputUnavailable = worker.Faulted || worker.LastWriteAgeMs is > 150;
        var frame = new StatusFrame(header, health.Armed && !outputUnavailable ? HostState.Active : HostState.Released, outputUnavailable ? ReleaseReason.OutputError : health.Reason);
        gate.NoteServerSend(seq, NowMs());
        try { socket.SendAsync(Pwr1Codec.EncodeStatus(frame, key), phone, stop.Token).GetAwaiter().GetResult(); }
        catch (SocketException) when (!usb && !stop.IsCancellationRequested) { return; }
        if (NowMs() - lastHapticMs < 50) return;
        lastHapticMs = NowMs();
        var level = output is ViGEmGamepadOutput pad ? pad.ReadRumbleLevel(gate.Armed) : (byte)0;
        if (level != 0 || rumbleActive)
        {
            seq = unchecked(++outboundSequence);
            var hapticHeader = new PacketHeader(PacketKind.Haptic, session, seq, NowUs(), gate.LastAcceptedSequence);
            var haptic = new HapticFrame(hapticHeader, level == 0 ? HapticEvent.Stop : HapticEvent.GamepadRumble,
                level, level == 0 ? (ushort)0 : (ushort)100);
            gate.NoteServerSend(seq, NowMs());
            try { socket.SendAsync(Pwr1Codec.EncodeHaptic(haptic, key), phone, stop.Token).GetAwaiter().GetResult(); }
            catch (SocketException) when (!usb && !stop.IsCancellationRequested) { }
        }
        rumbleActive = level != 0;
});

// Console rendering is separate from networking and the 4 ms watchdog.
var display = Task.Run(async () =>
{
    if (!monitor) return;
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
    while (await timer.WaitForNextTickAsync(stop.Token))
    {
        if (!Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
        {
            stop.Cancel(); break;
        }
        var diagnostic = gate.GetDiagnostics(NowMs());
        var state = phone is null ? "Waiting for connection" : !diagnostic.HasRecentInput ? "Input stream lost · controls released" : diagnostic.Armed ? "Driving active" : $"Output blocked · {ExplainRelease(diagnostic.Reason)}";
        if (worker.Faulted) state = $"Gamepad output fault · retrying neutral ({worker.LastError})";
        else if (worker.LastWriteAgeMs is > 150) state = "Gamepad driver not responding · controls released";
        state = (usb ? "USB · " : "Wi-Fi · ") + state;
        string Values(Controls c) => $"Steering {c.Steer:P0}  Brake {c.Brake:P0}  Throttle {c.Throttle:P0}";
        var received = diagnostic.HasRecentInput ? Values(diagnostic.Received) : "No data (check connection / PC mode)";
        var line = $"{state}\nPhone input: {received}\nGame output: {Values(worker.Faulted ? Controls.Neutral : diagnostic.Output)}";
        line += $"\nStops {diagnostic.ReleaseCount} / {diagnostic.LastRelease} · Pad errors {worker.FailureCount} · ACK rejects {diagnostic.RejectedChallenges}";
        if (phone is null) line += $"\nPackets received {Interlocked.Read(ref receivedPackets)} · Authentication rejected {Interlocked.Read(ref rejectedPackets)} (check new QR / connection mode)";
        pairingWindow?.Update(line);
        if (Console.IsOutputRedirected) Console.WriteLine(line);
        else Console.Write("\r" + line.Replace("\n", " | ") + "    ");
    }
}, stop.Token);

// Any failing worker cancels its peers, allowing the controller to be disposed.
async Task Supervise(Task task)
{
    try { await task; }
    catch { stop.Cancel(); throw; }
}
try { await Task.WhenAll(new[] { receive, watchdog, status, display, usbSetup }.Select(Supervise)); }
catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
catch (SocketException) when (stop.IsCancellationRequested) { } // UDP ICMP may race an intentional shutdown.
finally { worker.Publish(Controls.Neutral); Console.WriteLine("\nReceiver stopped · controls released"); }
return 0;
}

static IPAddress FindLocalAddress()
{
    var adapters = NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork));
    foreach (var adapter in adapters)
    foreach (var address in adapter.GetIPProperties().UnicastAddresses)
    {
        var ip = address.Address;
        if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("169.254.")) return ip;
    }
    return IPAddress.Loopback;
}

static string ExplainRelease(ReleaseReason reason) => reason switch {
    ReleaseReason.User => "release controls and center to start",
    ReleaseReason.Sensor => "check sensor / centering",
    ReleaseReason.Calibration => "waiting for auto center / neutral",
    ReleaseReason.Inactive => "keep the phone app visible",
    ReleaseReason.Timeout => "connection lost · check receiver",
    ReleaseReason.OutputError => "virtual gamepad error",
    _ => reason.ToString()
};

static string? GetOption(string[] values, string name)
{
    var index = Array.IndexOf(values, name);
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}
static ulong RandomNonZeroUInt64()
{
    ulong value;
    do { value = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)); } while (value == 0);
    return value;
}
