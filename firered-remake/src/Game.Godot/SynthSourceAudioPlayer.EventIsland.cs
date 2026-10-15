using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record EventIslandSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendEventIslandCues(string root, bool includeSevenIsland, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeSevenIsland) throw new InvalidDataException("Event Island audio requires Seven Island.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root), EventIslandPresentationAssets.Base + "synth-manifest.json"));
        var extension = JsonSerializer.Deserialize<EventIslandSynthManifest>(bytes, options) ?? throw new InvalidDataException("Event Island synth missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                ["CRY_LUGIA_NORMAL", "CRY_HO_OH_NORMAL", "CRY_DEOXYS_NORMAL", "CRY_LUGIA_ENCOUNTER", "CRY_HO_OH_ENCOUNTER", "CRY_DEOXYS_ENCOUNTER", "MUS_ENCOUNTER_DEOXYS", "MUS_VS_DEOXYS", "MUS_VS_LEGEND", "SE_DEOXYS_MOVE", "SE_THUNDER", "SE_THUNDER2", "SE_THUNDERSTORM_STOP"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Event Island source cue scope differs.");
        return [..cues, ..extension.Cues];
    }
}
