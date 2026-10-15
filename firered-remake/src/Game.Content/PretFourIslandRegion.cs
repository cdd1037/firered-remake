using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Eight exact Four Island and Icefall Cave maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFourIslandRegion
{
    public const string Profile = "firered-four-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement IceFallPairs => Contracts.GetProperty("iceFallPairs");
    public JsonElement IceMetatilePatches => Contracts.GetProperty("iceMetatilePatches");
    public JsonElement WaterfallLanes => Contracts.GetProperty("waterfallLanes");
    public JsonElement SlidingIceCells => Contracts.GetProperty("slidingIceCells");
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretFourIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        "MAP_FOUR_ISLAND_HARBOR" => new("map.four-island-harbor"),
        "MAP_FOUR_ISLAND_HOUSE1" => new("map.four-island-house1"),
        "MAP_FOUR_ISLAND_HOUSE2" => new("map.four-island-house2"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_1F" => new("map.four-island-icefall-cave-1f"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_B1F" => new("map.four-island-icefall-cave-b1f"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_BACK" => new("map.four-island-icefall-cave-back"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_ENTRANCE" => new("map.four-island-icefall-cave-entrance"),
        "MAP_FOUR_ISLAND_LORELEIS_HOUSE" => new("map.four-island-loreleis-house"),
        "MAP_FOUR_ISLAND_MART" => new("map.four-island-mart"),
        "MAP_FOUR_ISLAND_POKEMON_CENTER_1F" => new("map.four-island-pokemon-center-1f"),
        "MAP_FOUR_ISLAND_POKEMON_CENTER_2F" => new("map.four-island-pokemon-center-2f"),
        "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" => new("map.four-island-pokemon-day-care"),
        _ => PretSeviiRubyRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown FourIsland connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretFourIslandRegion Load(string root)
    {
        var source = FourIslandSource.Region(root);
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "FourIsland source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Four Island and Icefall Cave source maps; application admission remains separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!;
            var relative = spec.GetProperty("directory").GetString()!;
            var runtime = PretRuntimeMapData.Decode(source.Read(relative + "/runtime-map.json"), stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol does not match its ID.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(new(stableId), map);
        }
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FourIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
