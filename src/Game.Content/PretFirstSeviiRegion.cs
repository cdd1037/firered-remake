using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Original first-Sevii maps and script-selected layouts. Application admission remains separate.</summary>
public sealed partial class PretFirstSeviiRegion
{
    public const string Profile = "firered-first-sevii-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, PretWorldMap> LayoutVariants { get; private init; } = ImmutableDictionary<string, PretWorldMap>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<Game.Domain.Encounters.EarlyLandEncounterTable> LandEncounters { get; private init; }
    public ImmutableArray<FieldItemServiceDefinition> ItemServices { get; private init; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
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
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), edges);
    }
    public static PretFirstSeviiRegion Load(string root)
    {
        var source = FirstSeviiSource.Region(root);

        var directory = source.Directory;
        var catalog = source.Document("catalog.json");

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original One/Two/Three Island first-visit source maps; application admission remains separate.", Profile);
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
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FirstSevii target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        var variants = ImmutableDictionary.CreateBuilder<string, PretWorldMap>(StringComparer.Ordinal);
        if (catalog.TryGetProperty("layoutVariants", out var layouts))
            foreach (var spec in layouts.EnumerateArray())
            {
                var id = spec.GetProperty("stableId").GetString()!;
                var relative = spec.GetProperty("mapPath").GetString()!;
                var runtime = PretRuntimeMapData.Decode(source.Read(relative), id, MapIdFromSourceSymbol);
                Need(maps.ContainsKey(new(id)) && MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id, "Alternate layout map binding differs.");
                variants.Add(spec.GetProperty("layoutSymbol").GetString()!, PretWorldMap.FromRuntime(runtime, metadata,
                    Safe(directory, relative), PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol));
            }
        using var encounters = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/first-sevii/encounters.json")));
        var land = PretWildEncounterTables.Land(encounters.RootElement.GetProperty("admitted"), MapIdFromSourceSymbol);
        Need(land.All(t => maps.ContainsKey(t.Map)), "Land encounter map is not mounted.");
        using var serviceData = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/first-sevii/services.json")));
        var services = PretFieldItemServices.Decode(serviceData.RootElement.GetProperty("serviceBindings"), maps, constants);
        return new(source, maps.ToImmutable(), constants) { LayoutVariants = variants.ToImmutable(), LandEncounters = land, ItemServices = services };
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
