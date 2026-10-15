using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original ordinary School/House interiors, with source scripts and unchanged exterior entrances.</summary>
public sealed class PretSchoolHouseInteriors
{
    public const string ResourceId = "firered-school-house-interiors-v1";
    public static MapId SchoolMapId { get; } = new("map.viridian-school");
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-1");
    public static MapId ThreeIslandHouseMapId { get; } = new("map.three-island-house2");
    private sealed record RoomSpec(MapId Id, string SourceName, string Layout, int Width, int Height,
        int Actors, int Backgrounds, string Destination, string DestinationIndex, (int X, int Elevation)[] Exits);
    private static readonly RoomSpec[] Rooms = [
        new(SchoolMapId, "ViridianCity_School", "LAYOUT_VIRIDIAN_CITY_SCHOOL", 10, 9, 2, 5,
            "MAP_VIRIDIAN_CITY", "3", [(3,0),(4,0),(5,3)]),
        new(HouseMapId, "FuchsiaCity_House1", "LAYOUT_HOUSE1", 11, 9, 3, 0,
            "MAP_FUCHSIA_CITY", "5", [(3,3),(4,3),(5,3)]),
        new(ThreeIslandHouseMapId, "ThreeIsland_House2", "LAYOUT_HOUSE3", 11, 9, 2, 0,
            "MAP_THREE_ISLAND", "3", [(4,3)])];
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSchoolHouseInteriors(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VIRIDIAN_CITY_SCHOOL" => SchoolMapId,
        "MAP_FUCHSIA_CITY_HOUSE1" => HouseMapId,
        "MAP_THREE_ISLAND_HOUSE2" => ThreeIslandHouseMapId,
        "MAP_THREE_ISLAND" => new("map.three-island"),
        "MAP_VIRIDIAN_CITY" => new("map.viridian-city"),
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        _ => throw new InvalidDataException("Unknown school/house interior map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(Rooms.Any(r => r.Id == map.Id) &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "School/house interiors must retain their disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretSchoolHouseInteriors Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/school-house-interiors");
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_WOMAN_2", "OBJ_EVENT_GFX_LASS",
            "OBJ_EVENT_GFX_OLD_MAN_1", "OBJ_EVENT_GFX_LITTLE_BOY", "OBJ_EVENT_GFX_RED_NORMAL",
            "OBJ_EVENT_GFX_ROCKER", "OBJ_EVENT_GFX_MAN"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!),
                "School/house interior actors must reuse the original existing resources.");
        }
        Need(graphics.Count == 0, "School/house interior actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == Rooms.Length ,
            "School/house interior catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Three source-authenticated ordinary interiors; grid and wall-map runtime admission remain separate.", ResourceId);

        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("mapPath").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative ,
                "School/house interior compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.FieldWarps.Length == Rooms.Single(r => r.Id == map.Id).Exits.Length &&
                map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
                map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "School/house interior event scope differs.");
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals(Rooms.Select(r => r.Id)), "School/house interior map collection differs.");
        foreach (var room in Rooms) ValidateRoom(maps[room.Id], room);
        ValidateAnchors(source, dependencyRoot, maps);

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["LOCALID_SCHOOL_WOMAN"] == 1 && constants["LOCALID_SCHOOL_LASS"] == 2 &&
            constants["MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD"] == 15 && constants["FAMECHECKER_OAK"] == 0 &&
            constants["FAMECHECKER_DAISY"] == 1 && constants["FAMECHECKER_BILL"] == 13 && constants["FAMECHECKER_SABRINA"] == 7,
            "Original school actor/menu and Fame constants differ.");
        return new(source, maps.ToImmutable(), constants);
    }

    private static void ValidateRoom(PretWorldMap map, RoomSpec room)
    {
        Need(map.Width == room.Width && map.Height == room.Height && map.Cells.Length == room.Width * room.Height &&
            map.Events.GetProperty("layout").GetString() == room.Layout &&
            map.Events.GetProperty("object_events").GetArrayLength() == room.Actors &&
            map.Events.GetProperty("bg_events").GetArrayLength() == room.Backgrounds,
            "Original interior geometry or event counts differ.");
        for (int i = 0; i < room.Exits.Length; i++)
        {
            var door = map.Events.GetProperty("warp_events")[i];
            Need(door.GetProperty("x").GetInt32() == room.Exits[i].X && door.GetProperty("y").GetInt32() == 7 &&
                door.GetProperty("elevation").GetInt32() == room.Exits[i].Elevation &&
                door.GetProperty("dest_map").GetString() == room.Destination &&
                door.GetProperty("dest_warp_id").GetString() == room.DestinationIndex, "Original interior exit differs.");
        }
    }

    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var anchor in Contracts.GetProperty("retainedAnchors").EnumerateArray())
        {
            var id = MapIdFromSourceSymbol(anchor.GetProperty("sourceMap").GetString()!);
            Need(existing.TryGetValue(id, out var map) ,
                "Retained original exterior identity differs.");
            var entrance = anchor.GetProperty("entrances")[0];
            Need(JsonElement.DeepEquals(map!.Events.GetProperty("warp_events")[entrance.GetProperty("index").GetInt32()],
                entrance.GetProperty("event")), "Retained original exterior entrance differs.");
        }
    }

    private static void ValidateAnchors(SilphSourcePackage source, string dependencyRoot,
        ImmutableDictionary<MapId, PretWorldMap>.Builder maps)
    {
        var anchors = source.Document("contracts.json").GetProperty("retainedAnchors");
        Need(anchors.GetArrayLength() == Rooms.Length, "School/house interior adjacent-map scope differs.");
        foreach (var retained in anchors.EnumerateArray())
        {
            Need(retained.GetProperty("currentPackage").GetString() == "content/generated", "School/house anchor must use the generated root.");
            var resource = retained.GetProperty("currentResource");
            var data = SilphSourcePackage.Json(SafariSource.Read(Path.Combine(dependencyRoot, "content/generated"), resource.GetProperty("path").GetString()!));
            var events = data.GetProperty("events");
            var entrance = retained.GetProperty("entrances")[0];
            int index = entrance.GetProperty("index").GetInt32();
            var door = events.GetProperty("warp_events")[index];
            Need(JsonElement.DeepEquals(door, entrance.GetProperty("event")), "School/house interior entrance differs.");
            var room = maps[MapIdFromSourceSymbol(door.GetProperty("dest_map").GetString()!)];
            int destinationIndex = int.Parse(door.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Need(destinationIndex >= 0 && destinationIndex < room.FieldWarps.Length, "Room entry warp index is outside original exits.");
            var exit = room.Events.GetProperty("warp_events")[destinationIndex];
            Need(exit.GetProperty("dest_map").GetString() == events.GetProperty("id").GetString() &&
                exit.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "School/house interior doors are not reciprocal.");
            Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
                "School/house interiors must not rewrite adjacent map geometry or events.");
        }
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
