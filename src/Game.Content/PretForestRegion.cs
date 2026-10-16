using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretForestRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement Encounters { get; }

    private PretForestRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/forest");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        Encounters = PretEarlyFieldData.LandHeaders(project, "MAP_ROUTE2", "MAP_VIRIDIAN_FOREST");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE2" => new("map.route-2"),
        "MAP_VIRIDIAN_FOREST" => new("map.viridian-forest"),
        "MAP_ROUTE2_VIRIDIAN_FOREST_SOUTH_ENTRANCE" => new("map.route-2-forest-south-gate"),
        "MAP_ROUTE2_VIRIDIAN_FOREST_NORTH_ENTRANCE" => new("map.route-2-forest-north-gate"),
        "MAP_PEWTER_CITY" => new("map.pewter-city"),
        "MAP_DIGLETTS_CAVE_NORTH_ENTRANCE" => new("map.digletts-cave-north-entrance"),
        "MAP_ROUTE2_HOUSE" => new("map.route-2-house"),
        "MAP_ROUTE2_EAST_BUILDING" => new("map.route-2-east-building"),
        _ => PretViridianRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretForestRegion Load(PretCurrentWorld world) => new(world);
}
