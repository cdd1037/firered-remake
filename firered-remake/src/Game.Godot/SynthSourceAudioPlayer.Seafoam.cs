using System.Text.Json;
using Game.Content;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendSeafoamCues(string root, bool includeFossilLab, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeFossilLab) throw new InvalidDataException("Seafoam audio requires its current Fossil Lab predecessor.");
        var art = PretSeafoamPresentation.Load(root);
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretSeafoamPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Seafoam synth metadata missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues is not [var normal] ||
            normal.Id != "CRY_ARTICUNO_NORMAL" || normal.Species != 144 || normal.CryMode != 0 || normal.SourcePlayer != 4 || normal.Priority != 10 ||
            normal.SourceSymbol != "SPECIES_ARTICUNO" || normal.SourcePath != "sound/direct_sound_samples/cries/articuno.wav" ||
            normal.SourceCommit != sourceCommit || normal.SongAddress != 0 || normal.Looping || normal.SourceCounter is not null ||
            cues.Any(c => c.Id is "CRY_ARTICUNO_NORMAL" or "CRY_ARTICUNO_ENCOUNTER"))
            throw new InvalidDataException("Seafoam normal cry binding differs.");
        foreach (var id in new[] { "MUS_VIRIDIAN_FOREST", "MUS_ROUTE3", "MUS_ENCOUNTER_GYM_LEADER", "MUS_VS_LEGEND", "SE_FALL", "SE_M_STRENGTH", "SE_LEDGE" })
            if (cues.Count(c => c.Id == id && c.SourceCommit == sourceCommit) != 1)
                throw new InvalidDataException("Seafoam shared source cue missing: " + id);
        // B4F's authenticated playmoncry SPECIES_ARTICUNO, CRY_MODE_ENCOUNTER selects mode2.
        // Both modes use the same indexed source cry; the shared core owns the different envelope.
        return [.. cues, normal, normal with { Id = "CRY_ARTICUNO_ENCOUNTER", CryMode = 2 }];
    }
}
