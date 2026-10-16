using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated resources for three cave maps. This is not a game profile or runtime admission.</summary>
public sealed class PretCeruleanCaveRegion
{
    public const string ResourceId = "firered-cerulean-cave-resources-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }

    public JsonElement Items { get; }
    public string GeneratedDirectory { get; }

    private PretCeruleanCaveRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants; GeneratedDirectory = source.Directory;
        Contracts = source.Document("contracts.json");

         Items = source.Document("items.json");

    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CERULEAN_CAVE_1F" => new("map.cerulean-cave-1f"),
        "MAP_CERULEAN_CAVE_2F" => new("map.cerulean-cave-2f"),
        "MAP_CERULEAN_CAVE_B1F" => new("map.cerulean-cave-b1f"),
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        _ => throw new InvalidDataException("Unknown Cerulean Cave map symbol: " + symbol)
    };

    public static PretCeruleanCaveRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/cerulean-cave");
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.Keys.ToHashSet().SetEquals(new[] { new MapId("map.cerulean-cave-1f"),new MapId("map.cerulean-cave-2f"),new MapId("map.cerulean-cave-b1f") }) , "Cerulean Cave map/warp collection differs.");

        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
