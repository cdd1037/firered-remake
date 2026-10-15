using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Fourteen exact first-Sevii maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFirstSeviiRegion
{
    public const string Profile = "firered-first-sevii-region-v1";
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
    public JsonElement Declarations { get; }
    public ImmutableDictionary<string, ImmutableArray<byte>> RawSources { get; }
    public JsonElement NpcSources { get; }
    public ImmutableDictionary<string, JsonElement> ReusedAnchorEvents { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretFirstSeviiRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); Geometry = source.Document("geometry.json"); Declarations = source.Document("declarations.json");
        RawSources = source.Raw.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
        ReusedAnchorEvents = new[] { "CinnabarIsland","CinnabarIsland_PokemonCenter_1F","VermilionCity" }.ToImmutableDictionary(n => n, n => SilphSourcePackage.Json(source.Raw["data/maps/" + n + "/map.json"]), StringComparer.Ordinal); NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CINNABAR_ISLAND" => new("map.cinnabar-island"),
        "MAP_ONE_ISLAND" => new("map.one-island"),
        "MAP_ONE_ISLAND_HARBOR" => new("map.one-island-harbor"),
        "MAP_ONE_ISLAND_HOUSE1" => new("map.one-island-house1"),
        "MAP_ONE_ISLAND_HOUSE2" => new("map.one-island-house2"),
        "MAP_ONE_ISLAND_KINDLE_ROAD" => new("map.one-island-kindle-road"),
        "MAP_ONE_ISLAND_POKEMON_CENTER_1F" => new("map.one-island-pokemon-center-1f"),
        "MAP_ONE_ISLAND_POKEMON_CENTER_2F" => new("map.one-island-pokemon-center-2f"),
        "MAP_ONE_ISLAND_TREASURE_BEACH" => new("map.one-island-treasure-beach"),
        "MAP_THREE_ISLAND" => new("map.three-island"),
        "MAP_THREE_ISLAND_BERRY_FOREST" => new("map.three-island-berry-forest"),
        "MAP_THREE_ISLAND_BOND_BRIDGE" => new("map.three-island-bond-bridge"),
        "MAP_THREE_ISLAND_DUNSPARCE_TUNNEL" => new("map.three-island-dunsparce-tunnel"),
        "MAP_THREE_ISLAND_HARBOR" => new("map.three-island-harbor"),
        "MAP_THREE_ISLAND_HOUSE1" => new("map.three-island-house1"),
        "MAP_THREE_ISLAND_HOUSE2" => new("map.three-island-house2"),
        "MAP_THREE_ISLAND_HOUSE3" => new("map.three-island-house3"),
        "MAP_THREE_ISLAND_HOUSE4" => new("map.three-island-house4"),
        "MAP_THREE_ISLAND_HOUSE5" => new("map.three-island-house5"),
        "MAP_THREE_ISLAND_MART" => new("map.three-island-mart"),
        "MAP_THREE_ISLAND_POKEMON_CENTER_1F" => new("map.three-island-pokemon-center-1f"),
        "MAP_THREE_ISLAND_POKEMON_CENTER_2F" => new("map.three-island-pokemon-center-2f"),
        "MAP_THREE_ISLAND_PORT" => new("map.three-island-port"),
        "MAP_TWO_ISLAND" => new("map.two-island"),
        "MAP_TWO_ISLAND_CAPE_BRINK" => new("map.two-island-cape-brink"),
        "MAP_TWO_ISLAND_HARBOR" => new("map.two-island-harbor"),
        "MAP_TWO_ISLAND_HOUSE" => new("map.two-island-house"),
        "MAP_TWO_ISLAND_JOYFUL_GAME_CORNER" => new("map.two-island-joyful-game-corner"),
        "MAP_TWO_ISLAND_POKEMON_CENTER_1F" => new("map.two-island-pokemon-center-1f"),
        "MAP_TWO_ISLAND_POKEMON_CENTER_2F" => new("map.two-island-pokemon-center-2f"),
        _ => PretCinnabarRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown FirstSevii connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretFirstSeviiRegion Load(string root)
    {
        var source = FirstSeviiSource.Region(root);
        var raw = source.Raw; var files = source.Files; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outputs = source.Outputs; var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "FirstSevii source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original One/Two/Three Island first-visit source maps; application admission remains separate.", Profile, files);
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
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FirstSevii target warp index differs.");
        var inventory = source.Document("scripts.json"); var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Need(inventory.GetProperty("blocks").GetArrayLength() == 702 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "FirstSevii source script closure differs.");
        foreach (var row in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = row.GetString()!; var text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing FirstSevii script label."); scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_labels.h"]),
            @"^#define\s+(METATILE_\w+)\s+0x([0-9A-Fa-f]+)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            Need(constants.TryGetValue(match.Groups[1].Value, out var value) && value ==
                int.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                "Generated FirstSevii metatile constant differs from source.");
        ValidateContracts(source, maps.ToImmutable(), constants);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
