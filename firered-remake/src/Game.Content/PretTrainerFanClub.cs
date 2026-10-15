using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Saffron Trainer Fan Club, with authenticated source scripts and unchanged town entrance.</summary>
public sealed class PretTrainerFanClub
{
    public const string ResourceId = "firered-trainer-fan-club-v1";
    public const string ScriptPath = "data/maps/SaffronCity_PokemonTrainerFanClub/scripts.inc";
    public static MapId RoomMapId { get; } = new("map.saffron-city-pokemon-trainer-fan-club");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretTrainerFanClub(SilphSourcePackage source, PretWorldMap map,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(map.Id, map);
        Constants = constants;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB" => RoomMapId,
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Trainer Fan Club map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == RoomMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Trainer Fan Club must retain its disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>dependencyRoot supplies unchanged Saffron City and the nine existing NPC sheets.</summary>
    public static PretTrainerFanClub Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"trainer-fan-club");
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_BEAUTY", "OBJ_EVENT_GFX_BLACK_BELT",
            "OBJ_EVENT_GFX_CRUSH_GIRL", "OBJ_EVENT_GFX_GENTLEMAN", "OBJ_EVENT_GFX_LITTLE_GIRL",
            "OBJ_EVENT_GFX_RED_NORMAL", "OBJ_EVENT_GFX_ROCKER", "OBJ_EVENT_GFX_WOMAN_3",
            "OBJ_EVENT_GFX_YOUNGSTER"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!),
                "Trainer Fan Club actors must reuse the original existing resources.");
        }
        Need(graphics.Count == 0, "Trainer Fan Club actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("maps").GetArrayLength() == 1 ,
            "Trainer Fan Club catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Saffron Trainer Fan Club and original shared script closure.", ResourceId);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == RoomMapId.Value && path == relative + "/runtime-map.json" ,
            "Trainer Fan Club compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Trainer Fan Club map identity differs.");
        var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
        Need(map.Width == 11 && map.Height == 17 && map.FieldWarps.Length == 1 &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null &&
            map.Events.GetProperty("object_events").GetArrayLength() == 10 &&
            map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
            map.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            map.Events.GetProperty("map_type").GetString() == "MAP_TYPE_INDOOR" &&
            map.Events.GetProperty("music").GetString() == "MUS_PEWTER" &&
            !map.Events.GetProperty("allow_cycling").GetBoolean() &&
            !map.Events.GetProperty("allow_escaping").GetBoolean() && !map.Events.GetProperty("allow_running").GetBoolean(),
            "Trainer Fan Club original map/event collection differs.");
        ValidateTopology(source, dependencyRoot, map);
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["NUM_TRAINER_FAN_CLUB_MEMBERS"] == 8 && constants["(NUM_TRAINER_FAN_CLUB_MEMBERS - 1)"] == 7 &&
            constants["VAR_FANCLUB_FAN_COUNTER"] == 0x4038 && constants["VAR_FANCLUB_LOSE_FAN_TIMER"] == 0x4039 &&
            constants["VAR_MAP_SCENE_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB"] == 0x4073 && constants["FAMECHECKER_BRUNO"] == 10,
            "Trainer Fan Club original constants differ.");
        return new(source, map, constants);
    }

    private static void ValidateTopology(SilphSourcePackage source, string dependencyRoot, PretWorldMap room)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedAnchor");
        string package = retained.GetProperty("currentPackage").GetString()!;
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot,package + "/" + town.GetProperty("path").GetString()));
        var entrance = data.GetProperty("events").GetProperty("warp_events")[14];
        var exit = room.Events.GetProperty("warp_events")[0];
        Need(Warp(entrance, 47, 21, 0, "MAP_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB", "0") &&
            Warp(exit, 5, 15, 3, "MAP_SAFFRON_CITY", "14") &&
            retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Trainer Fan Club must retain its original reciprocal town door.");
    }

    private static bool Warp(JsonElement value, int x, int y, int elevation, string map, string index) =>
        value.GetProperty("x").GetInt32() == x && value.GetProperty("y").GetInt32() == y &&
        value.GetProperty("elevation").GetInt32() == elevation && value.GetProperty("dest_map").GetString() == map &&
        value.GetProperty("dest_warp_id").GetString() == index;

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
