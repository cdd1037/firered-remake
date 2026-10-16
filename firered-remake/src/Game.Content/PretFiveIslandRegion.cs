using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Five exact Five Island and Rocket Warehouse maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFiveIslandRegion
{
    public const string Profile = "firered-five-island-region-v1";
    // Authenticated source spans preserve the unloaded original connections.
    private const int WaterLabyrinthConnectionWidth = 72;
    private const int MemorialPillarConnectionHeight = 60;
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement WarehouseDoor => Contracts.GetProperty("warehouseDoor");
    public JsonElement Admin1Arrows => Contracts.GetProperty("admin1Arrows");
    public JsonElement SapphireRecovery => Contracts.GetProperty("sapphireRecovery");
    public JsonElement SpinTiles => Contracts.GetProperty("spinTiles");
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretFiveIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FIVE_ISLAND" => new("map.five-island"),
        "MAP_FIVE_ISLAND_HARBOR" => new("map.five-island-harbor"),
        "MAP_FIVE_ISLAND_HOUSE1" => new("map.five-island-house1"),
        "MAP_FIVE_ISLAND_HOUSE2" => new("map.five-island-house2"),
        "MAP_FIVE_ISLAND_MEADOW" => new("map.five-island-meadow"),
        "MAP_FIVE_ISLAND_MEMORIAL_PILLAR" => new("map.five-island-memorial-pillar"),
        "MAP_FIVE_ISLAND_POKEMON_CENTER_1F" => new("map.five-island-pokemon-center-1f"),
        "MAP_FIVE_ISLAND_POKEMON_CENTER_2F" => new("map.five-island-pokemon-center-2f"),
        "MAP_FIVE_ISLAND_ROCKET_WAREHOUSE" => new("map.five-island-rocket-warehouse"),
        "MAP_FIVE_ISLAND_WATER_LABYRINTH" => new("map.five-island-water-labyrinth"),
        _ => PretSixIslandRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown FiveIsland connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!),
                c.GetProperty("map").GetString() switch
                {
                    "MAP_FIVE_ISLAND_WATER_LABYRINTH" => WaterLabyrinthConnectionWidth,
                    "MAP_FIVE_ISLAND_MEMORIAL_PILLAR" => MemorialPillarConnectionHeight,
                    _ => (int?)null
                })).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), edges);
    }
    public static PretFiveIslandRegion Load(string root)
    {
        var source = FiveIslandSource.Region(root);

        var directory = source.Directory;
        var catalog = source.Document("catalog.json");

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Five Island and Rocket Warehouse source maps; application admission remains separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!;
            var relative = spec.GetProperty("mapPath").GetString()!;
            var runtime = PretRuntimeMapData.Decode(source.Read(relative), stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == stableId, "Map symbol does not match its ID.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(new(stableId), map);
        }
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FiveIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
