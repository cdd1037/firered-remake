using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

/// <summary>Source PCM identity and sequencer stop are independent of lossy delivery and its release tail.</summary>
public sealed record RepelAudioMetadata(string Id, string SourceCommit, string SourceSymbol, string SourcePath,
    string SourceMidiSha256, int SourcePlayer, int Priority, int? SourceCounter, string Renderer,
    string RendererSourceSha256, string RendererBinarySha256, string Encoder, string SourcePcmSha256,
    int SourceSampleRate, int SourceChannels, int SourceFrameCount, int SourceSequenceStoppedVBlank,
    int FrameRateNumerator, int FrameRateDenominator, int SampleRate, int Channels, int LogicalFrameCount,
    int? LoopStartFrame, int? LoopEndFrame, string Path, string EncodedSha256, int ByteLength,
    string? DecodedPcmSha256, double SourceEnergyAbove12kHzPercent, bool Resampled, bool Normalized,
    bool LosslessToSourceChannel, bool AudibleOutputVerified, bool OriginalScriptWaitVerified);

/// <summary>One freshly read authenticated QOA container. Data is not cached or shared between calls.</summary>
public sealed record RepelAudioAsset(RepelAudioMetadata Metadata, byte[] Data);

/// <summary>Independent SE_REPEL increment; reads only its three portable local runtime files.</summary>
public static class PretRepelAudio
{
    public const string GeneratedDirectory = "content/generated/repel-audio";
    public const string Profile = "firered-repel-audio-qoa48-v1";
    public const string SourceLockSha256 = "ee1dc00c0cea171b401a4c687c86287c9662365e517f760bb7425129b2ef8b98";
    public const string ManifestSha256 = "472771205cbf99908c102885acdcec65ef525c1cb42e7f27685bc35472c192c3";
    public const string EncodedSha256 = "a09661a54a686f836ca05962ec17c2ae697d3012de5455202538882d8ab07c4d";
    public const string SourcePcmSha256 = "8dc901dd7d77093b949b6531d7d0b89f2cd5fa7e6ed5d0479c12f8544df4d0e2";
    public const int MaximumEncodedBytes = 16 * 1024;
    public const int EncodedBytes = 9984;
    public const int LogicalFrameCount = 24632;
    public const int SourceSequenceStoppedVBlank = 27;

    private sealed record Manifest(int SchemaVersion, string Profile, string SourceLockSha256,
        string StudyManifestSha256, string RomSha1, RepelAudioMetadata Metadata, ImmutableArray<string> Limitations);

    /// <summary>Checks handle lengths before allocation, fixed hashes and QOA frames before returning bytes.</summary>
    public static RepelAudioAsset Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        string directory = SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        ReadPinned(directory, "source-lock.json", 3889, SourceLockSha256, 8 * 1024);
        byte[] json = ReadPinned(directory, "manifest.json", 2282, ManifestSha256, 8 * 1024);
        var manifest = ContentJson.ParseStrict<Manifest>(new UTF8Encoding(false, true).GetString(json), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && manifest.Profile == Profile && manifest.SourceLockSha256 == SourceLockSha256
            && manifest.StudyManifestSha256 == "545523faab56c15b1a388aebfbf285958e439c7a160c303bab3d597eda1d038d"
            && manifest.RomSha1 == PretEarlyAudio.RomSha1 && manifest.Limitations.Length == 4,
            "Repel audio frozen manifest differs.");
        var m = manifest.Metadata;
        ValidateMetadata(m);
        byte[] bytes = ReadPinned(directory, m.Path, m.ByteLength, m.EncodedSha256, MaximumEncodedBytes);
        PretSmallAudioDelivery.ValidateQoaData(bytes, m.SampleRate, m.Channels, m.LogicalFrameCount);
        return new(m, bytes);
    }

    /// <summary>Structural validation. Load additionally authenticates the complete fixed manifest and asset.</summary>
    public static void ValidateMetadata(RepelAudioMetadata m)
    {
        ArgumentNullException.ThrowIfNull(m);
        Require(m.Id == "SE_REPEL" && m.SourceCommit == PretEarlyAudio.SourceCommit && m.SourceSymbol == m.Id
            && m.SourcePath == "sound/songs/midi/se_repel.mid"
            && m.SourceMidiSha256 == "4f1cb6a3e9d5c4141b084d9301336fdd9f1cb904188165c7c017c38591e12d2e"
            && m.SourcePlayer == 1 && m.Priority == 4 && m.SourceCounter is null
            && m.Renderer == PretEarlyAudio.Renderer && m.RendererSourceSha256 == PretEarlyAudio.RendererSourceSha256
            && m.RendererBinarySha256 == PretEarlyAudio.RenderBinarySha256
            && m.Encoder == "Godot 4.6.3-stable official AudioStreamWAV QOA"
            && m.SourcePcmSha256 == SourcePcmSha256 && m.SourceSampleRate == 48000 && m.SourceChannels == 1
            && m.SourceFrameCount == LogicalFrameCount && m.SourceSequenceStoppedVBlank == SourceSequenceStoppedVBlank
            && m.FrameRateNumerator == 16777216 && m.FrameRateDenominator == 280896,
            "Repel audio source identity/timing differs.");
        Require(m.Path == "assets/se_repel.qoa" && m.EncodedSha256 == EncodedSha256 && m.ByteLength == EncodedBytes
            && m.SampleRate == 48000 && m.Channels == 1 && m.LogicalFrameCount == LogicalFrameCount
            && m.LoopStartFrame is null && m.LoopEndFrame is null && m.DecodedPcmSha256 is null
            && m.SourceEnergyAbove12kHzPercent == 19.530189995773792
            && !m.Resampled && !m.Normalized && !m.LosslessToSourceChannel && !m.AudibleOutputVerified
            && !m.OriginalScriptWaitVerified, "Repel audio delivery or acceptance claim differs.");
    }

    private static byte[] ReadPinned(string root, string relative, int expectedBytes, string sha256, int maximum)
    {
        Require(expectedBytes > 0 && expectedBytes <= maximum, "Invalid Repel allocation bound.");
        string path = SafeFile(root, relative);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // Length is obtained from the opened handle, before the sole byte-buffer allocation.
        Require(stream.Length == expectedBytes && stream.Length <= maximum, "Repel audio pinned size differs: " + relative);
        SafeFile(root, relative);
        byte[] bytes = new byte[expectedBytes];
        stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && stream.Length == expectedBytes, "Repel audio changed during bounded read.");
        Require(Convert.ToHexStringLower(SHA256.HashData(bytes)) == sha256, "Repel audio pinned digest differs: " + relative);
        return bytes;
    }

    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Contains(':') && relative.Split('/').All(p => p.Length > 0 && p is not "." and not ".."),
            "Unsafe Repel audio path.");
        var current = Path.GetFullPath(root);
        for (var ancestor = new DirectoryInfo(current); ancestor is not null; ancestor = ancestor.Parent)
            Require((ancestor.Attributes & FileAttributes.ReparsePoint) == 0, "Symlink in Repel audio root.");
        foreach (string part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Symlink in Repel audio path.");
        }
        return current;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
