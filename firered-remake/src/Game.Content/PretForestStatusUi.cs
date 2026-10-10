using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record ForestStatusUiPoint(int X, int Y);
public sealed record ForestStatusUiInput(string Path, int ByteLength, string GitBlobSha1, string Sha256, string SourceUrl);
public sealed record ForestStatusUiSourceRef(string Path, string Symbol, int StartLine, int EndLine, string Sha256, string Url);
public sealed record ForestStatusUiAsset(string Id, string TexturePath, string RgbaPath, int Width, int Height,
    int X, int Y, string Context, string Status, int SourceTile, string SourceImagePath, string SourcePalettePath,
    ImmutableArray<int> PaletteRgb555, int TransparentIndex, string Sha256, string RgbaSha256);
public sealed record ForestStatusUiDisplay(int LogicalWidth, int LogicalHeight, string TextureFilter, string PaletteExpansion,
    bool PlayerKeepsHpLabel, bool OpponentSuppressesHpLabel, bool OpponentStatusHidesCaughtBall,
    ImmutableArray<ForestStatusUiPoint> PartyStatusCenters, ImmutableArray<ForestStatusUiPoint> PartyStatusTopLeft,
    bool PartyFaintedPrecedesStatus, bool PartyHealthyIconHidden);
public sealed record ForestStatusUiTerrain(string MapId, string ComparisonMapId, string MapType, string MapBattleScene,
    string Terrain, int TerrainId, string Condition, bool SameAsRoute1TallGrass, string TexturePath, string TextureSha256);
public sealed record ForestStatusUiDocument(int SchemaVersion, string Profile, string Repository, string SourceCommit,
    string FullSourceHash, ImmutableArray<ForestStatusUiInput> Inputs, ImmutableArray<ForestStatusUiAsset> Assets,
    ImmutableArray<ForestStatusUiSourceRef> SourceRefs, ForestStatusUiDisplay Display,
    ForestStatusUiTerrain Terrain, ImmutableArray<string> Limitations);

