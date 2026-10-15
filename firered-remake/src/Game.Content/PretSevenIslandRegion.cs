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
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Geometry { get; }
    public JsonElement Items { get; }
    public SevenIslandFieldData Field { get; }
    public ImmutableDictionary<string, ImmutableArray<byte>> RawSources { get; }
    public JsonElement NpcSources { get; }
    public ImmutableDictionary<string, JsonElement> ReusedAnchorEvents { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSevenIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); Geometry = source.Document("geometry.json"); Items = source.Document("items.json");
        RawSources = source.Raw.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
        ReusedAnchorEvents = new[] { "VermilionCity" }.ToImmutableDictionary(n => n, n => SilphSourcePackage.Json(source.Raw["data/maps/" + n + "/map.json"]), StringComparer.Ordinal); NpcSources = source.Document("npc-source.json");
        InitializeField(source);
        Field = LoadField(source, constants);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SEVEN_ISLAND" => new("map.seven-island"),
        "MAP_SEVEN_ISLAND_HARBOR" => new("map.seven-island-harbor"),
        "MAP_SEVEN_ISLAND_HOUSE_ROOM1" => new("map.seven-island-house-room1"),
        "MAP_SEVEN_ISLAND_HOUSE_ROOM2" => new("map.seven-island-house-room2"),
        "MAP_SEVEN_ISLAND_MART" => new("map.seven-island-mart"),
        "MAP_SEVEN_ISLAND_POKEMON_CENTER_1F" => new("map.seven-island-pokemon-center-1f"),
        "MAP_SEVEN_ISLAND_POKEMON_CENTER_2F" => new("map.seven-island-pokemon-center-2f"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON" => new("map.seven-island-sevault-canyon"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON_ENTRANCE" => new("map.seven-island-sevault-canyon-entrance"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON_HOUSE" => new("map.seven-island-sevault-canyon-house"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON_TANOBY_KEY" => new("map.seven-island-sevault-canyon-tanoby-key"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS" => new("map.seven-island-tanoby-ruins"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_DILFORD_CHAMBER" => new("map.seven-island-tanoby-ruins-dilford-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_LIPTOO_CHAMBER" => new("map.seven-island-tanoby-ruins-liptoo-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_MONEAN_CHAMBER" => new("map.seven-island-tanoby-ruins-monean-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_RIXY_CHAMBER" => new("map.seven-island-tanoby-ruins-rixy-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_SCUFIB_CHAMBER" => new("map.seven-island-tanoby-ruins-scufib-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_VIAPOIS_CHAMBER" => new("map.seven-island-tanoby-ruins-viapois-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_WEEPTH_CHAMBER" => new("map.seven-island-tanoby-ruins-weepth-chamber"),
        "MAP_SEVEN_ISLAND_TRAINER_TOWER" => new("map.seven-island-trainer-tower"),
        "MAP_TRAINER_TOWER_LOBBY" => new("map.trainer-tower-lobby"),
        _ => PretSixIslandRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Seven Island connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSevenIslandRegion Load(string root)
    {
        var source = new SilphSourcePackage(root,"seven-island-region",            "content/sources/seven-island-region.lock.json");
        var raw = source.Raw; var files = source.Files; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outputs = source.Outputs; var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Seven Island source maps; application admission remains separate.", Profile, files);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "SevenIsland runtime certificate differs.");
            var payload = outputs[relative + "/runtime-map.json"];
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = Safe(directory, "npc/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 18 && maps.Values.Sum(m => m.Width * m.Height) == 14685 && maps.Values.Sum(m => m.FieldWarps.Length) == 29,
            "SevenIsland source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SevenIsland target warp index differs.");
        var inventory = source.Document("scripts.json"); var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Need(inventory.GetProperty("blocks").GetArrayLength() == 425 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "SevenIsland source script closure differs.");
        foreach (var row in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = row.GetString()!; var text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing SevenIsland script label."); scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_labels.h"]),
            @"^#define\s+(METATILE_\w+)\s+0x([0-9A-Fa-f]+)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            Need(constants.TryGetValue(match.Groups[1].Value, out var value) && value ==
                int.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                "Generated SevenIsland metatile constant differs from source.");
        ValidateContracts(source, maps.ToImmutable(), constants);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
