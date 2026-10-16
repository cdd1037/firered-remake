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
    public JsonElement EncountersAndItems { get; }
    public string GeneratedDirectory { get; }

    private PretCapeBrinkTutor(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        EncountersAndItems = source.Document("encounters-items.json");
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory;

    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TWO_ISLAND_CAPE_BRINK" => CapeMapId,
        "MAP_TWO_ISLAND_CAPE_BRINK_HOUSE" => HouseMapId,
        "MAP_TWO_ISLAND" => new("map.two-island"),
        _ => throw new InvalidDataException("Unknown Cape Brink map symbol: " + symbol)
    };

    public CapeBrinkTutorCatalog AuthenticateTutor(PokemonCatalog owned) => CapeBrinkTutorCatalog.Authenticate(owned);



    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretCapeBrinkTutor Load(PretCurrentWorld world, string? dependencyRoot = null)
    {
        var projectRoot = world.ProjectRoot;
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/cape-brink-tutor");
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
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
        Need(PretWorldMapConnections.Create(cape).Connections.Length == 1 && PretWorldMapConnections.Create(house).Connections.IsEmpty,
            "Cape Brink connected map scope differs.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
