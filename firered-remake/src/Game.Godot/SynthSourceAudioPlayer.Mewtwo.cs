using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record MewtwoSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendMewtwoCues(string root, bool includeCelio, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeCelio) throw new InvalidDataException("Mewtwo audio requires Celio.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root),MewtwoPresentationAssets.Base+"synth-manifest.json"));
        if (bytes.Length != MewtwoPresentationAssets.SynthBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != MewtwoPresentationAssets.SynthSha256)
            throw new InvalidDataException("Mewtwo synth identity differs.");
        var extension = JsonSerializer.Deserialize<MewtwoSynthManifest>(bytes,options) ?? throw new InvalidDataException("Mewtwo synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != MewtwoPresentationAssets.SynthCueCount ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["CRY_MEWTWO_NORMAL","CRY_MEWTWO_ENCOUNTER","MUS_VS_MEWTWO"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Mewtwo source cue scope differs.");
        if (extension.InheritedCues is not {Length:1} ||
            !extension.InheritedCues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["MUS_ENCOUNTER_GYM_LEADER"]) ||
            extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("Mewtwo inherited cues differ.");
        // The script introduction retains its existing sequence; no duplicate cue is inserted.
        return [..cues,..extension.Cues];
    }
}
