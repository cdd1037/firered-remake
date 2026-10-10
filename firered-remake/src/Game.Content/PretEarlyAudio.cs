using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game.Content;

public sealed record EarlyAudioMeasurements(int PeakPcm16, double RmsPcm16, int NonzeroSampleValues,
    int FirstNonzeroFrame, int LastNonzeroFrame);
public sealed record EarlyAudioClip(string Id, string SourceCommit, string SourceSymbol, string SourcePath,
    int SourcePlayer, int Priority, int? SourceCounter, string Renderer, bool IsOriginalTestFixture,
    string SourceSoundMode, int SourceSoundCntH, string WavPath, string WavSha256, string PcmSha256,
    int SampleRate, int Channels, int FrameCount, int? LoopStartFrame, int? LoopEndFrame,
    JsonElement? SourceLoop, int? SourceSequenceFirstStoppedVBlank, string TracePath, string TraceSha256,
    EarlyAudioMeasurements Measurements, bool AudibleOutputVerified);
public sealed record EarlyAudioManifest(int SchemaVersion, string SourceLockSha256, string RomSha1,
    string RendererSourceSha256, string RenderBinarySha256, int FrameRateNumerator,
    int FrameRateDenominator, ImmutableArray<EarlyAudioClip> Clips, ImmutableArray<string> Limitations);

/// <summary>Authenticated metadata, with WAV/trace bytes verified on demand and never cached here.</summary>
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
        PretEarlyAudio.ReadPinned(_directory, clip.TracePath, clip.TraceSha256);
        return bytes;
    }
}

