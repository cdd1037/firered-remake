using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PlayerGenderArtPoint(int X, int Y);
public sealed record PlayerGenderArtDirection(int Stand, ImmutableArray<int> Walk, bool FlipH);
public sealed record PlayerGenderArtSourceRef(string Kind, string Repository, string Commit, string Path, string Symbol, string Sha256);
public sealed record PlayerGenderArtAsset(int Gender, string GenderSymbol, string GraphicsId, int SourceGraphicsId,
    string TexturePath, int ByteLength, string Sha256, string RgbaSha256, string IndexSha256,
    int Width, int Height, int FrameWidth, int FrameHeight, int FrameCount, string FrameLayout,
    ImmutableArray<int> SourceFrameIndices, string SourceImagePath, string SourcePalettePath,
    string PaletteTag, string PaletteSlot, ImmutableArray<int> PaletteRgb555, int TransparentIndex,
    ImmutableDictionary<string, PlayerGenderArtDirection> Directions, PlayerGenderArtPoint TileCenterAnchor,
    PlayerGenderArtPoint DrawOffsetFromTileOrigin, int WalkFrameDurationSourceFrames, int SourceTimingHz,
    ImmutableArray<PlayerGenderArtSourceRef> SourceRefs);
public sealed record PlayerGenderArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    string FullSourceHash, string PaletteExpansion, ImmutableArray<PlayerGenderArtAsset> Assets,
    ImmutableArray<string> Limitations);

/// <summary>Authenticated source Red/Green standing and walking art. Gender is the canonical MALE=0/FEMALE=1.</summary>
public sealed class PretPlayerGenderArt
{
    public const string Profile = "firered-player-gender-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "03e875e209c26d64215d7d6349dd28f2d39b17c21a518a1b6c6ee589f3341c58";
    public const string RawManifestSha256 = "7f90fd89e31eec7f6c955224b95a00cf03d5cb71c1d4986804bcc1b5679aed98";
    public const string ManifestSha256 = "5138430cb4a17f216158f230a407b3d9f38eb330783d14ead937b9f81deb7c02";
    public const string GeneratedSha256 = "fab3ad216457652d2e125c8f96f99f680809eaa22b9fdc7a859b4e1e56a52881";
    public const string FullSourceHash = "4942034a3f0a1de98e4c7f3b01bdf5588bedbab318d673dae58159788d665e95";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly ImmutableDictionary<int, byte[]> textures;
    private readonly ImmutableDictionary<int, byte[]> rgba;
    public PlayerGenderArtDocument Document { get; }
    public ImmutableArray<PlayerGenderArtAsset> Assets => Document.Assets;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public string GeneratedDirectory { get; }

    private PretPlayerGenderArt(string directory, PlayerGenderArtDocument document,
        ImmutableArray<PretWorldSourceFile> sources, ImmutableDictionary<int, byte[]> textureBytes,
        ImmutableDictionary<int, byte[]> pixelBytes)
    {
        GeneratedDirectory = directory; Document = document; SourceFiles = sources;
        textures = textureBytes; rgba = pixelBytes;
    }

