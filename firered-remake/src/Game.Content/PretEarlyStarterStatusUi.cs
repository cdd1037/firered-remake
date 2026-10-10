using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record EarlyStarterStatusUiPoint(int X, int Y);
public sealed record EarlyStarterStatusUiInput(string Path, int ByteLength, string GitBlobSha1, string Sha256, string SourceUrl);
public sealed record EarlyStarterStatusUiSourceRef(string Path, string Symbol, int StartLine, int EndLine, string Sha256, string Url);
public sealed record EarlyStarterStatusUiAsset(string Id, string TexturePath, string RgbaPath, int Width, int Height,
    int X, int Y, string Context, string Status, int SourceTile, string SourceImagePath, string SourcePalettePath,
    string IndexSha256, ImmutableArray<int> PaletteRgb555, int TransparentIndex, string Sha256, string RgbaSha256);
public sealed record EarlyStarterStatusUiDocument(int SchemaVersion, string Profile, string Repository, string SourceCommit,
    string RawRoot, string FullSourceHash, ImmutableArray<EarlyStarterStatusUiInput> Inputs,
    ImmutableArray<EarlyStarterStatusUiAsset> Assets, ImmutableArray<EarlyStarterStatusUiSourceRef> SourceRefs,
    ImmutableArray<EarlyStarterStatusUiPoint> PartyStatusTopLeft, ImmutableArray<string> Limitations);

/// <summary>Authenticated offline status pixels and source layout facts; never changes battle or party state.</summary>
public static class PretEarlyStarterStatusUi
{
    public const string Profile = "firered-early-starter-status-ui-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string LockSha256 = "65c40b7f394c624f14eee023c234615515593d352d0361cca193d48f7a0067cb";
    public const string CatalogSha256 = "89b61cca08d122eb2223e07145e2b5c7f345d1a93a0549dc0a8ce43c7f1bc6af";
    public const string ManifestSha256 = "08d98187d952b0cfdc93073152e658413fe5a135b93509c23daa3c8b7d27fb23";
    public const string FullSourceHash = "5b9053e55ebbde94b047678c084adb8c74561db9a8433f4a90a135beb8b88659";
    private const string Generated = "content/generated/early-starter-status";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, (int Width, int X, int Y, int Tile, string Png, string Rgba, string Indices)> Expected =
        new Dictionary<string, (int, int, int, int, string, string, string)>(StringComparer.Ordinal)
        {
            ["battle-player-sleep"] = (24, 142, 96, 27, "bfb791ea77322db36e3ceeb2df063868524bb883b473df12311016f8f9737668", "358a1a5cf8a823849c59ca1623b645edac0f94ba4be746be7e4aa74e980d5b2a", "b4430f85169b22d57f0c87d886fda0f7f825a0a81b695540deffbd1ab5d07255"),
            ["battle-opponent-sleep"] = (24, 20, 30, 77, "bfb791ea77322db36e3ceeb2df063868524bb883b473df12311016f8f9737668", "358a1a5cf8a823849c59ca1623b645edac0f94ba4be746be7e4aa74e980d5b2a", "377c4e0561a8ebd35be258ccc7a0552149562b9976b9c1acd8b516ea3ed3d3a1"),
            ["party-sleep"] = (32, 0, 0, 8, "f6a27e4aaeffc02a464a2705fb48745deb9c70d1681fc1cd140f1afb405eb3ff", "f25aad714560e830c8085f3127ccb2d83e3f7a1b4a13b89bd483f3b04be0d096", "bab0e503cff3e8ddc28a580328b0c39fb9778e268f39a9695ec5ba0854033fd6"),
        }.ToImmutableDictionary(StringComparer.Ordinal);
    private static readonly int[] HealthPalette = [0, 8456, 28671, 23387, 15096, 16975, 12714, 228, 8519, 927, 19071, 32552, 31744, 992, 31, 32333];
    private static readonly int[] PartyPalette = [17126, 7399, 32767, 28539, 24984, 26137, 3831, 17209, 18068, 19158, 29393, 31507, 10716, 13887, 4246, 6456];

