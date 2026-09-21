using System.Net;
using System.Net.Sockets;

namespace PhoneWheel.Core;

public static class PairingPayload
{
    public static string Create(IPAddress host, int port, ulong session, byte[] key)
    {
        if (host.AddressFamily != AddressFamily.InterNetwork || port is < 1 or > 65535 || session == 0 || key.Length != 32)
            throw new ArgumentException("Invalid pairing settings.");
        var encodedKey = Convert.ToBase64String(key).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"phonewheel://pair?v=1&host={host}&port={port}&session={session:X16}&key={encodedKey}";
    }
}
