using System.Diagnostics;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Text.Json;
using PhoneWheel.Core;
using Vosk;

namespace PhoneWheel.Host;

// Synthetic, local TTS fixtures only. Never records a microphone or drives a pad.
internal static class VoskSpeechBenchmark
{
    public static int Stream(string path) {
        Vosk.Vosk.SetLogLevel(-1);
        using var model=new Model(path);using var recognizer=new VoskRecognizer(model,16000);recognizer.SetWords(true);
        using var synth=new SpeechSynthesizer();synth.SelectVoice(synth.GetInstalledVoices().First(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en").VoiceInfo.Name);
        using var fixture=new MemoryStream();
        foreach(var phrase in new[]{"Radio check","Tell me the time gap","Any front wing damage"}) {
            using var pcm=new MemoryStream();synth.SetOutputToAudioStream(pcm,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));synth.Speak(phrase);
            fixture.Write(pcm.ToArray());fixture.Write(new byte[64000]);
        }
        var bytes=fixture.ToArray();var buffer=new WendySpeechBuffer();var clock=Stopwatch.StartNew();var results=new List<string>();var resets=0;double maxDecodeMs=0;
        var producer=Task.Run(async()=> {
            for(var offset=0;offset<bytes.Length;offset+=1600) {
                var due=offset/32d;var wait=due-clock.Elapsed.TotalMilliseconds;if(wait>0)await Task.Delay((int)wait);
                buffer.Push(bytes.AsSpan(offset,Math.Min(1600,bytes.Length-offset)).ToArray(),1,clock.Elapsed.TotalMilliseconds);
            }
        });
        while(!producer.IsCompleted || clock.Elapsed.TotalMilliseconds<bytes.Length/32d+1000) {
            var frame=buffer.Take(1,clock.Elapsed.TotalMilliseconds,out var reset);
            if(reset){resets++;recognizer.Reset();}
            if(frame!=null) {
                var start=Stopwatch.GetTimestamp();var final=recognizer.AcceptWaveform(frame.Pcm,frame.Pcm.Length);maxDecodeMs=Math.Max(maxDecodeMs,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                if(final && WendySpeechBuffer.ParseResult(recognizer.Result()) is {} result)results.Add(result.Text);
            }else Thread.Sleep(5);
        }
        producer.GetAwaiter().GetResult();
        Console.WriteLine(JsonSerializer.Serialize(new{model=Path.GetFileName(path),syntheticRealtime=true,resets,maxDecodeMs,results}));
        return resets==0&&results.Count==3?0:2;
    }
    public static int Run(string path) {
        Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.BelowNormal;
        Vosk.Vosk.SetLogLevel(-1);var watch=Stopwatch.StartNew();
        using var model=new Model(path);var loadMs=watch.Elapsed.TotalMilliseconds;
        using var synth=new SpeechSynthesizer();
        synth.SelectVoice(synth.GetInstalledVoices().First(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en").VoiceInfo.Name);
        using var recognizer=new VoskRecognizer(model,16000);recognizer.SetWords(true);
        var phrases=new[]{"Radio check","Tell me the time gap","What's the gap behind","How are my tyres","What are my tyre temperatures","Do I need to pit","Any front wing damage","How much fuel do I have","The rear feels loose on exit","Thank you"};
        var results=new List<object>();var matched=0;double audioSeconds=0,decodeMs=0;
        foreach(var phrase in phrases) {
            using var pcm=new MemoryStream();synth.SetOutputToAudioStream(pcm,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));synth.Speak(phrase);
            var bytes=pcm.ToArray();audioSeconds+=bytes.Length/32000d;recognizer.Reset();watch.Restart();
            recognizer.AcceptWaveform(bytes,bytes.Length);
            var answer=WendySpeechBuffer.ParseResult(recognizer.FinalResult());var elapsed=watch.Elapsed.TotalMilliseconds;decodeMs+=elapsed;
            var expected=(WendyLanguage.RaceRequest(phrase)??WendyLanguage.Fallback(phrase)).Intent;
            var actual=(WendyLanguage.RaceRequest(answer?.Text??"")??WendyLanguage.Fallback(answer?.Text??"")).Intent;
            if(expected==actual)matched++;
            results.Add(new{phrase,recognized=answer?.Text,confidence=answer?.Confidence,expected,actual,decodeMs=elapsed});
        }
        // Silence must not become a command.
        recognizer.Reset();recognizer.AcceptWaveform(new byte[32000],32000);
        var silence=WendySpeechBuffer.ParseResult(recognizer.FinalResult());
        Console.WriteLine(JsonSerializer.Serialize(new{model=Path.GetFileName(path),syntheticOnly=true,loadMs,audioSeconds,decodeMs,realTimeFactor=decodeMs/1000/audioSeconds,matched,total=phrases.Length,silenceEmpty=silence==null,peakWorkingSetMb=Process.GetCurrentProcess().PeakWorkingSet64/1048576d,results}));
        return silence==null?0:2;
    }
}