    /// <summary>Checks the lock, all 11 reused raw source blobs, catalog, manifest, PNGs, and raw RGBA before returning.</summary>
    public static EarlyStarterStatusUiDocument Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var generated = SafeFile(project, Generated);
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/fetch-early-starter-status.lock.json", LockSha256));
        using var manifest = JsonDocument.Parse(ReadPinned(generated, "manifest.json", ManifestSha256));
        var catalogBytes = ReadPinned(generated, "early-starter-status-ui.json", CatalogSha256);
        var doc = ContentJson.ParseStrict<EarlyStarterStatusUiDocument>(Utf8.GetString(catalogBytes), ContentJson.CreateOptions());
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.Repository == "pret/pokefirered"
            && doc.SourceCommit == SourceCommit && doc.FullSourceHash == FullSourceHash
            && doc.RawRoot == "content/raw/forest-status-ui"
            && doc.Inputs.Length == 11 && doc.Assets.Length == 3
            && doc.Assets.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() == 3
            && !doc.SourceRefs.IsDefaultOrEmpty && !doc.Limitations.IsDefaultOrEmpty,
            "Early-starter status catalog identity differs.");
        using var catalog = JsonDocument.Parse(catalogBytes);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputs"), sourceLock.RootElement.GetProperty("files")),
            "Early-starter status lock/catalog source inventory differs.");
        Require(doc.Inputs.Select(i => i.Path).Distinct(StringComparer.Ordinal).Count() == 11,
            "Early-starter status raw source inventory is incomplete.");
        foreach (var input in doc.Inputs)
        {
            var bytes = ReadPinned(project, "content/raw/forest-status-ui/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == input.GitBlobSha1
                && input.SourceUrl == $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{input.Path}",
                "Early-starter status source Git identity differs: " + input.Path);
        }
        var material = string.Concat(doc.Inputs.OrderBy(i => i.Path, StringComparer.Ordinal).Select(i => i.Path + "\0" + i.Sha256 + "\n"));
        Require(Hash(Utf8.GetBytes(material)) == FullSourceHash, "Early-starter status source-set hash differs.");
        foreach (var source in doc.SourceRefs)
            Require(source.StartLine > 0 && source.EndLine >= source.StartLine
                && doc.Inputs.Any(i => i.Path == source.Path && i.Sha256 == source.Sha256)
                && source.Url == $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{source.Path}#L{source.StartLine}-L{source.EndLine}",
                "Early-starter status source reference differs.");
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile
            && m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash
            && !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean()
            && !m.GetProperty("publiclyUploaded").GetBoolean() && !m.GetProperty("runtimeShadersUsed").GetBoolean(),
            "Early-starter status manifest identity differs.");
        var outputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.Add(path), "Duplicate early-starter status output.");
            _ = ReadPinned(generated, path, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        }
        Require(outputs.SetEquals(doc.Assets.SelectMany(a => new[] { a.TexturePath, a.RgbaPath }).Append("early-starter-status-ui.json")),
            "Early-starter status output inventory differs.");
        foreach (var asset in doc.Assets)
        {
            _ = ReadTexture(project, asset);
            _ = ReadRgba(project, asset);
        }
        return doc;
    }

    /// <summary>Read an authenticated generated PNG. projectRoot is the repository root, not the generated directory.</summary>
    public static byte[] ReadTexture(string projectRoot, EarlyStarterStatusUiAsset asset)
    {
        ValidateAsset(asset);
        var bytes = ReadPinned(projectRoot, Generated + "/" + asset.TexturePath, asset.Sha256);
        VerifyRgba(bytes, asset);
        return bytes;
    }

    /// <summary>Already converted RGBA8, left-to-right rows, binary alpha, ready for Image.CreateFromData.</summary>
    public static byte[] ReadRgba(string projectRoot, EarlyStarterStatusUiAsset asset)
    {
        ValidateAsset(asset);
        return ReadPinned(projectRoot, Generated + "/" + asset.RgbaPath, asset.RgbaSha256, asset.Width * asset.Height * 4);
    }

    private static void ValidateAsset(EarlyStarterStatusUiAsset asset)
    {
        Require(asset is not null && Expected.TryGetValue(asset.Id, out var e)
            && asset.TexturePath == asset.Id + ".png" && asset.RgbaPath == asset.Id + ".rgba"
            && (asset.Width, asset.Height, asset.X, asset.Y) == (e.Width, 8, e.X, e.Y)
            && asset.Sha256 == e.Png && asset.RgbaSha256 == e.Rgba && asset.TransparentIndex == 0
            && asset.PaletteRgb555.Length == 16 && asset.SourceTile == e.Tile && asset.IndexSha256 == e.Indices
            && asset.Status == "sleep" && asset.Context == asset.Id[..^6]
            && asset.SourceImagePath == (asset.Context == "party" ? "graphics/interface/status_icons.png" : "graphics/battle_interface/healthbox_elements.png")
            && asset.SourcePalettePath == (asset.Context == "party" ? "graphics/interface/status_icons.png" : "graphics/battle_interface/healthbox.pal"),
            "Early-starter status asset differs from the reviewed source pixels.");
        var palette = (asset!.Context == "party" ? PartyPalette : HealthPalette).ToArray();
        if (asset.Context != "party") palette[asset.Context == "battle-player" ? 12 : 13] = 18068;
        Require(asset.PaletteRgb555.SequenceEqual(palette), "Early-starter status source palette differs.");
    }

    private static void VerifyRgba(byte[] bytes, EarlyStarterStatusUiAsset asset)
    {
        Require(bytes.Length >= 57 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) == 13 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == asset.Width
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == asset.Height
            && bytes.AsSpan(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }) && bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8)
            && bytes.AsSpan(bytes.Length - 12, 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }),
            "Early-starter status PNG encoding differs.");
        var count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)));
        Require(count == bytes.Length - 57, "Early-starter status PNG chunk lengths differ.");
        using var memory = new MemoryStream(bytes, 41, count, writable: false);
        using var zlib = new ZLibStream(memory, CompressionMode.Decompress);
        int stride = checked(asset.Width * 4);
        var scanlines = new byte[checked((stride + 1) * asset.Height)];
        zlib.ReadExactly(scanlines);
        Require(zlib.ReadByte() == -1, "Early-starter status RGBA stream exceeds its expected size.");
        var rgba = new byte[stride * asset.Height];
        for (int y = 0; y < asset.Height; y++)
        {
            Require(scanlines[y * (stride + 1)] == 0, "Early-starter status PNG filter differs.");
            scanlines.AsSpan(y * (stride + 1) + 1, stride).CopyTo(rgba.AsSpan(y * stride, stride));
        }
        Require(Hash(rgba) == asset.RgbaSha256, "Early-starter status decoded RGBA hash differs.");
    }

    private static byte[] ReadPinned(string root, string path, string hash, int? length = null)
    {
        var filename = SafeFile(root, path);
        var info = new FileInfo(filename);
        Require(info.Length is > 0 and <= 1_000_000 && (length is null || info.Length == length),
            "Early-starter status file size differs: " + path);
        var bytes = File.ReadAllBytes(filename);
        Require(Hash(bytes) == hash, "Early-starter status file hash differs: " + path);
        return bytes;
    }

    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe early-starter status path.");
        var directory = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(directory, relative));
        Require(path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            "Early-starter status path escapes its root.");
        Require(new FileInfo(path).LinkTarget is null, "Early-starter status file symlink rejected.");
        for (var current = new DirectoryInfo(Path.GetDirectoryName(path)!); current is not null; current = current.Parent)
            Require(current.LinkTarget is null, "Early-starter status directory symlink rejected.");
        return path;
    }

    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
