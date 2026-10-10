using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Independent 24/48 kHz lossy q0/QOA delivery. Original q6 and source authentication remain unchanged.</summary>
public static class PretSmallAudioDelivery
{
    public const string GeneratedDirectory = "content/generated/small-audio-delivery";
    public const string Profile = "firered-early-delivery-q0-qoa-24-48-v1";
    public const string ManifestSha256 = "bd9e9027ad41aa16b10d7bea0a3aebed77a0901f593c531ff002a7ec8bc4360d";
    public const string TimingSha256 = "67c8996d160bb302f27ace4fd2de5e7a16db14b1caa2a88fd4cc15e02f84182f";
    public const string FrozenSha256 = "0f278728df3bce2cb665abbee10511c06559694f1751ee5f6ed53391945e6c00";
    public const string CandidateManifestSha256 = "bc517cd9e0063068b743cb5e236a698125d43254cd3a7b837517a0a27912ab57";
    public const long AudioBytes = 6258027;
    public const long PackageBytes = 7287741;
    public const int MaximumEncodedBytes = 16 * 1024 * 1024;

    /// <summary>Manifest.SampleRate=0 denotes mixed rates. Always use the authenticated per-clip SampleRate.</summary>
    public static AudioDeliveryCatalog Load(string projectRoot)
    {
        string directory = PretEarlyAudio.SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        ReadMetadata(directory, "source-lock.json", PretEarlyAudio.SourceLockSha256,488695);
        ReadMetadata(directory, "source-FROZEN.json", PretEarlyAudio.FrozenSha256,1984);
        ReadMetadata(directory, "FROZEN.json", FrozenSha256,396);
        ReadMetadata(directory, "timing.json", TimingSha256,49129);
        byte[] sourceBytes = ReadMetadata(directory, "source-manifest.json", PretEarlyAudio.ManifestSha256,174967);
        byte[] bytes = ReadMetadata(directory, "manifest.json", ManifestSha256,314543);
        var options = ContentJson.CreateOptions();
        var manifest = ContentJson.ParseStrict<AudioDeliveryManifest>(new UTF8Encoding(false, true).GetString(bytes), options);
        var source = ContentJson.ParseStrict<EarlyAudioManifest>(new UTF8Encoding(false, true).GetString(sourceBytes), options);
        Require(manifest.SchemaVersion == 1 && manifest.Profile == Profile
            && manifest.SourceManifestSha256 == PretEarlyAudio.ManifestSha256 && manifest.SourceLockSha256 == PretEarlyAudio.SourceLockSha256
            && manifest.SourceFrozenSha256 == PretEarlyAudio.FrozenSha256 && manifest.SampleRate == 0
            && manifest.FrameRateNumerator == 16777216 && manifest.FrameRateDenominator == 280896
            && manifest.AudioBytes == AudioBytes && manifest.Clips.Length == 134
            && manifest.Clips.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == 134
            && manifest.Clips.Count(c => c.Codec == "vorbis") == 31 && manifest.Clips.Count(c => c.Codec == "qoa") == 103
            && manifest.Clips.Count(c => c.SampleRate == 24000) == 111 && manifest.Clips.Count(c => c.SampleRate == 48000) == 23
            && manifest.Limitations.Length == 5 && manifest.Limitations.All(s => !string.IsNullOrWhiteSpace(s)),
            "Small delivery frozen identity/inventory differs.");
        var originals = source.Clips.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Require(originals.Count == 134, "Incomplete original metadata inventory.");
        long total = 0;
        foreach (var clip in manifest.Clips)
        {
            ValidateClip(clip);
            Require(originals.TryGetValue(clip.Id, out var original)
                && ContentJson.Canonicalize(JsonSerializer.SerializeToElement(original, options))
                    == ContentJson.Canonicalize(JsonSerializer.SerializeToElement(clip.Original, options)),
                "Small delivery source differs from frozen original: " + clip.Id);
            var info = new FileInfo(PretEarlyAudio.SafeFile(directory, clip.Path));
            Require(info.Exists && info.Length == clip.ByteLength, "Missing/truncated small audio: " + clip.Id);
            total = checked(total + info.Length);
        }
        Require(total == AudioBytes, "Small delivery audio total differs.");
        return new AudioDeliveryCatalog(directory, manifest, ValidateEncodedBytes);
    }

    private static byte[] ReadMetadata(string directory,string path,string hash,int expectedBytes)
    {
        Require(expectedBytes is >0 and <=2*1024*1024,"Audio metadata bound invalid.");
        using var stream=new FileStream(PretEarlyAudio.SafeFile(directory,path),FileMode.Open,FileAccess.Read,FileShare.Read);
        Require(stream.Length==expectedBytes,"Audio metadata pinned length differs: "+path);
        byte[] bytes=new byte[expectedBytes];stream.ReadExactly(bytes);
        Require(stream.ReadByte()==-1&&stream.Length==expectedBytes,"Audio metadata changed during bounded read: "+path);
        Require(Convert.ToHexStringLower(SHA256.HashData(bytes))==hash,"Audio metadata pinned digest differs: "+path);
        return bytes;
    }

