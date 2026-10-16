using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>One exact Viridian Gym map with the reused city door gate. Application admission remains separate.</summary>
public sealed partial class PretViridianGymRegion
{
    public const string Profile = "firered-viridian-gym-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    private PretViridianGymRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;

        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VIRIDIAN_CITY_GYM" => new("map.viridian-city-gym"),
        "MAP_VIRIDIAN_CITY" => new("map.viridian-city"),
        _ => PretFirstSeviiRegion.MapIdFromSourceSymbol(symbol)
    };

    private static SilphSourcePackage ViridianGymSource(string root) => new(root,"maps/regions/viridian-gym");
    public static PretViridianGymRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = ViridianGymSource(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "ViridianGym target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
