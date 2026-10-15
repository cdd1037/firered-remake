using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Fuchsia City Move Deleter house, source scripts and existing town door.</summary>
public sealed class PretMoveDeleterHouse
{
    public const string ResourceId = "firered-move-deleter-v1";
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-3");
    private readonly string _projectRoot;
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretMoveDeleterHouse(SilphSourcePackage source, PretWorldMap house,
        ImmutableDictionary<string, int> constants)
    {
        _projectRoot = PretRuntimeMapValidation.ProjectRoot(source.Directory);
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house); Constants = constants;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FUCHSIA_CITY_HOUSE3" => HouseMapId,
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        _ => throw new InvalidDataException("Unknown Move Deleter house map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Move Deleter house must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Bind only after composition has constructed its final owned catalog. Reuses authenticated source bytes.</summary>
    public MoveDeleterCatalog Bind(PokemonCatalog owned) => MoveDeleterCatalog.Authenticate(owned, PretMoveDeleterTexts.LoadProject(_projectRoot));

    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretMoveDeleterHouse Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/move-deleter");
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Move Deleter house actor collection differs.");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in npc.EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && graphic is "OBJ_EVENT_GFX_BALDING_MAN" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Move Deleter house actors must reuse the original existing resources.");
        }
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 ,
            "Move Deleter house catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Fuchsia City Move Deleter house; service rules and current-game admission are separate.", ResourceId);

        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("mapPath").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == HouseMapId.Value && path == relative ,
            "Move Deleter house compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id && runtime.Width == 11 && runtime.Height == 9 &&
            runtime.Events.GetProperty("layout").GetString() == "LAYOUT_HOUSE1", "Move Deleter house layout differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 1 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Move Deleter original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_BALDING_MAN" &&
            actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("movement_type").GetString() == "MOVEMENT_TYPE_FACE_RIGHT" &&
            actor.GetProperty("script").GetString() == "FuchsiaCity_House3_EventScript_MoveDeleter", "Move Deleter original actor differs.");
        var door = house.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == 4 && door.GetProperty("y").GetInt32() == 7 &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY" &&
            door.GetProperty("dest_warp_id").GetString() == "10", "Move Deleter original exit differs.");
        ValidateTown(source, dependencyRoot);

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["PARTY_SIZE"] == 6 && constants["MAX_MON_MOVES"] == 4 &&
            constants["YES"] == 1 && constants["MUS_MOVE_DELETED"] == 270,
            "Move Deleter source party/move/fanfare constants differ.");
        return new(source, house, constants);
    }

    private static void ValidateTown(SilphSourcePackage source, string dependencyRoot)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedTown");
        string package = retained.GetProperty("currentPackage").GetString()!;
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot,package + "/" + town.GetProperty("path").GetString()));
        var entrance = data.GetProperty("events").GetProperty("warp_events")[10];
        Need(entrance.GetProperty("x").GetInt32() == 19 && entrance.GetProperty("y").GetInt32() == 31 &&
            entrance.GetProperty("elevation").GetInt32() == 0 && entrance.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY_HOUSE3" &&
            entrance.GetProperty("dest_warp_id").GetString() == "0", "Original Fuchsia City house entrance differs.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Move Deleter house must not rewrite its original town entrance.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
