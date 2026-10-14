using System.Diagnostics;
using System.Text.Json;
using NAudio.Wave;
using PhoneWheel.Core;
using Vosk;

namespace PhoneWheel.Host;

// This entry point runs in a separate process: native model/microphone failures
// cannot take down the receiver. Audio is bounded RAM only, never an IPC payload.
internal static class VoskSpeechWorker
{
    private sealed record Mode(bool Listen,long Epoch);
    public static string ModelPath => Path.Combine(AppContext.BaseDirectory,"speech","vosk-model-small-en-us-0.15");
    public static int Run(string path) {
        var mode=new Mode(false,0);var closed=0;var outputLock=new object();
        void Send(object message) {lock(outputLock){Console.WriteLine(JsonSerializer.Serialize(message));Console.Out.Flush();}}
        _=Task.Run(()=> {
            try {string? line;while((line=Console.ReadLine())!=null) {
                if(line.Length>256)break;
                var next=JsonSerializer.Deserialize<Mode>(line);
                if(next==null)break;Volatile.Write(ref mode,next);
            }}catch(Exception){}finally{Volatile.Write(ref closed,1);}
        });
        try {
            Vosk.Vosk.SetLogLevel(-1);
            Send(new{kind="status",epoch=0,state="LOADING",health="Loading offline English model…"});
            using var model=new Model(path);
            using var recognizer=new VoskRecognizer(model,16000);recognizer.SetWords(true);
            var buffer=new WendySpeechBuffer();WaveInEvent? microphone=null;
            var applied=new Mode(false,-1);long heartbeat=0;string? captureError=null;var clock=Stopwatch.StartNew();var discardUtterance=false;
            void Stop(){if(microphone!=null){microphone.StopRecording();microphone.Dispose();microphone=null;}buffer.Clear();recognizer.Reset();discardUtterance=false;}
            try {
                while(Volatile.Read(ref closed)==0) {
                    var desired=Volatile.Read(ref mode);
                    if(desired!=applied) {
                        Stop();applied=desired;
                        if(desired.Listen) {
                            var ticket=desired.Epoch;
                            microphone=new WaveInEvent{DeviceNumber=-1,WaveFormat=new WaveFormat(16000,16,1),BufferMilliseconds=50,NumberOfBuffers=3};
                            microphone.DataAvailable+=(_,e)=> {
                                if(Volatile.Read(ref mode)!=desired)return;
                                buffer.Push(e.Buffer.AsSpan(0,e.BytesRecorded).ToArray(),ticket,clock.Elapsed.TotalMilliseconds);
                            };
                            microphone.RecordingStopped+=(_,e)=>{if(e.Exception!=null)Volatile.Write(ref captureError,"Microphone unavailable. Check Windows default input and permissions.");};
                            microphone.StartRecording();
                        }
                        Send(new{kind="status",epoch=desired.Epoch,state=desired.Listen?"LISTENING":"IDLE",health="PC English microphone · Vosk offline"});
                    }
                    if(Interlocked.Exchange(ref captureError,null) is {} error)throw new IOException(error);
                    if(desired.Listen) {
                        var frame=buffer.Take(desired.Epoch,clock.Elapsed.TotalMilliseconds,out var reset);
                        if(reset){recognizer.Reset();discardUtterance=true;Send(new{kind="partial",epoch=desired.Epoch,hearing=false});}
                        if(frame!=null) {
                            if(recognizer.AcceptWaveform(frame.Pcm,frame.Pcm.Length)) {
                                var result=WendySpeechBuffer.ParseResult(recognizer.Result());
                                if(!discardUtterance && Volatile.Read(ref mode)==desired && result!=null)Send(new{kind="result",epoch=desired.Epoch,text=result.Text,confidence=result.Confidence});
                                discardUtterance=false;
                                Send(new{kind="partial",epoch=desired.Epoch,hearing=false});
                            } else {
                                using var partial=JsonDocument.Parse(recognizer.PartialResult());
                                Send(new{kind="partial",epoch=desired.Epoch,hearing=partial.RootElement.GetProperty("partial").GetString()?.Length>0});
                            }
                        }
                    }
                    if(clock.ElapsedMilliseconds-heartbeat>=1000) {heartbeat=clock.ElapsedMilliseconds;Send(new{kind="heartbeat",epoch=desired.Epoch});}
                    Thread.Sleep(10);
                }
            } finally {Stop();}
            return 0;
        } catch(Exception) {Send(new{kind="status",epoch=Volatile.Read(ref mode).Epoch,state="ERROR",health="Offline microphone failed. Check the model, input device and microphone permission."});return 2;}
    }
}
