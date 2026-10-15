using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Thirteen exact source maps. This package does not activate scripts, rewards or field transitions.</summary>
public sealed partial class PretSilphRegion
{
    public const string Profile = "firered-silph-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalSaffronEvents => Catalog.GetProperty("externalSaffronEvents");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSilphRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants; 
        Catalog = source.Document("silph-region-catalog.json"); Contracts = source.Document("contracts.json");
        ObjectTemplates = source.Document("objects.json"); NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol.StartsWith("MAP_SILPH_CO_", StringComparison.Ordinal) || symbol.StartsWith("MAP_SAFFRON_CITY", StringComparison.Ordinal)
        ? new("map." + symbol[4..].ToLowerInvariant().Replace('_', '-'))
        : symbol == "MAP_DYNAMIC" ? PretRocketHideoutRegion.DynamicMapId : PretRegionalServiceMaps.MapIdFromSourceSymbol(symbol);
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Silph connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSilphRegion Load(string root)
    {
        var source = new SilphSourcePackage(root,"silph-region");
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("silph-region-catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Saffron approach and12 Silph floors/elevator; application admission remains separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "Silph runtime certificate differs.");
            var payload = source.Read(relative + "/runtime-map.json");
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 13 && maps.Values.Sum(m => m.Width * m.Height) == 10644 && maps.Values.Sum(m => m.FieldWarps.Length) == 84,
            "Silph source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "Silph target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("doors").GetArrayLength() == 20 && contracts.GetProperty("pads").GetArrayLength() == 34 &&
            contracts.GetProperty("elevatorFloors").GetArrayLength() == 11 && contracts.GetProperty("pickups").GetArrayLength() == 17 &&
            contracts.GetProperty("hiddenItems").GetArrayLength() == 12, "Silph field source scope differs.");
        foreach (var door in contracts.GetProperty("doors").EnumerateArray())
        {
            var map = maps[new(door.GetProperty("map").GetString()!)]; var label = door.GetProperty("script").GetString();
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Any(e => e.TryGetProperty("script", out var v) && v.GetString() == label), "Silph door lacks source event.");
        }
        foreach (var pad in contracts.GetProperty("pads").EnumerateArray())
        {
            var map = maps[new(pad.GetProperty("map").GetString()!)]; var warp = map.FieldWarps[pad.GetProperty("warpIndex").GetInt32()];
            Need(map.SourceBehaviorAt(warp.X, warp.Y) == 103 && warp.X == pad.GetProperty("x").GetInt32() && warp.Y == pad.GetProperty("y").GetInt32() &&
                warp.DestinationMapId == MapIdFromSourceSymbol(pad.GetProperty("destinationMap").GetString()!) && warp.DestinationWarpId == pad.GetProperty("destinationWarp").GetInt32(), "Silph pad source endpoint differs.");
        }
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
