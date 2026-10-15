using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>24/48 kHz lossy q0/QOA audio delivery.</summary>
public static class PretSmallAudioDelivery
{
    public const string GeneratedDirectory = "content/generated/small-audio-delivery";
    public const string Profile = "firered-early-delivery-q0-qoa-24-48-v1";
    public const long AudioBytes = 6258027;
    public const int MaximumEncodedBytes = 16 * 1024 * 1024;

    /// <summary>Manifest.SampleRate=0 denotes mixed rates. Use the per-clip SampleRate.</summary>
    public static AudioDeliveryCatalog Load(string projectRoot)
    {
        string directory = PretEarlyAudio.SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        byte[] bytes = File.ReadAllBytes(PretEarlyAudio.SafeFile(directory, "manifest.json"));
        var options = ContentJson.CreateOptions();
        var manifest = ContentJson.ParseStrict<AudioDeliveryManifest>(new UTF8Encoding(false, true).GetString(bytes), options);
        Require(manifest.SchemaVersion == 1 && manifest.SampleRate == 0
            && manifest.FrameRateNumerator == 16777216 && manifest.FrameRateDenominator == 280896
            && !manifest.Clips.IsDefaultOrEmpty,
            "Invalid small delivery manifest.");
        foreach (var clip in manifest.Clips)
        {
            ValidateClip(clip);
            var info = new FileInfo(PretEarlyAudio.SafeFile(directory, clip.Path));
            Require(info.Exists , "Missing/truncated small audio: " + clip.Id);
        }
        return new AudioDeliveryCatalog(directory, manifest, ValidateEncodedBytes);
    }

    /// <summary>Checks codec, sample rate and loop geometry used by playback.</summary>
    public static void ValidateClip(AudioDeliveryClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip); PretEarlyAudio.ValidateClip(clip.Original);
        var s = clip.Original; bool vorbis = clip.Codec == "vorbis";
        Require((vorbis || clip.Codec == "qoa") && vorbis == clip.Id.StartsWith("MUS_", StringComparison.Ordinal)
            && clip.Id == s.Id && clip.Path == "assets/" + clip.Id.ToLowerInvariant() + (vorbis ? ".ogg" : ".qoa")
            && clip.SampleRate is 24000 or 48000 && clip.Channels == 1
,
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

    }

    public static void ValidateEncodedBytes(AudioDeliveryClip clip, byte[] bytes)
    {
        ValidateClip(clip); ArgumentNullException.ThrowIfNull(bytes);
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
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
