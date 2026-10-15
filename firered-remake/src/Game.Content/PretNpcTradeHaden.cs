using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Complete original Route18 and east gate maps. Runtime admission is bounded to Haden's eastern walking approach.</summary>
public sealed class PretNpcTradeHaden
{
    public const string ResourceId = "firered-npc-trade-haden-v1";
    public static MapId RouteMapId { get; } = new("map.route-18");
    public static MapId Gate1FMapId { get; } = new("map.route-18-east-entrance-1f");
    public static MapId Gate2FMapId { get; } = new("map.route-18-east-entrance-2f");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretNpcTradeHaden(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE18" => RouteMapId,
        "MAP_ROUTE18_EAST_ENTRANCE_1F" => Gate1FMapId,
        "MAP_ROUTE18_EAST_ENTRANCE_2F" => Gate2FMapId,
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        // Preserve the source connection; this does not register or open Route17.
        "MAP_ROUTE17" => new("map.route-17"),
        _ => throw new InvalidDataException("Unknown original Haden approach destination: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == RouteMapId || map.Id == Gate1FMapId || map.Id == Gate2FMapId, "Unrecognized Haden approach map.");
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown original Haden connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Source-authenticated eastern foot cells only. Other maps return false. Scene=0 and normal movement/warp rules remain Application responsibilities.</summary>
    public bool AllowsFootDestination(MapId mapId, int x, int y)
    {
        if (!NewMaps.TryGetValue(mapId, out var map) || x < 0 || y < 0 || x >= map.Width || y >= map.Height) return false;
        int minimumX = mapId == RouteMapId ? 47 : mapId == Gate1FMapId ? 7 : 0;
        var cell = map.GetSourceCell(x, y)!;
        return x >= minimumX && cell.Collision == 0 && cell.Elevation == 3 &&
            cell.BehaviorName is "MB_NORMAL" or "MB_WEST_ARROW_WARP" or "MB_EAST_ARROW_WARP" or
                "MB_UP_RIGHT_STAIR_WARP" or "MB_DOWN_LEFT_STAIR_WARP";
    }

    public static PretNpcTradeHaden Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"maps/regions/npc-trade-haden");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 3 ,
            "Haden source/catalog identity differs.");
        var npcSources = source.Document("npc-source.json");
        Need(npcSources.GetProperty("graphics").GetArrayLength() == 4, "Haden original NPC/player cohort differs.");
        var contract = source.Document("contracts.json");
        var retained = contract.GetProperty("retainedCity");
        var city = SilphSourcePackage.Json(SafariSource.Read(projectRoot,retained.GetProperty("path").GetString()!));
        Need(            JsonElement.DeepEquals(city.GetProperty("events").GetProperty("connections")[1], retained.GetProperty("connection")),
            "Haden retained Fuchsia source events/connection differ.");
        Need(contract.GetProperty("tradeId").GetInt32() == 5 && contract.GetProperty("actorLocalId").GetInt32() == 1 &&
            contract.GetProperty("expectedMapCounts").GetProperty("predecessor").GetInt32() == 316 &&
            contract.GetProperty("expectedMapCounts").GetProperty("successor").GetInt32() == 319 &&
            contract.GetProperty("changesToTradeDefinitions").GetArrayLength() == 0 &&
            !contract.GetProperty("sourceCyclingState").GetProperty("specialAdmitted").GetBoolean(),
            "Haden scope or pending cycling boundary differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Route18 and Haden's two east gate floors.", ResourceId);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string relative = spec.GetProperty("mapPath").GetString()!, path = spec.GetProperty("path").GetString()!;
            string id = spec.GetProperty("stableId").GetString()!;
            var bytes = source.Read(path);
            Need(path == relative , "Haden compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(bytes, id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id, "Haden map identity differs.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 3 && (maps[RouteMapId].Width, maps[RouteMapId].Height, maps[RouteMapId].FieldWarps.Length) == (60, 20, 2) &&
            (maps[Gate1FMapId].Width, maps[Gate1FMapId].Height, maps[Gate1FMapId].FieldWarps.Length) == (13, 12, 3) &&
            (maps[Gate2FMapId].Width, maps[Gate2FMapId].Height, maps[Gate2FMapId].FieldWarps.Length) == (13, 11, 1),
            "Original Route18 gate geometry/warps differ.");
        Need(maps[RouteMapId].Events.GetProperty("object_events").GetArrayLength() == 3 &&
            maps[Gate1FMapId].Events.GetProperty("coord_events").GetArrayLength() == 11 &&
            JsonElement.DeepEquals(maps[Gate2FMapId].Events.GetProperty("object_events")[0], contract.GetProperty("actor")),
            "Original Route18 trainers, cycling triggers or Haden actor differ.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["INGAME_TRADE_LICKITUNG"] == 5 && constants["FLAG_DID_MARC_TRADE"] == 599 &&
            constants["VAR_MAP_SCENE_ROUTE16"] == 16478 && constants["FLAG_SYS_ON_CYCLING_ROAD"] == 2096 &&
            constants["FLAG_GOT_BICYCLE"] == 625 && constants["VAR_TEMP_1"] == 16385, "Original Haden/cycling constants differ.");
        var result = new PretNpcTradeHaden(source, maps.ToImmutable(), constants);
        foreach (var path in contract.GetProperty("footAccess").GetProperty("geometryOnlyPaths").EnumerateArray())
        {
            var mapId = new MapId(path.GetProperty("map").GetString()!);
            foreach (var point in path.GetProperty("cells").EnumerateArray())
                Need(result.AllowsFootDestination(mapId, point[0].GetInt32(), point[1].GetInt32()), "Haden authenticated eastern walking path differs.");
        }
        return result;
    }

    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
