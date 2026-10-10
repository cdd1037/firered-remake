using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record Route11StatusAsset(string Id, int Width, int Height, int X, int Y, byte[] Rgba);
public sealed record Route11ArtBundle(ImmutableArray<ForestTrainerPortrait> Portraits,
    ImmutableArray<Route11StatusAsset> StatusAssets);

/// <summary>Authenticated original GAMER and freeze pixels; never changes game state.</summary>
public static class PretRoute11Art
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string Profile = "firered-route11-art-v1";
    public const string LockSha256 = "22412d1b5b5dbf8df7d76bf7e0a9fe481a41fe1467c9a191885db5592d10a8a4";
    public const string CatalogSha256 = "179338bc17149fa55e719ea220e3cb8d9afdcee68211a71fe81dfc377ecfb2c0";
    public const string ManifestSha256 = "611ac0ea0adcffb4b9b82f84ffb626cee43d2af37a6f2f24722bb4ff7eaaa3c2";
    public const string FullSourceHash = "b96da9dccac38992a6467dd45eb49075b99ba1b3d93fd1c5de61bf2699247bc1";
    public const string RawManifestSha256 = "a785713909516c0b0bad13afa11a2b1e6086c76371eed5256c25b61b1c2c6ada";
    private const string Generated = "content/generated/route11-art/";
    private const string Raw = "content/raw/route11-art/";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, (int W, int H, int X, int Y, int Tile, string Rgba)> Expected =
        new Dictionary<string, (int, int, int, int, int, string)>(StringComparer.Ordinal)
        {
            ["gamer-front"] = (64, 64, 144, 8, 0, "953a3e8e14dd224d79c6853573e49ab90b9890df7c94c320116e5ffc15668602"),
            ["battle-player-freeze"] = (24, 8, 142, 96, 30, "f8dadfc5dc68f80d097650b7dbd9525b63752bfd095da0da15810f7c34429bb2"),
            ["battle-opponent-freeze"] = (24, 8, 20, 30, 80, "f8dadfc5dc68f80d097650b7dbd9525b63752bfd095da0da15810f7c34429bb2"),
            ["party-freeze"] = (32, 8, 0, 0, 12, "323a37cb7c22779d5bf5bd11750d6f6c12b43d6780db5d7397bdf677bce2067e"),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static Route11ArtBundle Load(string root)
    {
        using var sourceLock = JsonDocument.Parse(Read(root, "scripts/route11-art.lock.json", LockSha256));
        using var rawManifest = JsonDocument.Parse(Read(root, Raw + "manifest.json", RawManifestSha256));
        using var catalog = JsonDocument.Parse(Read(root, Generated + "route11-art.json", CatalogSha256));
        using var manifest = JsonDocument.Parse(Read(root, Generated + "manifest.json", ManifestSha256));
        var l = sourceLock.RootElement; var c = catalog.RootElement; var m = manifest.RootElement;
        Require(S(l, "profile") == Profile && S(l, "repository") == "pret/pokefirered"
            && S(l, "commit") == SourceCommit && S(l, "tree") == SourceTree
            && S(l, "gameVersion") == "FIRERED" && I(l, "revision") == 0, "Route11 art lock identity differs.");
        Require(I(c, "schemaVersion") == 1 && S(c, "profile") == Profile && S(c, "repository") == "pret/pokefirered"
            && S(c, "sourceCommit") == SourceCommit && S(c, "sourceTree") == SourceTree
            && S(c, "gameVersion") == "FIRERED" && I(c, "revision") == 0
            && S(c, "rawRoot") == Raw.TrimEnd('/') && S(c, "lockSha256") == LockSha256
            && S(c, "fullSourceHash") == FullSourceHash && c.GetProperty("assets").GetArrayLength() == 4
            && JsonElement.DeepEquals(c.GetProperty("inputs"), l.GetProperty("files")), "Route11 art catalog identity differs.");
        var sources = new Dictionary<string, (byte[] Bytes, string Hash)>(StringComparer.Ordinal);
        foreach (var input in l.GetProperty("files").EnumerateArray())
        {
            var path = S(input, "path"); var hash = S(input, "sha256");
            var data = Read(root, Raw + path, hash, I(input, "byteLength"));
            Require(sources.TryAdd(path, (data, hash)), "Duplicate Route11 art source.");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data])) == S(input, "gitBlobSha1")
                && S(input, "sourceUrl") == $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{path}", "Route11 original Git source identity differs.");
        }
        Require(sources.Count == 18 && Hash(Utf8.GetBytes(string.Concat(sources.OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => e.Key + "\0" + e.Value.Hash + "\n")))) == FullSourceHash, "Route11 source set differs.");
        foreach (var r in c.GetProperty("sourceRefs").EnumerateArray())
        {
            string path = S(r, "path"); int start = I(r, "startLine"), end = I(r, "endLine");
            Require(sources.TryGetValue(path, out var input) && input.Hash == S(r, "sha256")
                && start > 0 && end >= start && end <= Utf8.GetString(input.Bytes).Count(ch => ch == '\n') + 1
                && S(r, "url") == $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{path}#L{start}-L{end}", "Route11 art source reference differs.");
        }
        Require(c.GetProperty("sourceRefs").GetArrayLength() == 35, "Incomplete Route11 source references.");
        var rm = rawManifest.RootElement;
        Require(S(rm, "profile") == Profile && S(rm, "lockSha256") == LockSha256
            && S(rm, "sourceCommit") == SourceCommit && S(rm, "sourceTree") == SourceTree
            && S(rm, "fullSourceHash") == FullSourceHash && I(rm, "inputCount") == sources.Count, "Route11 raw provenance differs.");
        Require(I(m, "schemaVersion") == 1 && S(m, "profile") == Profile && S(m, "sourceCommit") == SourceCommit
            && S(m, "sourceTree") == SourceTree && S(m, "lockSha256") == LockSha256 && S(m, "fullSourceHash") == FullSourceHash
            && !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean()
            && !m.GetProperty("publiclyUploaded").GetBoolean() && !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Route11 art manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = S(output, "path");
            Require(outputs.TryAdd(path, Read(root, Generated + path, S(output, "sha256"), I(output, "byteLength"))), "Duplicate Route11 art output.");
        }
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Expected.Keys.SelectMany(id => new[] { id + ".png", id + ".rgba" })
            .Append("route11-art.json")), "Route11 output inventory differs.");
        var portraits = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var statuses = ImmutableArray.CreateBuilder<Route11StatusAsset>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in c.GetProperty("assets").EnumerateArray())
        {
            string id = S(asset, "id");
            Require(seen.Add(id) && Expected.ContainsKey(id), "Route11 asset identity differs.");
            var e = Expected[id];
            Require((I(asset, "width"), I(asset, "height"), I(asset, "x"), I(asset, "y"), I(asset, "sourceTile")) == (e.W, e.H, e.X, e.Y, e.Tile)
                && S(asset, "texturePath") == id + ".png" && S(asset, "rgbaPath") == id + ".rgba"
                && S(asset, "rgbaSha256") == e.Rgba && I(asset, "transparentIndex") == 0
                && asset.GetProperty("paletteRgb555").GetArrayLength() == 16
                && sources.ContainsKey(S(asset, "sourceImagePath")) && sources.ContainsKey(S(asset, "sourcePalettePath")), "Route11 asset source/layout differs.");
            var pixels = outputs[id + ".rgba"]; var png = outputs[id + ".png"];
            Require(pixels.Length == e.W * e.H * 4 && Hash(pixels) == e.Rgba && Hash(png) == S(asset, "sha256"), "Route11 exported pixels differ.");
            VerifyPng(png, e.W, e.H, pixels);
            Require(Enumerable.Range(0, pixels.Length / 4).All(i => pixels[4 * i + 3] is 0 or 255)
                && Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[4 * i + 3] == 255), "Route11 pixel alpha differs.");
            if (id == "gamer-front")
            {
                Require(S(asset, "kind") == "trainer" && I(asset, "sourceTrainerPicId") == 97 && I(asset, "sourceFrame") == 0
                    && I(asset, "sourceSpriteCenterX") == 176 && I(asset, "sourceSpriteCenterY") == 40, "GAMER binding differs.");
                portraits.Add(new(id, e.X, e.Y, pixels));
            }
            else
            {
                Require(S(asset, "kind") == "status", "Freeze kind differs.");
                if (id != "party-freeze") Require(asset.GetProperty("paletteRgb555")[id == "battle-player-freeze" ? 12 : 13].GetInt32() == 29393,
                    "Freeze dynamic RGB555 entry differs.");
                statuses.Add(new(id, e.W, e.H, e.X, e.Y, pixels));
            }
        }
        Require(portraits.Count == 1 && statuses.Count == 3 && seen.SetEquals(Expected.Keys), "Route11 art bundle incomplete.");
        return new(portraits.ToImmutable(), statuses.ToImmutable());
    }

    private static void VerifyPng(byte[] bytes, int width, int height, byte[] expected)
    {
        Require(bytes.Length >= 57 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) == 13 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == width && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == height
            && bytes.AsSpan(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }) && bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8)
            && bytes.AsSpan(bytes.Length - 12, 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }), "Route11 PNG encoding differs.");
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)));
        Require(count == bytes.Length - 57, "Route11 PNG chunk length differs.");
        using var memory = new MemoryStream(bytes, 41, count, writable: false);
        using var zlib = new ZLibStream(memory, CompressionMode.Decompress);
        int stride = checked(width * 4); var rows = new byte[checked((stride + 1) * height)];
        zlib.ReadExactly(rows); Require(zlib.ReadByte() == -1, "Route11 PNG exceeds expected size.");
        for (int y = 0; y < height; y++) Require(rows[y * (stride + 1)] == 0
            && rows.AsSpan(y * (stride + 1) + 1, stride).SequenceEqual(expected.AsSpan(y * stride, stride)), "Route11 PNG pixels differ from RGBA.");
    }

    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        Require(!string.IsNullOrWhiteSpace(root) && !root.Split(Path.DirectorySeparatorChar).Contains("..")
            && !string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Split('/').Any(p => p is "" or "." or ".."), "Unsafe Route11 art path.");
        var directory = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(directory, relative));
        Require(path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Route11 art path escapes its root.");
        var file = new FileInfo(path); Require(file.LinkTarget is null, "Route11 art file symlink rejected.");
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            Require(parent.LinkTarget is null, "Route11 art directory symlink rejected.");
        Require(file.Length is > 0 and <= 1_000_000 && (length is null || file.Length == length), "Route11 art size differs.");
        var bytes = File.ReadAllBytes(path); Require(Hash(bytes) == hash, "Route11 art file hash differs: " + relative);
        return bytes;
    }

    private static string S(JsonElement value, string key) => value.GetProperty(key).GetString() ?? throw new InvalidDataException("Null Route11 art property.");
    private static int I(JsonElement value, string key) => value.GetProperty(key).GetInt32();
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
