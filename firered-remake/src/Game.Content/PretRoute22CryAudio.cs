using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

/// <summary>Normal-mode ROM cry provenance. Background trace timing is not a cry completion counter.</summary>
public sealed record Route22CryAudioMetadata(string Id, int SpeciesId, int CryTableIndex, int CryMode, string SourceCommit, string SourceSymbol,
    string SourcePath, int SourcePlayer, int Priority, int? SourceCounter, string SourceFunction,
    int SourceFunctionAddress, string Renderer, string Encoder, string Codec, int SourceSampleRate,
    int SourceChannels, int SourceFrameCount, int StudyBackgroundSequenceStoppedVBlank,
    int? DirectCryStoppedVBlank, int FrameRateNumerator, int FrameRateDenominator, int SampleRate,
    int Channels, int LogicalFrameCount, int? LoopStartFrame, int? LoopEndFrame, string Path,
    double SourceEnergyAbove12kHzPercent, bool Resampled, bool Normalized, bool LosslessToSourceChannel,
    bool AudibleOutputVerified, bool OriginalScriptWaitVerified);

public sealed record Route22CryAudioManifest(int SchemaVersion, string Profile, int AudioBytes, ImmutableArray<Route22CryAudioMetadata> Clips,
    ImmutableArray<string> Limitations);

/// <summary>Five portable runtime files, with a fresh bounded authenticated byte buffer for every read.</summary>
public sealed class Route22CryAudioCatalog
{
    private readonly string _directory;
    public Route22CryAudioManifest Manifest { get; }
    public ImmutableDictionary<string, Route22CryAudioMetadata> Clips { get; }
    internal Route22CryAudioCatalog(string directory, Route22CryAudioManifest manifest)
    {
        _directory = directory; Manifest = manifest;
        Clips = manifest.Clips.ToImmutableDictionary(c => c.Id, StringComparer.Ordinal);
    }
    public byte[] ReadEncodedBytes(string id)
    {
        if (!Clips.TryGetValue(id, out var clip)) throw new InvalidDataException("Unknown Route22 cry ID: " + id);
        byte[] data = PretRoute22CryAudio.ReadPinned(_directory, clip.Path);
        PretRoute22CryAudio.ValidateEncodedBytes(clip, data);
        return data;
    }
    public byte[] GetAssetBytes(string id) => ReadEncodedBytes(id);
}

/// <summary>Only three normal-mode cries. No ROM, study, source WAV, or Godot resource is read at runtime.</summary>
public static class PretRoute22CryAudio
{
    public const string GeneratedDirectory = "content/generated/route22-cry-audio";
    public const string Profile = "firered-route22-normal-cry-qoa24-v1";
    public const int AudioBytes = 26552;
    public const int MaximumEncodedBytes = 16 * 1024;

    private sealed record Identity(int SpeciesId, int CryTableIndex, int SourceFrames, int Frames, double Energy);
    private static readonly ImmutableDictionary<string, Identity> Identities = new Dictionary<string, Identity>(StringComparer.Ordinal)
    {
        ["CRY_SPEAROW_NORMAL"] = new(21, 20, 44800, 22400, 0.3234160534193367),["CRY_FEAROW_NORMAL"] = new(22, 21, 43939, 21970, 0.3317563930830482),["CRY_MANKEY_NORMAL"] = new(56, 55, 42044, 21022, 0.7918304550743629)    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static Route22CryAudioCatalog Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        string directory = SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        byte[] json = ReadPinned(directory, "manifest.json");
        var manifest = ContentJson.ParseStrict<Route22CryAudioManifest>(new UTF8Encoding(false, true).GetString(json), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1             && manifest.AudioBytes == AudioBytes && manifest.Clips.Length == 3
            && manifest.Clips.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(Identities.Keys)
            && manifest.Limitations.Length == 5, "Route22 cry frozen manifest differs.");
        foreach (var clip in manifest.Clips)
        {
            ValidateMetadata(clip);
            using var stream = new FileStream(SafeFile(directory, clip.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
            Require(stream.Length <= MaximumEncodedBytes,
                "Route22 cry size differs before allocation: " + clip.Id);
        }
        return new(directory, manifest);
    }

    /// <summary>Exact source and delivery identity; no direct cry-stop observation or source wait is claimed.</summary>
    public static void ValidateMetadata(Route22CryAudioMetadata m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!Identities.TryGetValue(m.Id, out var expected)) throw new InvalidDataException("Unknown Route22 normal cry.");
        string species = m.Id[4..^7];
        Require(m.SourceSymbol == "SPECIES_" + species
            && m.SourcePath == "sound/direct_sound_samples/cries/" + species.ToLowerInvariant() + ".wav"
            && m.CryMode == 0 && m.SourcePlayer == 4 && m.Priority == 10 && m.SourceCounter is null
            && m.SourceFunction == "PlayCry_ByMode" && m.SourceFunctionAddress == 134684256
            && m.Renderer == PretEarlyAudio.Renderer             && m.Encoder == "Godot 4.6.3-stable official AudioStreamWAV QOA" && m.Codec == "qoa"
            && m.SourceSampleRate == 48000 && m.SourceChannels == 1
            && m.StudyBackgroundSequenceStoppedVBlank == 142 && m.DirectCryStoppedVBlank is null
            && m.FrameRateNumerator == 16777216 && m.FrameRateDenominator == 280896,
            "Route22 cry source format/timing differs: " + m.Id);
        Require(m.SampleRate == 24000 && m.Channels == 1 && m.LoopStartFrame is null && m.LoopEndFrame is null
            && m.Path == "assets/" + m.Id.ToLowerInvariant() + ".qoa"             && m.Resampled && !m.Normalized && !m.LosslessToSourceChannel
            && !m.AudibleOutputVerified && !m.OriginalScriptWaitVerified,
            "Route22 cry path/delivery/acceptance differs: " + m.Id);
    }

    public static void ValidateEncodedBytes(Route22CryAudioMetadata m, byte[] data)
    {
        ValidateMetadata(m); ArgumentNullException.ThrowIfNull(data);
        ValidateContainer(m, data);
    }

    /// <summary>Every QOA frame is checked; callers needing authentication must use ReadEncodedBytes.</summary>
    public static void ValidateContainer(Route22CryAudioMetadata m, ReadOnlySpan<byte> data)
    {
        ValidateMetadata(m);
        Require(data.Length <= MaximumEncodedBytes, "Route22 cry container exceeds bound.");
        PretSmallAudioDelivery.ValidateQoaData(data, m.SampleRate, m.Channels, m.LogicalFrameCount);
    }

    internal static byte[] ReadPinned(string root, string relative)
    {
        string path = SafeFile(root, relative);
var bytes = File.ReadAllBytes(path);
return bytes;
    }

    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Contains(':') && relative.Split('/').All(p => p.Length > 0 && p is not "." and not ".."),
            "Unsafe Route22 cry path.");
        var current = Path.GetFullPath(root);
        for (var ancestor = new DirectoryInfo(current); ancestor is not null; ancestor = ancestor.Parent)
            Require((ancestor.Attributes & FileAttributes.ReparsePoint) == 0, "Symlink in Route22 cry root.");
        foreach (string part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Symlink in Route22 cry path.");
        }
        return current;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
