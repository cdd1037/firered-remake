using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Three exact source interiors. This package does not activate scripts, rewards or field transitions.</summary>
public sealed partial class PretSaffronGymRegion
{
    public const string Profile = "firered-saffron-gym-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }

    public JsonElement ExternalSaffronEvents { get; }
    public string GeneratedDirectory { get; }
    private PretSaffronGymRegion(PretCurrentWorld world, SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory;
        using var saffron = JsonDocument.Parse(world.ReadEvents("data/maps/SaffronCity/map.json"));
        ExternalSaffronEvents = saffron.RootElement.Clone(); NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");
    }
    public static PretSaffronGymRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root,"maps/regions/saffron-gym");

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "Silph target warp index differs.");

        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(world, source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
