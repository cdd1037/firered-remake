using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretCeladonApproachRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.lavender-town")];
    public JsonElement ExternalLavenderEvents { get; }
    public JsonElement ExternalRoute16Events { get; }

    private PretCeladonApproachRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/celadon-approach");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalLavenderEventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/LavenderTown/map.json"));
        ExternalLavenderEvents = ExternalLavenderEventsDocument.RootElement.Clone();
        using var ExternalRoute16EventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/Route16/map.json"));
        ExternalRoute16Events = ExternalRoute16EventsDocument.RootElement.Clone();
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CELADON_CITY" => new("map.celadon-city"),
        "MAP_CELADON_CITY_CONDOMINIUMS_1F" => new("map.celadon-city-condominiums-1f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_1F" => new("map.celadon-city-department-store-1f"),
        "MAP_CELADON_CITY_GAME_CORNER" => new("map.celadon-city-game-corner"),
        "MAP_CELADON_CITY_GAME_CORNER_PRIZE_ROOM" => new("map.celadon-city-game-corner-prize-room"),
        "MAP_CELADON_CITY_GYM" => new("map.celadon-city-gym"),
        "MAP_CELADON_CITY_HOTEL" => new("map.celadon-city-hotel"),
        "MAP_CELADON_CITY_HOUSE1" => new("map.celadon-city-house-1"),
        "MAP_CELADON_CITY_POKEMON_CENTER_1F" => new("map.celadon-city-pokemon-center-1f"),
        "MAP_CELADON_CITY_POKEMON_CENTER_2F" => new("map.celadon-city-pokemon-center-2f"),
        "MAP_CELADON_CITY_RESTAURANT" => new("map.celadon-city-restaurant"),
        "MAP_LAVENDER_TOWN" => new("map.lavender-town"),
        "MAP_LAVENDER_TOWN_HOUSE1" => new("map.lavender-town-house-1"),
        "MAP_LAVENDER_TOWN_MART" => new("map.lavender-town-mart"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_1F" => new("map.lavender-town-pokemon-center-1f"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_2F" => new("map.lavender-town-pokemon-center-2f"),
        "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE" => new("map.lavender-town-volunteer-pokemon-house"),
        "MAP_ROUTE16" => new("map.route-16"),
        "MAP_ROUTE7" => new("map.route-7"),
        "MAP_ROUTE7_EAST_ENTRANCE" => new("map.route-7-east-entrance"),
        "MAP_ROUTE8" => new("map.route-8"),
        "MAP_ROUTE8_WEST_ENTRANCE" => new("map.route-8-west-entrance"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_SAFFRON_CITY_CONNECTION" => new("map.saffron-city-connection"),
        "MAP_TRADE_CENTER" => new("map.trade-center"),
        "MAP_UNDERGROUND_PATH_EAST_ENTRANCE" => new("map.underground-path-east-entrance"),
        "MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL" => new("map.underground-path-east-west-tunnel"),
        "MAP_UNDERGROUND_PATH_WEST_ENTRANCE" => new("map.underground-path-west-entrance"),
        "MAP_UNION_ROOM" => new("map.union-room"),
        _ => PretRockTunnelRegion.MapIdFromSourceSymbol(symbol)
    };

    public static PretCeladonApproachRegion Load(PretCurrentWorld world) => new(world);
}
