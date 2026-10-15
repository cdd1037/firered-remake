using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record ForestStatusUiPoint(int X, int Y);
public sealed record ForestStatusUiInput(string Path, string SourceUrl);
public sealed record ForestStatusUiSourceRef(string Path, string Symbol, int StartLine, int EndLine, string Url);
public sealed record ForestStatusUiAsset(string Id, string TexturePath, int Width, int Height, int X, int Y, string Context,
    string Status, int SourceTile, string SourceImagePath, string SourcePalettePath,
    ImmutableArray<int> PaletteRgb555, int TransparentIndex);
public sealed record ForestStatusUiDisplay(int LogicalWidth, int LogicalHeight, string TextureFilter, string PaletteExpansion,
    bool PlayerKeepsHpLabel, bool OpponentSuppressesHpLabel, bool OpponentStatusHidesCaughtBall,
    ImmutableArray<ForestStatusUiPoint> PartyStatusCenters, ImmutableArray<ForestStatusUiPoint> PartyStatusTopLeft,
    bool PartyFaintedPrecedesStatus, bool PartyHealthyIconHidden);
public sealed record ForestStatusUiTerrain(string MapId, string ComparisonMapId, string MapType, string MapBattleScene, string Terrain,
    int TerrainId, string Condition, bool SameAsRoute1TallGrass, string TexturePath);
public sealed record ForestStatusUiDocument(int SchemaVersion, string Profile, string Repository, string SourceCommit,
    ImmutableArray<ForestStatusUiInput> Inputs, ImmutableArray<ForestStatusUiAsset> Assets,
    ImmutableArray<ForestStatusUiSourceRef> SourceRefs, ForestStatusUiDisplay Display,
    ForestStatusUiTerrain Terrain, ImmutableArray<string> Limitations);

/// <summary>Loads status textures and their display layout.</summary>
public static class PretForestStatusUi
{
    public const string Profile = "firered-forest-status-ui-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private const string Generated = "content/generated/forest-status-ui";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, (int Width, int X, int Y)> Expected =
        new Dictionary<string, (int, int, int)>(StringComparer.Ordinal)
        {
            ["battle-player-poison"] = (24, 142, 96),
            ["battle-opponent-poison"] = (24, 20, 30),
            ["party-poison"] = (32, 0, 0),
            ["battle-player-paralysis"] = (24, 142, 96),
            ["battle-opponent-paralysis"] = (24, 20, 30),
            ["party-paralysis"] = (32, 0, 0),
            ["battle-player-burn"] = (24, 142, 96),
            ["battle-opponent-burn"] = (24, 20, 30),
            ["party-burn"] = (32, 0, 0),
            ["battle-opponent-hp-border"] = (8, 44, 30),
            ["party-fainted"] = (32, 0, 0)
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Loads the status catalog and textures.</summary>
    public static ForestStatusUiDocument Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var generated = SafeFile(project, Generated);
        using var manifest = JsonDocument.Parse(ReadPinned(generated,"manifest.json"));
        var catalogBytes = ReadPinned(generated,"forest-status-ui.json");
        var doc = ContentJson.ParseStrict<ForestStatusUiDocument>(Utf8.GetString(catalogBytes), ContentJson.CreateOptions());
        Require(doc.SchemaVersion == 1 && doc.Assets.Length == 11
            && doc.Assets.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() == doc.Assets.Length,
            "Forest status catalog layout differs.");
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Forest status manifest schema differs.");
        var outputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.Add(path), "Duplicate forest status output.");
        }
        Require(outputs.SetEquals(doc.Assets.Select(a => a.TexturePath).Append("forest-status-ui.json")),
            "Forest status output inventory differs.");
        foreach (var asset in doc.Assets)
        {
            _ = ReadTexture(project, asset);
            _ = ReadRgba(project, asset);
        }
        return doc;
    }

    /// <summary>Reads a generated PNG using the repository root.</summary>
    public static byte[] ReadTexture(string projectRoot, ForestStatusUiAsset asset)
    {
        ValidateAsset(asset);
        var bytes = ReadPinned(projectRoot,Generated + "/" + asset.TexturePath);
        VerifyRgba(bytes, asset);
        return bytes;
    }

    /// <summary>Already converted RGBA8, left-to-right rows, binary alpha, ready for Image.CreateFromData.</summary>
    public static byte[] ReadRgba(string projectRoot, ForestStatusUiAsset asset)
    {
        ValidateAsset(asset);
        return PngRgba8.Decode(ReadPinned(projectRoot,Generated + "/" + asset.TexturePath),asset.Width,asset.Height);
    }

    private static void ValidateAsset(ForestStatusUiAsset asset)
    {
        Require(asset is not null && Expected.TryGetValue(asset.Id, out var e)
            && asset.TexturePath == asset.Id + ".png"
            && (asset.Width, asset.Height, asset.X, asset.Y) == (e.Width, 8, e.X, e.Y)
            && asset.TransparentIndex == 0
            && asset.PaletteRgb555.Length == 16 && asset.PaletteRgb555.All(c => c is >= 0 and <= 32767),
            "Forest status asset layout is invalid.");
    }

    private static void VerifyRgba(byte[] bytes, ForestStatusUiAsset asset)
    {
        Require(bytes.Length >= 57 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) == 13 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == asset.Width
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == asset.Height
            && bytes.AsSpan(24, 5).SequenceEqual(new byte[] { 8,6,0,0,0 }) && bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8)
            && bytes.AsSpan(bytes.Length - 12, 12).SequenceEqual(new byte[] { 0,0,0,0,73,69,78,68,174,66,96,130 }),
            "Forest status PNG encoding differs.");
        var count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)));
        Require(count == bytes.Length - 57, "Forest status PNG chunk lengths differ.");
        using var memory = new MemoryStream(bytes, 41, count, writable: false);
        using var zlib = new ZLibStream(memory, CompressionMode.Decompress);
        int stride = checked(asset.Width * 4);
        var scanlines = new byte[checked((stride + 1) * asset.Height)];
        zlib.ReadExactly(scanlines);
        Require(zlib.ReadByte() == -1, "Forest status RGBA stream exceeds its expected size.");
        for (int y = 0; y < asset.Height; y++)
        {
            Require(scanlines[y * (stride + 1)] == 0, "Forest status PNG filter differs.");
        }
    }

    private static byte[] ReadPinned(string root,string path)
    {
        var filename = SafeFile(root, path);
        var info = new FileInfo(filename);
        Require(info.Length is > 0 and <= 1_000_000 ,
            "Forest status file size differs: " + path);
        var bytes = File.ReadAllBytes(filename);
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
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
