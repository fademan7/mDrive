using PhoneWheel.Core;

if (args.Length != 2 || args[0] is not ("write" or "verify-kotlin"))
{
    Console.Error.WriteLine("Usage: PhoneWheel.Interop write|verify-kotlin <directory>");
    return 1;
}
var directory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(directory);
var key = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
const ulong session = 0x0102030405060708;
var header = new PacketHeader(PacketKind.Control, session, 0x01020304, 0x0102030405060708, 0x0A0B0C0D);
var expected = new ControlFrame(header, new Controls(-0.5f, 1, 0.5f, 0x1000), Pwr1Codec.Ready, 1);

if (args[0] == "write")
{
    File.WriteAllBytes(Path.Combine(directory, "csharp-control.bin"), Pwr1Codec.EncodeControl(expected, key));
    Console.WriteLine("Wrote authenticated C# control datagram.");
    return 0;
}

var packet = File.ReadAllBytes(Path.Combine(directory, "kotlin-control.bin"));
var decoded = Pwr1Codec.DecodeControl(packet, key, session);
if (decoded != expected) throw new InvalidDataException($"Kotlin datagram mismatch: {decoded}");
Console.WriteLine("PASS C# decoded the authenticated Kotlin control datagram.");
return 0;
