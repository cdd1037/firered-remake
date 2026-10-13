using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record LeagueRematchSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendLeagueRematchCues(string root, bool includeCeruleanCave, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeCeruleanCave) throw new InvalidDataException("League rematch audio requires Cerulean Cave.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), LeagueRematchPresentationAssets.Base + "synth-manifest.json"));
        if (bytes.Length != LeagueRematchPresentationAssets.SynthBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != LeagueRematchPresentationAssets.SynthSha256)
            throw new InvalidDataException("League rematch synth identity differs.");
        var extension = JsonSerializer.Deserialize<LeagueRematchSynthManifest>(bytes, options) ?? throw new InvalidDataException("League rematch synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != LeagueRematchPresentationAssets.SynthCueCount ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_CROBAT_NORMAL", "CRY_MISDREAVUS_NORMAL", "CRY_HERACROSS_NORMAL", "CRY_KINGDRA_NORMAL", "CRY_TYRANITAR_NORMAL"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("League rematch source cue scope differs.");
        if (extension.InheritedCues is not { Length: 5 } ||
            !extension.InheritedCues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_STEELIX_NORMAL", "MUS_VS_GYM_LEADER", "MUS_VS_CHAMPION", "MUS_VICTORY_TRAINER", "MUS_VICTORY_GYM_LEADER"]) ||
            extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("League rematch inherited cues differ.");
        return [..cues, ..extension.Cues];
    }
}
