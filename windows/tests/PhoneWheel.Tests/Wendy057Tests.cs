using System.Buffers.Binary;
using PhoneWheel.Core;

internal static class Wendy057Tests
{
    private static void Check(bool yes, string why) { if(!yes) throw new Exception(why); }
    private sealed class Output : IWendyActionOutput {
        public bool Enabled {get;set;} = true;
        public int Count; public bool Fail;
        public Task<bool> Pulse(WendySetting s,int direction,CancellationToken token) { Count++; return Fail ? Task.FromException<bool>(new IOException("injected")) : Task.FromResult(!token.IsCancellationRequested); }
    }
    private sealed class Fixture {
        public readonly F1RaceState Race = new();
        public readonly Dictionary<int,byte[]> Packets = new();
        private uint frame;
        public Fixture() {
            foreach(var (id,size) in new[]{(1,753),(2,1285),(5,1133),(6,1352),(7,1239),(10,1041)}) {
                var p=new byte[size]; BinaryPrimitives.WriteUInt16LittleEndian(p,2025); p[5]=1;p[6]=(byte)id;p[28]=255;BinaryPrimitives.WriteUInt64LittleEndian(p.AsSpan(7),1);Packets[id]=p;
            }
            B(1,3,20); BinaryPrimitives.WriteUInt16LittleEndian(Packets[1].AsSpan(33),5000);
            B(2,32,1);B(2,33,1);B(2,44,1);B(2,45,2);
            BinaryPrimitives.WriteUInt32LittleEndian(Packets[2].AsSpan(29),60000);
            B(7,3,54); B(7,25,18);B(7,27,4);B(7,28,1); F(7,5,20);
            B(5,2,55);B(5,3,40);
            BinaryPrimitives.WriteUInt16LittleEndian(Packets[6].AsSpan(29),130);F(6,2,.7f);F(6,6,.2f);
            for(var i=0;i<4;i++) { B(6,34+i,90);F(5,29+4*i,23); }
        }
        public void B(int id,int offset,int value) => Packets[id][29+offset]=(byte)value;
        public void F(int id,int offset,float value) => BinaryPrimitives.WriteSingleLittleEndian(Packets[id].AsSpan(29+offset),value);
        public void Feed(double now) { foreach(var p in Packets.Values) { BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(23),++frame);Check(Race.Ingest(p,now),"fixture"); } }
    }
    public static void Commands() {
        foreach(var phrase in new[]{"Set brake bias to 54.","Set the diff to 55.","Reduce differential by five.","Move brake bias forward one.","Set brake bias to fifty four."})
            Check(WendyActions.Parse(phrase,out _,out _,out _) && WendyLanguage.Fallback(phrase).Intent == DriverIntent.CHANGE_SETTING,"exact phrase: "+phrase);
        foreach(var phrase in new[]{"Set brake bias to -54", "Set brake bias to 54 or 55", "Maybe reduce differential by five", "Don't set brake bias to 54", "Set brake bias to 54.5", "Set brake bias to 54 and diff to 55"})
            Check(!WendyActions.Parse(phrase,out _,out _,out _),"ambiguous phrase: "+phrase);
        var f=new Fixture(); var output=new Output();var a=new WendyActions(output);f.Feed(0);
        Check(a.Request("Set brake bias to 56",1,f.Race,0).Kind=="executing","requested");
        a.Tick(f.Race,0);Check(output.Count==1,"one step only");
        f.Feed(10);a.Tick(f.Race,10);Check(output.Count==1,"await post-write telemetry");
        Check(a.Request("Set brake bias to 55",1,f.Race,10).Kind=="rejected","busy no queue");
        f.B(7,3,55);f.Feed(20);a.Tick(f.Race,20);Check(output.Count==2,"second step after telemetry");
        f.Feed(30);a.Tick(f.Race,30);f.B(7,3,56);f.Feed(40);a.Tick(f.Race,40);
        Check(a.State==WendyCommandState.Confirmed && a.TakeCompletion()?.Text.Contains("56")==true && a.TakeCompletion()==null,"one confirmed result");
        a.Request("Reduce differential by five",1,f.Race,40);a.Tick(f.Race,40);f.Feed(50);a.Tick(f.Race,50);f.Feed(1600);a.Tick(f.Race,1600);
        Check(a.State==WendyCommandState.Unconfirmed && output.Count==3,"no confirmation/no retry");
        a.Request("Set brake bias to 54",.2,f.Race,1600);Check(a.State==WendyCommandState.Rejected,"confidence");
        a.Request("Set brake bias to 54",1,f.Race,1600);a.Invalidate();a.Tick(f.Race,1600);Check(output.Count==3 && a.State==WendyCommandState.Unconfirmed,"disconnect cancellation");
        a.Request("Set brake bias to 54",1,f.Race,1600);f.Race.Reset();a.Tick(f.Race,1600);Check(output.Count==3,"session/flashback cancellation");
        f.Feed(1700);output.Fail=true;a.Request("Set brake bias to 54",1,f.Race,1700);a.Tick(f.Race,1700);a.Tick(f.Race,1701);Check(a.State==WendyCommandState.Unconfirmed,"output exception");
        output.Fail=false;output.Enabled=false;Check(a.Request("Set brake bias to 54",1,f.Race,1701).Kind=="rejected","disabled default");
        output.Enabled=true;a.Request("Set brake bias to 54",1,f.Race,1701);f.B(7,3,57);f.Feed(1710);a.Tick(f.Race,1710);Check(a.State==WendyCommandState.Unconfirmed,"manual changes cancel");
        Console.WriteLine("  exact/relative/ambiguous, step ACK, timeout, duplicate, session/reconnect and output exception PASS");
    }
    public static void LifeAndAdvisor() {
        var f=new Fixture();var life=new WendyTyreLife();var advisor=new WendyAdvisor();double now=0;
        var feedback=new WendyIntent(DriverIntent.DRIVER_FEEDBACK,DriverSymptom.REAR_INSTABILITY,CornerPhase.CORNER_EXIT);
        for(var lap=1;lap<=4;lap++) {
            for(var sample=0;sample<(lap==4?1:600);sample++) {
                f.B(2,33,lap);f.B(7,27,lap+3);f.F(2,20,sample/600f*5000);
                for(var wheel=0;wheel<4;wheel++) f.F(10,wheel*4,35+(lap-1+sample/600f)*4);
                f.Feed(now); life.Observe(f.Race,now);advisor.Observe(f.Race,now);
                if(sample==300) { Check(advisor.Feedback(feedback,f.Race,now)?.Contains("three clean")==true,"no early numeric advice"); }
                now+=100;
            }
        }
        var estimate=life.Estimate(f.Race,now-100);Check(estimate!=null && estimate.Low<estimate.High && estimate.Samples==3,"stable clean wear range");
        Check(life.Summary(f.Race,now-100).StartsWith("TYRE REMAIN"),"HUD life");
        Check(advisor.Proactive(f.Race,now-100)==null,"no proactive setup while cornering");
        f.F(6,6,0);f.Feed(now-100);Check(advisor.Proactive(f.Race,now-100)?.Contains("Agree?")==true,"straight proactive after repeated reports");advisor.ClearApproval();
        var advice=advisor.Feedback(feedback,f.Race,now-100);Check(advice?.Contains("five percentage")==true,"three completed lap evidence");
        var approval=advisor.Approve(f.Race,now-100);Check(approval?.Command=="Set differential to 50" && advisor.Approve(f.Race,now)==null,"one smallest step consumed");
        Check(advisor.Feedback(feedback,f.Race,now)==null,"no repeated drift");
        f.Packets[10][57]=18;f.Feed(now);Check(life.Summary(f.Race,now)=="FW DAMAGE 18%","damage priority");
        for(var w=0;w<4;w++)f.F(10,w*4,76);f.Feed(now+100);Check(life.Summary(f.Race,now+100)=="PIT THIS LAP","pit priority");
        Check(life.Summary(f.Race,now+4000)=="","stale clears");
        f.Race.Reset();f.Feed(now+5000);life.Observe(f.Race,now+5000);Check(life.Estimate(f.Race,now+5000)==null,"new session clears learned trend");
        advisor.ResetConversation();Check(advisor.Approve(f.Race,now+5000)==null,"no stale approval");
    }
    public static void ApprovalInvalidation() {
        foreach(var invalidate in new[]{"session","reconnect","expiry","setup","temperature","invalid lap"}) {
            var f=new Fixture();var advisor=new WendyAdvisor();double now=0;
            var feedback=new WendyIntent(DriverIntent.DRIVER_FEEDBACK,DriverSymptom.UNDERSTEER,CornerPhase.CORNER_ENTRY);
            f.F(6,2,0);f.F(6,10,.5f);
            for(var lap=1;lap<=4;lap++) for(var s=0;s<(lap==4?1:300);s++) {
                f.B(2,33,lap); f.F(2,20,s/300f*5000);f.Feed(now);advisor.Observe(f.Race,now);
                if(s==100) advisor.Feedback(feedback,f.Race,now);
                now+=200;
            }
            Check(advisor.Feedback(feedback,f.Race,now-200)?.Contains("garage")==true,"garage suggestion has three clean laps");
            switch(invalidate) {
                case "session": f.Race.Reset();f.Feed(now);break;
                case "reconnect": advisor.ResetConversation();break;
                case "expiry": now+=31000;f.Feed(now);break;
                case "setup": f.B(5,2,60);f.Feed(now);break;
                case "temperature": f.B(6,34,120);f.Feed(now);break;
                case "invalid lap": f.B(2,37,1);f.Feed(now);break;
            }
            Check(advisor.Approve(f.Race,now)==null,"invalidated approval: "+invalidate);
        }
    }
}
