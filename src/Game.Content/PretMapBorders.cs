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
    int LayerType);
public readonly record struct PretMapBorderRectangle(int X, int Y, int Width, int Height);
internal sealed record PretMapBorderDocument(int SchemaVersion, string StableId, string MapPath, SourceRef SourceRef, SourceRef RuleSourceRef,
    PretMapBorderRepeatRule RepeatRule, ImmutableArray<PretWorldArtifact> Artifacts);

/// <summary>
/// Source-phase visual fallback for the closed Pallet collection. Coordinates are map-local metatiles,
/// not the original engine's +7 backup-map coordinates. This class does not make borders walkable,
/// implement connections, change the camera, or override any in-map cell.
/// </summary>
public sealed class PretMapBorders
{
    private readonly PretMapBorderDocument _data;
    private readonly PretWorldMap _map;
    private readonly ImmutableArray<PretMapBorderCell> _cells;
    public const string SourceCommit = PretWorldMap.SourceCommit;
    public MapId Id { get; }
    public int Width => _map.Width;
    public int Height => _map.Height;
    public int TileSize => _map.TileSize;
    public int BorderWidth => _map.BorderWidth;
    public int BorderHeight => _map.BorderHeight;
    public SourceRef SourceRef => _data.SourceRef;
    public SourceRef RuleSourceRef => _data.RuleSourceRef;
    public PretMapBorderRepeatRule RepeatRule => _data.RepeatRule;
    public PretWorldMetadata Metadata => _map.Metadata;
    public ImmutableArray<PretMapBorderCell> Cells => _cells;
    public string DirectoryPath { get; }
    public string BottomTexturePath => Path.Combine(DirectoryPath, "bottom.png");
    public string MiddleTexturePath => Path.Combine(DirectoryPath, "middle.png");
    public string TopTexturePath => Path.Combine(DirectoryPath, "top.png");
    public string TexturePath => Path.Combine(DirectoryPath, "map.png");

    private PretMapBorders(PretMapBorderDocument data, string directory, PretWorldMap map, ImmutableArray<PretMapBorderCell> cells)
    {
        _data = data; _map = map; _cells = cells;
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

    /// <summary>Pass content/generated/maps/borders, not the parent world collection directory.</summary>
    public static ImmutableDictionary<MapId, PretMapBorders> LoadCatalog(string borderCollectionRoot)
        => PretWorldCatalog.Specifications.Select(s => Load(Path.Combine(borderCollectionRoot, s.Name)))
            .ToImmutableDictionary(m => m.Id);

    public static PretMapBorders Load(string generatedDirectory)
    {
        string directory = Path.GetFullPath(generatedDirectory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters = true,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var data = JsonSerializer.Deserialize<PretMapBorderDocument>(File.ReadAllBytes(Path.Combine(directory, "border.json")), options)
            ?? throw new InvalidDataException("Border document is null.");
        var generated = PretRuntimeMapValidation.GeneratedRoot(directory);
        var spec = PretWorldCatalog.Specifications.SingleOrDefault(s => s.StableId == data.StableId);
        Require(data.SchemaVersion == 2 && spec is not null && data.MapPath == "maps/regions/opening/" + spec.Name + ".json", "Unsupported border map reference.");
        var map = PretWorldMap.Load(PretRuntimeMapValidation.SafeFile(generated, data.MapPath));
        Require(data.RepeatRule == new PretMapBorderRepeatRule("map-local-metatiles", 7, 8, 0, 0, "euclidean-remainder", "not-implemented"),
            "Border repetition rule differs from source phase.");
        var cells = map.BorderCells.Select(cell => new PretMapBorderCell(
            cell.X, cell.Y, cell.RawBlock, cell.MetatileId, cell.RawAttributes, cell.LayerType)).ToImmutableArray();
        var names = new HashSet<string>(["bottom.png", "middle.png", "top.png", "map.png"], StringComparer.Ordinal);
        foreach (var artifact in data.Artifacts)
        {
            Require(names.Remove(artifact.Path), "Unknown/duplicate border texture.");
        }
        Require(names.Count == 0, "Incomplete border texture set.");
        return new(data, directory, map, cells);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
