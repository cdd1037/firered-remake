using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Twenty-four exact event-island maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretEventIslandRegion
{
    public const string Profile = "firered-event-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public EventIslandFieldData Field { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    private PretEventIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, PretCurrentWorld world)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");


        Field = LoadField(world, NewMaps, source, constants);
    }
    public static PretEventIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root,"maps/regions/event-island");

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "EventIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants, world);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
