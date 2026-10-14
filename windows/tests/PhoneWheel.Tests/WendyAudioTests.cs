using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using PhoneWheel.Core;
using PhoneWheel.Host;

internal static class WendyAudioTests
{
    private sealed class Voice : IWendyPcVoice {
        public string State=>Speaking?"SPEAKING":Listen?"LISTENING":"IDLE";
        public string Health=>"Fake PC audio";
        public bool Speaking {get;set;}
        public bool HearingSpeech=>false;
        public bool Listen,Muted;public int Volume;
        public WendySpeechInput? Input;public string? Spoken;
        public void Configure(bool listen,bool muted,int volume){Listen=listen;Muted=muted;Volume=volume;}
        public WendySpeechInput? TakeInput()=>Interlocked.Exchange(ref Input,null);
        public void Speak(string text){Spoken=text;Speaking=true;}
        public void Cancel(){Input=null;Spoken=null;Speaking=false;}
        public void Dispose(){}
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Routes()=>Run().GetAwaiter().GetResult();
    private static async Task Run() {
        var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        using var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));var telemetry=((IPEndPoint)udp.Client.LocalEndPoint!).Port;udp.Close();
        using var voice=new Voice();using var service=new WendyService(IPAddress.Loopback,new byte[32],port,telemetry,voice);
        Check(service.Audio.Input==WendyAudioDevice.PC&&service.Audio.Output==WendyAudioDevice.PC,"default PC/PC");
        service.SetEnabled(true);
        await Task.Delay(250);voice.Input=new("Radio check",.99);
        for(var i=0;i<30&&voice.Spoken==null;i++)await Task.Delay(50);
        Check(voice.Spoken?.Contains("Loud and clear")==true,"PC/PC works without Android client");voice.Speaking=false;voice.Spoken=null;
        using var client=new TcpClient();await client.ConnectAsync(IPAddress.Loopback,port);var stream=client.GetStream();
        using var timeout=new CancellationTokenSource(12000);var nonce=new byte[32];await stream.ReadExactlyAsync(nonce,timeout.Token);ulong sequence=0;
        async Task<JsonDocument> Poll(string text="",long? revision=null,string state="IDLE",bool radio=false) {
            var json=JsonSerializer.SerializeToUtf8Bytes(new{state,text,confidence=.99,audioRevision=revision??service.Audio.Revision,gameRadio=radio});
            var wire=WendyWire.Encode(json,new byte[32],nonce,0,++sequence);var size=new byte[4];BinaryPrimitives.WriteInt32BigEndian(size,wire.Length);
            await stream.WriteAsync(size,timeout.Token);await stream.WriteAsync(wire,timeout.Token);await stream.ReadExactlyAsync(size,timeout.Token);
            var result=new byte[BinaryPrimitives.ReadInt32BigEndian(size)];await stream.ReadExactlyAsync(result,timeout.Token);
            return JsonDocument.Parse(WendyWire.Decode(result,new byte[32],nonce,1,sequence));
        }
        service.ConfigureAudio(WendyAudioDevice.Android,WendyAudioDevice.PC,42);await Task.Delay(250);
        using(var reply=await Poll("Radio check",state:"PROCESSING")) {
            Check(reply.RootElement.GetProperty("audioOutput").GetString()=="PC","PC output advertised");
            Check(reply.RootElement.GetProperty("volume").GetInt32()==42,"volume advertised");
        }
        Check(voice.Spoken?.Contains("Loud and clear")==true,"Android/PC speech");voice.Spoken=null;voice.Speaking=false;
        var old=service.Audio.Revision;service.ConfigureAudio(WendyAudioDevice.PC,WendyAudioDevice.Android,55);await Task.Delay(250);
        using(var ignored=await Poll("Radio check",old,"PROCESSING"))Check(ignored.RootElement.GetProperty("intent").GetString()=="","stale Android query ignored");
        using(var ready=await Poll()){}
        voice.Input=new("Radio check",.99);await Task.Delay(350);
        using(var reply=await Poll())Check(reply.RootElement.GetProperty("text").GetString()!.Contains("Loud and clear"),"PC/Android delivery");
        Check(voice.Spoken==null,"PC does not duplicate Android output");
        using(var once=await Poll())Check(once.RootElement.GetProperty("text").GetString()=="","Android output delivered once");
        service.ConfigureAudio(WendyAudioDevice.Android,WendyAudioDevice.Android,70);await Task.Delay(250);
        using(var reply=await Poll("Radio check",state:"PROCESSING"))Check(reply.RootElement.GetProperty("text").GetString()!.Contains("Loud and clear"),"Android/Android");
        Check(voice.Spoken==null,"no duplicate PC output");
        service.ControllerRadio(true);await Task.Delay(250);Check(voice.Muted,"LB pauses PC voice");
        using(var reply=await Poll("Radio check",state:"PROCESSING"))Check(reply.RootElement.GetProperty("text").GetString()=="","LB rejects Android query too");
        service.ControllerRadio(false);await Task.Delay(150);Check(voice.Muted,"LB release grace");
        service.SetEnabled(false);Check(voice.Muted,"OFF mutes PC voice");
    }
}
