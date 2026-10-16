using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretVermilionRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement ExternalCeruleanEvents { get; }
    public JsonElement Encounters { get; }

    private PretVermilionRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/vermilion");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalCeruleanEventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/CeruleanCity/map.json"));
        ExternalCeruleanEvents = ExternalCeruleanEventsDocument.RootElement.Clone();
        Encounters = PretEarlyFieldData.LandHeaders(project, "MAP_ROUTE5", "MAP_ROUTE6");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE5" => new("map.route-5"),
        "MAP_ROUTE5_SOUTH_ENTRANCE" => new("map.route-5-south-entrance"),
        "MAP_UNDERGROUND_PATH_NORTH_ENTRANCE" => new("map.underground-path-north-entrance"),
        "MAP_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL" => new("map.underground-path-north-south-tunnel"),
        "MAP_UNDERGROUND_PATH_SOUTH_ENTRANCE" => new("map.underground-path-south-entrance"),
        "MAP_ROUTE6_NORTH_ENTRANCE" => new("map.route-6-north-entrance"),
        "MAP_ROUTE6" => new("map.route-6"),
        "MAP_VERMILION_CITY" => new("map.vermilion-city"),
        "MAP_VERMILION_CITY_POKEMON_CENTER_1F" => new("map.vermilion-city-pokemon-center-1f"),
        "MAP_VERMILION_CITY_MART" => new("map.vermilion-city-mart"),
        "MAP_ROUTE5_POKEMON_DAY_CARE" => new("map.route-5-pokemon-day-care"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_SAFFRON_CITY_CONNECTION" => new("map.saffron-city-connection"),
        "MAP_ROUTE11" => new("map.route-11"),
        "MAP_SSANNE_EXTERIOR" => new("map.ssanne-exterior"),
        "MAP_VERMILION_CITY_HOUSE1" => new("map.vermilion-city-house-1"),
        "MAP_VERMILION_CITY_HOUSE2" => new("map.vermilion-city-house2"),
        "MAP_VERMILION_CITY_HOUSE3" => new("map.vermilion-city-house-3"),
        "MAP_VERMILION_CITY_POKEMON_FAN_CLUB" => new("map.vermilion-city-pokemon-fan-club"),
        "MAP_VERMILION_CITY_GYM" => new("map.vermilion-city-gym"),
        "MAP_VERMILION_CITY_POKEMON_CENTER_2F" => new("map.vermilion-city-pokemon-center-2f"),
        _ => PretBillRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretVermilionRegion Load(PretCurrentWorld world) => new(world);
}
