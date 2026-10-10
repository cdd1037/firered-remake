using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

/// <summary>Source counter, sequence stop, PCM identity, and lossy delivery duration are separate measurements.</summary>
public sealed record TownRouteAudioMetadata(string Id, int SongId, string SourceCommit, string SourceSymbol,
    string SourcePath, string SourceMidiSha256, int SourcePlayer, int Priority, int? SourceCounter,
    string Renderer, string RendererSourceSha256, string RendererBinarySha256, string Encoder, string Codec,
    string SourcePcmSha256, int SourceSampleRate, int SourceChannels, int SourceFrameCount,
    int? SourceSequenceStoppedVBlank, int? SourceLoopStartFrame, int? SourceLoopEndFrame,
    int? SourceLoopVBlankBegin, int? SourceLoopVBlankEnd, int FrameRateNumerator, int FrameRateDenominator,
    int SampleRate, int Channels, int LogicalFrameCount, int? LoopStartFrame, int? LoopEndFrame,
    string Path, string EncodedSha256, int ByteLength, string? DecodedPcmSha256,
    double SourceEnergyAbove12kHzPercent, bool Resampled, bool Normalized, bool LosslessToSourceChannel,
    bool AudibleOutputVerified, bool SampleExactLoopSeamVerified, bool OriginalScriptWaitVerified);

public sealed record TownRouteAudioManifest(int SchemaVersion, string Profile, string SourceLockSha256,
    string StudyManifestSha256, string StudyInventorySha256, string RomSha1, int AudioBytes,
    ImmutableArray<TownRouteAudioMetadata> Clips, ImmutableArray<string> Limitations);

/// <summary>Portable five-clip catalog. Each asset request authenticates a fresh, bounded byte buffer.</summary>
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
        byte[] data = PretTownRouteAudio.ReadPinned(_directory, clip.Path, clip.ByteLength,
            clip.EncodedSha256, PretTownRouteAudio.MaximumEncodedBytes);
        PretTownRouteAudio.ValidateEncodedBytes(clip, data);
        return data;
    }
    public byte[] ReadEncodedBytes(string id) => GetAssetBytes(id);
}

/// <summary>Independent frozen increment; never reads a ROM, trace, source tree, master PCM, or Godot resource.</summary>
public static class PretTownRouteAudio
{
    public const string GeneratedDirectory = "content/generated/town-route-audio";
    public const string Profile = "firered-town-route-audio-q0-qoa-24-48-v1";
    public const string SourceLockSha256 = "7c25b020c9da9a3fce879b624f64c3d880ce8b0ee70c9c1c75bdddbbec434d4a";
    public const string ManifestSha256 = "182e04c7ccf922a7ed6b5d52493ed202ce9215cc7b23beddc2e3472620710c62";
    public const string StudyManifestSha256 = "545523faab56c15b1a388aebfbf285958e439c7a160c303bab3d597eda1d038d";
    public const string StudyInventorySha256 = "26598ab2d5f1d67d2925f02d3570c1a6e1323a2814f773bd0077069f18d926c0";
    public const int AudioBytes = 652177;
    public const int PackageBytes = 669692;
    public const int MaximumEncodedBytes = 512 * 1024;

    private sealed record Identity(int SongId, int Player, int Priority, int? Counter, int SourceFrames,
        int? Stopped, int? SourceBegin, int? SourceEnd, int? VBlankBegin, int? VBlankEnd,
        int Rate, int Frames, int? Begin, int? End, int Bytes, double Energy,
        string MidiHash, string SourcePcmHash, string EncodedHash);

