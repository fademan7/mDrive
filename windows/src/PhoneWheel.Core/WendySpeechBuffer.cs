using System.Text.Json;

namespace PhoneWheel.Core;

// Audio only, never used by the controller. A gap invalidates the whole utterance.
public sealed class WendySpeechBuffer
{
    public sealed record Frame(byte[] Pcm, long Epoch, double At);
    private readonly object sync=new();
    private readonly Queue<Frame> frames=new();
    private bool discontinuity;
    public void Push(byte[] pcm,long epoch,double at) {
        lock(sync) {
            if(frames.Count>=10) {frames.Clear();discontinuity=true;}
            frames.Enqueue(new(pcm,epoch,at));
        }
    }
    public Frame? Take(long epoch,double now,out bool reset) {
        lock(sync) {
            reset=discontinuity;discontinuity=false;
            if(reset) {frames.Clear();return null;}
            if(frames.Count==0)return null;
            var frame=frames.Dequeue();
            if(frame.Epoch!=epoch || now-frame.At>500 || now<frame.At) {frames.Clear();reset=true;return null;}
            return frame;
        }
    }
    public void Clear(){lock(sync){frames.Clear();discontinuity=false;}}
    public static WendySpeechInput? ParseResult(string json) {
        if(json.Length>32768)return null;
        try {
            using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
            if(!root.TryGetProperty("text",out var value)||value.ValueKind!=JsonValueKind.String)return null;
            var text=value.GetString()!.Trim();if(text.Length is 0 or >240)return null;
            if(!root.TryGetProperty("result",out var words)||words.ValueKind!=JsonValueKind.Array)return null;
            var confidence=1d;var count=0;
            foreach(var word in words.EnumerateArray()) {
                if(!word.TryGetProperty("conf",out var c)||!c.TryGetDouble(out var n)||!double.IsFinite(n)||n<0||n>1)return null;
                confidence=Math.Min(confidence,n);count++;
            }
            return count>0?new(text,confidence):null;
        } catch(JsonException){return null;}
    }
}
