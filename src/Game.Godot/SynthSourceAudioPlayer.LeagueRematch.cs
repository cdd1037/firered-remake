using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record LeagueRematchSynthManifest(int SchemaVersion, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendLeagueRematchCues(string root, bool includeCeruleanCave, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeCeruleanCave) throw new InvalidDataException("League rematch audio requires Cerulean Cave.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), LeagueRematchPresentationAssets.Base + "synth-manifest.json"));
        var extension = JsonSerializer.Deserialize<LeagueRematchSynthManifest>(bytes, options) ?? throw new InvalidDataException("League rematch synth missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_CROBAT_NORMAL", "CRY_MISDREAVUS_NORMAL", "CRY_HERACROSS_NORMAL", "CRY_KINGDRA_NORMAL", "CRY_TYRANITAR_NORMAL"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("League rematch source cue scope differs.");
        return [..cues, ..extension.Cues];
    }
}