/// <summary>Fixed FireRed revision-0 m4a renders. No synthesizer, emulator or substitute-tone fallback.</summary>
public static class PretEarlyAudio
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceLockSha256 = "6bd7cb1ea5e5f01d74781ab15ba290cde8324f4a29c5dbeaad5bd6e520b521be";
    public const string ManifestSha256 = "cd4d857bb6e220925834302119ae66efb1cfdc4a014f49053dbee1956a62aaec";
    public const string FrozenSha256 = "55b52063287673387d84037049f95c3a3a8de8700ec9c680eea88ea7b9829818";
    public const string VerificationSha256 = "2cf0a6a44cacd6f87b60d24766016dd4bf7f21b62651e24c6ebd73a80f556667";
    public const string RomSha1 = "41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc";
    public const string Renderer = "mGBA-0.10.5-original-FireRed-rev0-m4a-offline-v1";
    public const string RendererSourceSha256 = "a8267d4ff5244c688f7a22a82a684134f9b731f066cd8ee122718be06a98db7f";
    public const string RenderBinarySha256 = "3d158c1301984157a724f37418ea12a6caadbcba2bd9a2b83955d4f15daff0fb";
    public const string GeneratedDirectory = "content/generated/early-audio";

    /// <summary>
    /// Checks frozen metadata, complete source inventory and all file paths/sizes. PCM hashes are checked
    /// by ReadWav before a clip can be played; loading the catalog does not read 289 MB of audio.
    /// </summary>
    public static EarlyAudioCatalog Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = SafeFile(project, GeneratedDirectory);
        var lockBytes = ReadPinned(project, "content/early-audio-source-lock.json", SourceLockSha256);
        ReadPinned(directory, "source-lock.json", SourceLockSha256);
        using var frozen = JsonDocument.Parse(ReadPinned(directory, "FROZEN.json", FrozenSha256));
        ReadPinned(directory, "verification.json", VerificationSha256);
        foreach (var input in frozen.RootElement.GetProperty("rendererSources").EnumerateArray())
            ReadPinned(directory, input.GetProperty("path").GetString()!, input.GetProperty("sha256").GetString()!);
        var bytes = ReadPinned(directory, "manifest.json", ManifestSha256);
        var manifest = ContentJson.ParseStrict<EarlyAudioManifest>(new UTF8Encoding(false, true).GetString(bytes), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && manifest.SourceLockSha256 == SourceLockSha256
            && manifest.RomSha1 == RomSha1 && manifest.RendererSourceSha256 == RendererSourceSha256
            && manifest.RenderBinarySha256 == RenderBinarySha256 && manifest.FrameRateNumerator == 16777216
            && manifest.FrameRateDenominator == 280896 && manifest.Clips.Length == 134
            && manifest.Clips.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == 134
            && manifest.Limitations.Length == 3 && manifest.Limitations.All(s => !string.IsNullOrWhiteSpace(s)),
            "Original audio manifest identity or complete inventory differs.");
        using var sourceLock = JsonDocument.Parse(lockBytes);
        var locked = sourceLock.RootElement;
        Require(locked.GetProperty("schemaVersion").GetInt32() == 1
            && locked.GetProperty("profile").GetString() == "firered-early-audio-source-v1"
            && locked.GetProperty("source").GetProperty("repository").GetString() == "pret/pokefirered"
            && locked.GetProperty("source").GetProperty("commit").GetString() == SourceCommit,
            "Original audio source lock identity differs.");
        var expected = new Dictionary<string, (string Symbol, string Path, int Player, int Priority, int? Counter)>(StringComparer.Ordinal);
        foreach (var song in locked.GetProperty("songs").EnumerateArray())
        {
            var symbol = song.GetProperty("symbol").GetString()!;
            var priority = Regex.Match(song.GetProperty("mid2agbOptions").GetString()!, @"(?:^|\s)-P(\d+)(?:\s|$)");
            var counter = song.GetProperty("fanfareCounterFrames");
            expected.Add(symbol, (symbol, song.GetProperty("path").GetString()!, song.GetProperty("player").GetInt32(),
                priority.Success ? int.Parse(priority.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0,
                counter.ValueKind == JsonValueKind.Null ? null : counter.GetInt32()));
        }
        foreach (var cry in locked.GetProperty("cries").EnumerateArray())
        {
            var species = cry.GetProperty("species").GetString()!;
            expected.Add("CRY_" + species[8..] + "_NORMAL", (species, cry.GetProperty("path").GetString()!, 4, 10, null));
        }
        Require(expected.Count == 134, "Original audio source inventory is incomplete.");
        foreach (var clip in manifest.Clips)
        {
            ValidateClip(clip);
            Require(expected.TryGetValue(clip.Id, out var e)
                && (clip.SourceSymbol, clip.SourcePath, clip.SourcePlayer, clip.Priority, clip.SourceCounter) == e,
                "Audio clip disagrees with locked source: " + clip.Id);
            var wav = new FileInfo(SafeFile(directory, clip.WavPath));
            Require(wav.Exists && wav.Length == checked(44L + (long)clip.FrameCount * 4), "Missing/truncated original WAV: " + clip.Id);
            var trace = new FileInfo(SafeFile(directory, clip.TracePath));
            Require(trace.Exists && trace.Length > 0, "Missing/empty original source-clock trace: " + clip.Id);
        }
        return new EarlyAudioCatalog(directory, manifest);
    }

    /// <summary>Structural validation is separate from Load's pinned-manifest authentication.</summary>
    public static void ValidateClip(EarlyAudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        Require(Regex.IsMatch(clip.Id, @"\A(?:MUS_|SE_|CRY_)[A-Z0-9_]+\z")
            && clip.SourceCommit == SourceCommit && !string.IsNullOrWhiteSpace(clip.SourceSymbol)
            && clip.SourcePath.StartsWith("sound/", StringComparison.Ordinal) && IsRelative(clip.SourcePath)
            && clip.Renderer == Renderer && !clip.IsOriginalTestFixture && !clip.AudibleOutputVerified
            && clip.SourcePlayer is >= 0 and <= 4 && clip.Priority is >= 0 and <= 255
            && (clip.SourceCounter is null or > 0) && clip.SourceSoundMode == "mono" && clip.SourceSoundCntH == 0x3302
            && clip.SampleRate == 48000 && clip.Channels == 2 && clip.FrameCount > 0
            && clip.WavPath == clip.Id.ToLowerInvariant() + ".wav" && clip.TracePath == clip.Id.ToLowerInvariant() + ".csv"
            && Digest(clip.WavSha256) && Digest(clip.PcmSha256) && Digest(clip.TraceSha256),
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
        Require(Hash(b) == clip.WavSha256 && Hash(b[44..]) == clip.PcmSha256,
            "Original WAV/PCM digest differs: " + clip.Id);
        bool nonzero = false;
        for (int i = 44; i < b.Length && !nonzero; i++) nonzero = b[i] != 0;
        Require(nonzero, "Original WAV is silent: " + clip.Id);
    }

    internal static byte[] ReadPinned(string root, string path, string expected)
    {
        var bytes = File.ReadAllBytes(SafeFile(root, path));
        Require(Hash(bytes) == expected, "Original audio digest differs: " + path);
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
    private static bool Digest(string value) => value is not null && Regex.IsMatch(value, @"\A[a-f0-9]{64}\z");
    private static uint U32(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private static ushort U16(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b[offset..]);
    private static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
