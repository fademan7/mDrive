using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text.Json;
using PhoneWheel.Core;
using PhoneWheel.Host;

internal static class WendyServiceTests
{
    public static void Conversation() => ConversationAsync().GetAwaiter().GetResult();
    private static async Task ConversationAsync() {
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        using var udpProbe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); var telemetryPort = ((IPEndPoint)udpProbe.Client.LocalEndPoint!).Port; udpProbe.Close();
        var key = new byte[32];
        using var service = new WendyService(IPAddress.Loopback, key, port, telemetryPort);
        service.SetEnabled(true);
        using var deadline = new CancellationTokenSource(10000);
        using var peer = new TcpClient();
        while (true) {
            try { await peer.ConnectAsync(IPAddress.Loopback, port, deadline.Token); break; }
            catch (SocketException) { await Task.Delay(30, deadline.Token); }
        }
        var stream = peer.GetStream(); var nonce = new byte[32]; await stream.ReadExactlyAsync(nonce, deadline.Token);
        ulong sequence = 0;
        foreach (var (text, intent) in new[] {
            ("Tell me the time gap", "GET_GAP_AHEAD"),
            ("Could you tell me the gap behind please", "GET_GAP_BEHIND"),
            ("Tell me my tyre temperatures", "GET_TYRE_TEMPERATURE"),
            ("What about the rear left", "FOLLOW_UP_WHEEL"),
            ("Hello there", "SMALL_TALK"), ("Radio check", "RADIO_CHECK") }) {
            var json = JsonSerializer.SerializeToUtf8Bytes(new { state = "PROCESSING", text, confidence = .99 });
            var wire = WendyWire.Encode(json, key, nonce, 0, ++sequence); var prefix = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(prefix, wire.Length);
            await stream.WriteAsync(prefix, deadline.Token); await stream.WriteAsync(wire, deadline.Token);
            await stream.ReadExactlyAsync(prefix, deadline.Token);
            var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
            if (length is < 40 or > 2088) throw new Exception("invalid reply length");
            var packet = new byte[length]; await stream.ReadExactlyAsync(packet, deadline.Token);
            using var reply = JsonDocument.Parse(WendyWire.Decode(packet, key, nonce, 1, sequence));
            if (reply.RootElement.GetProperty("intent").GetString() != intent || reply.RootElement.GetProperty("source").GetString() != "Race rules")
                throw new Exception("authenticated conversation: " + text);
            var answer = reply.RootElement.GetProperty("text").GetString()!;
            if (string.IsNullOrWhiteSpace(answer) || answer.Length > 400) throw new Exception("reply bound");
            if (intent == "RADIO_CHECK" && !answer.Contains("Loud and clear")) throw new Exception("radio round trip");
        }
    }
    public static void Run() => CheckAsync().GetAwaiter().GetResult();
    private static async Task CheckAsync()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        using var udpProbe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); var telemetryPort = ((IPEndPoint)udpProbe.Client.LocalEndPoint!).Port; udpProbe.Close();
        using var service = new WendyService(IPAddress.Loopback, new byte[32], port, telemetryPort);
        if (service.Enabled) throw new Exception("default ON");
        for (var iteration = 0; iteration < 3; iteration++)
        {
            service.SetEnabled(true);
            using var peer = new TcpClient(); using var deadline = new CancellationTokenSource(3000);
            while (true)
            {
                try { await peer.ConnectAsync(IPAddress.Loopback, port, deadline.Token); break; }
                catch (SocketException) { await Task.Delay(30, deadline.Token); }
            }
            var nonce = new byte[32]; await peer.GetStream().ReadExactlyAsync(nonce, deadline.Token);
            service.SetEnabled(false); // Interrupt an incomplete unauthenticated request.
            var read = new byte[1];
            try { if (await peer.GetStream().ReadAsync(read, deadline.Token) != 0) throw new Exception("OFF peer remained open"); }
            catch (IOException) { }
            await Task.Delay(60, deadline.Token);
            if (service.Enabled || !service.Display.Contains("OFF")) throw new Exception("OFF state");
            var tcpCheck = new TcpListener(IPAddress.Loopback, port); tcpCheck.Start(); tcpCheck.Stop();
            using var udpCheck = new UdpClient(new IPEndPoint(IPAddress.Loopback, telemetryPort));
        }
        service.SetEnabled(true); service.SetEnabled(false); service.SetEnabled(true); service.SetEnabled(false);
    }
}
