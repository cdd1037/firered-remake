using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace Game.Content;

/// <summary>Source counter, sequence stop, PCM identity, and lossy delivery duration are separate measurements.</summary>
public sealed record TownRouteAudioMetadata(string Id, int SongId, string SourceCommit, string SourceSymbol, string SourcePath, int SourcePlayer,
    int Priority, int? SourceCounter, string Renderer, string Encoder, string Codec, int SourceSampleRate,
    int SourceChannels, int SourceFrameCount, int? SourceSequenceStoppedVBlank, int? SourceLoopStartFrame,
    int? SourceLoopEndFrame, int? SourceLoopVBlankBegin, int? SourceLoopVBlankEnd, int FrameRateNumerator,
    int FrameRateDenominator, int SampleRate, int Channels, int LogicalFrameCount, int? LoopStartFrame,
    int? LoopEndFrame, string Path, double SourceEnergyAbove12kHzPercent, bool Resampled, bool Normalized,
    bool LosslessToSourceChannel, bool AudibleOutputVerified, bool SampleExactLoopSeamVerified,
    bool OriginalScriptWaitVerified);

public sealed record TownRouteAudioManifest(int SchemaVersion, string Profile, int AudioBytes, ImmutableArray<TownRouteAudioMetadata> Clips,
    ImmutableArray<string> Limitations);

/// <summary>Town and route audio catalog. Asset reads validate the encoded audio structure.</summary>
public sealed class TownRouteAudioCatalog
{
    private readonly string _directory;
    public TownRouteAudioManifest Manifest { get; }
    public ImmutableDictionary<string, TownRouteAudioMetadata> Clips { get; }
    internal TownRouteAudioCatalog(string directory, TownRouteAudioManifest manifest)
    {
        _directory = directory; Manifest = manifest;
        Clips = manifest.Clips.ToImmutableDictionary(c => c.Id, StringComparer.Ordinal);
    }
    public byte[] GetAssetBytes(string id)
    {
        if (!Clips.TryGetValue(id, out var clip)) throw new InvalidDataException("Unknown Town/Route audio ID: " + id);
        byte[] data = PretTownRouteAudio.ReadPinned(_directory, clip.Path);
        PretTownRouteAudio.ValidateEncodedBytes(clip, data);
        return data;
    }
    public byte[] ReadEncodedBytes(string id) => GetAssetBytes(id);
}

/// <summary>Loads town and route audio metadata and encoded clips.</summary>
public static class PretTownRouteAudio
{
    public const string GeneratedDirectory = "content/generated/town-route-audio";
    public const string Profile = "firered-town-route-audio-q0-qoa-24-48-v1";
    public const int AudioBytes = 652177;
    public const int MaximumEncodedBytes = 512 * 1024;

    private static readonly ImmutableHashSet<string> ClipIds = ImmutableHashSet.Create(StringComparer.Ordinal,
        "MUS_DEX_RATING", "MUS_ROUTE3", "MUS_VICTORY_ROAD", "SE_M_SPIT_UP", "SE_M_HYPER_BEAM2");

