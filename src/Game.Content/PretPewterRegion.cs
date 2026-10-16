using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretPewterRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretPewterRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/pewter");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_PEWTER_CITY" => new("map.pewter-city"),
        "MAP_PEWTER_CITY_GYM" => new("map.pewter-gym"),
        "MAP_PEWTER_CITY_MART" => new("map.pewter-mart"),
        "MAP_PEWTER_CITY_POKEMON_CENTER_1F" => new("map.pewter-pokemon-center-1f"),
        "MAP_PEWTER_CITY_HOUSE1" => new("map.pewter-house-1"),
        "MAP_PEWTER_CITY_HOUSE2" => new("map.pewter-house-2"),
        "MAP_PEWTER_CITY_MUSEUM_1F" => new("map.pewter-museum-1f"),
        "MAP_PEWTER_CITY_MUSEUM_2F" => new("map.pewter-museum-2f"),
        "MAP_PEWTER_CITY_POKEMON_CENTER_2F" => new("map.pewter-pokemon-center-2f"),
        "MAP_ROUTE3" => new("map.route-3"),
        _ => PretForestRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretPewterRegion Load(PretCurrentWorld world) => new(world);
}
