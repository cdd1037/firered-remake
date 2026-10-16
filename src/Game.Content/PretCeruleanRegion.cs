using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretCeruleanRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement ExternalRoute9Events { get; }

    private PretCeruleanRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/cerulean");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalRoute9EventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/Route9/map.json"));
        ExternalRoute9Events = ExternalRoute9EventsDocument.RootElement.Clone();
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        "MAP_CERULEAN_CITY_POKEMON_CENTER_1F" => new("map.cerulean-city-pokemon-center-1f"),
        "MAP_CERULEAN_CITY_POKEMON_CENTER_2F" => new("map.cerulean-city-pokemon-center-2f"),
        "MAP_CERULEAN_CITY_MART" => new("map.cerulean-city-mart"),
        "MAP_CERULEAN_CITY_GYM" => new("map.cerulean-city-gym"),
        "MAP_CERULEAN_CITY_BIKE_SHOP" => new("map.cerulean-city-bike-shop"),
        "MAP_CERULEAN_CITY_HOUSE1" => new("map.cerulean-city-house-1"),
        "MAP_CERULEAN_CITY_HOUSE2" => new("map.cerulean-city-house-2"),
        "MAP_CERULEAN_CITY_HOUSE3" => new("map.cerulean-city-house-3"),
        "MAP_CERULEAN_CITY_HOUSE4" => new("map.cerulean-city-house-4"),
        "MAP_CERULEAN_CITY_HOUSE5" => new("map.cerulean-city-house-5"),
        "MAP_ROUTE4" => new("map.route-4"),
        "MAP_ROUTE5" => new("map.route-5"),
        "MAP_ROUTE9" => new("map.route-9"),
        "MAP_ROUTE24" => new("map.route-24"),
        "MAP_CERULEAN_CAVE_1F" => new("map.cerulean-cave-1f"),
        "MAP_UNION_ROOM" => new("map.union-room"),
        "MAP_TRADE_CENTER" => new("map.trade-center"),
        _ => PretMtMoonRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretCeruleanRegion Load(PretCurrentWorld world) => new(world);
}
