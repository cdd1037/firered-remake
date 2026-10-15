using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Ten exact Route21, Cinnabar and Mansion maps. This package does not activate scripts, rewards or field transitions.</summary>
public sealed partial class PretCinnabarRegion
{
    public const string Profile = "firered-cinnabar-source-preparation-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretCinnabarRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants; 
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE21_NORTH" => new("map.route-21-north"),
        "MAP_ROUTE21_SOUTH" => new("map.route-21-south"),
        "MAP_ROUTE20" => new("map.route-20"),
        "MAP_CINNABAR_ISLAND" => new("map.cinnabar-island"),
        "MAP_CINNABAR_ISLAND_GYM" => new("map.cinnabar-island-gym"),
        "MAP_CINNABAR_ISLAND_MART" => new("map.cinnabar-island-mart"),
        "MAP_CINNABAR_ISLAND_POKEMON_CENTER_1F" => new("map.cinnabar-island-pokemon-center-1f"),
        "MAP_CINNABAR_ISLAND_POKEMON_CENTER_2F" => new("map.cinnabar-island-pokemon-center-2f"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        "MAP_POKEMON_MANSION_1F" => new("map.pokemon-mansion-1f"),
        "MAP_POKEMON_MANSION_2F" => new("map.pokemon-mansion-2f"),
        "MAP_POKEMON_MANSION_3F" => new("map.pokemon-mansion-3f"),
        "MAP_POKEMON_MANSION_B1F" => new("map.pokemon-mansion-b1f"),
        _ => PretRegionalServiceMaps.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Cinnabar connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretCinnabarRegion Load(string root)
    {
        var source = CinnabarSource.Region(root);
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "Cinnabar source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Route21, Cinnabar and Mansion source maps; application admission remains separate.", Profile, files);
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
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "Cinnabar target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("fallWarps").GetArrayLength() == 5 &&
            contracts.GetProperty("itemPickups").GetArrayLength() == 12 && NpcGraphics(source) == 20 &&
            contracts.GetProperty("tilePatches").EnumerateArray().Sum(p => p.GetProperty("changes").GetArrayLength()) == 194 &&
            contracts.GetProperty("mansionSwitchFlag").GetString() == "FLAG_POKEMON_MANSION_SWITCH_STATE", "Cinnabar source field scope differs.");
        var key = contracts.GetProperty("secretKeyPickup"); var door = contracts.GetProperty("gymDoorTrigger");
        Need(key.GetProperty("localId").GetInt32() == 6 && key.GetProperty("event").GetProperty("flag").GetString() == "FLAG_HIDE_POKEMON_MANSION_B1F_SECRET_KEY" &&
            door.GetProperty("x").GetInt32() == 20 && door.GetProperty("y").GetInt32() == 5 &&
            door.GetProperty("var").GetString() == "VAR_TEMP_1" && door.GetProperty("var_value").GetString() == "0", "Secret Key/gym door source differs.");
        foreach (var fall in contracts.GetProperty("fallWarps").EnumerateArray())
        {
            var map = maps[new(fall.GetProperty("map").GetString()!)]; var warp = map.FieldWarps[fall.GetProperty("sourceWarpIndex").GetInt32()];
            var endpoint = fall.GetProperty("source");
            Need(map.SourceBehaviorAt(warp.X, warp.Y) == 0x66 && warp.X == endpoint.GetProperty("x").GetInt32() && warp.Y == endpoint.GetProperty("y").GetInt32() &&
                warp.DestinationMapId == MapIdFromSourceSymbol(endpoint.GetProperty("dest_map").GetString()!) &&
                warp.DestinationWarpId == int.Parse(endpoint.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture), "Cinnabar fall warp endpoint differs.");
        }
        return new(source, maps.ToImmutable(), constants);
    }
    private static int SourceWorldLength(string stableId) => stableId switch
    {
        "map.route-21-north" => 902171,
        "map.route-21-south" => 906232,
        "map.cinnabar-island" => 514613,
        "map.pokemon-mansion-1f" => 978430,
        "map.pokemon-mansion-2f" => 1035589,
        "map.pokemon-mansion-3f" => 968984,
        "map.pokemon-mansion-b1f" => 978773,
        "map.cinnabar-island-pokemon-center-1f" => 323671,
        "map.cinnabar-island-mart" => 284169,
        "map.cinnabar-island-gym" => 691753,
        _ => throw new InvalidDataException("Unknown Cinnabar rich source identity.")
    };
    private static int NpcGraphics(SilphSourcePackage source) => source.Document("npc-source.json").GetProperty("graphics").GetArrayLength();
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