    private static readonly ImmutableDictionary<string, Identity> Identities = new Dictionary<string, Identity>(StringComparer.Ordinal)
    {
        // These per-clip values are independently fixed in the pinned study/source lock.
        ["MUS_DEX_RATING"] = new(317, 2, 5, 196, 153918, 202, null, null, null, null, 24000, 76959, null, null, 13409, 0.5956784038093409, "37f821779c98b63f65c348d113db82b3113b033869eac53ba1ecfcb275f12cea", "d4a140371a0b37ea39c0c19cdf5b802d87386c6063c5c50f63f9374f360ce73b", "e186153dab74976dfea9165d5502af7242112a8aa9d4224e1d1c2396fb1cc9ac"),
        ["MUS_ROUTE3"] = new(293, 0, 0, null, 3232277, null, 1739098, 3232277, 2164, 4022, 48000, 3232277, 1739098, 3232277, 398808, 1.7738164285983806, "94316b0825349a37d1b36547743322f239c60c807492fbb1b69b22a942abaa5f", "1b4622b1da7e4d5caee6828503066c8bbdd5ae8bf63ce93ed16e7fda8f9156dc", "d3bd0c42db9bffa8b0c706f27380aec4a460bd5af3fad2afd8ec583916a9aa0e"),
        ["MUS_VICTORY_ROAD"] = new(295, 0, 0, null, 2915637, null, 1554258, 2915637, 1934, 3628, 24000, 1457819, 777129, 1457819, 228432, 0.19950109813328068, "e782c3ea2cebb670bc1e9ca5957625d1a40cfec122c24e5394d60ec482ffd61a", "cf66245fd7dbd5b4767c4def011538dfdc0a28e00b3da9fefba024fd2568e560", "7e5d23f7b7f2e4733f6372aadcad60cbc2d501c82eb42398301b2b14d896627d"),
        ["SE_M_SPIT_UP"] = new(225, 1, 4, null, 27855, 17, null, null, null, null, 24000, 13928, null, null, 5656, 0.25816809529332496, "dfc271f2d24fbab7be2e8f5b465f9ece2e07fd64747b2dc167551707c1984b74", "dc8efc5afc1fbef1a7e4c34a51c9074925017af412058fbf90645f3b6eaae01d", "14d32c0633a0599d4a0c3cfbb461dd53c37c11c5145eb3882c17497a19da1d5c"),
        ["SE_M_HYPER_BEAM2"] = new(240, 1, 4, null, 28936, 26, null, null, null, null, 24000, 14468, null, null, 5872, 0.3115926285963722, "0ec3aebca27b222e6b5b660e106fcd5e39b6d45aced5901fb4dc4c2cec2b8a7e", "a98198b8afeff415eb1e9cdaabfd0dfaeec7dd33061de4611d70bcdc494d650f", "0cc3a929348b6434dcf584982fa6764c17346aaef551f1669e24eabab8a3a6b5"),
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static TownRouteAudioCatalog Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        string directory = SafeFile(Path.GetFullPath(projectRoot), GeneratedDirectory);
        ReadPinned(directory, "source-lock.json", 7369, SourceLockSha256, 16 * 1024);
        byte[] json = ReadPinned(directory, "manifest.json", 10146, ManifestSha256, 16 * 1024);
        var manifest = ContentJson.ParseStrict<TownRouteAudioManifest>(new UTF8Encoding(false, true).GetString(json), ContentJson.CreateOptions());
        Require(manifest.SchemaVersion == 1 && manifest.Profile == Profile && manifest.SourceLockSha256 == SourceLockSha256
            && manifest.StudyManifestSha256 == StudyManifestSha256 && manifest.StudyInventorySha256 == StudyInventorySha256
            && manifest.RomSha1 == PretEarlyAudio.RomSha1 && manifest.AudioBytes == AudioBytes
            && manifest.Clips.Length == 5 && manifest.Clips.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(Identities.Keys)
            && manifest.Limitations.Length == 5, "Town/Route frozen manifest differs.");
        foreach (var clip in manifest.Clips)
        {
            ValidateMetadata(clip);
            using var stream = new FileStream(SafeFile(directory, clip.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
            Require(stream.Length == clip.ByteLength && stream.Length <= MaximumEncodedBytes,
                "Town/Route audio size differs before allocation: " + clip.Id);
        }
        Require(manifest.Clips.Sum(c => c.ByteLength) == AudioBytes, "Town/Route audio byte total differs.");
        return new(directory, manifest);
    }

    /// <summary>Exact metadata validation. Load additionally authenticates the complete source-lock and manifest.</summary>
    public static void ValidateMetadata(TownRouteAudioMetadata m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!Identities.TryGetValue(m.Id, out var expected)) throw new InvalidDataException("Unknown Town/Route source ID.");
        bool music = m.Id.StartsWith("MUS_", StringComparison.Ordinal);
        Require(new Identity(m.SongId, m.SourcePlayer, m.Priority, m.SourceCounter, m.SourceFrameCount,
            m.SourceSequenceStoppedVBlank, m.SourceLoopStartFrame, m.SourceLoopEndFrame,
            m.SourceLoopVBlankBegin, m.SourceLoopVBlankEnd, m.SampleRate, m.LogicalFrameCount,
            m.LoopStartFrame, m.LoopEndFrame, m.ByteLength, m.SourceEnergyAbove12kHzPercent,
            m.SourceMidiSha256, m.SourcePcmSha256, m.EncodedSha256) == expected,
            "Town/Route per-clip source/delivery identity differs: " + m.Id);
        Require(m.SourceCommit == PretEarlyAudio.SourceCommit && m.SourceSymbol == m.Id
            && m.SourcePath == "sound/songs/midi/" + m.Id.ToLowerInvariant() + ".mid"
            && m.Renderer == PretEarlyAudio.Renderer && m.RendererSourceSha256 == PretEarlyAudio.RendererSourceSha256
            && m.RendererBinarySha256 == PretEarlyAudio.RenderBinarySha256
            && m.Encoder == (music ? "ffmpeg/libvorbis q0 (study run_study.py)" : "Godot 4.6.3-stable official AudioStreamWAV QOA")
            && m.Codec == (music ? "vorbis" : "qoa") && m.SourceSampleRate == 48000 && m.SourceChannels == 1
            && m.FrameRateNumerator == 16777216 && m.FrameRateDenominator == 280896 && m.Channels == 1,
            "Town/Route source format differs: " + m.Id);
        Require(m.Path == "assets/" + m.Id.ToLowerInvariant() + (music ? ".ogg" : ".qoa")
            && m.DecodedPcmSha256 is null && m.Resampled == (m.SampleRate != 48000)
            && !m.Normalized && !m.LosslessToSourceChannel && !m.AudibleOutputVerified
            && !m.SampleExactLoopSeamVerified && !m.OriginalScriptWaitVerified,
            "Town/Route path or acceptance claim differs: " + m.Id);
    }

    public static void ValidateEncodedBytes(TownRouteAudioMetadata m, byte[] data)
    {
        ValidateMetadata(m); ArgumentNullException.ThrowIfNull(data);
        Require(data.Length == m.ByteLength && Convert.ToHexStringLower(SHA256.HashData(data)) == m.EncodedSha256,
            "Town/Route encoded size/digest differs: " + m.Id);
        ValidateContainer(m, data);
    }

    /// <summary>Container structure only; use ValidateEncodedBytes or GetAssetBytes for authentication.</summary>
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

    internal static byte[] ReadPinned(string root, string relative, int expectedBytes, string hash, int maximum)
    {
        Require(expectedBytes > 0 && expectedBytes <= maximum, "Invalid Town/Route allocation bound.");
        using var stream = new FileStream(SafeFile(root, relative), FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length == expectedBytes && stream.Length <= maximum, "Town/Route pinned size differs: " + relative);
        SafeFile(root, relative);
        byte[] data = new byte[expectedBytes]; stream.ReadExactly(data);
        Require(stream.ReadByte() == -1 && stream.Length == expectedBytes, "Town/Route file changed during bounded read.");
        Require(Convert.ToHexStringLower(SHA256.HashData(data)) == hash, "Town/Route pinned digest differs: " + relative);
        return data;
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
