using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Encounters;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Exact Six Island source maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretSixIslandRegion
{
    public const string Profile = "firered-six-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public ImmutableArray<EarlyLandEncounterTable> LandEncounters { get; }
    public string GeneratedDirectory { get; }
    private PretSixIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, Func<string, MapId> resolveMap)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");

        LandEncounters = PretWildEncounterTables.Land(source.Document("encounters.json"), resolveMap);
        Need(LandEncounters.All(t => maps.ContainsKey(t.Map)), "Six Island encounter map is not mounted.");
        InitializeField(source);
    }
    public static PretSixIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = SixIslandSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.Count > 0, "Empty Six Island map collection.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SixIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants, world.Resolve);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
