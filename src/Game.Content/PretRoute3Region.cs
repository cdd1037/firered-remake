using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretRoute3Region
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement Encounters { get; }

    private PretRoute3Region(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/route3");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        Encounters = PretEarlyFieldData.LandHeaders(project, "MAP_ROUTE3", "MAP_ROUTE4");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE3" => new("map.route-3"),
        "MAP_ROUTE4" => new("map.route-4"),
        "MAP_ROUTE4_POKEMON_CENTER_1F" => new("map.route-4-pokemon-center-1f"),
        "MAP_ROUTE4_POKEMON_CENTER_2F" => new("map.route-4-pokemon-center-2f"),
        "MAP_MT_MOON_1F" => new("map.mt-moon-1f"),
        "MAP_MT_MOON_B1F" => new("map.mt-moon-b1f"),
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        _ => PretRoute22Region.MapIdFromSourceSymbol(symbol)
    };



    public static PretRoute3Region Load(PretCurrentWorld world) => new(world);
}
