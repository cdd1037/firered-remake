using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Fifteen newly admitted source cries plus Tower BGM; the shared synth pack is unchanged.</summary>
public static class PretTrainerTowerAudio
{
    public const string BasePath = "content/generated/trainer-tower-audio/";
    public const string Profile = "firered-trainer-tower-audio-v1";
    public const string SynthManifestSha256 = "9c32d45626b5d88e838cee1085b468a5240ed63c2045a07524b8c4fce5d1a5d4";
    public const int SynthManifestBytes = 7506;
    public const int AdditionalCueCount = 16;
    public const int InheritedCueCount = 3;
    public const string ManifestSha256 = "89d5f7187531942af8e5b68a6678e37d452f4b4a89dbc8c48618265811cc6c92";
    public const string LockSha256 = "dee124f8514d34169146c27b15fd3e60445a6303e2614ce790def5d638d30fb5";
    private static byte[] Read(string root, string path, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("trainer_tower_audio_bytes:" + path);
        return bytes;
    }
    public static JsonElement LoadSynthManifest(string root)
    {
        var bytes = Read(root, BasePath + "synth-manifest.json", SynthManifestSha256);
        if (bytes.Length != SynthManifestBytes) throw new InvalidDataException("trainer_tower_audio_size");
        var d = JsonSerializer.Deserialize<JsonElement>(bytes);
        if (d.GetProperty("schemaVersion").GetInt32() != 1 || d.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            d.GetProperty("cues").GetArrayLength() != AdditionalCueCount || d.GetProperty("inheritedCues").GetArrayLength() != InheritedCueCount)
            throw new InvalidDataException("trainer_tower_audio_scope");
        return d;
    }
    public static JsonElement LoadManifest(string root)
    {
        var d = JsonSerializer.Deserialize<JsonElement>(Read(root, BasePath + "manifest.json", ManifestSha256));
        if (d.GetProperty("schemaVersion").GetInt32() != 1 || d.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            d.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree || d.GetProperty("profile").GetString() != Profile ||
            d.GetProperty("cries").GetArrayLength() != 18 || d.GetProperty("inputs").GetArrayLength() != 24 ||
            d.GetProperty("dependencyInputs").GetArrayLength() != 4 || !d.GetProperty("originalPackUnchanged").GetBoolean() ||
            d.GetProperty("newAudioAssets").GetInt32() != 0 || d.GetProperty("physicalAudioVerified").GetBoolean())
            throw new InvalidDataException("trainer_tower_audio_manifest");
        _ = LoadSynthManifest(root); return d;
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        _ = Read(root, "scripts/trainer-tower-audio.lock.json", LockSha256); var d = LoadManifest(root);
        foreach (var row in d.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read(root, "content/raw/trainer-tower-audio/" + row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!);
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("trainer_tower_audio_source");
        }
        foreach (var row in d.GetProperty("dependencyInputs").EnumerateArray())
            if (Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!).Length != row.GetProperty("byteLength").GetInt32())
                throw new InvalidDataException("trainer_tower_audio_dependency");
    }
}
