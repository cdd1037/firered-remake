using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Five exact League approach maps with four new maps and a reused Route23 anchor. Application admission remains separate.</summary>
public sealed partial class PretVictoryRoadRegion
{
    public const string Profile = "firered-victory-road-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    private PretVictoryRoadRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; SourceMaps = maps; NewMaps = maps.Remove(new MapId("map.route-23")); Constants = constants;

        InitializeField(source);
    }
    private static SilphSourcePackage VictoryRoadSource(string root) => new(root,"maps/regions/victory-road");
    public static PretVictoryRoadRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = VictoryRoadSource(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "VictoryRoad target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
