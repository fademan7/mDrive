using System.Diagnostics;
using System.Text.Json;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

internal sealed class VoskSpeechProcess : IDisposable
{
    private Process? process;private CpuProcessJob? job;
    private long epoch,lastMessage;private long retryAt;
    private bool listening;
    private string state="IDLE",health="Vosk offline English ready.";
    private int hearing;private WendySpeechInput? input;
    public string State=>Volatile.Read(ref state);
    public string Health=>Volatile.Read(ref health);
    public bool Hearing=>Volatile.Read(ref hearing)!=0;
    public WendySpeechInput? TakeInput()=>Interlocked.Exchange(ref input,null);
    public void Listen(bool enabled) {
        if(process!=null && (process.HasExited || Stopwatch.GetElapsedTime(Interlocked.Read(ref lastMessage)).TotalSeconds>30)) {
            Stop();retryAt=Stopwatch.GetTimestamp()+10*Stopwatch.Frequency;
            Volatile.Write(ref state,"ERROR");Volatile.Write(ref health,"PC speech worker stopped. Retrying; controller unaffected.");
        }
        if(process==null && enabled && Stopwatch.GetTimestamp()>=retryAt) {
            try {Start();}catch(Exception){Stop();retryAt=Stopwatch.GetTimestamp()+10*Stopwatch.Frequency;Volatile.Write(ref state,"ERROR");Volatile.Write(ref health,"Vosk model/worker unavailable. Reinstall the speech component or select Android input.");}
        }
        if(process==null || listening==enabled)return;
        listening=enabled;var ticket=Interlocked.Increment(ref epoch);
        Interlocked.Exchange(ref input,null);Volatile.Write(ref hearing,0);
        try {process.StandardInput.WriteLine(JsonSerializer.Serialize(new{Listen=enabled,Epoch=ticket}));process.StandardInput.Flush();}
        catch(IOException){Stop();}
    }
    private void Start() {
        if(!File.Exists(Path.Combine(VoskSpeechWorker.ModelPath,"am","final.mdl")))throw new FileNotFoundException();
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        // dotnet-hosted debug runs need the assembly before the worker argument.
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"PhoneWheel.Receiver.dll"));
        info.ArgumentList.Add("--vosk-worker");info.ArgumentList.Add(VoskSpeechWorker.ModelPath);
        info.Environment["OPENBLAS_NUM_THREADS"]="1";info.Environment["OMP_NUM_THREADS"]="1";
        process=Process.Start(info)??throw new IOException("Speech worker could not start.");
        job=new CpuProcessJob(process);process.PriorityClass=ProcessPriorityClass.BelowNormal;
        Interlocked.Exchange(ref lastMessage,Stopwatch.GetTimestamp());
        process.OutputDataReceived+=(_,e)=> {
            if(e.Data==null || e.Data.Length>2048)return;
            try {
                using var doc=JsonDocument.Parse(e.Data);var root=doc.RootElement;
                Interlocked.Exchange(ref lastMessage,Stopwatch.GetTimestamp());
                if(root.GetProperty("epoch").GetInt64()!=Interlocked.Read(ref epoch))return;
                switch(root.GetProperty("kind").GetString()) {
                    case "status":var status=root.GetProperty("state").GetString();Volatile.Write(ref state,status is "IDLE" or "LISTENING" or "ERROR"?status:"PROCESSING");Volatile.Write(ref health,root.GetProperty("health").GetString()!);break;
                    case "partial":Volatile.Write(ref hearing,root.GetProperty("hearing").GetBoolean()?1:0);break;
                    case "result":
                        var text=root.GetProperty("text").GetString()!;var confidence=root.GetProperty("confidence").GetDouble();
                        if(text.Length is >0 and <=240 && double.IsFinite(confidence)&&confidence>=0&&confidence<=1)Interlocked.Exchange(ref input,new(text,confidence));break;
                }
            }catch(Exception){}
        };
        // Drain native diagnostics without persisting audio or transcripts.
        process.ErrorDataReceived+=(_,_)=>{};process.BeginOutputReadLine();process.BeginErrorReadLine();
        Volatile.Write(ref state,"PROCESSING");Volatile.Write(ref health,"Loading microphone. Wait for LISTENING before speaking.");
    }
    private void Stop() {
        Interlocked.Increment(ref epoch);listening=false;Interlocked.Exchange(ref input,null);Volatile.Write(ref hearing,0);
        try{process?.StandardInput.Close();job?.Dispose();if(process is {HasExited:false})process.Kill();}catch(Exception){}
        job=null;process?.Dispose();process=null;
    }
    public void Dispose()=>Stop();
}
