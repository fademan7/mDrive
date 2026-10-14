namespace PhoneWheel.Core;

public enum WendyAudioDevice { PC, Android }
public sealed record WendyAudioSettings(WendyAudioDevice Input = WendyAudioDevice.PC, WendyAudioDevice Output = WendyAudioDevice.PC, int Volume = 100, long Revision = 0, string Voice = "", int SettingsVersion = 1);
public sealed record WendySpeechInput(string Text, double Confidence);
public interface IWendyPcVoice : IDisposable
{
    string State { get; }
    string Health { get; }
    bool Speaking { get; }
    bool HearingSpeech { get; }
    void Configure(bool listen, bool muted, int volume);
    WendySpeechInput? TakeInput();
    void Speak(string text);
    void Cancel();
    void ReleaseInput() { }
    void SelectVoice(string name) { }
}
