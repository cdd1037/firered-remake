using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

/// <summary>Normal-mode ROM cry provenance. Background trace timing is not a cry completion counter.</summary>
public sealed record Route22CryAudioMetadata(string Id, int SpeciesId, int CryTableIndex, int CryMode,
    string SourceCommit, string SourceSymbol, string SourcePath, string SourceWavSha256,
    string SourceCompressedSha256, int SourcePlayer, int Priority, int? SourceCounter,
    string SourceFunction, int SourceFunctionAddress, string Renderer, string RendererSourceSha256,
    string RendererBinarySha256, string Encoder, string Codec, string SourcePcmSha256,
    int SourceSampleRate, int SourceChannels, int SourceFrameCount, int StudyBackgroundSequenceStoppedVBlank,
    int? DirectCryStoppedVBlank, int FrameRateNumerator, int FrameRateDenominator,
    int SampleRate, int Channels, int LogicalFrameCount, int? LoopStartFrame, int? LoopEndFrame,
    string Path, string EncodedSha256, int ByteLength, string? DecodedPcmSha256,
    double SourceEnergyAbove12kHzPercent, bool Resampled, bool Normalized, bool LosslessToSourceChannel,
    bool AudibleOutputVerified, bool OriginalScriptWaitVerified);

public sealed record Route22CryAudioManifest(int SchemaVersion, string Profile, string SourceLockSha256,
    string StudyManifestSha256, string StudyInventorySha256, string StudyRuntimeManifestSha256,
    string RomSha1, int AudioBytes, ImmutableArray<Route22CryAudioMetadata> Clips,
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
        byte[] data = PretRoute22CryAudio.ReadPinned(_directory, clip.Path, clip.ByteLength,
            clip.EncodedSha256, PretRoute22CryAudio.MaximumEncodedBytes);
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
    public const string SourceLockSha256 = "963f4ba1f7d2477d2777996685751667cd8c8be0bfa1886f14e4a3412eebdc77";
    public const string ManifestSha256 = "2620feb68e59e504ec608e33dfc90b93d46e60af91882e09b96b11c8736590da";
    public const string StudyManifestSha256 = "545523faab56c15b1a388aebfbf285958e439c7a160c303bab3d597eda1d038d";
    public const string StudyInventorySha256 = "26598ab2d5f1d67d2925f02d3570c1a6e1323a2814f773bd0077069f18d926c0";
    public const string StudyRuntimeManifestSha256 = "bb4707a91a93f2cc852b5ffd6b7070b5359b9517bf0c5c021466a62bca45ae72";
    public const int AudioBytes = 26552;
    public const int PackageBytes = 40095;
    public const int MaximumEncodedBytes = 16 * 1024;

    private sealed record Identity(int SpeciesId, int CryTableIndex, int SourceFrames, int Frames, int Bytes,
        double Energy, string WavHash, string CompressedHash, string SourcePcmHash, string EncodedHash);
    private static readonly ImmutableDictionary<string, Identity> Identities = new Dictionary<string, Identity>(StringComparer.Ordinal)
    {
        ["CRY_SPEAROW_NORMAL"] = new(21, 20, 44800, 22400, 9088, 0.3234160534193367, "34df66e764c86d6335fea4a93049b73f4519d55ed94bb2c831705f2d77cb48db", "98cc58a13eadde1ab484290ca03405765296fac0ebc4eab1f1020ad42de60df0", "e92dd7342a95e841e6092c8568bc08869d2a3e3724f864a314956b1b62bd279c", "818a46ec398b20c409791b9937f687e70c4cccae3c7f332d32c9f25c496179e9"),
        ["CRY_FEAROW_NORMAL"] = new(22, 21, 43939, 21970, 8920, 0.3317563930830482, "52e689a78da95a654a3a89806bdddabdd0d1f35082fb2a5848024dd10ad84e78", "0c95e83fbb42f4dabc1e0c292086dbaf07600a12f42076f60ca4656685265ae4", "926a94e87314e02277ee38da2f835150210623105aa19a13c73d195747ce74f8", "c03ce9c6359cb9d244184149d180952e1cc6523108e058accc1c54ac7cb14082"),
        ["CRY_MANKEY_NORMAL"] = new(56, 55, 42044, 21022, 8544, 0.7918304550743629, "7a5ff39d7eb01025c0a38adb73d69d0bc099b85e4ae59b662e6adcdd18aa9077", "c100dfe33e64013d34f5999740c0c960dce034d6feb07049b5e51cc7964a9c25", "28e3cea52ddf2d2c69d4bf14ca340819077ce97af40a1de6607bf6efc49ea9e0", "65e524910a0d12659f980fb215e39d79f64dc0c5d249539174c831d299807677"),
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static Route22CryAudioCatalog Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        string directory = SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        ReadPinned(directory, "source-lock.json", 6560, SourceLockSha256, 8 * 1024);
        byte[] json = ReadPinned(directory, "manifest.json", 6983, ManifestSha256, 8 * 1024);
        var manifest = ContentJson.ParseStrict<Route22CryAudioManifest>(new UTF8Encoding(false, true).GetString(json), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && manifest.Profile == Profile && manifest.SourceLockSha256 == SourceLockSha256
            && manifest.StudyManifestSha256 == StudyManifestSha256 && manifest.StudyInventorySha256 == StudyInventorySha256
            && manifest.StudyRuntimeManifestSha256 == StudyRuntimeManifestSha256 && manifest.RomSha1 == PretEarlyAudio.RomSha1
            && manifest.AudioBytes == AudioBytes && manifest.Clips.Length == 3
            && manifest.Clips.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(Identities.Keys)
            && manifest.Limitations.Length == 5, "Route22 cry frozen manifest differs.");
        foreach (var clip in manifest.Clips)
        {
            ValidateMetadata(clip);
            using var stream = new FileStream(SafeFile(directory, clip.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
            Require(stream.Length == clip.ByteLength && stream.Length <= MaximumEncodedBytes,
                "Route22 cry size differs before allocation: " + clip.Id);
        }
        Require(manifest.Clips.Sum(c => c.ByteLength) == AudioBytes, "Route22 cry byte total differs.");
        return new(directory, manifest);
    }

    /// <summary>Exact source and delivery identity; no direct cry-stop observation or source wait is claimed.</summary>
    public static void ValidateMetadata(Route22CryAudioMetadata m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!Identities.TryGetValue(m.Id, out var expected)) throw new InvalidDataException("Unknown Route22 normal cry.");
        Require(new Identity(m.SpeciesId, m.CryTableIndex, m.SourceFrameCount, m.LogicalFrameCount, m.ByteLength,
            m.SourceEnergyAbove12kHzPercent, m.SourceWavSha256, m.SourceCompressedSha256,
            m.SourcePcmSha256, m.EncodedSha256) == expected, "Route22 cry source/delivery identity differs: " + m.Id);
        string species = m.Id[4..^7];
        Require(m.SourceCommit == PretEarlyAudio.SourceCommit && m.SourceSymbol == "SPECIES_" + species
            && m.SourcePath == "sound/direct_sound_samples/cries/" + species.ToLowerInvariant() + ".wav"
            && m.CryMode == 0 && m.SourcePlayer == 4 && m.Priority == 10 && m.SourceCounter is null
            && m.SourceFunction == "PlayCry_ByMode" && m.SourceFunctionAddress == 134684256
            && m.Renderer == PretEarlyAudio.Renderer && m.RendererSourceSha256 == PretEarlyAudio.RendererSourceSha256
            && m.RendererBinarySha256 == PretEarlyAudio.RenderBinarySha256
            && m.Encoder == "Godot 4.6.3-stable official AudioStreamWAV QOA" && m.Codec == "qoa"
            && m.SourceSampleRate == 48000 && m.SourceChannels == 1
            && m.StudyBackgroundSequenceStoppedVBlank == 142 && m.DirectCryStoppedVBlank is null
            && m.FrameRateNumerator == 16777216 && m.FrameRateDenominator == 280896,
            "Route22 cry source format/timing differs: " + m.Id);
        Require(m.SampleRate == 24000 && m.Channels == 1 && m.LoopStartFrame is null && m.LoopEndFrame is null
            && m.Path == "assets/" + m.Id.ToLowerInvariant() + ".qoa" && m.DecodedPcmSha256 is null
            && m.Resampled && !m.Normalized && !m.LosslessToSourceChannel
            && !m.AudibleOutputVerified && !m.OriginalScriptWaitVerified,
            "Route22 cry path/delivery/acceptance differs: " + m.Id);
    }

    public static void ValidateEncodedBytes(Route22CryAudioMetadata m, byte[] data)
    {
        ValidateMetadata(m); ArgumentNullException.ThrowIfNull(data);
        Require(data.Length == m.ByteLength && Convert.ToHexStringLower(SHA256.HashData(data)) == m.EncodedSha256,
            "Route22 cry encoded size/digest differs: " + m.Id);
        ValidateContainer(m, data);
    }

    /// <summary>Every QOA frame is checked; callers needing authentication must use ReadEncodedBytes.</summary>
    public static void ValidateContainer(Route22CryAudioMetadata m, ReadOnlySpan<byte> data)
    {
        ValidateMetadata(m);
        Require(data.Length <= MaximumEncodedBytes, "Route22 cry container exceeds bound.");
        PretSmallAudioDelivery.ValidateQoaData(data, m.SampleRate, m.Channels, m.LogicalFrameCount);
    }

    internal static byte[] ReadPinned(string root, string relative, int expectedBytes, string sha256, int maximum)
    {
        Require(expectedBytes > 0 && expectedBytes <= maximum, "Invalid Route22 cry allocation bound.");
        string path = SafeFile(root, relative);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length == expectedBytes && stream.Length <= maximum, "Route22 cry pinned size differs: " + relative);
        SafeFile(root, relative);
        byte[] bytes = new byte[expectedBytes];
        stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && stream.Length == expectedBytes, "Route22 cry changed during bounded read.");
        Require(Convert.ToHexStringLower(SHA256.HashData(bytes)) == sha256, "Route22 cry pinned digest differs: " + relative);
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
