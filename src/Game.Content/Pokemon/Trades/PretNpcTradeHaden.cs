using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Complete original Route18 and east gate maps. Runtime admission is bounded to Haden's eastern walking approach.</summary>
public sealed class PretNpcTradeHaden
{
    public const string ResourceId = "firered-npc-trade-haden-v1";
    public static MapId RouteMapId { get; } = new("map.route-18");
    public static MapId Gate1FMapId { get; } = new("map.route-18-east-entrance-1f");
    public static MapId Gate2FMapId { get; } = new("map.route-18-east-entrance-2f");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretNpcTradeHaden(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/npc-trade-haden");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE18" => RouteMapId,
        "MAP_ROUTE18_EAST_ENTRANCE_1F" => Gate1FMapId,
        "MAP_ROUTE18_EAST_ENTRANCE_2F" => Gate2FMapId,
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        // Preserve the source connection; this does not register or open Route17.
        "MAP_ROUTE17" => new("map.route-17"),
        _ => throw new InvalidDataException("Unknown original Haden approach destination: " + symbol)
    };



    /// <summary>Source-authenticated eastern foot cells only. Other maps return false. Scene=0 and normal movement/warp rules remain Application responsibilities.</summary>
    public bool AllowsFootDestination(MapId mapId, int x, int y)
    {
        if (!NewMaps.TryGetValue(mapId, out var map) || x < 0 || y < 0 || x >= map.Width || y >= map.Height) return false;
        int minimumX = mapId == RouteMapId ? 47 : mapId == Gate1FMapId ? 7 : 0;
        var cell = map.GetSourceCell(x, y)!;
        return x >= minimumX && cell.Collision == 0 && cell.Elevation == 3 &&
            cell.BehaviorName is "MB_NORMAL" or "MB_WEST_ARROW_WARP" or "MB_EAST_ARROW_WARP" or
                "MB_UP_RIGHT_STAIR_WARP" or "MB_DOWN_LEFT_STAIR_WARP";
    }

    public static PretNpcTradeHaden Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
