using System.Net;
using System.Net.Sockets;

namespace PhoneWheel.Host;

internal interface IReceiverTransport : IDisposable
{
    Task<UdpReceiveResult> ReceiveAsync(CancellationToken token);
    UdpReceiveResult Receive(CancellationToken token) => ReceiveAsync(token).GetAwaiter().GetResult();
    Task SendAsync(byte[] packet, IPEndPoint peer, CancellationToken token);
    void Send(byte[] packet, IPEndPoint peer, CancellationToken token) => SendAsync(packet, peer, token).GetAwaiter().GetResult();
}

internal sealed class WifiTransport(IPAddress address, int port) : IReceiverTransport
{
    private readonly UdpClient socket = new(new IPEndPoint(address, port));
    private bool configured;
    public UdpReceiveResult Receive(CancellationToken token) {
        // Complete on the dedicated receiver thread, not a shared thread-pool
        // continuation. Timeout only polls cancellation; it is NOT a safety limit.
        if (!configured) { socket.Client.ReceiveTimeout = 50; configured = true; }
        while (true) {
            token.ThrowIfCancellationRequested();
            try {
                IPEndPoint peer = new(IPAddress.Any, 0);
                return new UdpReceiveResult(socket.Receive(ref peer), peer);
            } catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut) { }
        }
    }
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken token) => await socket.ReceiveAsync(token);
    public void Send(byte[] packet, IPEndPoint peer, CancellationToken token) { token.ThrowIfCancellationRequested(); socket.Send(packet, peer); }
    public async Task SendAsync(byte[] packet, IPEndPoint peer, CancellationToken token) => await socket.SendAsync(packet, peer, token);
    public void Dispose() => socket.Dispose();
}
