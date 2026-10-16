using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretMtMoonRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement Encounters { get; }

    private PretMtMoonRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/mtmoon");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        Encounters = PretEarlyFieldData.LandHeaders(project, "MAP_MT_MOON_1F", "MAP_MT_MOON_B1F", "MAP_MT_MOON_B2F");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_MT_MOON_1F" => new("map.mt-moon-1f"),
        "MAP_MT_MOON_B1F" => new("map.mt-moon-b1f"),
        "MAP_MT_MOON_B2F" => new("map.mt-moon-b2f"),
        _ => PretRoute3Region.MapIdFromSourceSymbol(symbol)
    };



    public static PretMtMoonRegion Load(PretCurrentWorld world) => new(world);
}
