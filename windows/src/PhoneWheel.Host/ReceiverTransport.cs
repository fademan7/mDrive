using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace PhoneWheel.Host;

internal interface IReceiverTransport : IDisposable
{
    Task<UdpReceiveResult> ReceiveAsync(CancellationToken token);
    Task SendAsync(byte[] packet, IPEndPoint peer, CancellationToken token);
}

internal sealed class WifiTransport(IPAddress address, int port) : IReceiverTransport
{
    private readonly UdpClient socket = new(new IPEndPoint(address, port));
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken token) => await socket.ReceiveAsync(token);
    public async Task SendAsync(byte[] packet, IPEndPoint peer, CancellationToken token) => await socket.SendAsync(packet, peer, token);
    public void Dispose() => socket.Dispose();
}

// ADB reverse carries this loopback-only TCP stream over the authorized USB cable.
// Each PWR1 packet has a two-byte big-endian length prefix; HMAC/ACK freshness
// and the independent safety watchdog remain identical to the UDP transport.
internal sealed class UsbTransport : IReceiverTransport
{
    private readonly TcpListener listener;
    private TcpClient? client;
    public UsbTransport(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start(1);
    }

    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken token)
    {
        try
        {
            if (client is null) {
                var accepted = await listener.AcceptTcpClientAsync(token);
                accepted.NoDelay = true;
                client = accepted;
            }
            var active = client;
            var stream = active.GetStream();
            var prefix = new byte[2];
            await stream.ReadExactlyAsync(prefix, token);
            var length = BinaryPrimitives.ReadUInt16BigEndian(prefix);
            if (length is < 48 or > 76) throw new IOException("Invalid USB frame size.");
            var packet = new byte[length];
            await stream.ReadExactlyAsync(packet, token);
            return new UdpReceiveResult(packet, (IPEndPoint)active.Client.RemoteEndPoint!);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException) {
            client?.Dispose(); client = null;
            throw new SocketException((int)SocketError.ConnectionReset);
        }
    }

    public async Task SendAsync(byte[] packet, IPEndPoint peer, CancellationToken token)
    {
        var active = client;
        if (active is null) return;
        try {
            if (!peer.Equals(active.Client.RemoteEndPoint)) return;
            var frame = new byte[packet.Length + 2];
            BinaryPrimitives.WriteUInt16BigEndian(frame, checked((ushort)packet.Length));
            packet.CopyTo(frame, 2);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(100);
            await active.GetStream().WriteAsync(frame, deadline.Token);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException ||
                                    ex is OperationCanceledException && !token.IsCancellationRequested) {
            active.Dispose(); // Unblock receive; the watchdog releases controls.
        }
    }
    public void Dispose() { listener.Stop(); client?.Dispose(); }
}
