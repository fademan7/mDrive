using System.Text.Json;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

internal static class WendyAudioPreferences
{
    private static readonly string PathName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"mDrive","audio.json");
    public static WendyAudioSettings Load() {
        try { var json=File.ReadAllText(PathName); var value=JsonSerializer.Deserialize<WendyAudioSettings>(json);
            using var doc=JsonDocument.Parse(json);
            // Migrate the former default only; preserve other chosen levels.
            if(value?.Volume==85 && !doc.RootElement.TryGetProperty("SettingsVersion",out _)) value=value with {Volume=100};
            return value!=null&&Enum.IsDefined(value.Input)&&Enum.IsDefined(value.Output)&&value.Volume is >=0 and <=100 ? value with {Revision=0} : new();
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) {return new();}
    }
    public static bool Save(WendyAudioSettings value) {
        try {Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(value));File.Move(PathName+".tmp",PathName,true);return true;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {return false;}
    }
}
