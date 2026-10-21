using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Eighteen exact Seven Island maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretSevenIslandRegion
{
    public const string Profile = "firered-seven-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public SevenIslandFieldData Field { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    private PretSevenIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, PretCurrentWorld world)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");


        InitializeField(source);
        Field = LoadField(world, NewMaps, Contracts, constants);
    }
    public static PretSevenIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root,"maps/regions/seven-island");

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SevenIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants, world);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
