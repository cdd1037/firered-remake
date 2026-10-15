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
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalSaffronEvents => Catalog.GetProperty("externalSaffronEvents");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSaffronGymRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants; SourceFiles = ImmutableArray<PretWorldSourceFile>.Empty;
        Catalog = source.Document("saffron-gym-region-catalog.json"); Contracts = source.Document("contracts.json");
        ObjectTemplates = source.Document("objects.json"); NpcSources = source.Document("npc-source.json");
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol.StartsWith("MAP_SAFFRON_CITY", StringComparison.Ordinal)
        ? new("map." + symbol[4..].ToLowerInvariant().Replace('_', '-'))
        : PretRegionalServiceMaps.MapIdFromSourceSymbol(symbol);
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Saffron Gym connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSaffronGymRegion Load(string root)
    {
        var source = new SilphSourcePackage(root,"saffron-gym-region");
 var files = ImmutableArray<PretWorldSourceFile>.Empty; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var directory = source.Directory;
        var catalog = source.Document("saffron-gym-region-catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Saffron Gym, PokemonCenter1F and Mart; application admission remains separate.", Profile);
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
        Need(maps.Count == 3 && maps.Values.Sum(m => m.Width * m.Height) == 974 && maps.Values.Sum(m => m.FieldWarps.Length) == 40,
            "Saffron Gym source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "Silph target warp index differs.");
        
        var constants = PretSharedConstants.ForPackage(source.Directory);
        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("pads").GetArrayLength() == 30 && contracts.GetProperty("entries").GetArrayLength() == 3 &&
            contracts.GetProperty("journals").GetArrayLength() == 2 && NpcGraphics(source) == 11 &&
            contracts.GetProperty("martStock").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(["ITEM_GREAT_BALL","ITEM_HYPER_POTION","ITEM_REVIVE","ITEM_FULL_HEAL","ITEM_ESCAPE_ROPE","ITEM_MAX_REPEL"]),
            "Saffron Gym field scope differs.");
        Need(constants["FLAG_BADGE06_GET"] == 0x825 && constants["ITEM_TM04"] == 292 && constants["ITEM_MAX_REPEL"] == 84,
            "Saffron Gym constants differ.");
        foreach (var pad in contracts.GetProperty("pads").EnumerateArray())
        {
            var map = maps[new(pad.GetProperty("map").GetString()!)]; var warp = map.FieldWarps[pad.GetProperty("warpIndex").GetInt32()];
            Need(map.SourceBehaviorAt(warp.X, warp.Y) == 103 && warp.X == pad.GetProperty("x").GetInt32() && warp.Y == pad.GetProperty("y").GetInt32() &&
                warp.DestinationMapId == MapIdFromSourceSymbol(pad.GetProperty("destinationMap").GetString()!) && warp.DestinationWarpId == pad.GetProperty("destinationWarp").GetInt32(), "Saffron Gym pad source endpoint differs.");
        }
        return new(source, maps.ToImmutable(), constants);
    }
    private static int NpcGraphics(SilphSourcePackage source) => source.Document("npc-source.json").GetProperty("graphics").GetArrayLength();
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
