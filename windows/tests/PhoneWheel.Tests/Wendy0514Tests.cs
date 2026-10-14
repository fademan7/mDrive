using System.Buffers.Binary;
using PhoneWheel.Core;

internal static class Wendy0514Tests
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Conversation() {
        var e=new WendyEngineer();var r=new F1RaceState();
        foreach(var phrase in new[]{"radio check","audio check","Please check my audio", "radio cheque", "Tell me the radio check", "Can you hear me now?","Are you listening?","Hey Wendy, can you hear me clearly?"})
            Check(e.Answer(phrase,1,r,0).Text.Contains("hear you"),phrase);
        foreach(var phrase in new[]{"How was your day?","Tell me a joke","I am tired","Can we chat?","Thank you","Good morning"}) {
            Check(WendyLanguage.Fallback(phrase).Intent==DriverIntent.SMALL_TALK,phrase);
            var response=e.Answer(phrase,1,r,0).Text;
            Check(response.Length>0 && !response.Contains("telemetry",StringComparison.OrdinalIgnoreCase),"off-track social response");
        }
        for(var t=0;t<600000;t+=1000)Check(e.Alert(r,t)==null,"no telemetry: no unsolicited invitations");
        var first=e.Answer("unrecognized noise",1,r,0).Text;
        Check(!first.Contains("gaps")&&!first.Contains("tyres"),"no race advertising");
        Check(e.Answer("unrecognized noise",1,r,100).Text=="","incidental speech cooldown");
        Check(e.Answer("Set brake bias to 54",1,r,100).Text.Contains("not supported"),"no live keyboard feature");
        Check(new WendyAudioSettings().Volume==100,"default 100 percent");
    }
    public static void Briefings() {
        var r=new F1RaceState();var e=new WendyEngineer();uint frame=0;
        var packets=new Dictionary<int,byte[]>();
        foreach(var (id,len) in new[]{(1,753),(2,1285),(6,1352),(7,1239),(10,1041)}) {
            var p=new byte[len];BinaryPrimitives.WriteUInt16LittleEndian(p,2025);p[5]=1;p[6]=(byte)id;p[28]=255;
            BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7),1);packets[id]=p;
        }
        packets[2][61]=1;packets[2][62]=3;packets[2][74]=2;packets[7][57]=1;
        BinaryPrimitives.WriteSingleLittleEndian(packets[7].AsSpan(34),20);
        BinaryPrimitives.WriteUInt16LittleEndian(packets[6].AsSpan(29),200);
        void Feed(double now){foreach(var p in packets.Values){BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23),++frame);Check(r.Ingest(p,now),"fixture");}}
        Feed(0);e.Alert(r,0);
        var said=new List<string>();
        for(var lap=1;lap<=8;lap++){var now=lap*61000d;Feed(now);if(e.Alert(r,now) is {} reply)said.Add(reply.Text);}
        Check(said.Any(s=>s.Contains("Weather")),"weather briefing");
        Check(said.Any(s=>s.Contains("temperatures")),"temperature briefing");
        Check(said.Any(s=>s.Contains("Fuel")),"fuel briefing");
        Check(said.Distinct().Count()>=4,"varied useful briefings");
        Check(e.Answer("tell me a joke",1,r,488000).Text.Contains("focus on the race"),"race-only conversation");
        Check(e.Alert(r,600000)==null,"stale data suppresses all briefings");
        Check(e.Answer("tell me a joke",1,r,600000).Text.Contains("Windows"),"automatically return to chat");
    }
}
