using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Content;

public sealed record AudioDeliveryTransform(string ChannelSelection, bool Resample, bool Normalization,
    string TailTrim, string Encoder);
public sealed record AudioDeliveryAcceptance(bool EncodedHashVerified, bool DecodedFrameCountVerified,
    bool LosslessToSourceChannel, bool GodotPlaybackVerified, bool AudibleOutputVerified, bool SeamlessLoopVerified);
public sealed record AudioDeliveryClip(string Id, string Path, string Codec, int SampleRate, int Channels, int LogicalFrameCount,
    int? LoopStartFrame, int? LoopEndFrame, double? LoopOffsetSeconds, AudioDeliveryTransform Transform,
    [property: JsonPropertyName("source")] EarlyAudioClip Original, AudioDeliveryAcceptance Acceptance);
public sealed record AudioDeliveryManifest(int SchemaVersion, string Profile, int SampleRate, int FrameRateNumerator, int FrameRateDenominator,
    long AudioBytes, ImmutableArray<AudioDeliveryClip> Clips, ImmutableArray<string> Limitations);

/// <summary>Generated playback catalog; media is decoded when requested.</summary>
public sealed class AudioDeliveryCatalog
{
    private readonly string _directory;
    private readonly Action<AudioDeliveryClip, byte[]> _validateEncodedBytes;
    public AudioDeliveryManifest Manifest { get; }
    public ImmutableDictionary<string, AudioDeliveryClip> Clips { get; }
    internal AudioDeliveryCatalog(string directory, AudioDeliveryManifest manifest,
        Action<AudioDeliveryClip, byte[]>? validateEncodedBytes = null)
    {
        _directory = directory; Manifest = manifest;
        _validateEncodedBytes = validateEncodedBytes ?? PretAudioDelivery.ValidateEncodedBytes;
        Clips = manifest.Clips.ToImmutableDictionary(c => c.Id, StringComparer.Ordinal);
    }
    public byte[] ReadEncodedBytes(string id)
    {
        if (!Clips.TryGetValue(id, out var clip)) throw new InvalidDataException("Unknown delivery audio ID: " + id);
        var bytes = File.ReadAllBytes(PretEarlyAudio.SafeFile(_directory, clip.Path));
        _validateEncodedBytes(clip, bytes);
        return bytes;
    }
}

/// <summary>The approved 31 Vorbis / 103 lossless mono PCM derivatives; never substitutes their hashes for original WAVs.</summary>
public static class PretAudioDelivery
{
    public const string GeneratedDirectory = "content/generated/audio-delivery";
    public const string Profile = "firered-early-delivery-q6-mono-v1";

    public static AudioDeliveryCatalog Load(string projectRoot)
    {
        string directory = PretEarlyAudio.SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        byte[] bytes = PretEarlyAudio.ReadPinned(directory,"manifest.json");
        var options = ContentJson.CreateOptions();
        var manifest = ContentJson.ParseStrict<AudioDeliveryManifest>(new UTF8Encoding(false, true).GetString(bytes), options);

        Require(manifest.SchemaVersion == 1             && manifest.SampleRate == 48000 && manifest.FrameRateNumerator == 16777216 && manifest.FrameRateDenominator == 280896
            && manifest.Clips.Length == 134
            && manifest.Clips.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == 134
            && manifest.Clips.Count(c => c.Codec == "vorbis") == 31
            && manifest.Clips.Count(c => c.Codec == "pcm_s16le_wav") == 103
            && manifest.Limitations.Length == 5 && manifest.Limitations.All(s => !string.IsNullOrWhiteSpace(s)),
            "Frozen delivery identity or inventory differs.");
        foreach (var clip in manifest.Clips)
        {
            ValidateClip(clip);
            if (!File.Exists(PretEarlyAudio.SafeFile(directory, clip.Path)))
                throw new FileNotFoundException("Missing delivery audio: " + clip.Id, clip.Path);
        }
        return new AudioDeliveryCatalog(directory, manifest);
    }

