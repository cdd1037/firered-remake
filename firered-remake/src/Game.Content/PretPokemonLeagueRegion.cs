using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Seven exact first-clear League interior maps; the existing exterior remains an external anchor. Application admission remains separate.</summary>
public sealed partial class PretPokemonLeagueRegion
{
    public const string Profile = "firered-pokemon-league-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Geometry { get; }
    public JsonElement Rooms => Contracts.GetProperty("rooms");
    public JsonElement Champion => Contracts.GetProperty("champion");
    public JsonElement HallOfFame => Contracts.GetProperty("hallOfFame");
    public JsonElement DefeatReset => Contracts.GetProperty("defeatReset");
    public JsonElement TilePatches => Contracts.GetProperty("tilePatches");
    public JsonElement Declarations { get; }
    public JsonElement TrainerOccurrences => Contracts.GetProperty("trainerOccurrences");
    public ImmutableDictionary<string, ImmutableArray<byte>> RawSources { get; }
    public JsonElement NpcSources { get; }
    public ImmutableDictionary<string, JsonElement> ReusedAnchorEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretPokemonLeagueRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; SourceMaps = maps; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); Geometry = source.Document("geometry.json"); Declarations = source.Document("declarations.json");
        RawSources = source.Raw.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
        ReusedAnchorEvents = new[] { "IndigoPlateau_Exterior","IndigoPlateau_PokemonCenter_2F" }.ToImmutableDictionary(n => n, n => SilphSourcePackage.Json(source.Raw["data/maps/" + n + "/map.json"]), StringComparer.Ordinal); NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_INDIGO_PLATEAU_POKEMON_CENTER_1F" => new("map.indigo-plateau-pokemon-center-1f"),
        "MAP_INDIGO_PLATEAU_POKEMON_CENTER_2F" => new("map.indigo-plateau-pokemon-center-2f"),
        "MAP_POKEMON_LEAGUE_LORELEIS_ROOM" => new("map.pokemon-league-loreleis-room"),
        "MAP_POKEMON_LEAGUE_BRUNOS_ROOM" => new("map.pokemon-league-brunos-room"),
        "MAP_POKEMON_LEAGUE_AGATHAS_ROOM" => new("map.pokemon-league-agathas-room"),
        "MAP_POKEMON_LEAGUE_LANCES_ROOM" => new("map.pokemon-league-lances-room"),
        "MAP_POKEMON_LEAGUE_CHAMPIONS_ROOM" => new("map.pokemon-league-champions-room"),
        "MAP_POKEMON_LEAGUE_HALL_OF_FAME" => new("map.pokemon-league-hall-of-fame"),
        _ => PretVictoryRoadRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown PokemonLeague connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretPokemonLeagueRegion Load(string root)
    {
        var source = PokemonLeagueSource(root);
        var raw = source.Raw; var files = source.Files; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outputs = source.Outputs; var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original seven Pokemon League interiors; battles, Hall of Fame saving and credits remain separate runtime work.", Profile, files);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "PokemonLeague runtime certificate differs.");
            var payload = outputs[relative + "/runtime-map.json"];
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = Safe(directory, "npc/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 7 && maps.Values.Sum(m => m.Width * m.Height) == 2032 && maps.Values.Sum(m => m.FieldWarps.Length) == 14,
            "PokemonLeague source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "PokemonLeague target warp index differs.");
        var inventory = source.Document("scripts.json"); var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Need(inventory.GetProperty("blocks").GetArrayLength() == 209 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "PokemonLeague source script closure differs.");
        foreach (var row in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = row.GetString()!; var text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing PokemonLeague script label."); scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        ValidateContracts(source, maps.ToImmutable(), constants);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
