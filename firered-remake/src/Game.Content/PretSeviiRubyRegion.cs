using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Eleven exact Mt. Ember Ruby region maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretSeviiRubyRegion
{
    public const string Profile = "firered-sevii-ruby-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSeviiRubyRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants; 
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
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
        "MAP_MT_EMBER_SUMMIT_PATH_3F" => new("map.mt-ember-summit-path-3f"),
        "MAP_ONE_ISLAND" => new("map.one-island"),
        "MAP_ONE_ISLAND_KINDLE_ROAD" => new("map.one-island-kindle-road"),
        "MAP_ONE_ISLAND_KINDLE_ROAD_EMBER_SPA" => new("map.one-island-kindle-road-ember-spa"),
        _ => PretFirstSeviiRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown SeviiRuby connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSeviiRubyRegion Load(string root)
    {
        var source = SeviiRubySource.Region(root);
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Kindle Road and Mt. Ember Ruby source maps; application admission remains separate.", Profile, files);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "SeviiRuby runtime certificate differs.");
            var payload = source.Read(relative + "/runtime-map.json");
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 11 && maps.Values.Sum(m => m.Width * m.Height) == 9764 && maps.Values.Sum(m => m.FieldWarps.Length) == 27,
            "SeviiRuby source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SeviiRuby target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
