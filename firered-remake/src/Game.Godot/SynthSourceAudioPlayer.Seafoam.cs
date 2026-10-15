using System.Text.Json;
using Game.Content;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendSeafoamCues(string root, bool includeFossilLab, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeFossilLab) throw new InvalidDataException("Seafoam audio requires its current Fossil Lab predecessor.");
        var art = PretSeafoamPresentation.Load(root);
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretSeafoamPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Seafoam synth metadata missing.");
        if (extension.SchemaVersion != 1 || extension.Cues is not [var normal] ||
            normal.Id != "CRY_ARTICUNO_NORMAL" || normal.Species != 144 || normal.CryMode != 0 || normal.SourcePlayer != 4 || normal.Priority != 10 ||
            normal.SourceSymbol != "SPECIES_ARTICUNO" || normal.SongAddress != 0 || normal.Looping || normal.SourceCounter is not null ||
            cues.Any(c => c.Id is "CRY_ARTICUNO_NORMAL" or "CRY_ARTICUNO_ENCOUNTER"))
            throw new InvalidDataException("Seafoam normal cry binding differs.");
        // B4F's authenticated playmoncry SPECIES_ARTICUNO, CRY_MODE_ENCOUNTER selects mode2.
        // Both modes use the same indexed source cry; the shared core owns the different envelope.
        return [.. cues, normal, normal with { Id = "CRY_ARTICUNO_ENCOUNTER", CryMode = 2 }];
    }
}
