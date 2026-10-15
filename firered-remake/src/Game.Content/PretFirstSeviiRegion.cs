using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Fourteen exact first-Sevii maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFirstSeviiRegion
{
    public const string Profile = "firered-first-sevii-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretFirstSeviiRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants; 
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CINNABAR_ISLAND" => new("map.cinnabar-island"),
        "MAP_ONE_ISLAND" => new("map.one-island"),
        "MAP_ONE_ISLAND_HARBOR" => new("map.one-island-harbor"),
        "MAP_ONE_ISLAND_HOUSE1" => new("map.one-island-house1"),
        "MAP_ONE_ISLAND_HOUSE2" => new("map.one-island-house2"),
        "MAP_ONE_ISLAND_KINDLE_ROAD" => new("map.one-island-kindle-road"),
        "MAP_ONE_ISLAND_POKEMON_CENTER_1F" => new("map.one-island-pokemon-center-1f"),
        "MAP_ONE_ISLAND_POKEMON_CENTER_2F" => new("map.one-island-pokemon-center-2f"),
        "MAP_ONE_ISLAND_TREASURE_BEACH" => new("map.one-island-treasure-beach"),
        "MAP_THREE_ISLAND" => new("map.three-island"),
        "MAP_THREE_ISLAND_BERRY_FOREST" => new("map.three-island-berry-forest"),
        "MAP_THREE_ISLAND_BOND_BRIDGE" => new("map.three-island-bond-bridge"),
        "MAP_THREE_ISLAND_DUNSPARCE_TUNNEL" => new("map.three-island-dunsparce-tunnel"),
        "MAP_THREE_ISLAND_HARBOR" => new("map.three-island-harbor"),
        "MAP_THREE_ISLAND_HOUSE1" => new("map.three-island-house1"),
        "MAP_THREE_ISLAND_HOUSE2" => new("map.three-island-house2"),
        "MAP_THREE_ISLAND_HOUSE3" => new("map.three-island-house3"),
        "MAP_THREE_ISLAND_HOUSE4" => new("map.three-island-house4"),
        "MAP_THREE_ISLAND_HOUSE5" => new("map.three-island-house5"),
        "MAP_THREE_ISLAND_MART" => new("map.three-island-mart"),
        "MAP_THREE_ISLAND_POKEMON_CENTER_1F" => new("map.three-island-pokemon-center-1f"),
        "MAP_THREE_ISLAND_POKEMON_CENTER_2F" => new("map.three-island-pokemon-center-2f"),
        "MAP_THREE_ISLAND_PORT" => new("map.three-island-port"),
        "MAP_TWO_ISLAND" => new("map.two-island"),
        "MAP_TWO_ISLAND_CAPE_BRINK" => new("map.two-island-cape-brink"),
        "MAP_TWO_ISLAND_HARBOR" => new("map.two-island-harbor"),
        "MAP_TWO_ISLAND_HOUSE" => new("map.two-island-house"),
        "MAP_TWO_ISLAND_JOYFUL_GAME_CORNER" => new("map.two-island-joyful-game-corner"),
        "MAP_TWO_ISLAND_POKEMON_CENTER_1F" => new("map.two-island-pokemon-center-1f"),
        "MAP_TWO_ISLAND_POKEMON_CENTER_2F" => new("map.two-island-pokemon-center-2f"),
        _ => PretCinnabarRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown FirstSevii connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretFirstSeviiRegion Load(string root)
    {
        var source = FirstSeviiSource.Region(root);
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "FirstSevii source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original One/Two/Three Island first-visit source maps; application admission remains separate.", Profile);
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
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FirstSevii target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
