using PhoneWheel.Core;

internal static class WendySpeechTests
{
    public static void Run() {
        static void Check(bool value){if(!value)throw new Exception("Speech buffer/result safety failed");}
        var buffer=new WendySpeechBuffer();buffer.Push([0,0],1,0);
        Check(buffer.Take(1,20,out var reset)!=null&&!reset);
        buffer.Push([0,0],1,0);Check(buffer.Take(1,501,out reset)==null&&reset);
        buffer.Push([0,0],1,300);Check(buffer.Take(2,300,out reset)==null&&reset);
        for(var i=0;i<11;i++)buffer.Push([0,0],2,400);
        Check(buffer.Take(2,420,out reset)==null&&reset);
        buffer.Push([0,0],2,430);buffer.Clear();Check(buffer.Take(2,440,out reset)==null&&!reset);
        var recognized=WendySpeechBuffer.ParseResult("""{"text":"radio check","result":[{"conf":0.98},{"conf":0.81}]}""");
        Check(recognized?.Text=="radio check"&&recognized.Confidence==.81);
        foreach(var invalid in new[]{"{}","no json","{\"text\":\"radio check\"}","{\"text\":\"test\",\"result\":[]}","{\"text\":\"test\",\"result\":[{\"conf\":2}]}"})Check(WendySpeechBuffer.ParseResult(invalid)==null);
    }
}