    /// <summary>Structural checks alone do not authenticate a manifest. Load performs the pinned authentication.</summary>
    public static void ValidateClip(AudioDeliveryClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        PretEarlyAudio.ValidateClip(clip.Original);
        var s = clip.Original; bool vorbis = clip.Codec == "vorbis";
        Require((vorbis || clip.Codec == "pcm_s16le_wav") && vorbis == clip.Id.StartsWith("MUS_", StringComparison.Ordinal)
            && clip.Id == s.Id && clip.Path == "assets/" + clip.Id.ToLowerInvariant() + (vorbis ? ".ogg" : ".wav")
            && clip.SampleRate == 48000 && clip.Channels == 1 , "Invalid delivery codec/identity: " + clip.Id);
        Require(clip.LoopStartFrame == s.LoopStartFrame && clip.LoopEndFrame == s.LoopEndFrame
            && clip.LogicalFrameCount == (vorbis && s.LoopEndFrame.HasValue ? s.LoopEndFrame.Value : s.FrameCount)
            && clip.LoopOffsetSeconds == (s.LoopStartFrame.HasValue ? s.LoopStartFrame.Value / 48000d : (double?)null)
            && (!vorbis || !s.LoopEndFrame.HasValue || s.FrameCount == s.LoopEndFrame.Value + 1),
            "Delivery loop/frame count differs: " + clip.Id);
        Require(clip.Transform == new AudioDeliveryTransform("left; L=R verified over every source frame", false, false,
            vorbis && s.LoopEndFrame.HasValue ? "one excluded frame at source loopEnd" : "none",
            vorbis ? "ffmpeg 7.1.5 / libvorbis q6" : "Python wave PCM16 mono")
            && clip.Acceptance == new AudioDeliveryAcceptance(true, true, !vorbis, false, false, false),
            "Unexpected transformation or acceptance claim: " + clip.Id);
    }

    public static void ValidateEncodedBytes(AudioDeliveryClip clip, byte[] bytes)
    {
        ValidateClip(clip); ArgumentNullException.ThrowIfNull(bytes);
        var b = bytes.AsSpan();
        if (clip.Codec == "vorbis")
        {
            Require(b.Length >= 58 && b[..4].SequenceEqual("OggS"u8) && b[4] == 0 && b[5] == 2
                && b[26] == 1 && b[27] == 30 && b.Slice(28, 7).SequenceEqual("\x01vorbis"u8)
                && U32(b, 35) == 0 && b[39] == clip.Channels && U32(b, 40) == clip.SampleRate,
                "Invalid delivery Vorbis identification: " + clip.Id);
            int offset = 0; long lastGranule = -1; bool eos = false;
            while (offset < b.Length)
            {
                Require(!eos && b.Length - offset >= 27 && b.Slice(offset, 4).SequenceEqual("OggS"u8) && b[offset + 4] == 0,
                    "Invalid/truncated Ogg page.");
                int count = b[offset + 26];
                Require(b.Length - offset >= 27 + count, "Truncated Ogg lacing.");
                int length = 27 + count;
                for (int i = 0; i < count; i++) length = checked(length + b[offset + 27 + i]);
                Require(b.Length - offset >= length, "Truncated Ogg payload.");
                lastGranule = BinaryPrimitives.ReadInt64LittleEndian(b[(offset + 6)..]);
                eos = (b[offset + 5] & 4) != 0; offset += length;
            }
            Require(eos && lastGranule == clip.LogicalFrameCount, "Ogg end granule differs from logical frames.");
        }
        else
        {
            Require(b.Length == 44L + clip.LogicalFrameCount * 2L && b.Length >= 46
                && b[..4].SequenceEqual("RIFF"u8) && U32(b, 4) == b.Length - 8 && b.Slice(8, 4).SequenceEqual("WAVE"u8)
                && b.Slice(12, 4).SequenceEqual("fmt "u8) && U32(b, 16) == 16 && U16(b, 20) == 1
                && U16(b, 22) == 1 && U32(b, 24) == 48000 && U32(b, 28) == 96000
                && U16(b, 32) == 2 && U16(b, 34) == 16 && b.Slice(36, 4).SequenceEqual("data"u8)
                && U32(b, 40) == b.Length - 44 ,
                "Invalid delivery PCM16 WAV: " + clip.Id);
            Require(b[44..].ContainsAnyExcept((byte)0), "Silent delivery PCM: " + clip.Id);
        }
    }
    private static uint U32(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private static ushort U16(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b[offset..]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