    public static TownRouteAudioCatalog Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        string directory = SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        byte[] json = ReadPinned(directory, "manifest.json");
        var manifest = ContentJson.ParseStrict<TownRouteAudioManifest>(new UTF8Encoding(false, true).GetString(json), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && !manifest.Clips.IsDefaultOrEmpty
            && manifest.Clips.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(ClipIds), "Town/Route clip inventory differs.");
        foreach (var clip in manifest.Clips)
        {
            ValidateMetadata(clip);
            using var stream = new FileStream(SafeFile(directory, clip.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
            Require(stream.Length <= MaximumEncodedBytes,
                "Town/Route audio size differs before allocation: " + clip.Id);
        }
        return new(directory, manifest);
    }

    /// <summary>Validates the clip format, playback path and delivery metadata.</summary>
    public static void ValidateMetadata(TownRouteAudioMetadata m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!ClipIds.Contains(m.Id)) throw new InvalidDataException("Unknown Town/Route source ID.");
        bool music = m.Id.StartsWith("MUS_", StringComparison.Ordinal);
        Require(m.SourceSymbol == m.Id
            && m.SourcePath == "sound/songs/midi/" + m.Id.ToLowerInvariant() + ".mid"
            && m.Renderer == PretEarlyAudio.Renderer             && m.Encoder == (music ? "ffmpeg/libvorbis q0 (study run_study.py)" : "Godot 4.6.3-stable official AudioStreamWAV QOA")
            && m.Codec == (music ? "vorbis" : "qoa") && m.SourceSampleRate == 48000 && m.SourceChannels == 1
            && m.FrameRateNumerator == 16777216 && m.FrameRateDenominator == 280896 && m.Channels == 1,
            "Town/Route source format differs: " + m.Id);
        Require(m.Path == "assets/" + m.Id.ToLowerInvariant() + (music ? ".ogg" : ".qoa")
&& m.Resampled == (m.SampleRate != 48000)
            && !m.Normalized && !m.LosslessToSourceChannel && !m.AudibleOutputVerified
            && !m.SampleExactLoopSeamVerified && !m.OriginalScriptWaitVerified,
            "Town/Route path or acceptance claim differs: " + m.Id);
    }

    public static void ValidateEncodedBytes(TownRouteAudioMetadata m, byte[] data)
    {
        ValidateMetadata(m); ArgumentNullException.ThrowIfNull(data);
        ValidateContainer(m, data);
    }

    /// <summary>Validates the encoded audio container, sample format and frame count.</summary>
    public static void ValidateContainer(TownRouteAudioMetadata m, ReadOnlySpan<byte> b)
    {
        ValidateMetadata(m);
        if (m.Codec == "qoa") { PretSmallAudioDelivery.ValidateQoaData(b, m.SampleRate, m.Channels, m.LogicalFrameCount); return; }
        Require(b.Length is >= 58 and <= MaximumEncodedBytes && b[..4].SequenceEqual("OggS"u8) && b[4] == 0 && b[5] == 2
            && b[26] == 1 && b[27] == 30 && b.Slice(28, 7).SequenceEqual("\x01vorbis"u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(b[35..]) == 0 && b[39] == m.Channels
            && BinaryPrimitives.ReadUInt32LittleEndian(b[40..]) == m.SampleRate, "Invalid Town/Route Vorbis identification.");
        int offset = 0; long granule = -1; bool eos = false;
        while (offset < b.Length)
        {
            Require(!eos && b.Length - offset >= 27 && b.Slice(offset, 4).SequenceEqual("OggS"u8) && b[offset + 4] == 0,
                "Invalid/truncated Town/Route Ogg page.");
            int count = b[offset + 26];
            Require(b.Length - offset >= 27 + count, "Truncated Town/Route Ogg lacing.");
            int length = 27 + count;
            for (int i = 0; i < count; i++) length += b[offset + 27 + i];
            Require(b.Length - offset >= length, "Truncated Town/Route Ogg payload.");
            granule = BinaryPrimitives.ReadInt64LittleEndian(b[(offset + 6)..]);
            eos = (b[offset + 5] & 4) != 0; offset += length;
        }
        Require(eos && granule == m.LogicalFrameCount, "Town/Route Ogg terminal granule differs.");
    }

    internal static byte[] ReadPinned(string root, string relative)
    {
var bytes = File.ReadAllBytes(SafeFile(root, relative));
return bytes;
    }

    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Contains(':') && relative.Split('/').All(p => p.Length > 0 && p is not "." and not ".."),
            "Unsafe Town/Route audio path.");
        string current = Path.GetFullPath(root);
        for (var ancestor = new DirectoryInfo(current); ancestor is not null; ancestor = ancestor.Parent)
            Require((ancestor.Attributes & FileAttributes.ReparsePoint) == 0, "Symlink in Town/Route audio root.");
        foreach (string part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Symlink in Town/Route audio path.");
        }
        return current;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
