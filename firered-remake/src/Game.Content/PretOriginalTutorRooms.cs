using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Two original tutor interiors and their unchanged adjacent entrances; teaching admission is separate.</summary>
public sealed class PretOriginalTutorRooms
{
    public const string ResourceId = "firered-original-tutor-rooms-v1";
    public static MapId BodySlamMapId { get; } = new("map.four-island-house1");
    public static MapId MetronomeMapId { get; } = new("map.cinnabar-island-pokemon-lab-research-room");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretOriginalTutorRooms(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND_HOUSE1" => BodySlamMapId,
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM" => MetronomeMapId,
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        _ => throw new InvalidDataException("Unknown original tutor room map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need((map.Id == BodySlamMapId || map.Id == MetronomeMapId) &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Original tutor rooms must retain their disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretOriginalTutorRooms Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"original-tutor-rooms");
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_FAT_MAN", "OBJ_EVENT_GFX_POKE_MANIAC",
            "OBJ_EVENT_GFX_SCIENTIST", "OBJ_EVENT_GFX_RED_NORMAL"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!),
                "Original tutor room actors must reuse the original existing resources.");
        }
        Need(graphics.Count == 0, "Original tutor room actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 2 ,
            "Original tutor room catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Two original tutor interiors; teaching definitions and current-game admission are separate.", ResourceId, []);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" ,
                "Original tutor room compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.FieldWarps.Length == 1 && map.Events.GetProperty("object_events").GetArrayLength() == 2 &&
                map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
                map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Original tutor room event scope differs.");
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals([BodySlamMapId, MetronomeMapId]), "Original tutor room map collection differs.");
        ValidateRoom(maps[BodySlamMapId], 11, 9, "LAYOUT_HOUSE3", 0, 4, 7, "MAP_FOUR_ISLAND", "2");
        ValidateRoom(maps[MetronomeMapId], 15, 11, "LAYOUT_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM", 2,
            7, 9, "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE", "4");
        ValidateAnchors(source, dependencyRoot, maps);
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["MOVETUTOR_BODY_SLAM"] == 3 && constants["MOVETUTOR_METRONOME"] == 8,
            "Original tutor room teaching indexes differ.");
        return new(source, maps.ToImmutable(), constants);
    }

    private static void ValidateRoom(PretWorldMap map, int width, int height, string layout, int backgroundCount,
        int exitX, int exitY, string destination, string destinationIndex)
    {
        Need(map.Width == width && map.Height == height && map.Cells.Length == width * height &&
            map.Events.GetProperty("layout").GetString() == layout && map.Events.GetProperty("bg_events").GetArrayLength() == backgroundCount,
            "Original tutor room geometry or background event count differs.");
        var door = map.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == exitX && door.GetProperty("y").GetInt32() == exitY &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == destination &&
            door.GetProperty("dest_warp_id").GetString() == destinationIndex, "Original tutor room exit differs.");
    }

    private static void ValidateAnchors(SilphSourcePackage source, string dependencyRoot,
        ImmutableDictionary<MapId, PretWorldMap>.Builder maps)
    {
        var anchors = source.Document("contracts.json").GetProperty("retainedAnchors");
        Need(anchors.GetArrayLength() == 2, "Original tutor room adjacent-map scope differs.");
        foreach (var retained in anchors.EnumerateArray())
        {
            string package = retained.GetProperty("currentPackage").GetString()!;
            var resource = retained.GetProperty("currentResource");
            var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot,package + "/" + resource.GetProperty("path").GetString()));
            var events = data.GetProperty("events");
            var entrance = retained.GetProperty("entrances")[0];
            int index = entrance.GetProperty("index").GetInt32();
            var door = events.GetProperty("warp_events")[index];
            Need(JsonElement.DeepEquals(door, entrance.GetProperty("event")), "Original tutor room entrance differs.");
            var room = maps[MapIdFromSourceSymbol(door.GetProperty("dest_map").GetString()!)];
            var exit = room.Events.GetProperty("warp_events")[0];
            Need(door.GetProperty("dest_warp_id").GetString() == "0" &&
                exit.GetProperty("dest_map").GetString() == events.GetProperty("id").GetString() &&
                exit.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Original tutor room doors are not reciprocal.");
            Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
                "Original tutor rooms must not rewrite adjacent map geometry or events.");
        }
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
