using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PretFishingArtAsset(int Gender, string GraphicsId, int SourceGraphicsId, string TexturePath,
    string Sha256, string RgbaSha256, int Width, int Height, int FrameWidth, int FrameHeight, int FrameCount,
    PlayerGenderArtPoint DrawOffsetFromTileOrigin, ImmutableArray<PlayerGenderArtSourceRef> SourceRefs);
public sealed record PretFishingAnimCommand(string Opcode, int Value, int Duration, bool FlipH, int OffsetX, int OffsetY);
public sealed record PretFishingAnimation(string Id, int SourceAnimationId, string Direction,
    ImmutableArray<PretFishingAnimCommand> Commands, string SourceText, PlayerGenderArtSourceRef SourceRef);
public sealed record PretFishingArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretFishingArtAsset> Assets, ImmutableArray<PretFishingAnimation> Animations,
    ImmutableDictionary<string, string> Texts, int SourceTimingHz, PlayerGenderArtSourceRef AlignmentSource,
    ImmutableArray<string> Limitations);

/// <summary>Authenticated source Red/Green Bicycle art and bounded land-cycling contracts. Gender is the canonical MALE=0/FEMALE=1.</summary>
public sealed class PretCityCycleArt
{
    public const string Profile = "firered-city-cycle-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "a442f8d442959fe69c100fb5c4f3cae6317a887aa3dc8599a6f70ec8dbc753b4";
    public const string RawManifestSha256 = "5dbe29efe0e7077c613c52b770c0af96062ddc05dad9a3fe03a1477008be6ada";
    public const string ManifestSha256 = "e5258cb49405024cf6a656524b68e941d9371ed30b33568fa967f6aeb8fbf97f";
    public const string GeneratedSha256 = "fb9041354e1c622822025e94bbcdb23c47d1da105e02bdda494fd4d21ae9e62b";
    public const string FullSourceHash = "68fcf900e3ef2608701663c3de4cbeb7fe30c061da899c5b9c2334f01f01a95e";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly ImmutableDictionary<int, byte[]> textures;
    private readonly ImmutableDictionary<int, byte[]> rgba;
    public PlayerGenderArtDocument Document { get; }
    public ImmutableArray<PlayerGenderArtAsset> Assets => Document.Assets;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public string GeneratedDirectory { get; }
    public JsonElement Rules { get; }
    public PretFishingArtDocument Fishing { get; }
    private readonly ImmutableDictionary<int, byte[]> fishingTextures;
    private readonly ImmutableDictionary<int, byte[]> fishingRgba;
    public ImmutableArray<string> AdditionalSourceHashes => ["city-cycle-art-manifest:" + ManifestSha256, "city-cycle-art-lock:" + LockSha256,
        .. SourceFiles.Select(f => "city-cycle-art:" + f.Path + ":" + f.Sha256)];

    private PretCityCycleArt(string directory, PlayerGenderArtDocument document,
        ImmutableArray<PretWorldSourceFile> sources, ImmutableDictionary<int, byte[]> textureBytes,
        ImmutableDictionary<int, byte[]> pixelBytes, JsonElement rules, PretFishingArtDocument fishing,
        ImmutableDictionary<int, byte[]> fishingTextureBytes, ImmutableDictionary<int, byte[]> fishingPixelBytes)
    {
        GeneratedDirectory = directory; Document = document; SourceFiles = sources;
        textures = textureBytes; rgba = pixelBytes; Rules = rules; Fishing = fishing;
        fishingTextures = fishingTextureBytes; fishingRgba = fishingPixelBytes;
    }

