using System.Text.Json;
using Game.Content;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendFossilLabCues(string root, bool includeTrainerTower, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeTrainerTower) throw new InvalidDataException("Fossil Lab audio requires the current Trainer Tower audio predecessor.");
        var art = PretFossilLabPresentation.Load(root);
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretFossilLabPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Fossil Lab synth manifest missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != 4 ||
            !extension.Cues.Select(c => c.Species).SequenceEqual(new[] { 138, 139, 140, 141 }) ||
            extension.Cues.Any(c => c.CryMode != 0 || c.SourcePlayer != 4 || c.Priority != 10 || cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Fossil Lab normal cry bindings differ.");
        return [.. cues, .. extension.Cues];
    }
}
