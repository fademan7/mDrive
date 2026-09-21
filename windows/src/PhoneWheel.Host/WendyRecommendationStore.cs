using System.Text.Json;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

internal sealed class WendyRecommendationStore
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mDrive", "wendy-recommendations.json");
    public SetupRecommendation[] Load()
    {
        try {
            if (!File.Exists(path) || new FileInfo(path).Length > 1_048_576) return [];
            return (JsonSerializer.Deserialize<SetupRecommendation[]>(File.ReadAllText(path)) ?? []).TakeLast(100).ToArray();
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }
    public async Task<bool> Save(SetupRecommendation[] values, CancellationToken token)
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(values.TakeLast(100)), token);
            File.Move(temp, path, true); return true;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
