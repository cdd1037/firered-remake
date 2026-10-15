using System.Text.Json;
using Game.Content;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendFossilLabCues(string root, bool includeTrainerTower, SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeTrainerTower) throw new InvalidDataException("Fossil Lab audio requires the current Trainer Tower audio predecessor.");
        var art = PretFossilLabPresentation.Load(root);
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretFossilLabPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Fossil Lab synth manifest missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Species).SequenceEqual(new[] { 138, 139, 140, 141 }) ||
            extension.Cues.Any(c => c.CryMode != 0 || c.SourcePlayer != 4 || c.Priority != 10 || cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Fossil Lab normal cry bindings differ.");
        var inherited = art.Manifest.GetProperty("audio").GetProperty("inheritedCues").EnumerateArray().Single();
        var aerodactyl = cues.SingleOrDefault(c => c.Id == "CRY_AERODACTYL_NORMAL");
        if (inherited.GetProperty("id").GetString() != "CRY_AERODACTYL_NORMAL" || aerodactyl is null ||
            aerodactyl.Species != 142 || aerodactyl.CryMode != 0 || aerodactyl.SourcePlayer != 4 || aerodactyl.Priority != 10 || aerodactyl.SourcePath != inherited.GetProperty("sourcePath").GetString())
            throw new InvalidDataException("Fossil Lab inherited Aerodactyl cry binding differs.");
        return [.. cues, .. extension.Cues];
    }
}
