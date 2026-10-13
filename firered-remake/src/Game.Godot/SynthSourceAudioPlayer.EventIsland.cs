using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record EventIslandSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendEventIslandCues(string root, bool includeSevenIsland, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeSevenIsland) throw new InvalidDataException("Event Island audio requires Seven Island.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), EventIslandPresentationAssets.Base + "synth-manifest.json"));
        if (bytes.Length != EventIslandPresentationAssets.SynthBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != EventIslandPresentationAssets.SynthSha256)
            throw new InvalidDataException("Event Island synth identity differs.");
        var extension = JsonSerializer.Deserialize<EventIslandSynthManifest>(bytes, options) ?? throw new InvalidDataException("Event Island synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != EventIslandPresentationAssets.SynthCueCount ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_LUGIA_NORMAL", "CRY_HO_OH_NORMAL", "CRY_DEOXYS_NORMAL", "CRY_LUGIA_ENCOUNTER", "CRY_HO_OH_ENCOUNTER", "CRY_DEOXYS_ENCOUNTER", "MUS_ENCOUNTER_DEOXYS", "MUS_VS_DEOXYS", "MUS_VS_LEGEND", "SE_DEOXYS_MOVE", "SE_THUNDER", "SE_THUNDER2", "SE_THUNDERSTORM_STOP"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Event Island source cue scope differs.");
        if (extension.InheritedCues is not { Length: 5 } ||
            !extension.InheritedCues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["MUS_SEVII_CAVE", "MUS_SEVII_ROUTE", "SE_M_CONFUSE_RAY", "SE_M_STRENGTH", "SE_M_WING_ATTACK"]) ||
            extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("Event Island inherited cues differ.");
        return [..cues, ..extension.Cues];
    }
}
