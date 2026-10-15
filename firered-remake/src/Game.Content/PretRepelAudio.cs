using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

/// <summary>Source PCM identity and sequencer stop are independent of lossy delivery and its release tail.</summary>
public sealed record RepelAudioMetadata(string Id, string SourceCommit, string SourceSymbol, string SourcePath, int SourcePlayer, int Priority,
    int? SourceCounter, string Renderer, string Encoder, int SourceSampleRate, int SourceChannels,
    int SourceFrameCount, int SourceSequenceStoppedVBlank, int FrameRateNumerator, int FrameRateDenominator,
    int SampleRate, int Channels, int LogicalFrameCount, int? LoopStartFrame, int? LoopEndFrame, string Path,
    double SourceEnergyAbove12kHzPercent, bool Resampled, bool Normalized, bool LosslessToSourceChannel,
    bool AudibleOutputVerified, bool OriginalScriptWaitVerified);

/// <summary>One freshly read authenticated QOA container. Data is not cached or shared between calls.</summary>
public sealed record RepelAudioAsset(RepelAudioMetadata Metadata, byte[] Data);

/// <summary>Independent SE_REPEL increment; reads only its three portable local runtime files.</summary>
public static class PretRepelAudio
{
    public const string GeneratedDirectory = "content/generated/repel-audio";
    public const string Profile = "firered-repel-audio-qoa48-v1";
    public const int MaximumEncodedBytes = 16 * 1024;
    public const int EncodedBytes = 9984;
    public const int LogicalFrameCount = 24632;
    public const int SourceSequenceStoppedVBlank = 27;

    private sealed record Manifest(int SchemaVersion, string Profile, RepelAudioMetadata Metadata, ImmutableArray<string> Limitations);

    /// <summary>Loads the manifest and checks the QOA frame structure.</summary>
    public static RepelAudioAsset Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        string directory = SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        byte[] json = ReadPinned(directory,"manifest.json");
        var manifest = ContentJson.ParseStrict<Manifest>(new UTF8Encoding(false, true).GetString(json), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && manifest.Limitations.Length == 4,
            "Repel audio frozen manifest differs.");
        var m = manifest.Metadata;
        ValidateMetadata(m);
        byte[] bytes = ReadPinned(directory,m.Path);
        PretSmallAudioDelivery.ValidateQoaData(bytes, m.SampleRate, m.Channels, m.LogicalFrameCount);
        return new(m, bytes);
    }

    /// <summary>Checks the playback metadata.</summary>
    public static void ValidateMetadata(RepelAudioMetadata m)
    {
        ArgumentNullException.ThrowIfNull(m);
        Require(m.Id == "SE_REPEL" && m.SourceSymbol == m.Id
            && m.SourcePath == "sound/songs/midi/se_repel.mid"
            && m.SourcePlayer == 1 && m.Priority == 4 && m.SourceCounter is null
            && m.Renderer == PretEarlyAudio.Renderer             && m.Encoder == "Godot 4.6.3-stable official AudioStreamWAV QOA"
&& m.SourceSampleRate == 48000 && m.SourceChannels == 1
            && m.SourceFrameCount == LogicalFrameCount && m.SourceSequenceStoppedVBlank == SourceSequenceStoppedVBlank
            && m.FrameRateNumerator == 16777216 && m.FrameRateDenominator == 280896,
            "Repel audio source identity/timing differs.");
        Require(m.Path == "assets/se_repel.qoa"             && m.SampleRate == 48000 && m.Channels == 1 && m.LogicalFrameCount == LogicalFrameCount
            && m.LoopStartFrame is null && m.LoopEndFrame is null             && m.SourceEnergyAbove12kHzPercent == 19.530189995773792
            && !m.Resampled && !m.Normalized && !m.LosslessToSourceChannel && !m.AudibleOutputVerified
            && !m.OriginalScriptWaitVerified, "Repel audio delivery or acceptance claim differs.");
    }

    private static byte[] ReadPinned(string root,string relative)
    {
        string path = SafeFile(root, relative);
var bytes = File.ReadAllBytes(path);
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
