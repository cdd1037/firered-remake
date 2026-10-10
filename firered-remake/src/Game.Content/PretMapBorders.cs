using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretMapBorderRepeatRule(string CoordinateSpace, int EngineMapOffset, int SourceBiasPeriods,
    int PhaseX, int PhaseY, string NegativeCoordinates, string Connections);
public sealed record PretMapBorderCell(int X, int Y, ushort SourceRawBlock, int MetatileId, uint RawAttributes,
    int LayerType, ImmutableArray<ushort> TileEntries);
public readonly record struct PretMapBorderRectangle(int X, int Y, int Width, int Height);
internal sealed record PretMapBorderDocument(int SchemaVersion, string StableId, int Width, int Height, int TileSize,
    int BorderWidth, int BorderHeight, SourceRef SourceRef, SourceRef RuleSourceRef,
    PretMapBorderRepeatRule RepeatRule, PretWorldMetadata Metadata, ImmutableArray<PretMapBorderCell> Cells,
    ImmutableArray<PretWorldArtifact> Artifacts, ImmutableArray<PretWorldEmbeddedSource> EmbeddedSource);

/// <summary>
/// Source-phase visual fallback for the closed Pallet collection. Coordinates are map-local metatiles,
/// not the original engine's +7 backup-map coordinates. This class does not make borders walkable,
/// implement connections, change the camera, or override any in-map cell.
/// </summary>
public sealed class PretMapBorders
{
    private readonly PretMapBorderDocument _data;
    public const string SourceCommit = PretWorldMap.SourceCommit;
    public MapId Id { get; }
    public int Width => _data.Width;
    public int Height => _data.Height;
    public int TileSize => _data.TileSize;
    public int BorderWidth => _data.BorderWidth;
    public int BorderHeight => _data.BorderHeight;
    public SourceRef SourceRef => _data.SourceRef;
    public SourceRef RuleSourceRef => _data.RuleSourceRef;
    public PretMapBorderRepeatRule RepeatRule => _data.RepeatRule;
    public PretWorldMetadata Metadata => _data.Metadata;
    public ImmutableArray<PretMapBorderCell> Cells => _data.Cells;
    public string DirectoryPath { get; }
    public string BottomTexturePath => Path.Combine(DirectoryPath, "bottom.png");
    public string MiddleTexturePath => Path.Combine(DirectoryPath, "middle.png");
    public string TopTexturePath => Path.Combine(DirectoryPath, "top.png");
    public string TexturePath => Path.Combine(DirectoryPath, "map.png");

    private PretMapBorders(PretMapBorderDocument data, string directory)
    {
        _data = data;
        DirectoryPath = directory;
        Id = new(data.StableId);
    }

    public bool IsOutsideMap(int mapX, int mapY) => mapX < 0 || mapY < 0 || mapX >= Width || mapY >= Height;

    /// <summary>
    /// Euclidean remainder with no offset or overflow. Equals the source's (local + 8*period) % period
    /// throughout the normal engine viewport; extends the pattern safely to arbitrary negative coordinates.
    /// </summary>
    public static int PatternCoordinate(int mapCoordinate, int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        int remainder = mapCoordinate % period;
        return remainder < 0 ? remainder + period : remainder;
    }

    public PretMapBorderRectangle SourceRectangleAt(int mapX, int mapY)
        => new(PatternCoordinate(mapX, BorderWidth) * TileSize,
            PatternCoordinate(mapY, BorderHeight) * TileSize, TileSize, TileSize);

    public PretMapBorderCell BorderCellAt(int mapX, int mapY)
        => Cells[PatternCoordinate(mapY, BorderHeight) * BorderWidth + PatternCoordinate(mapX, BorderWidth)];

    /// <summary>Pass content/generated/map-borders, not the parent world collection directory.</summary>
    public static ImmutableDictionary<MapId, PretMapBorders> LoadCatalog(string borderCollectionRoot)
        => PretWorldCatalog.Specifications.Select(s => Load(Path.Combine(borderCollectionRoot, s.Directory)))
            .ToImmutableDictionary(m => m.Id);

