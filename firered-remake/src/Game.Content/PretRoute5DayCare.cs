using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Route 5 Day Care, source scripts and existing town door.</summary>
public sealed class PretRoute5DayCare
{
    public const string ResourceId = "firered-route5-daycare-v1";
    public static MapId HouseMapId { get; } = new("map.route-5-pokemon-day-care");
    public static MapId DayCareMapId => HouseMapId;
    public const string EntryScript = "Route5_PokemonDayCare_EventScript_DaycareMan";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretRoute5DayCare(SilphSourcePackage source, PretWorldMap house,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house);
        Constants = constants;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE5_POKEMON_DAY_CARE" => HouseMapId,
        "MAP_ROUTE5" => new("map.route-5"),
        _ => throw new InvalidDataException("Unknown Route 5 Day Care map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Route 5 Day Care must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretRoute5DayCare Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"route5-daycare");
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Route 5 Day Care actor collection differs.");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in npc.EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && graphic is "OBJ_EVENT_GFX_GENTLEMAN" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Route 5 Day Care actors must reuse the original existing resources.");
        }
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 ,
            "Route 5 Day Care catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Route 5 Day Care; service rules and current-game admission are separate.", ResourceId);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" ,
            "Route 5 Day Care compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 12 && runtime.Height == 9 &&
            runtime.Events.GetProperty("layout").GetString() == "LAYOUT_ROUTE5_POKEMON_DAY_CARE", "Route 5 Day Care layout differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 3 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Route 5 Day Care original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_GENTLEMAN" &&
            actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("movement_type").GetString() == "MOVEMENT_TYPE_FACE_DOWN" &&
            actor.GetProperty("script").GetString() == "Route5_PokemonDayCare_EventScript_DaycareMan", "Route 5 Day Care original actor differs.");
        for (int i = 0; i < 3; i++)
        {
            var door = house.Events.GetProperty("warp_events")[i];
            Need(door.GetProperty("x").GetInt32() == 3 + i && door.GetProperty("y").GetInt32() == 7 &&
                door.GetProperty("elevation").GetInt32() == 0 && door.GetProperty("dest_map").GetString() == "MAP_ROUTE5" &&
                door.GetProperty("dest_warp_id").GetString() == "1", "Route 5 Day Care original exit differs.");
        }
        ValidateTown(source, dependencyRoot);
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["PARTY_SIZE"] == 6 && constants["GAME_STAT_USED_DAYCARE"] == 47 &&
            constants["YES"] == 1 && constants["MUS_PEWTER"] == 314,
            "Route 5 Day Care source party/stat/music constants differ.");
        return new(source, house, constants);
    }

    private static void ValidateTown(SilphSourcePackage source, string dependencyRoot)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedTown");
        string package = retained.GetProperty("currentPackage").GetString()!;
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot,package + "/" + town.GetProperty("path").GetString()));
        var entrance = data.GetProperty("events").GetProperty("warp_events")[1];
        Need(entrance.GetProperty("x").GetInt32() == 23 && entrance.GetProperty("y").GetInt32() == 25 &&
            entrance.GetProperty("elevation").GetInt32() == 0 && entrance.GetProperty("dest_map").GetString() == "MAP_ROUTE5_POKEMON_DAY_CARE" &&
            entrance.GetProperty("dest_warp_id").GetString() == "1", "Original Route 5 house entrance differs.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Route 5 Day Care must not rewrite its original town entrance.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