    /// <summary>Structural validation is not authentication; Load pins the exact original and derivative manifests.</summary>
    public static void ValidateClip(AudioDeliveryClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip); PretEarlyAudio.ValidateClip(clip.Original);
        var s = clip.Original; bool vorbis = clip.Codec == "vorbis";
        Require((vorbis || clip.Codec == "qoa") && vorbis == clip.Id.StartsWith("MUS_", StringComparison.Ordinal)
            && clip.Id == s.Id && clip.Path == "assets/" + clip.Id.ToLowerInvariant() + (vorbis ? ".ogg" : ".qoa")
            && clip.SampleRate is 24000 or 48000 && clip.Channels == 1
            && clip.ByteLength is > 0 and <= MaximumEncodedBytes && Digest(clip.Sha256) && clip.PcmSha256 is null,
            "Invalid small delivery identity/codec: " + clip.Id);
        int divisor = 48000 / clip.SampleRate;
        int sourceFrames = vorbis && s.LoopEndFrame.HasValue ? s.LoopEndFrame.Value : s.FrameCount;
        int? begin = s.LoopStartFrame.HasValue ? (s.LoopStartFrame.Value + divisor - 1) / divisor : null;
        int? end = s.LoopEndFrame.HasValue ? (s.LoopEndFrame.Value + divisor - 1) / divisor : null;
        Require(clip.LogicalFrameCount == (vorbis ? (sourceFrames + divisor - 1) / divisor : sourceFrames / divisor)
            && clip.LoopStartFrame == begin && clip.LoopEndFrame == end
            && clip.LoopOffsetSeconds == (begin.HasValue ? (double)begin.Value / clip.SampleRate : null)
            && clip.LogicalFrameCount > 0 && clip.LogicalFrameCount <= clip.SampleRate * 600
            && begin.HasValue == end.HasValue && !(begin < 0) && !(begin >= end) && !(end > clip.LogicalFrameCount),
            "Small delivery frame/loop quantization differs: " + clip.Id);
        Require(clip.Transform == new AudioDeliveryTransform("left; L=R verified over every source frame", clip.SampleRate != 48000, false,
            vorbis && end.HasValue ? "exclusive source loop end; target-frame quantization" : "target-frame quantization only",
            vorbis ? "ffmpeg 7.1.5 / libvorbis q0" : "Godot 4.7.2 AudioStreamWAV QOA")
            && clip.Acceptance == new AudioDeliveryAcceptance(true, true, false, false, false, false),
            "Unexpected small delivery transform/acceptance claim: " + clip.Id);
    }

    public static void ValidateEncodedBytes(AudioDeliveryClip clip, byte[] bytes)
    {
        ValidateClip(clip); ArgumentNullException.ThrowIfNull(bytes);
        Require(bytes.Length == clip.ByteLength && Hash(bytes) == clip.Sha256, "Small delivery encoded digest/size differs: " + clip.Id);
        var b = bytes.AsSpan();
        if (clip.Codec == "qoa") { ValidateQoaData(b, clip.SampleRate, clip.Channels, clip.LogicalFrameCount); return; }
        Require(b.Length >= 58 && b[..4].SequenceEqual("OggS"u8) && b[4] == 0 && b[5] == 2
            && b[26] == 1 && b[27] == 30 && b.Slice(28, 7).SequenceEqual("\x01vorbis"u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(b[35..]) == 0 && b[39] == clip.Channels
            && BinaryPrimitives.ReadUInt32LittleEndian(b[40..]) == clip.SampleRate, "Invalid small delivery Vorbis identification.");
        int offset = 0; long lastGranule = -1; bool eos = false;
        while (offset < b.Length)
        {
            Require(!eos && b.Length - offset >= 27 && b.Slice(offset, 4).SequenceEqual("OggS"u8) && b[offset + 4] == 0,
                "Invalid/truncated Ogg page.");
            int count = b[offset + 26]; Require(b.Length - offset >= 27 + count, "Truncated Ogg lacing.");
            int length = 27 + count;
            for (int i = 0; i < count; i++) length += b[offset + 27 + i];
            Require(b.Length - offset >= length, "Truncated Ogg payload.");
            lastGranule = BinaryPrimitives.ReadInt64LittleEndian(b[(offset + 6)..]); eos = (b[offset + 5] & 4) != 0; offset += length;
        }
        Require(eos && lastGranule == clip.LogicalFrameCount, "Ogg terminal granule differs from logical frames.");
    }

    /// <summary>Pure QOA Data container only. Rejects arbitrary Godot resources, streaming/variable layouts and trailing data.</summary>
    public static void ValidateQoaData(ReadOnlySpan<byte> b, int rate, int channels, int frames)
    {
        Require(rate is >= 8000 and <= 192000 && channels is 1 or 2 && frames > 0 && frames <= rate * 600
            && b.Length is >= 32 and <= MaximumEncodedBytes && b[..4].SequenceEqual("qoaf"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(b[4..]) == frames, "Invalid QOA file header/limits.");
        int offset = 8, consumed = 0;
        while (offset < b.Length)
        {
            Require(b.Length - offset >= 8, "Truncated QOA frame header.");
            var header = b.Slice(offset, 8);
            int frameRate = header[1] << 16 | header[2] << 8 | header[3];
            int samples = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
            int size = BinaryPrimitives.ReadUInt16BigEndian(header[6..]);
            Require(header[0] == channels && frameRate == rate && samples == Math.Min(5120, frames - consumed) && samples > 0,
                "QOA frame format/sample count differs.");
            Require(size == 8 + 16 * channels + 8 * channels * ((samples + 19) / 20) && b.Length - offset >= size,
                "Invalid/truncated QOA frame size.");
            offset += size; consumed += samples;
        }
        Require(offset == b.Length && consumed == frames, "QOA total sample count differs.");
    }
    private static bool Digest(string? s) => s is { Length: 64 } && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
