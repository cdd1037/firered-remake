using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game.Content;

public sealed record EarlyAudioMeasurements(int PeakPcm16, double RmsPcm16, int NonzeroSampleValues,
    int FirstNonzeroFrame, int LastNonzeroFrame);
public sealed record EarlyAudioClip(string Id, string SourceCommit, string SourceSymbol, string SourcePath, int SourcePlayer, int Priority,
    int? SourceCounter, string Renderer, bool IsOriginalTestFixture, string SourceSoundMode,
    int SourceSoundCntH, string WavPath, int SampleRate, int Channels, int FrameCount, int? LoopStartFrame,
    int? LoopEndFrame, JsonElement? SourceLoop, int? SourceSequenceFirstStoppedVBlank, string TracePath,
    EarlyAudioMeasurements Measurements, bool AudibleOutputVerified);
public sealed record EarlyAudioManifest(int SchemaVersion, int FrameRateNumerator, int FrameRateDenominator, ImmutableArray<EarlyAudioClip> Clips,
    ImmutableArray<string> Limitations);

/// <summary>Audio metadata with WAV bytes read on demand.</summary>
public sealed class EarlyAudioCatalog
{
    private readonly string _directory;
    public EarlyAudioManifest Manifest { get; }
    public ImmutableDictionary<string, EarlyAudioClip> Clips { get; }
    internal EarlyAudioCatalog(string directory, EarlyAudioManifest manifest)
    {
        _directory = directory; Manifest = manifest;
        Clips = manifest.Clips.ToImmutableDictionary(c => c.Id, StringComparer.Ordinal);
    }

    public byte[] ReadWav(string id)
    {
        if (!Clips.TryGetValue(id, out var clip)) throw new InvalidDataException("Unknown original audio ID: " + id);
        var bytes = File.ReadAllBytes(PretEarlyAudio.SafeFile(_directory, clip.WavPath));
        PretEarlyAudio.ValidateWav(clip, bytes);

        return bytes;
    }
}

