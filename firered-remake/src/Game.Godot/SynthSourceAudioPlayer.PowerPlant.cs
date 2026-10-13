using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record PowerPlantSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendPowerPlantCues(string root, bool includeEventIslands, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeEventIslands) throw new InvalidDataException("Power Plant audio requires Event Islands.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), PowerPlantPresentationAssets.Base + "synth-manifest.json"));
        if (bytes.Length != PowerPlantPresentationAssets.SynthBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != PowerPlantPresentationAssets.SynthSha256)
            throw new InvalidDataException("Power Plant synth identity differs.");
        var extension = JsonSerializer.Deserialize<PowerPlantSynthManifest>(bytes, options) ?? throw new InvalidDataException("Power Plant synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != PowerPlantPresentationAssets.SynthCueCount ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_ELECTABUZZ_NORMAL", "CRY_ZAPDOS_NORMAL", "CRY_ZAPDOS_ENCOUNTER", "CRY_ELECTRODE_ENCOUNTER"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Power Plant source cue scope differs.");
        if (extension.InheritedCues is not { Length: 6 } ||
            !extension.InheritedCues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_JOLTEON_NORMAL", "CRY_ELECTRODE_NORMAL", "MUS_POKE_MANSION", "MUS_ENCOUNTER_GYM_LEADER", "MUS_VS_WILD", "MUS_VS_LEGEND"]) ||
            extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("Power Plant inherited cues differ.");
        return [..cues, ..extension.Cues];
    }
}
