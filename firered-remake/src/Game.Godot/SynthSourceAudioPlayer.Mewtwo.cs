using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record MewtwoSynthManifest(int SchemaVersion, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendMewtwoCues(string root, bool includeCelio, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeCelio) throw new InvalidDataException("Mewtwo audio requires Celio.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root),MewtwoPresentationAssets.Base+"synth-manifest.json"));
        var extension = JsonSerializer.Deserialize<MewtwoSynthManifest>(bytes,options) ?? throw new InvalidDataException("Mewtwo synth missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["CRY_MEWTWO_NORMAL","CRY_MEWTWO_ENCOUNTER","MUS_VS_MEWTWO"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Mewtwo source cue scope differs.");
        // The script introduction retains its existing sequence; no duplicate cue is inserted.
        return [..cues,..extension.Cues];
    }
}
