using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Cape Brink and tutor house; source-authenticated teaching definitions and unchanged Two Island edge.</summary>
public sealed class PretCapeBrinkTutor
{
    public const string ResourceId = "firered-cape-brink-tutor-v1";
    public static MapId CapeMapId { get; } = new("map.two-island-cape-brink");
    public static MapId HouseMapId { get; } = new("map.two-island-cape-brink-house");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement EncountersAndItems { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretCapeBrinkTutor(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        EncountersAndItems = source.Document("encounters-items.json");
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TWO_ISLAND_CAPE_BRINK" => CapeMapId,
        "MAP_TWO_ISLAND_CAPE_BRINK_HOUSE" => HouseMapId,
        "MAP_TWO_ISLAND" => new("map.two-island"),
        _ => throw new InvalidDataException("Unknown Cape Brink map symbol: " + symbol)
    };

    public CapeBrinkTutorCatalog AuthenticateTutor(PokemonCatalog owned) => CapeBrinkTutorCatalog.Authenticate(owned);

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == CapeMapId || map.Id == HouseMapId, "Cape Brink map scope differs.");
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(
                c.GetProperty("direction").GetString() == "down" ? Direction.South : throw new InvalidDataException("Cape Brink direction differs."),
                c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretCapeBrinkTutor Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/cape-brink-tutor");
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_CHANNELER", "OBJ_EVENT_GFX_RED_NORMAL"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!),
                "Cape Brink actors must reuse the original existing resources.");
        }
        Need(graphics.Count == 0, "Cape Brink actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Cape Brink catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Cape Brink and ultimate tutor house; current-game admission is separate.", ResourceId);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("mapPath").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative ,
                "Cape Brink compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id, "Cape Brink source identity differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.FieldWarps.Length == 1 && map.Events.GetProperty("coord_events").GetArrayLength() == 0,
                "Cape Brink event scope differs.");
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals([CapeMapId, HouseMapId]), "Cape Brink map collection differs.");
        ValidateMaps(source, maps);
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["MOVETUTOR_FRENZY_PLANT"] == 15 && constants["MOVETUTOR_BLAST_BURN"] == 16 &&
            constants["MOVETUTOR_HYDRO_CANNON"] == 17 && constants["LOCALID_CAPE_BRINK_TUTOR"] == 1,
            "Cape Brink teaching indexes differ.");
        return new(source, maps.ToImmutable(), constants);
    }

    private static void ValidateMaps(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap>.Builder maps)
    {
        var cape = maps[CapeMapId]; var house = maps[HouseMapId];
        Need(cape.Width == 24 && cape.Height == 40 && house.Width == 11 && house.Height == 9 &&
            cape.Cells.Length + house.Cells.Length == 1059 && cape.BorderCells.Length + house.BorderCells.Length == 8,
            "Cape Brink source geometry differs.");
        Need(cape.Events.GetProperty("object_events").GetArrayLength() == 0 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            cape.Events.GetProperty("bg_events").GetArrayLength() == 2 && house.Events.GetProperty("bg_events").GetArrayLength() == 0,
            "Cape Brink original actor/item counts differ.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("local_id").GetString() == "LOCALID_CAPE_BRINK_TUTOR" &&
            actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_CHANNELER" &&
            actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("script").GetString() == "TwoIsland_CapeBrink_House_EventScript_StarterTutor", "Original Cape Brink actor differs.");
        Need(cape.Warps[0].DestinationMapId == HouseMapId && cape.Warps[0].DestinationWarpId == 0 &&
            house.Warps[0].DestinationMapId == CapeMapId && house.Warps[0].DestinationWarpId == 0,
            "Original Cape Brink reciprocal doors differ.");
        Need(ConnectedMap(cape).Connections.Length == 1 && ConnectedMap(house).Connections.IsEmpty,
            "Cape Brink connected map scope differs.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
