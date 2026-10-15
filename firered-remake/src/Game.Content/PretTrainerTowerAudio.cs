using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Fifteen newly admitted source cries plus Tower BGM; the shared synth pack is unchanged.</summary>
public static class PretTrainerTowerAudio
{
    public const string BasePath = "content/generated/audio/cues/trainer-tower/";
    public const int AdditionalCueCount = 16;
    public const int InheritedCueCount = 3;
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    public static JsonElement LoadSynthManifest(string root)
    {
        var bytes = Read(root,BasePath + "synth-manifest.json");
        var d = JsonSerializer.Deserialize<JsonElement>(bytes);
        if (d.GetProperty("schemaVersion").GetInt32() != 1 ||
            d.GetProperty("cues").GetArrayLength() != AdditionalCueCount || d.GetProperty("inheritedCues").GetArrayLength() != InheritedCueCount)
            throw new InvalidDataException("trainer_tower_audio_scope");
        return d;
    }
}
