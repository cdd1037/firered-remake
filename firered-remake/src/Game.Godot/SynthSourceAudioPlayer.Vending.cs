using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendVendingCues(string root, SynthCue[] cues,
        JsonSerializerOptions options, string sourceCommit)
    {
        if (sourceCommit != VendingSynthManifestIdentity.SourceCommit)
            throw new InvalidDataException("Vending audio source revision differs.");
        string path = Path.Combine(Path.GetFullPath(root), VendingSynthManifestIdentity.Path);
        using var stream = File.OpenRead(path);
        if (stream.Length != VendingSynthManifestIdentity.Bytes)
            throw new InvalidDataException("Vending synth manifest length differs.");
        byte[] bytes = new byte[VendingSynthManifestIdentity.Bytes];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || stream.Length != bytes.Length ||
            Convert.ToHexStringLower(SHA256.HashData(bytes)) != VendingSynthManifestIdentity.Sha256)
            throw new InvalidDataException("Vending synth manifest identity differs.");
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Vending synth manifest missing.");
        var expected = new SynthCue("SE_VEND", sourceCommit, "SE_VEND",
            "sound/songs/midi/se_vend.mid", 1, 4, null, 0x086B7154, 0, false, 0);
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit ||
            extension.Cues is not [var cue] || cue != expected || cues.Any(c => c.Id == expected.Id))
            throw new InvalidDataException("Vending sound binding differs.");
        // The constructor authenticates the unchanged pack and validates this song header.
        // SE1 uses the existing effect path, with no independent fanfare counter.
        return [.. cues, cue];
    }
}
