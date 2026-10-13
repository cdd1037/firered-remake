using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record SevenIslandSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendSevenIslandCues(string root, bool includeLeagueRematch, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeLeagueRematch) throw new InvalidDataException("Seven Island audio requires League rematch.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), SevenIslandPresentationAssets.Base + "synth-manifest.json"));
        if (bytes.Length != SevenIslandPresentationAssets.SynthBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != SevenIslandPresentationAssets.SynthSha256)
            throw new InvalidDataException("Seven Island synth identity differs.");
        var extension = JsonSerializer.Deserialize<SevenIslandSynthManifest>(bytes, options) ?? throw new InvalidDataException("Seven Island synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != SevenIslandPresentationAssets.SynthCueCount ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_PHANPY_NORMAL", "CRY_DONPHAN_NORMAL", "CRY_LARVITAR_NORMAL", "CRY_PUPITAR_NORMAL", "CRY_SKARMORY_NORMAL", "CRY_UNOWN_NORMAL", "CRY_GIRAFARIG_NORMAL", "CRY_HITMONTOP_NORMAL", "CRY_MILTANK_NORMAL", "CRY_PINECO_NORMAL", "CRY_SMEARGLE_NORMAL", "CRY_URSARING_NORMAL", "MUS_SCHOOL", "SE_LAVARIDGE_FALL_WARP", "SE_TRUCK_MOVE"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Seven Island source cue scope differs.");
        if (extension.InheritedCues is not { Length: 6 } ||
            !extension.InheritedCues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_TYRANITAR_NORMAL", "MUS_POKE_CENTER", "MUS_SEVII_67", "MUS_SEVII_DUNGEON", "MUS_SEVII_ROUTE", "SE_M_STRENGTH"]) ||
            extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("Seven Island inherited cues differ.");
        return [..cues, ..extension.Cues];
    }
}
