using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Original Kindle Road, Mt. Ember Ruby and summit maps. Application admission remains separate.</summary>
public sealed partial class PretSeviiRubyRegion
{
    public const string Profile = "firered-sevii-ruby-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    private PretSeviiRubyRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");

        InitializeField(source);
    }
    public static PretSeviiRubyRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = SeviiRubySource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.Count > 0, "SeviiRuby map collection is empty.");
        foreach (var map in maps.Values)
            foreach (var warp in map.FieldWarps)
            {
                Need(map.GetTile(warp.X, warp.Y) is not null, "SeviiRuby warp is outside its map.");
                if (!maps.TryGetValue(warp.DestinationMapId, out var target)) continue;
                Need(warp.DestinationWarpId >= 0 && warp.DestinationWarpId < target.FieldWarps.Length,
                    "SeviiRuby target warp index differs.");
                var endpoint = target.FieldWarps[warp.DestinationWarpId];
                Need(endpoint.DestinationMapId == map.Id && target.GetTile(endpoint.X, endpoint.Y) is not null,
                    "SeviiRuby warp has no valid return endpoint.");
            }
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