/// <summary>Fixed FireRed revision-0 m4a renders. No synthesizer, emulator or substitute-tone fallback.</summary>
public static class PretEarlyAudio
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Renderer = "mGBA-0.10.5-original-FireRed-rev0-m4a-offline-v1";
    public const string GeneratedDirectory = "content/generated/early-audio";

    /// <summary>Loads generated clip metadata; audio bytes are read when played.</summary>
    public static EarlyAudioCatalog Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = SafeFile(project, GeneratedDirectory);
        var bytes = ReadPinned(directory, "manifest.json");
        var manifest = ContentJson.ParseStrict<EarlyAudioManifest>(new UTF8Encoding(false, true).GetString(bytes), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && manifest.FrameRateNumerator == 16777216
            && manifest.FrameRateDenominator == 280896 && manifest.Clips.Length == 134
            && manifest.Clips.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == 134
            && manifest.Limitations.Length == 3 && manifest.Limitations.All(s => !string.IsNullOrWhiteSpace(s)),
            "Original audio manifest identity or complete inventory differs.");
        foreach (var clip in manifest.Clips)
        {
            ValidateClip(clip);
            if (!File.Exists(SafeFile(directory, clip.WavPath)))
                throw new FileNotFoundException("Missing original WAV: " + clip.Id, clip.WavPath);
        }
        return new EarlyAudioCatalog(directory, manifest);
    }

    /// <summary>Structural validation is separate from Load's pinned-manifest authentication.</summary>
    public static void ValidateClip(EarlyAudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        Require(Regex.IsMatch(clip.Id, @"\A(?:MUS_|SE_|CRY_)[A-Z0-9_]+\z")
&& !string.IsNullOrWhiteSpace(clip.SourceSymbol)
            && clip.SourcePath.StartsWith("sound/", StringComparison.Ordinal) && IsRelative(clip.SourcePath)
            && clip.Renderer == Renderer && !clip.IsOriginalTestFixture && !clip.AudibleOutputVerified
            && clip.SourcePlayer is >= 0 and <= 4 && clip.Priority is >= 0 and <= 255
            && (clip.SourceCounter is null or > 0) && clip.SourceSoundMode == "mono" && clip.SourceSoundCntH == 0x3302
            && clip.SampleRate == 48000 && clip.Channels == 2 && clip.FrameCount > 0
            && clip.WavPath == clip.Id.ToLowerInvariant() + ".wav" && clip.TracePath == clip.Id.ToLowerInvariant() + ".csv"
,
            "Invalid original audio metadata: " + clip.Id);
        Require(clip.LoopStartFrame.HasValue == clip.LoopEndFrame.HasValue
            && (!clip.LoopStartFrame.HasValue || (clip.LoopStartFrame >= 0 && clip.LoopStartFrame < clip.LoopEndFrame
                && clip.LoopEndFrame <= clip.FrameCount))
            && (clip.SourceLoop.HasValue == clip.LoopStartFrame.HasValue)
            && (clip.SourcePlayer != 4 || (clip.SourceCounter is null && clip.LoopStartFrame is null))
            && (clip.SourceCounter is null || (clip.SourcePlayer == 2 && clip.LoopStartFrame is null)),
            "Invalid original audio loop/counter: " + clip.Id);
        var m = clip.Measurements;
        Require(m is not null && m.PeakPcm16 is > 0 and <= 32768 && double.IsFinite(m.RmsPcm16)
            && m.RmsPcm16 > 0 && m.RmsPcm16 <= m.PeakPcm16 && m.NonzeroSampleValues > 0
            && m.NonzeroSampleValues <= (long)clip.FrameCount * 2 && m.FirstNonzeroFrame >= 0
            && m.FirstNonzeroFrame <= m.LastNonzeroFrame && m.LastNonzeroFrame < clip.FrameCount,
            "Silent/invalid original PCM measurements: " + clip.Id);
    }

    /// <summary>Accepts only the exact PCM16 RIFF layout emitted by the frozen renderer.</summary>
    public static void ValidateWav(EarlyAudioClip clip, byte[] wav)
    {
        ValidateClip(clip);
        ArgumentNullException.ThrowIfNull(wav);
        var b = wav.AsSpan();
        Require(b.Length == 44L + (long)clip.FrameCount * 4 && b.Length >= 48
            && b[..4].SequenceEqual("RIFF"u8) && U32(b, 4) == b.Length - 8 && b.Slice(8, 4).SequenceEqual("WAVE"u8)
            && b.Slice(12, 4).SequenceEqual("fmt "u8) && U32(b, 16) == 16 && U16(b, 20) == 1
            && U16(b, 22) == clip.Channels && U32(b, 24) == clip.SampleRate && U32(b, 28) == clip.SampleRate * 4
            && U16(b, 32) == 4 && U16(b, 34) == 16 && b.Slice(36, 4).SequenceEqual("data"u8)
            && U32(b, 40) == b.Length - 44, "Invalid original PCM16 WAV structure: " + clip.Id);
        bool nonzero = false;
        for (int i = 44; i < b.Length && !nonzero; i++) nonzero = b[i] != 0;
        Require(nonzero, "Original WAV is silent: " + clip.Id);
    }

    internal static byte[] ReadPinned(string root, string path)
    {
        var bytes = File.ReadAllBytes(SafeFile(root, path));
        return bytes;
    }
    internal static string SafeFile(string root, string relative)
    {
        Require(IsRelative(relative), "Unsafe original audio path: " + relative);
        var current = Path.GetFullPath(root);
        Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Symlink in original audio root.");
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Symlink in original audio path: " + relative);
        }
        return current;
    }
    private static bool IsRelative(string value) => !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value)
        && !value.Contains('\\') && !value.Contains(':') && value.Split('/').All(p => p.Length > 0 && p is not "." and not "..");
    private static uint U32(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private static ushort U16(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b[offset..]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