    /// <summary>Validates the source lock, thirteen raw inputs, both manifests, metadata and both sprite sheets.</summary>
    public static PretPlayerGenderArt Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var rawDirectory = Path.Combine(root, "content/raw/player-gender-art");
        var directory = Path.Combine(root, "content/generated/player-gender-art");
        using var sourceLock = JsonDocument.Parse(ReadPinned(root, "content/player-gender-art-source-lock.json", LockSha256, 3471));
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256, 378);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256, 936));
        var l = sourceLock.RootElement; var m = manifest.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32() == 1 && l.GetProperty("profile").GetString() == Profile
            && l.GetProperty("repository").GetString() == "pret/pokefirered" && l.GetProperty("commit").GetString() == SourceCommit
            && l.GetProperty("tree").GetString() == SourceTree && l.GetProperty("gameEdition").GetString() == "FIRERED"
            && l.GetProperty("romRevision").GetInt32() == 0, "Player art source identity differs.");
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile
            && m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree
            && m.GetProperty("lockSha256").GetString() == LockSha256 && m.GetProperty("fullSourceHash").GetString() == FullSourceHash
            && m.GetProperty("inputCount").GetInt32() == 13 && m.GetProperty("outputs").GetArrayLength() == 3
            && !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(),
            "Player art manifest identity differs.");
        var sources = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in l.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var hash = entry.GetProperty("sha256").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var bytes = ReadPinned(rawDirectory, path, hash, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Player art source Git blob differs: " + path);
            sources.Add(new(path, blob, length, hash, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var sourceFiles = sources.ToImmutable();
        Require(sourceFiles.Length == 13 && sourceFiles.Select(s => s.Path).Distinct(StringComparer.Ordinal).Count() == 13
            && Hash(Utf8.GetBytes(string.Concat(sourceFiles.OrderBy(s => s.Path, StringComparer.Ordinal)
                .Select(s => s.Path + "\0" + s.Sha256 + "\n")))) == FullSourceHash, "Player art source inventory differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, entry.GetProperty("sha256").GetString()!,
                entry.GetProperty("byteLength").GetInt32(), path.EndsWith(".png", StringComparison.Ordinal))),
                "Duplicate player art output.");
        }
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(["player-gender-art.json", "player-male.png", "player-female.png"])
            && Hash(outputs["player-gender-art.json"]) == GeneratedSha256, "Player art output inventory differs.");
        var document = ContentJson.ParseStrict<PlayerGenderArtDocument>(Utf8.GetString(outputs["player-gender-art.json"]), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == SourceCommit
            && document.FullSourceHash == FullSourceHash && document.PaletteExpansion == "floor(rgb5 * 255 / 31)"
            && document.Assets.Length == 2 && document.Assets.Select(a => a.Gender).Order().SequenceEqual([0, 1])
            && !document.Limitations.IsDefaultOrEmpty, "Player art document differs.");
        var textures = ImmutableDictionary.CreateBuilder<int, byte[]>();
        var rgba = ImmutableDictionary.CreateBuilder<int, byte[]>();
        foreach (var asset in document.Assets)
        {
            ValidateAsset(asset);
            foreach (var source in asset.SourceRefs)
                Require(source.Kind == "pret" && source.Repository == "pret/pokefirered" && source.Commit == SourceCommit
                    && sourceFiles.Any(s => s.Path == source.Path && s.Sha256 == source.Sha256), "Player art provenance differs.");
            var bytes = outputs[asset.TexturePath];
            Require(bytes.Length == asset.ByteLength && Hash(bytes) == asset.Sha256, "Player texture identity differs.");
            textures.Add(asset.Gender, bytes);
            rgba.Add(asset.Gender, DecodeRgba(bytes, asset.RgbaSha256));
        }
        return new(directory, document, sourceFiles, textures.ToImmutable(), rgba.ToImmutable());
    }

    public PlayerGenderArtAsset Sprite(int gender)
    {
        ValidateGender(gender);
        return Assets.Single(asset => asset.Gender == gender);
    }

    /// <summary>Fresh authenticated PNG bytes; subsequent caller or disk mutations cannot alter this snapshot.</summary>
    public byte[] ReadTexture(int gender) { ValidateGender(gender); return [.. textures[gender]]; }
    public byte[] ReadRgba(int gender) { ValidateGender(gender); return [.. rgba[gender]]; }

    /// <summary>Convenience one-shot load for canonical gender 0 or 1; prefer one Load for repeated rendering.</summary>
    public static byte[] ReadTexture(string projectRoot, int gender)
    {
        ValidateGender(gender);
        return Load(projectRoot).ReadTexture(gender);
    }

    private static void ValidateGender(int gender)
    {
        if (gender is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(gender), "Expected canonical MALE=0 or FEMALE=1.");
    }

    private static void ValidateAsset(PlayerGenderArtAsset a)
    {
        ValidateGender(a.Gender);
        bool female = a.Gender == 1;
        string name = female ? "GREEN" : "RED";
        Require(a.GenderSymbol == (female ? "FEMALE" : "MALE") && a.SourceGraphicsId == (female ? 7 : 0)
            && a.GraphicsId == "OBJ_EVENT_GFX_" + name + "_NORMAL" && a.TexturePath == (female ? "player-female.png" : "player-male.png")
            && a.ByteLength == 18532 && a.Width == 144 && a.Height == 32 && a.FrameWidth == 16 && a.FrameHeight == 32
            && a.FrameCount == 9 && a.FrameLayout == "horizontal" && a.SourceFrameIndices.SequenceEqual(Enumerable.Range(0, 9))
            && a.SourceImagePath == "graphics/object_events/pics/people/" + name.ToLowerInvariant() + "_normal.png"
            && a.SourcePalettePath == "graphics/object_events/palettes/player.pal" && a.PaletteTag == "OBJ_EVENT_PAL_TAG_PLAYER_" + name
            && a.PaletteSlot == "PALSLOT_PLAYER" && a.PaletteRgb555.Length == 16 && a.PaletteRgb555.All(w => w is >= 0 and <= 32767)
            && a.TransparentIndex == 0 && a.TileCenterAnchor == new PlayerGenderArtPoint(8, 24)
            && a.DrawOffsetFromTileOrigin == new PlayerGenderArtPoint(0, -16) && a.WalkFrameDurationSourceFrames == 8
            && a.SourceTimingHz == 60 && a.SourceRefs.Length == 12 && a.Directions.Count == 4, "Player sprite metadata differs.");
        foreach (var (direction, stand, walk) in new (string, int, int[])[]
        { ("south", 0, [3, 0, 4, 0]), ("north", 1, [5, 1, 6, 1]), ("west", 2, [7, 2, 8, 2]), ("east", 2, [7, 2, 8, 2]) })
            Require(a.Directions.TryGetValue(direction, out var d) && d.Stand == stand && d.FlipH == (direction == "east")
                && d.Walk.SequenceEqual(walk), "Player sprite facing/walk frames differ.");
    }

    private static byte[] ReadPinned(string root, string relative, string hash, int length, bool texture = false)
    {
        Require(length is > 0 and <= 1048576, "Player art input length exceeds bounded profile.");
        var path = SafeFile(root, relative);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // Check length and, for textures, fixed PNG dimensions before allocating any pixel/file buffer.
        Require(stream.Length == length, "Player art file length differs: " + relative);
        if (texture)
        {
            Span<byte> header = stackalloc byte[33];
            stream.ReadExactly(header);
            ValidatePngHeader(header);
            stream.Position = 0;
        }
        var bytes = new byte[length];
        stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && Hash(bytes) == hash, "Player art checksum differs: " + relative);
        return bytes;
    }

    private static void ValidatePngHeader(ReadOnlySpan<byte> bytes)
    {
        Require(bytes.Length >= 33 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(8, 4)) == 13 && bytes.Slice(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)) == 144
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)) == 32
            && bytes.Slice(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }), "Player PNG header differs.");
    }

    private static byte[] DecodeRgba(byte[] bytes, string expectedHash)
    {
        ValidatePngHeader(bytes);
        Require(bytes.Length == 18532 && bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)) == bytes.Length - 57
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(bytes.Length - 12, 4)) == 0
            && bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8), "Player PNG chunk layout differs.");
        using var compressed = new MemoryStream(bytes, 41, bytes.Length - 57, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var scanlines = new byte[577 * 32];
        zlib.ReadExactly(scanlines);
        Require(zlib.ReadByte() == -1, "Player RGBA stream exceeds expected size.");
        var rgba = new byte[144 * 32 * 4];
        for (int y = 0; y < 32; y++)
        {
            Require(scanlines[y * 577] == 0, "Player PNG scanline filter differs.");
            scanlines.AsSpan(y * 577 + 1, 576).CopyTo(rgba.AsSpan(y * 576));
        }
        Require(Hash(rgba) == expectedHash, "Player RGBA hash differs.");
        return rgba;
    }

    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Split('/').Any(s => s is "" or "." or ".."), "Unsafe player art path.");
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            Require(cursor.LinkTarget is null, "Player art root/ancestor symlink rejected.");
        string path = root;
        foreach (var segment in relative.Split('/'))
        {
            path = Path.Combine(path, segment);
            Require(new FileInfo(path).LinkTarget is null && new DirectoryInfo(path).LinkTarget is null,
                "Player art source/output symlink rejected.");
        }
        return path;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