    public static PretMapBorders Load(string generatedDirectory)
    {
        string directory = Path.GetFullPath(generatedDirectory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var data = JsonSerializer.Deserialize<PretMapBorderDocument>(File.ReadAllBytes(Path.Combine(directory, "border.json")), options)
            ?? throw new InvalidDataException("Border document is null.");
        Validate(data, directory);
        return new(data, directory);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    private static bool SafePath(string path) => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path)
        && !path.Contains('\\') && path.Split('/').All(p => p is not ("" or "." or ".."));

    private static void Validate(PretMapBorderDocument d, string directory)
    {
        var specification = PretWorldCatalog.Specifications.SingleOrDefault(s => s.StableId == d.StableId);
        Require(d.SchemaVersion == 1 && specification is not null, "Unsupported border schema/map.");
        var spec = specification!;
        Require(d.Metadata is not null && d.SourceRef is not null && d.RuleSourceRef is not null, "Missing border provenance.");
        var metadata = d.Metadata!;
        Require(metadata.Repository == "pret/pokefirered" && metadata.Commit == SourceCommit
            && metadata.FullSourceHash == PretWorldMap.FullSourceHash, "Border source identity differs.");
        Require(!metadata.Files.IsDefaultOrEmpty && metadata.Files.All(f => f is not null && SafePath(f.Path)), "Missing/unsafe source inventory.");
        var files = metadata.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        Require(files.Count == metadata.Files.Length, "Duplicate border source inventory.");
        string sourceMaterial = string.Concat(files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => f.Key + "\0" + f.Value.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(sourceMaterial)) == PretWorldMap.FullSourceHash, "Border source set hash differs.");
        string inventory = string.Concat(files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
            f.Key + "\0" + f.Value.GitBlobSha1 + "\0" + f.Value.ByteLength.ToString(CultureInfo.InvariantCulture)
            + "\0" + f.Value.Sha256 + "\0" + f.Value.SourceUrl + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(inventory)) == PretWorldMap.SourceInventoryHash, "Pinned border source inventory differs.");
        Require(d.RepeatRule == new PretMapBorderRepeatRule("map-local-metatiles", 7, 8, 0, 0, "euclidean-remainder", "not-implemented"),
            "Border repetition rule differs from fixed source phase.");
        Require(d.Width > 0 && d.Height > 0 && d.TileSize == 16 && d.BorderWidth > 0 && d.BorderHeight > 0,
            "Invalid border geometry.");
        Require(!d.Cells.IsDefault && d.Cells.Length == (long)d.BorderWidth * d.BorderHeight, "Incomplete border cells.");
        Require(!d.EmbeddedSource.IsDefaultOrEmpty, "Missing original border evidence.");
        var embedded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var e in d.EmbeddedSource)
        {
            Require(e is not null && files.ContainsKey(e.Path), "Unknown embedded border evidence.");
            byte[] bytes = Convert.FromBase64String(e!.Base64);
            var file = files[e.Path];
            byte[] header = Encoding.ASCII.GetBytes("blob " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\0");
            Require(bytes.Length == file.ByteLength && Hash(bytes) == file.Sha256 && e.Sha256 == file.Sha256
                && Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == file.GitBlobSha1, "Original border evidence hash differs.");
            Require(embedded.TryAdd(e.Path, bytes), "Duplicate embedded border evidence.");
        }
        string mapPath = $"data/maps/{spec.SourceName}/map.json";
        Require(embedded.ContainsKey(mapPath) && embedded.ContainsKey("data/layouts/layouts.json"), "Missing border map/layout evidence.");
        using var map = JsonDocument.Parse(embedded[mapPath]);
        using var layouts = JsonDocument.Parse(embedded["data/layouts/layouts.json"]);
        Require(map.RootElement.GetProperty("id").GetString() == spec.SourceMapSymbol, "Original map identity differs.");
        string layoutId = map.RootElement.GetProperty("layout").GetString()!;
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l => l.TryGetProperty("id", out var id) && id.GetString() == layoutId);
        Require(d.Width == layout.GetProperty("width").GetInt32() && d.Height == layout.GetProperty("height").GetInt32()
            && d.BorderWidth == layout.GetProperty("border_width").GetInt32() && d.BorderHeight == layout.GetProperty("border_height").GetInt32(),
            "Border geometry differs from source layout.");
        string borderPath = layout.GetProperty("border_filepath").GetString()!;
        string primary = PretWorldCatalog.TilesetBase(layout.GetProperty("primary_tileset").GetString()!);
        string secondary = PretWorldCatalog.TilesetBase(layout.GetProperty("secondary_tileset").GetString()!);
        var required = new HashSet<string>([mapPath, "data/layouts/layouts.json", borderPath, "include/fieldmap.h", "include/global.fieldmap.h",
            "src/fieldmap.c", "src/field_camera.c", primary + "/metatiles.bin", primary + "/metatile_attributes.bin",
            secondary + "/metatiles.bin", secondary + "/metatile_attributes.bin"], StringComparer.Ordinal);
        Require(required.SetEquals(embedded.Keys), "Original border evidence inventory differs.");
        void CheckSourceRef(SourceRef source, string path, string symbol) => Require(source.Kind == SourceKind.Pret
            && source.Repository == metadata.Repository && source.Commit == SourceCommit && source.Path == path
            && source.Symbol == symbol && source.Sha256 == files[path].Sha256, "Border SourceRef differs.");
        CheckSourceRef(d.SourceRef!, borderPath, layoutId);
        CheckSourceRef(d.RuleSourceRef!, "src/fieldmap.c", "GetBorderBlockAt");
        byte[] border = embedded[borderPath];
        Require(border.Length == d.Cells.Length * 2, "Original border byte count differs.");
        for (int i = 0; i < d.Cells.Length; i++)
        {
            var cell = d.Cells[i];
            ushort block = BinaryPrimitives.ReadUInt16LittleEndian(border.AsSpan(i * 2, 2));
            int mid = block & 1023;
            string tileset = mid < 640 ? primary : secondary;
            int local = mid < 640 ? mid : mid - 640;
            byte[] attributes = embedded[tileset + "/metatile_attributes.bin"], tiles = embedded[tileset + "/metatiles.bin"];
            Require(local * 4 + 4 <= attributes.Length && local * 16 + 16 <= tiles.Length, "Border metatile exceeds source tileset.");
            uint attr = BinaryPrimitives.ReadUInt32LittleEndian(attributes.AsSpan(local * 4, 4));
            Require(cell is not null && cell.X == i % d.BorderWidth && cell.Y == i / d.BorderWidth && cell.SourceRawBlock == block
                && cell.MetatileId == mid && cell.RawAttributes == attr && cell.LayerType == ((attr >> 29) & 3)
                && cell.LayerType <= 2 && !cell.TileEntries.IsDefault && cell.TileEntries.Length == 8, "Border cell differs from original bytes.");
            for (int q = 0; q < 8; q++)
                Require(cell!.TileEntries[q] == BinaryPrimitives.ReadUInt16LittleEndian(tiles.AsSpan(local * 16 + q * 2, 2)), "Border tile entry differs.");
        }
        Require(!d.Artifacts.IsDefaultOrEmpty && d.Artifacts.All(a => a is not null), "Missing border textures.");
        var expectedNames = new HashSet<string>(["bottom.png", "middle.png", "top.png", "map.png"], StringComparer.Ordinal);
        foreach (var a in d.Artifacts)
        {
            Require(expectedNames.Remove(a.Path), "Unknown/duplicate border texture.");
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, a.Path));
            Require(Hash(bytes) == a.Sha256 && bytes.Length > 24
                && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Border texture hash/PNG differs.");
            Require(a.Width == d.BorderWidth * 16 && a.Height == d.BorderHeight * 16
                && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == a.Width
                && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == a.Height, "Border texture dimensions differ.");
        }
        Require(expectedNames.Count == 0, "Incomplete border texture set.");
        string artifactMaterial = string.Concat(d.Artifacts.OrderBy(a => a.Path, StringComparer.Ordinal).Select(a =>
            a.Path + "\0" + a.Sha256 + "\0" + a.Width.ToString(CultureInfo.InvariantCulture) + "\0" + a.Height.ToString(CultureInfo.InvariantCulture) + "\n"));
        // Independently pixel-checked render baselines, not hashes accepted from the loaded document.
        string expectedArtifacts = d.StableId == "map.pallet-town"
            ? "464321907a100df8cc728a4d9dc01e4e668ff3fd9be82a7ea7f4b9c04209af9b"
            : "0edc3ac74d1bee40c80581e5abd802d9fa0945f0d4de362bb7990c0122806d06";
        Require(Hash(Encoding.UTF8.GetBytes(artifactMaterial)) == expectedArtifacts, "Pinned border render artifact set differs.");
    }
}
