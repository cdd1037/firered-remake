using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record PowerPlantSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendPowerPlantCues(string root, bool includeEventIslands, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeEventIslands) throw new InvalidDataException("Power Plant audio requires Event Islands.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), PowerPlantPresentationAssets.Base + "synth-manifest.json"));
        var extension = JsonSerializer.Deserialize<PowerPlantSynthManifest>(bytes, options) ?? throw new InvalidDataException("Power Plant synth missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_ELECTABUZZ_NORMAL", "CRY_ZAPDOS_NORMAL", "CRY_ZAPDOS_ENCOUNTER", "CRY_ELECTRODE_ENCOUNTER"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Power Plant source cue scope differs.");
        return [..cues, ..extension.Cues];
    }
}
