using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendFieldFlyCues(string root, SynthCue[] cues,
        JsonSerializerOptions options)
    {
        string path = Path.Combine(Path.GetFullPath(root), "content/generated/audio/cues/field-fly.json");
        byte[] bytes = File.ReadAllBytes(path);
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("FieldFly synth manifest missing.");
        var expected = new SynthCue("SE_M_FLY", "SE_M_FLY", 1, 4, null, 0x086B89CC, 0, false, 0);
        if (extension.SchemaVersion != 1 ||
            extension.Cues is not [var cue] || cue != expected || cues.Any(c => c.Id == expected.Id))
            throw new InvalidDataException("FieldFly sound binding differs.");
        // The constructor validates the song header in the source pack.
        // SE1 uses the existing effect path, with no independent fanfare counter.
        return [.. cues, cue];
    }
}
