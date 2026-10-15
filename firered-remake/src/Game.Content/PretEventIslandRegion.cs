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
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement Items { get; }
    public EventIslandFieldData Field { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretEventIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        Items = source.Document("items.json");
        NpcSources = source.Document("npc-source.json");
        Field = LoadField(source, constants);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_BIRTH_ISLAND_EXTERIOR" => new("map.birth-island-exterior"),
        "MAP_BIRTH_ISLAND_HARBOR" => new("map.birth-island-harbor"),
        "MAP_NAVEL_ROCK_1F" => new("map.navel-rock-1f"),
        "MAP_NAVEL_ROCK_B1F" => new("map.navel-rock-b1f"),
        "MAP_NAVEL_ROCK_BASE" => new("map.navel-rock-base"),
        "MAP_NAVEL_ROCK_BASE_PATH_B10F" => new("map.navel-rock-base-path-b10f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B11F" => new("map.navel-rock-base-path-b11f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B1F" => new("map.navel-rock-base-path-b1f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B2F" => new("map.navel-rock-base-path-b2f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B3F" => new("map.navel-rock-base-path-b3f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B4F" => new("map.navel-rock-base-path-b4f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B5F" => new("map.navel-rock-base-path-b5f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B6F" => new("map.navel-rock-base-path-b6f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B7F" => new("map.navel-rock-base-path-b7f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B8F" => new("map.navel-rock-base-path-b8f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B9F" => new("map.navel-rock-base-path-b9f"),
        "MAP_NAVEL_ROCK_EXTERIOR" => new("map.navel-rock-exterior"),
        "MAP_NAVEL_ROCK_FORK" => new("map.navel-rock-fork"),
        "MAP_NAVEL_ROCK_HARBOR" => new("map.navel-rock-harbor"),
        "MAP_NAVEL_ROCK_SUMMIT" => new("map.navel-rock-summit"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_2F" => new("map.navel-rock-summit-path-2f"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_3F" => new("map.navel-rock-summit-path-3f"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_4F" => new("map.navel-rock-summit-path-4f"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_5F" => new("map.navel-rock-summit-path-5f"),
        _ => PretSevenIslandRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown event-island connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretEventIslandRegion Load(string root)
    {
        var source = new SilphSourcePackage(root,"event-island-region");
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "EventIsland source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original event-island source maps; application admission remains separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!;
            var relative = spec.GetProperty("directory").GetString()!;
            var runtime = PretRuntimeMapData.Decode(source.Read(relative + "/runtime-map.json"), stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol does not match its ID.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(new(stableId), map);
        }
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "EventIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
