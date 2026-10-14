using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendFieldFlyCues(string root, SynthCue[] cues,
        JsonSerializerOptions options, string sourceCommit)
    {
        if (sourceCommit != FieldFlySynthManifestIdentity.SourceCommit)
            throw new InvalidDataException("FieldFly audio source revision differs.");
        string path = Path.Combine(Path.GetFullPath(root), FieldFlySynthManifestIdentity.Path);
        using var stream = File.OpenRead(path);
        if (stream.Length != FieldFlySynthManifestIdentity.Bytes)
            throw new InvalidDataException("FieldFly synth manifest length differs.");
        byte[] bytes = new byte[FieldFlySynthManifestIdentity.Bytes];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || stream.Length != bytes.Length ||
            Convert.ToHexStringLower(SHA256.HashData(bytes)) != FieldFlySynthManifestIdentity.Sha256)
            throw new InvalidDataException("FieldFly synth manifest identity differs.");
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("FieldFly synth manifest missing.");
        var expected = new SynthCue("SE_M_FLY", sourceCommit, "SE_M_FLY",
            "sound/songs/midi/se_m_fly.mid", 1, 4, null, 0x086B89CC, 0, false, 0);
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit ||
            extension.Cues is not [var cue] || cue != expected || cues.Any(c => c.Id == expected.Id))
            throw new InvalidDataException("FieldFly sound binding differs.");
        // The constructor authenticates the unchanged pack and validates this song header.
        // SE1 uses the existing effect path, with no independent fanfare counter.
        return [.. cues, cue];
    }
}