/// <summary>Authenticated offline status pixels and source layout facts; never changes battle or party state.</summary>
public static class PretForestStatusUi
{
    public const string Profile = "firered-forest-status-ui-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string LockSha256 = "f64c97885a41a6ddc83d35540eed246b6b196bcd73611f1892371cd2b7eec861";
    public const string CatalogSha256 = "5392e2bf1b12b35328ced3f9c4667a2010fa9decf7f63c5234539056e4a7b47d";
    public const string ManifestSha256 = "5f7a0d0bfd2ffa06a7221e25c3b65ec03993febd224a460d9fd08a8ad6156a18";
    public const string FullSourceHash = "901465303190691e3ef6bbe314755a3fade8f822f57a5e10edcb0f5208c6ab4b";
    private const string Generated = "content/generated/forest-status-ui";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, (int Width, int X, int Y, string Png, string Rgba)> Expected =
        new Dictionary<string, (int, int, int, string, string)>(StringComparer.Ordinal)
        {
            ["battle-player-poison"] = (24, 142, 96, "eba144e10c4f779babd16c40a8dfcc0d107b12361096ac7cc10d6876f3346125", "6744f720352df69f7034ee61bc3f3f09071386fdc01478a24565bf76fe517acd"),
            ["battle-opponent-poison"] = (24, 20, 30, "eba144e10c4f779babd16c40a8dfcc0d107b12361096ac7cc10d6876f3346125", "6744f720352df69f7034ee61bc3f3f09071386fdc01478a24565bf76fe517acd"),
            ["party-poison"] = (32, 0, 0, "ce62c0d94374e2386ee2445c37c25d6f9e903c1af3c47713006082c90b32fe6f", "80feff71b1db13e37d9ff9bae43671f5cc10b79362044dff2c26ce4f8f62170f"),
            ["battle-player-paralysis"] = (24, 142, 96, "b05d859793209779238036e7b0dbbdb487fb62cce3442c6607281a388b3b2aaa", "cb618c6171910862ae5912c0feb198fc3e715bf828f9079c57b8f4f2d1fe11bc"),
            ["battle-opponent-paralysis"] = (24, 20, 30, "b05d859793209779238036e7b0dbbdb487fb62cce3442c6607281a388b3b2aaa", "cb618c6171910862ae5912c0feb198fc3e715bf828f9079c57b8f4f2d1fe11bc"),
            ["party-paralysis"] = (32, 0, 0, "70be908ba83dfa496eb2358201ff4f9f0dbc21a80055b918d614a164f34adc78", "c65bdf0e75240262cc713aa8454e98084ee8fdbc22c3433cdf48951b6f1cea88"),
            ["battle-player-burn"] = (24, 142, 96, "f251771feb572f9068b83cb983aa4444445f4b1823bdf8ddba006e7a1bd28609", "6eb57ee9bff3a657ad8c3ce0fbfbbb77c867fc88ef7f7723df2fbb84bd976581"),
            ["battle-opponent-burn"] = (24, 20, 30, "f251771feb572f9068b83cb983aa4444445f4b1823bdf8ddba006e7a1bd28609", "6eb57ee9bff3a657ad8c3ce0fbfbbb77c867fc88ef7f7723df2fbb84bd976581"),
            ["party-burn"] = (32, 0, 0, "8fed6d3ae798a364b509e3b467890e144bc4adfc51980163cb58015564566c2b", "b3bbc583c130df8e23a1abba5ad0bc6b4e016f23bdab38733ddeb3e10b1b6411"),
            ["battle-opponent-hp-border"] = (8, 44, 30, "ccc5a72415af17ba9e929dddde894fdf33eddacbd8d3409d4424a7b82e8fdf9e", "ec61f8c236563cb6f8beafc0bbc427433b5d21123ab27ea5e8a63447a35348dc"),
            ["party-fainted"] = (32, 0, 0, "84997af7b9ed1a4b6d80691442475122bb847f62758be4263bda3471af6b61a9", "6b5956099add043d28b5c7685ab3b4aaeefc9e2f7aee0a812eb13ee52b623099"),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Checks the lock, all 22 raw source blobs, catalog, manifest, PNGs, and raw RGBA before returning.</summary>
    public static ForestStatusUiDocument Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var generated = SafeFile(project, Generated);
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/forest-status-ui.lock.json", LockSha256));
        using var manifest = JsonDocument.Parse(ReadPinned(generated, "manifest.json", ManifestSha256));
        var catalogBytes = ReadPinned(generated, "forest-status-ui.json", CatalogSha256);
        var doc = ContentJson.ParseStrict<ForestStatusUiDocument>(Utf8.GetString(catalogBytes), ContentJson.CreateOptions());
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.Repository == "pret/pokefirered"
            && doc.SourceCommit == SourceCommit && doc.FullSourceHash == FullSourceHash
            && doc.Inputs.Length == 22 && doc.Assets.Length == 11
            && doc.Assets.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() == 11
            && !doc.SourceRefs.IsDefaultOrEmpty && !doc.Limitations.IsDefaultOrEmpty,
            "Forest status catalog identity differs.");
        using var catalog = JsonDocument.Parse(catalogBytes);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputs"), sourceLock.RootElement.GetProperty("files")),
            "Forest status lock/catalog source inventory differs.");
        Require(doc.Inputs.Select(i => i.Path).Distinct(StringComparer.Ordinal).Count() == 22,
            "Forest status raw source inventory is incomplete.");
        foreach (var input in doc.Inputs)
        {
            var bytes = ReadPinned(project, "content/raw/forest-status-ui/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == input.GitBlobSha1
                && input.SourceUrl == $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{input.Path}",
                "Forest status source Git identity differs: " + input.Path);
        }
        var material = string.Concat(doc.Inputs.OrderBy(i => i.Path, StringComparer.Ordinal).Select(i => i.Path + "\0" + i.Sha256 + "\n"));
        Require(Hash(Utf8.GetBytes(material)) == FullSourceHash, "Forest status source-set hash differs.");
        foreach (var source in doc.SourceRefs)
            Require(source.StartLine > 0 && source.EndLine >= source.StartLine
                && doc.Inputs.Any(i => i.Path == source.Path && i.Sha256 == source.Sha256)
                && source.Url == $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{source.Path}#L{source.StartLine}-L{source.EndLine}",
                "Forest status source reference differs.");
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile
            && m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash
            && !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean()
            && !m.GetProperty("publiclyUploaded").GetBoolean() && !m.GetProperty("runtimeShadersUsed").GetBoolean(),
            "Forest status manifest identity differs.");
        var outputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.Add(path), "Duplicate forest status output.");
            _ = ReadPinned(generated, path, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        }
        Require(outputs.SetEquals(doc.Assets.SelectMany(a => new[] { a.TexturePath, a.RgbaPath }).Append("forest-status-ui.json")),
            "Forest status output inventory differs.");
        foreach (var asset in doc.Assets)
        {
            _ = ReadTexture(project, asset);
            _ = ReadRgba(project, asset);
        }
        return doc;
    }

    /// <summary>Read an authenticated generated PNG. projectRoot is the repository root, not the generated directory.</summary>
    public static byte[] ReadTexture(string projectRoot, ForestStatusUiAsset asset)
    {
        ValidateAsset(asset);
        var bytes = ReadPinned(projectRoot, Generated + "/" + asset.TexturePath, asset.Sha256);
        VerifyRgba(bytes, asset);
        return bytes;
    }

    /// <summary>Already converted RGBA8, left-to-right rows, binary alpha, ready for Image.CreateFromData.</summary>
    public static byte[] ReadRgba(string projectRoot, ForestStatusUiAsset asset)
    {
        ValidateAsset(asset);
        return ReadPinned(projectRoot, Generated + "/" + asset.RgbaPath, asset.RgbaSha256, asset.Width * asset.Height * 4);
    }

    private static void ValidateAsset(ForestStatusUiAsset asset)
    {
        Require(asset is not null && Expected.TryGetValue(asset.Id, out var e)
            && asset.TexturePath == asset.Id + ".png" && asset.RgbaPath == asset.Id + ".rgba"
            && (asset.Width, asset.Height, asset.X, asset.Y) == (e.Width, 8, e.X, e.Y)
            && asset.Sha256 == e.Png && asset.RgbaSha256 == e.Rgba && asset.TransparentIndex == 0
            && asset.PaletteRgb555.Length == 16 && asset.PaletteRgb555.All(c => c is >= 0 and <= 32767),
            "Forest status asset differs from the reviewed source pixels.");
    }

    private static void VerifyRgba(byte[] bytes, ForestStatusUiAsset asset)
    {
        Require(bytes.Length >= 57 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) == 13 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == asset.Width
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == asset.Height
            && bytes.AsSpan(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }) && bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8)
            && bytes.AsSpan(bytes.Length - 12, 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }),
            "Forest status PNG encoding differs.");
        var count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)));
        Require(count == bytes.Length - 57, "Forest status PNG chunk lengths differ.");
        using var memory = new MemoryStream(bytes, 41, count, writable: false);
        using var zlib = new ZLibStream(memory, CompressionMode.Decompress);
        int stride = checked(asset.Width * 4);
        var scanlines = new byte[checked((stride + 1) * asset.Height)];
        zlib.ReadExactly(scanlines);
        Require(zlib.ReadByte() == -1, "Forest status RGBA stream exceeds its expected size.");
        var rgba = new byte[stride * asset.Height];
        for (int y = 0; y < asset.Height; y++)
        {
            Require(scanlines[y * (stride + 1)] == 0, "Forest status PNG filter differs.");
            scanlines.AsSpan(y * (stride + 1) + 1, stride).CopyTo(rgba.AsSpan(y * stride, stride));
        }
        Require(Hash(rgba) == asset.RgbaSha256, "Forest status decoded RGBA hash differs.");
    }

    private static byte[] ReadPinned(string root, string path, string hash, int? length = null)
    {
        var filename = SafeFile(root, path);
        var info = new FileInfo(filename);
        Require(info.Length is > 0 and <= 1_000_000 && (length is null || info.Length == length),
            "Forest status file size differs: " + path);
        var bytes = File.ReadAllBytes(filename);
        Require(Hash(bytes) == hash, "Forest status file hash differs: " + path);
        return bytes;
    }

    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe forest status path.");
        var directory = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(directory, relative));
        Require(path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            "Forest status path escapes its root.");
        Require(new FileInfo(path).LinkTarget is null, "Forest status file symlink rejected.");
        for (var current = new DirectoryInfo(Path.GetDirectoryName(path)!); current is not null; current = current.Parent)
            Require(current.LinkTarget is null, "Forest status directory symlink rejected.");
        return path;
    }

    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
