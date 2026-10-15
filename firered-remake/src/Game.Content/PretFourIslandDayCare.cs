using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Four Island Day Care, source scripts and existing town door.</summary>
public sealed class PretFourIslandDayCare
{
    public const string ResourceId = "firered-four-island-daycare-v1";
    public static MapId HouseMapId { get; } = new("map.four-island-pokemon-day-care");
    public static MapId DayCareMapId => HouseMapId;
    public const string EntryScript = "FourIsland_PokemonDayCare_EventScript_DaycareWoman";
    public const string ManEntryScript = "FourIsland_EventScript_DaycareMan";
    public const string PendingEggPositionScript = "FourIsland_EventScript_TrySetDayCareManPos";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretFourIslandDayCare(SilphSourcePackage source, PretWorldMap house,
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
        "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" => HouseMapId,
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        _ => throw new InvalidDataException("Unknown Four Island Day Care map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Four Island Day Care must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretFourIslandDayCare Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"four-island-daycare");
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 3, "Four Island Day Care actor collection differs.");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in npc.EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && graphic is "OBJ_EVENT_GFX_OLD_WOMAN" or "OBJ_EVENT_GFX_OLD_MAN_1" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Four Island Day Care actors must reuse the original existing resources.");
        }
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Four Island Day Care catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Four Island Day Care; service rules and current-game admission are separate.", ResourceId);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" ,
            "Four Island Day Care compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 12 && runtime.Height == 9 &&
            runtime.Events.GetProperty("layout").GetString() == "LAYOUT_FOUR_ISLAND_POKEMON_DAY_CARE", "Four Island Day Care layout differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 1 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Four Island Day Care original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_OLD_WOMAN" &&
            actor.GetProperty("x").GetInt32() == 2 && actor.GetProperty("y").GetInt32() == 2 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("movement_type").GetString() == "MOVEMENT_TYPE_FACE_DOWN" &&
            actor.GetProperty("script").GetString() == EntryScript, "Four Island Day Care original actor differs.");
        var door = house.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == 4 && door.GetProperty("y").GetInt32() == 7 &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == "MAP_FOUR_ISLAND" &&
            door.GetProperty("dest_warp_id").GetString() == "1", "Four Island Day Care original exit differs.");
        ValidateTown(source, dependencyRoot);
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["PARTY_SIZE"] == 6 && constants["GAME_STAT_USED_DAYCARE"] == 47 &&
            constants["YES"] == 1 && constants["MUS_SEVII_45"] == 336 && constants["FLAG_PENDING_DAYCARE_EGG"] == 614 &&
            constants["LOCALID_FOUR_ISLAND_DAYCARE_MAN"] == 1 && constants["LOCALID_FOUR_ISLAND_DAYCARE_WOMAN"] == 1 &&
            constants["DAYCARE_NO_MONS"] == 0 && constants["DAYCARE_EGG_WAITING"] == 1 &&
            constants["DAYCARE_ONE_MON"] == 2 && constants["DAYCARE_TWO_MONS"] == 3 && constants["DAYCARE_EXITED_LEVEL_MENU"] == 2,
            "Four Island Day Care source party/stat/music constants differ.");
        return new(source, house, constants);
    }

    private static void ValidateTown(SilphSourcePackage source, string dependencyRoot)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedTown");
        string package = retained.GetProperty("currentPackage").GetString()!;
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot,package + "/" + town.GetProperty("path").GetString()));
        var entrance = data.GetProperty("events").GetProperty("warp_events")[1];
        Need(entrance.GetProperty("x").GetInt32() == 12 && entrance.GetProperty("y").GetInt32() == 13 &&
            entrance.GetProperty("elevation").GetInt32() == 0 && entrance.GetProperty("dest_map").GetString() == "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" &&
            entrance.GetProperty("dest_warp_id").GetString() == "0", "Original Four Island house entrance differs.");
        var man = data.GetProperty("events").GetProperty("object_events")[0];
        Need(man.GetProperty("local_id").GetString() == "LOCALID_FOUR_ISLAND_DAYCARE_MAN" &&
            man.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_OLD_MAN_1" &&
            man.GetProperty("x").GetInt32() == 16 && man.GetProperty("y").GetInt32() == 13 &&
            man.GetProperty("elevation").GetInt32() == 3 && man.GetProperty("script").GetString() == ManEntryScript &&
            JsonElement.DeepEquals(man, retained.GetProperty("dayCareMan").GetProperty("actor")),
            "Original exterior Day Care man differs.");
        Need(retained.GetProperty("dayCareMan").GetProperty("pendingEggPosition").GetProperty("x").GetInt32() == 16 &&
            retained.GetProperty("dayCareMan").GetProperty("pendingEggPosition").GetProperty("y").GetInt32() == 14,
            "Original pending-egg exterior position differs.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Four Island Day Care must not rewrite its original town entrance.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
