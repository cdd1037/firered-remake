using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record CelioSynthManifest(int SchemaVersion, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendCelioCues(string root, bool includeFiveIsland, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeFiveIsland) throw new InvalidDataException("Celio audio requires Five Island.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root),CelioPresentationAssets.Base+"synth-manifest.json"));
        var extension = JsonSerializer.Deserialize<CelioSynthManifest>(bytes,options) ?? throw new InvalidDataException("Celio synth missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["CRY_RAIKOU_NORMAL","CRY_ENTEI_NORMAL","CRY_SUICUNE_NORMAL"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Celio source cue scope differs.");
        // Handoff cues retain the existing instances and sequence-driven playback path.
        return [..cues,..extension.Cues];
    }
}
