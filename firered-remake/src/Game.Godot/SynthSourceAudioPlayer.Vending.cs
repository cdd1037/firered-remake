using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendVendingCues(string root, SynthCue[] cues,
        JsonSerializerOptions options, string sourceCommit)
    {
        string path = Path.Combine(Path.GetFullPath(root), "content/generated/vending-sound/synth-manifest.json");
        byte[] bytes = File.ReadAllBytes(path);
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Vending synth manifest missing.");
        var expected = new SynthCue("SE_VEND", sourceCommit, "SE_VEND",
            "sound/songs/midi/se_vend.mid", 1, 4, null, 0x086B7154, 0, false, 0);
        if (extension.SchemaVersion != 1 ||
            extension.Cues is not [var cue] || cue != expected || cues.Any(c => c.Id == expected.Id))
            throw new InvalidDataException("Vending sound binding differs.");
        // The constructor validates the song header in the source pack.
        // SE1 uses the existing effect path, with no independent fanfare counter.
        return [.. cues, cue];
    }
}
