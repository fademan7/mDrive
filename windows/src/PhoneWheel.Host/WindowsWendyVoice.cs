using System.Diagnostics;
using System.Speech.Synthesis;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

// SAPI calls live on an optional, low-priority voice thread, never controller/UI workers.
internal sealed class WindowsWendyVoice : IWendyPcVoice
{
    private sealed record Mode(bool Listen, bool Muted, int Volume);
    private sealed record Utterance(string Text,int Epoch);
    private Mode mode = new(false,true,100);
    private WendySpeechInput? input;
    private Utterance? speech;
    private string state="IDLE", health="PC voice idle.";
    private string requestedVoice="";
    public void SelectVoice(string name)=>Volatile.Write(ref requestedVoice,name??"");
    private int speaking, hearing, cancelEpoch,releaseInput;
    private volatile bool closed;
    private readonly Thread thread;
    public string State => Volatile.Read(ref state);
    public string Health => Volatile.Read(ref health);
    public bool Speaking => Volatile.Read(ref speaking)!=0 || Volatile.Read(ref speech)!=null;
    public bool HearingSpeech => Volatile.Read(ref hearing)!=0;
    public WindowsWendyVoice() {
        thread=new Thread(Run) {IsBackground=true,Name="Wendy PC audio",Priority=ThreadPriority.BelowNormal};
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    public void Configure(bool listen,bool muted,int volume) => Volatile.Write(ref mode,new(listen,muted,Math.Clamp(volume,0,100)));
    public WendySpeechInput? TakeInput() => Interlocked.Exchange(ref input,null);
    public void Speak(string text) { if(text.Length is >0 and <=1600) Interlocked.Exchange(ref speech,new(text,Volatile.Read(ref cancelEpoch))); }
    public void Cancel() { Interlocked.Increment(ref cancelEpoch); Interlocked.Exchange(ref input,null); Interlocked.Exchange(ref speech,null); }
    public void ReleaseInput()=>Interlocked.Exchange(ref releaseInput,1);
    private void Run() {
        using var microphone=new VoskSpeechProcess();SpeechSynthesizer? synth=null;
        var seenEpoch=0;var wasMuted=false;
        string appliedVoice="";
        Prompt? activePrompt=null;long speechStarted=0;
        void StopMic() {
            microphone.Listen(false);microphone.TakeInput();Volatile.Write(ref hearing,0);
        }
        try {
            while(!closed) {
                if(Interlocked.Exchange(ref releaseInput,0)!=0)microphone.Dispose();
                var current=Volatile.Read(ref mode); var epoch=Volatile.Read(ref cancelEpoch);
                if((current.Muted && !wasMuted) || epoch!=seenEpoch) {
                    seenEpoch=epoch; StopMic(); Interlocked.Exchange(ref input,null);
                    if(current.Muted) Interlocked.Exchange(ref speech,null);
                    synth?.SpeakAsyncCancelAll();activePrompt=null; Volatile.Write(ref speaking,0);
                    Volatile.Write(ref state,"IDLE");
                }
                wasMuted=current.Muted;
                if(activePrompt!=null && (activePrompt.IsCompleted || Stopwatch.GetElapsedTime(speechStarted).TotalSeconds>=30)) {
                    if(!activePrompt.IsCompleted) synth?.SpeakAsyncCancelAll();
                    activePrompt=null;Volatile.Write(ref speaking,0);Volatile.Write(ref state,"IDLE");
                }
                var voiceName=Volatile.Read(ref requestedVoice);
                if(synth!=null && appliedVoice!=voiceName && !Speaking) {synth.Dispose();synth=null;}
                if(!current.Muted && Interlocked.Exchange(ref speech,null) is { } utterance && utterance.Epoch==epoch) {
                    StopMic();synth?.SpeakAsyncCancelAll();
                    try {
                        if(synth==null) {
                            synth=new SpeechSynthesizer(); var available=synth.GetInstalledVoices().Where(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en").ToArray();
                            var english=available.FirstOrDefault(v=>v.VoiceInfo.Name==voiceName)??available.FirstOrDefault();
                            appliedVoice=voiceName;
                            if(english==null) throw new InvalidOperationException("English TTS voice is not installed.");
                            synth.SelectVoice(english.VoiceInfo.Name); synth.SetOutputToDefaultAudioDevice();
                        }
                        synth.Volume=current.Volume;
                        Volatile.Write(ref speaking,1);Volatile.Write(ref state,"SPEAKING");
                        activePrompt=synth.SpeakAsync(utterance.Text);speechStarted=Stopwatch.GetTimestamp();
                    } catch(Exception ex) { synth?.Dispose();synth=null;Volatile.Write(ref speaking,0);Volatile.Write(ref state,"ERROR");Volatile.Write(ref health,"PC output unavailable: "+ex.Message); }
                }
                if(synth!=null) synth.Volume=current.Volume;
                if(!current.Listen || current.Muted || Speaking) StopMic();
                else {
                    microphone.Listen(true);Volatile.Write(ref hearing,microphone.Hearing?1:0);
                    Volatile.Write(ref state,microphone.State);Volatile.Write(ref health,microphone.Health);
                    if(microphone.TakeInput() is {} recognized) {
                        var desired=Volatile.Read(ref mode);
                        if(desired.Listen&&!desired.Muted&&!Speaking&&epoch==Volatile.Read(ref cancelEpoch)) {
                            Interlocked.Exchange(ref input,recognized);Volatile.Write(ref state,"PROCESSING");
                        }
                    }
                }
                Thread.Sleep(50);
            }
        } catch(Exception ex) { Volatile.Write(ref state,"ERROR");Volatile.Write(ref health,"PC audio stopped: "+ex.Message); }
        finally { StopMic();try{synth?.SpeakAsyncCancelAll();synth?.Dispose();}catch(Exception){} }
    }
    public void Dispose() { closed=true;Cancel();thread.Join(1500); }
}