    /// <summary>Validates the source lock, twenty-five raw inputs, both manifests, metadata and four sprite sheets.</summary>
    public static PretCityCycleArt Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var rawDirectory = Path.Combine(root, "content/raw/city-cycle-art");
        var directory = Path.Combine(root, "content/generated/city-cycle-art");
        using var sourceLock = JsonDocument.Parse(ReadPinned(root, "content/sources/city-services-cycle-art.lock.json", LockSha256, 6282));
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256, 375);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256, 1552));
        var l = sourceLock.RootElement; var m = manifest.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32() == 1 && l.GetProperty("profile").GetString() == Profile
            && l.GetProperty("repository").GetString() == "pret/pokefirered" && l.GetProperty("commit").GetString() == SourceCommit
            && l.GetProperty("tree").GetString() == SourceTree && l.GetProperty("gameEdition").GetString() == "FIRERED"
            && l.GetProperty("romRevision").GetInt32() == 0, "Player art source identity differs.");
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile
            && m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree
            && m.GetProperty("lockSha256").GetString() == LockSha256 && m.GetProperty("fullSourceHash").GetString() == FullSourceHash
            && m.GetProperty("inputCount").GetInt32() == 25 && m.GetProperty("outputs").GetArrayLength() == 7
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
        Require(sourceFiles.Length == 25 && sourceFiles.Select(s => s.Path).Distinct(StringComparer.Ordinal).Count() == 25
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
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(["city-cycle-art.json", "cycle-rules.json", "cycle-male.png", "cycle-female.png", "fish-male.png", "fish-female.png", "fishing-art.json"])
            && Hash(outputs["city-cycle-art.json"]) == GeneratedSha256, "Player art output inventory differs.");
        var document = ContentJson.ParseStrict<PlayerGenderArtDocument>(Utf8.GetString(outputs["city-cycle-art.json"]), ContentJson.CreateOptions());
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
        using var rules = JsonDocument.Parse(outputs["cycle-rules.json"]);
        Require(rules.RootElement.GetProperty("profile").GetString() == Profile && !rules.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            rules.RootElement.GetProperty("ordinaryMovement").GetProperty("pixelsPerSourceFrame").EnumerateArray().Select(p => p.GetInt32()).SequenceEqual([2,3,3,2,3,3]),
            "Cycle source movement contract differs.");
        var fishing = ContentJson.ParseStrict<PretFishingArtDocument>(Utf8.GetString(outputs["fishing-art.json"]), ContentJson.CreateOptions());
        Require(fishing.SchemaVersion == 1 && fishing.Profile == Profile && fishing.SourceCommit == SourceCommit &&
            fishing.SourceTimingHz == 60 && fishing.Assets.Length == 2 && fishing.Animations.Length == 12 && fishing.Texts.Count == 3,
            "Fishing art scope differs.");
        var fishTextures = ImmutableDictionary.CreateBuilder<int, byte[]>();
        var fishRgba = ImmutableDictionary.CreateBuilder<int, byte[]>();
        foreach (var asset in fishing.Assets)
        {
            ValidateGender(asset.Gender);
            Require(asset.SourceGraphicsId == (asset.Gender == 0 ? 4 : 11) && asset.Width == 384 && asset.Height == 32 &&
                asset.FrameWidth == 32 && asset.FrameHeight == 32 && asset.FrameCount == 12 &&
                asset.DrawOffsetFromTileOrigin == new PlayerGenderArtPoint(-8, -16) && Hash(outputs[asset.TexturePath]) == asset.Sha256,
                "Fishing sprite metadata differs.");
            foreach (var source in asset.SourceRefs)
                Require(source.Kind == "pret" && source.Repository == "pret/pokefirered" && source.Commit == SourceCommit &&
                    sourceFiles.Any(f => f.Path == source.Path && f.Sha256 == source.Sha256), "Fishing art provenance differs.");
            fishTextures.Add(asset.Gender, outputs[asset.TexturePath]);
            var pixels = DecodeRgba(outputs[asset.TexturePath], asset.RgbaSha256);
            Require(pixels.Length == 384 * 32 * 4, "Fishing sprite pixel dimensions differ.");
            fishRgba.Add(asset.Gender, pixels);
        }
        Require(fishing.Animations.Select(a => a.SourceAnimationId).Order().SequenceEqual(Enumerable.Range(0, 12)) &&
            fishing.Animations.All(a => a.Commands.All(c => c.Opcode is "frame" or "loop" or "jump" or "end" &&
                (c.Opcode != "frame" || c.Value is >= 0 and < 12 && c.Duration > 0))), "Fishing animation commands differ.");
        return new(directory, document, sourceFiles, textures.ToImmutable(), rgba.ToImmutable(), rules.RootElement.Clone(),
            fishing, fishTextures.ToImmutable(), fishRgba.ToImmutable());
    }

    public PlayerGenderArtAsset Sprite(int gender)
    {
        ValidateGender(gender);
        return Assets.Single(asset => asset.Gender == gender);
    }

    public PretFishingArtAsset FishingSprite(int gender)
    {
        ValidateGender(gender);
        return Fishing.Assets.Single(a => a.Gender == gender);
    }
    public byte[] ReadFishingTexture(int gender) { ValidateGender(gender); return [.. fishingTextures[gender]]; }
    public byte[] ReadFishingRgba(int gender) { ValidateGender(gender); return [.. fishingRgba[gender]]; }
    public PretFishingAnimation FishingAnimation(string action, string direction) => Fishing.Animations.Single(a => a.Id == action + "." + direction);

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
        Require(a.GenderSymbol == (female ? "FEMALE" : "MALE") && a.SourceGraphicsId == (female ? 8 : 1)
            && a.GraphicsId == "OBJ_EVENT_GFX_" + name + "_BIKE" && a.TexturePath == (female ? "cycle-female.png" : "cycle-male.png")
            && a.ByteLength == 36964 && a.Width == 288 && a.Height == 32 && a.FrameWidth == 32 && a.FrameHeight == 32
            && a.FrameCount == 9 && a.FrameLayout == "horizontal" && a.SourceFrameIndices.SequenceEqual(Enumerable.Range(0, 9))
            && a.SourceImagePath == "graphics/object_events/pics/people/" + name.ToLowerInvariant() + "_bike.png"
            && a.SourcePalettePath == "graphics/object_events/palettes/player.pal" && a.PaletteTag == "OBJ_EVENT_PAL_TAG_PLAYER_" + name
            && a.PaletteSlot == "PALSLOT_PLAYER" && a.PaletteRgb555.Length == 16 && a.PaletteRgb555.All(w => w is >= 0 and <= 32767)
            && a.TransparentIndex == 0 && a.TileCenterAnchor == new PlayerGenderArtPoint(16, 24)
            && a.DrawOffsetFromTileOrigin == new PlayerGenderArtPoint(-8, -16) && a.WalkFrameDurationSourceFrames == 4
            && a.SourceTimingHz == 60 && a.SourceRefs.Length == 12 && a.Directions.Count == 4, "Player sprite metadata differs.");
        foreach (var (direction, stand, walk) in new (string, int, int[])[]
        { ("south", 0, [3, 0, 4, 0]), ("north", 1, [5, 1, 6, 1]), ("west", 2, [7, 2, 8, 2]), ("east", 2, [7, 2, 8, 2]) })
            Require(a.Directions.TryGetValue(direction, out var d) && d.Stand == stand && d.FlipH == (direction == "east")
                && d.Walk.SequenceEqual(walk), "Player sprite facing/walk frames differ.");
    }

    private static byte[] ReadPinned(string root, string relative, string hash, int length, bool texture = false)
    {
        Require(length is > 0 and <= 10481152, "Player art input length exceeds bounded profile.");
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
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)) is 288 or 384
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)) == 32
            && bytes.Slice(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }), "Player PNG header differs.");
    }

    private static byte[] DecodeRgba(byte[] bytes, string expectedHash)
    {
        ValidatePngHeader(bytes);
        Require(bytes.Length is 36964 or 49252 && bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)) == bytes.Length - 57
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(bytes.Length - 12, 4)) == 0
            && bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8), "Player PNG chunk layout differs.");
        using var compressed = new MemoryStream(bytes, 41, bytes.Length - 57, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)));
        var rowBytes = width * 4;
        var stride = rowBytes + 1;
        var scanlines = new byte[stride * 32];
        zlib.ReadExactly(scanlines);
        Require(zlib.ReadByte() == -1, "Player RGBA stream exceeds expected size.");
        var rgba = new byte[width * 32 * 4];
        for (int y = 0; y < 32; y++)
        {
            Require(scanlines[y * stride] == 0, "Player PNG scanline filter differs.");
            scanlines.AsSpan(y * stride + 1, rowBytes).CopyTo(rgba.AsSpan(y * rowBytes));
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
