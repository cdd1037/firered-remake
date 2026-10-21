using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Five exact Five Island and Rocket Warehouse maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFiveIslandRegion
{
    public const string Profile = "firered-five-island-region-v1";
    // Authenticated source spans preserve the unloaded original connections.
    private const int WaterLabyrinthConnectionWidth = 72;
    private const int MemorialPillarConnectionHeight = 60;
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public string GeneratedDirectory { get; }
    private PretFiveIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");

        InitializeField(source);
    }
    public static PretFiveIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = FiveIslandSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FiveIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
