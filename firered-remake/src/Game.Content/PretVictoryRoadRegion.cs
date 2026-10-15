using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Five exact League approach maps with four new maps and a reused Route23 anchor. Application admission remains separate.</summary>
public sealed partial class PretVictoryRoadRegion
{
    public const string Profile = "firered-victory-road-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement BadgeGates => Contracts.GetProperty("badgeGates");
    public JsonElement Switches => Contracts.GetProperty("switches");
    public JsonElement TrainerOccurrences => Contracts.GetProperty("trainerOccurrences");
    public JsonElement HiddenItems => Contracts.GetProperty("hiddenItems");
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretVictoryRoadRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; SourceMaps = maps; NewMaps = maps.Remove(new MapId("map.route-23")); Constants = constants; 
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE23" => new("map.route-23"),
        "MAP_ROUTE22" => new("map.route-22"),
        "MAP_ROUTE22_NORTH_ENTRANCE" => new("map.route-22-north-entrance"),
        "MAP_VICTORY_ROAD_1F" => new("map.victory-road-1f"),
        "MAP_VICTORY_ROAD_2F" => new("map.victory-road-2f"),
        "MAP_VICTORY_ROAD_3F" => new("map.victory-road-3f"),
        "MAP_INDIGO_PLATEAU_EXTERIOR" => new("map.indigo-plateau-exterior"),
        "MAP_INDIGO_PLATEAU_POKEMON_CENTER_1F" => new("map.indigo-plateau-pokemon-center-1f"),
        _ => PretFirstSeviiRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown VictoryRoad connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    private static SilphSourcePackage VictoryRoadSource(string root) => new(root,"victory-road-region");
    public static PretVictoryRoadRegion Load(string root)
    {
        var source = VictoryRoadSource(root);
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Route23, Victory Road and Indigo Plateau exterior source; application admission remains separate.", Profile, files);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "VictoryRoad runtime certificate differs.");
            var payload = source.Read(relative + "/runtime-map.json");
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 5 && maps.Values.Sum(m => m.Width * m.Height) == 7488 && maps.Values.Sum(m => m.FieldWarps.Length) == 21,
            "VictoryRoad source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "VictoryRoad target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
