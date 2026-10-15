using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendMoveDeleterCues(string root, bool includeSeafoam,
        SynthCue[] cues, JsonSerializerOptions options)
    {
        if (!includeSeafoam)
            throw new InvalidDataException("Move Deleter audio requires its current Seafoam predecessor.");
        string path = Path.Combine(Path.GetFullPath(root), "content/generated/audio/cues/move-deleter.json");
        byte[] bytes = File.ReadAllBytes(path);
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Move Deleter synth manifest missing.");
        var expected = new SynthCue("MUS_MOVE_DELETED", "MUS_MOVE_DELETED", 2, 5, 180, 0x086C0204, 0, false, 0);
        if (extension.SchemaVersion != 1 ||
            extension.Cues is not [var cue] || cue != expected || cues.Any(c => c.Id == expected.Id))
            throw new InvalidDataException("Move Deleter fanfare binding differs.");
        // Initialize loads the source pack and reads each song header.
        // Its existing fanfare task owns the 180 decrements and separate zero-counter frame.
        return [.. cues, cue];
    }
}
