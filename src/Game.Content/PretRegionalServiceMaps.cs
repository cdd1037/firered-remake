using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretRegionalServiceMaps
{
    public const string Profile = "firered-regional-service-maps-import-v1";
    public static readonly MapId ElevatorMapId = new("map.celadon-city-department-store-elevator");
    /// <summary>All 17 authenticated source maps. Elevator events retain MAP_DYNAMIC / WARP_ID_DYNAMIC unchanged.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    /// <summary>16 maps with static warp records. Still require explicit field and service admission.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-5-south-entrance"), new("map.route-6-north-entrance"), new("map.route-7-east-entrance"), new("map.route-8-west-entrance")];
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretRegionalServiceMaps(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = directory; SourceMaps = maps; NewMaps = maps.Remove(ElevatorMapId);
        Constants = constants; }

    /// <summary>Checks the existing gate instances before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.TryGetValue(id, out var map), "Existing gate is missing: " + id.Value);
        }
    }

    /// <summary>Maps use the compact representation; source cells expand only on request.</summary>
    public static PretRegionalServiceMaps Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/regional-service-maps");
        var maps = world.MapsInDirectory(directory);
        var constants = PretSharedConstants.ForPackage(directory);
        Need(constants["ITEM_TEA"] == 369 && constants["FLAG_GOT_TEA"] == 678 && constants["FLAG_GOT_EEVEE"] == 611 && constants["LOCALID_EEVEE_POKEBALL"] == 2 && constants["SPECIES_EEVEE"] == 133, "Tea/Eevee source constants differ.");
        return new(directory, maps, constants);
    }
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Regional source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Regional source file symlink rejected.");
        }
        return result;
    }
}
