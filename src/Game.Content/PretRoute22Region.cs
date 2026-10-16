using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretRoute22Region
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement Encounters { get; }

    private PretRoute22Region(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/route22");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        Encounters = PretEarlyFieldData.LandHeaders(project, "MAP_ROUTE22");

    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE22" => new("map.route-22"),
        "MAP_ROUTE22_NORTH_ENTRANCE" => new("map.route-22-north-entrance"),
        "MAP_ROUTE23" => new("map.route-23"),
        "MAP_VICTORY_ROAD_1F" => new("map.victory-road-1f"),
        "MAP_VICTORY_ROAD_2F" => new("map.victory-road-2f"),
        "MAP_INDIGO_PLATEAU_EXTERIOR" => new("map.indigo-plateau-exterior"),
        _ => PretPewterRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretRoute22Region Load(PretCurrentWorld world) => new(world);
}
