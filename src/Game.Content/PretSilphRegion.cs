using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Thirteen exact source maps. This package does not activate scripts, rewards or field transitions.</summary>
public sealed partial class PretSilphRegion
{
    public const string Profile = "firered-silph-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public JsonElement ExternalSaffronEvents { get; }
    public string GeneratedDirectory { get; }
    private PretSilphRegion(PretCurrentWorld world, SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory;
        using var saffron = JsonDocument.Parse(world.ReadEvents("data/maps/SaffronCity/map.json"));
        ExternalSaffronEvents = saffron.RootElement.Clone(); NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol.StartsWith("MAP_SILPH_CO_", StringComparison.Ordinal) || symbol.StartsWith("MAP_SAFFRON_CITY", StringComparison.Ordinal)
        ? new("map." + symbol[4..].ToLowerInvariant().Replace('_', '-'))
        : symbol == "MAP_DYNAMIC" ? PretRocketHideoutRegion.DynamicMapId : PretRegionalServiceMaps.MapIdFromSourceSymbol(symbol);

    public static PretSilphRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root,"maps/regions/silph");

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "Silph target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("doors").GetArrayLength() == 20 && contracts.GetProperty("pads").GetArrayLength() == 34 &&
            contracts.GetProperty("elevatorFloors").GetArrayLength() == 11 && contracts.GetProperty("pickups").GetArrayLength() == 17 &&
            contracts.GetProperty("hiddenItems").GetArrayLength() == 12, "Silph field source scope differs.");
        foreach (var door in contracts.GetProperty("doors").EnumerateArray())
        {
            var map = maps[new(door.GetProperty("map").GetString()!)]; var label = door.GetProperty("script").GetString();
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Any(e => e.TryGetProperty("script", out var v) && v.GetString() == label), "Silph door lacks source event.");
        }
        foreach (var pad in contracts.GetProperty("pads").EnumerateArray())
        {
            var map = maps[new(pad.GetProperty("map").GetString()!)]; var warp = map.FieldWarps[pad.GetProperty("warpIndex").GetInt32()];
            Need(map.SourceBehaviorAt(warp.X, warp.Y) == 103 && warp.X == pad.GetProperty("x").GetInt32() && warp.Y == pad.GetProperty("y").GetInt32() &&
                warp.DestinationMapId == MapIdFromSourceSymbol(pad.GetProperty("destinationMap").GetString()!) && warp.DestinationWarpId == pad.GetProperty("destinationWarp").GetInt32(), "Silph pad source endpoint differs.");
        }
        return new(world, source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
