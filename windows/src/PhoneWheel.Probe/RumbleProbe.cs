using System.Runtime.InteropServices;
using PhoneWheel.Output.ViGEm;

internal static class RumbleProbe {
    [StructLayout(LayoutKind.Sequential)] private struct Vibration { public ushort Large, Small; }
    [DllImport("xinput1_4.dll", ExactSpelling=true)] private static extern uint XInputSetState(uint index, ref Vibration vibration);
    public static int Run(ViGEmGamepadOutput pad) {
        // Only our newly created virtual pad. Never enumerate/write other pads.
        var index=-1;
        for(var i=0;i<100 && index<0;i++) {try{index=pad.UserIndex;}catch{Thread.Sleep(20);}}
        if(index is <0 or >3) throw new InvalidOperationException("Own XInput slot unavailable.");
        Console.WriteLine($"Own virtual Xbox slot {index}; neutral controls throughout.");
        void Set(ushort large,ushort small) {var v=new Vibration{Large=large,Small=small};var rc=XInputSetState((uint)index,ref v);if(rc!=0)throw new IOException($"XInputSetState failed: {rc}");}
        try {
            Set(65535,0);Thread.Sleep(150);
            var first=pad.ReadRumbleLevel(true);var callbacks=pad.FeedbackCount;
            Thread.Sleep(650);var held=pad.ReadRumbleLevel(true);
            Console.WriteLine($"Single sustained XInput request: initial={first}, after800ms={held}, callbacks={callbacks}->{pad.FeedbackCount}");
            Set(0,0);Thread.Sleep(100);var stopped=pad.ReadRumbleLevel(true);
            Set(0,65535);Thread.Sleep(100);var small=pad.ReadRumbleLevel(true);
            Set(0,0);Thread.Sleep(100);pad.ReadRumbleLevel(true);
            Set(65535,65535);Thread.Sleep(15);Set(0,0);Thread.Sleep(15);var pulse=pad.ReadRumbleLevel(true);
            Thread.Sleep(100);var expired=pad.ReadRumbleLevel(true);
            Console.WriteLine($"Stop={stopped}, small motor={small}, short pulse={pulse}, after pulse={expired}");
            return first==3 && held==3 && stopped==0 && small==3 && pulse==3 && expired==0 ? 0 : 1;
        } finally {Set(0,0);pad.Neutralize();}
    }
}
