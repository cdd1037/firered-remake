using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretRoute11Region
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-2")];
    public JsonElement ExternalRoute2Events { get; }

    private PretRoute11Region(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/route11");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalRoute2EventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/Route2/map.json"));
        ExternalRoute2Events = ExternalRoute2EventsDocument.RootElement.Clone();
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE11" => new("map.route-11"),
        "MAP_DIGLETTS_CAVE_SOUTH_ENTRANCE" => new("map.digletts-cave-south-entrance"),
        "MAP_DIGLETTS_CAVE_B1F" => new("map.digletts-cave-b1f"),
        "MAP_DIGLETTS_CAVE_NORTH_ENTRANCE" => new("map.digletts-cave-north-entrance"),
        "MAP_ROUTE2_EAST_BUILDING" => new("map.route-2-east-building"),
        "MAP_ROUTE2_HOUSE" => new("map.route-2-house"),
        "MAP_ROUTE11_EAST_ENTRANCE_1F" => new("map.route-11-east-entrance-1f"),
        "MAP_ROUTE11_EAST_ENTRANCE_2F" => new("map.route-11-east-entrance-2f"),
        "MAP_ROUTE12" => new("map.route-12"),
        _ => PretSsAnneRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretRoute11Region Load(PretCurrentWorld world) => new(world);
}
