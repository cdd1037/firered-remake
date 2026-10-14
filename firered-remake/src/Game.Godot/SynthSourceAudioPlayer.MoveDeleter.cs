using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendMoveDeleterCues(string root, bool includeSeafoam,
        SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeSeafoam)
            throw new InvalidDataException("Move Deleter audio requires its current Seafoam predecessor.");
        if (sourceCommit != MoveDeleterSynthManifestIdentity.SourceCommit)
            throw new InvalidDataException("Move Deleter audio source revision differs.");
        string path = Path.Combine(Path.GetFullPath(root), MoveDeleterSynthManifestIdentity.Path);
        if (new FileInfo(path).Length != MoveDeleterSynthManifestIdentity.Bytes)
            throw new InvalidDataException("Move Deleter synth manifest length differs.");
        byte[] bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != MoveDeleterSynthManifestIdentity.Sha256)
            throw new InvalidDataException("Move Deleter synth manifest identity differs.");
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Move Deleter synth manifest missing.");
        var expected = new SynthCue("MUS_MOVE_DELETED", sourceCommit, "MUS_MOVE_DELETED",
            "sound/songs/midi/mus_move_deleted.mid", 2, 5, 180, 0x086C0204, 0, false, 0);
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit ||
            extension.Cues is not [var cue] || cue != expected || cues.Any(c => c.Id == expected.Id))
            throw new InvalidDataException("Move Deleter fanfare binding differs.");
        // Initialize authenticates the existing pack before extensions, then reads each song header.
        // Its existing fanfare task owns the 180 decrements and separate zero-counter frame.
        return [.. cues, cue];
    }
}
