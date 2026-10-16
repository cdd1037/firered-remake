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
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_MT_EMBER_EXTERIOR" => new("map.mt-ember-exterior"),
        "MAP_MT_EMBER_RUBY_PATH_1F" => new("map.mt-ember-ruby-path-1f"),
        "MAP_MT_EMBER_RUBY_PATH_B1F" => new("map.mt-ember-ruby-path-b1f"),
        "MAP_MT_EMBER_RUBY_PATH_B1F_STAIRS" => new("map.mt-ember-ruby-path-b1f-stairs"),
        "MAP_MT_EMBER_RUBY_PATH_B2F" => new("map.mt-ember-ruby-path-b2f"),
        "MAP_MT_EMBER_RUBY_PATH_B2F_STAIRS" => new("map.mt-ember-ruby-path-b2f-stairs"),
        "MAP_MT_EMBER_RUBY_PATH_B3F" => new("map.mt-ember-ruby-path-b3f"),
        "MAP_MT_EMBER_RUBY_PATH_B4F" => new("map.mt-ember-ruby-path-b4f"),
        "MAP_MT_EMBER_RUBY_PATH_B5F" => new("map.mt-ember-ruby-path-b5f"),
        "MAP_MT_EMBER_SUMMIT" => new("map.mt-ember-summit"),
        "MAP_MT_EMBER_SUMMIT_PATH_1F" => new("map.mt-ember-summit-path-1f"),
        "MAP_MT_EMBER_SUMMIT_PATH_2F" => new("map.mt-ember-summit-path-2f"),
        "MAP_MT_EMBER_SUMMIT_PATH_3F" => new("map.mt-ember-summit-path-3f"),
        "MAP_ONE_ISLAND" => new("map.one-island"),
        "MAP_ONE_ISLAND_KINDLE_ROAD" => new("map.one-island-kindle-road"),
        "MAP_ONE_ISLAND_KINDLE_ROAD_EMBER_SPA" => new("map.one-island-kindle-road-ember-spa"),
        _ => PretFirstSeviiRegion.MapIdFromSourceSymbol(symbol)
    };

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
