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
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Geometry { get; }
    public JsonElement Items { get; }
    public EventIslandFieldData Field { get; }
    public ImmutableDictionary<string, ImmutableArray<byte>> RawSources { get; }
    public JsonElement NpcSources { get; }
    public ImmutableDictionary<string, JsonElement> ReusedAnchorEvents { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretEventIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); Geometry = source.Document("geometry.json"); Items = source.Document("items.json");
        RawSources = source.Raw.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
        ReusedAnchorEvents = new[] { "VermilionCity" }.ToImmutableDictionary(n => n, n => SilphSourcePackage.Json(source.Raw["data/maps/" + n + "/map.json"]), StringComparer.Ordinal); NpcSources = source.Document("npc-source.json");
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
        var source = new SilphSourcePackage(root,"event-island-region",            "content/sources/event-island-region.lock.json");
        var raw = source.Raw; var files = source.Files; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outputs = source.Outputs; var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "EventIsland source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original event-island source maps; application admission remains separate.", Profile, files);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!;
            var relative = spec.GetProperty("directory").GetString()!;
            var runtime = PretRuntimeMapData.Decode(outputs[relative + "/runtime-map.json"], stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol does not match its ID.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), Safe(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(new(stableId), map);
        }
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "EventIsland target warp index differs.");
        var inventory = source.Document("scripts.json"); var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Need(inventory.GetProperty("blocks").GetArrayLength() == 125 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "EventIsland source script closure differs.");
        foreach (var row in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = row.GetString()!; var text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing EventIsland script label."); scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_labels.h"]),
            @"^#define\s+(METATILE_\w+)\s+0x([0-9A-Fa-f]+)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            Need(constants.TryGetValue(match.Groups[1].Value, out var value) && value ==
                int.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                "Generated EventIsland metatile constant differs from source.");
        ValidateContracts(source, maps.ToImmutable(), constants);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
