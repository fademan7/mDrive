using System.Net;
using System.Net.Sockets;
using PhoneWheel.Host;

internal static class WendyServiceTests
{
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
