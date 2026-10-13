using System.Text.Json;
using Game.Content;

public partial class SynthSourceAudioPlayer
{
    private sealed record TrainerTowerSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);
    private static SynthCue[] ExtendTrainerTowerCues(string root, bool includePowerPlant, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includePowerPlant) throw new InvalidDataException("Trainer Tower audio requires Power Plant.");
        var source = PretTrainerTowerAudio.LoadSynthManifest(root);
        var extension = source.Deserialize<TrainerTowerSynthManifest>(options) ?? throw new InvalidDataException("Trainer Tower synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != PretTrainerTowerAudio.AdditionalCueCount ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)) || extension.Cues.Select(c => c.Id).Distinct().Count() != extension.Cues.Length ||
            extension.InheritedCues.Length != PretTrainerTowerAudio.InheritedCueCount || extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("Trainer Tower synth source bindings differ.");
        return [.. cues, .. extension.Cues];
    }
}
