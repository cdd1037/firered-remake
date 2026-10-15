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
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement Rooms => Contracts.GetProperty("rooms");
    public JsonElement Champion => Contracts.GetProperty("champion");
    public JsonElement HallOfFame => Contracts.GetProperty("hallOfFame");
    public JsonElement DefeatReset => Contracts.GetProperty("defeatReset");
    public JsonElement TilePatches => Contracts.GetProperty("tilePatches");
    public JsonElement TrainerOccurrences => Contracts.GetProperty("trainerOccurrences");
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretPokemonLeagueRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; SourceMaps = maps; NewMaps = maps; Constants = constants;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
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
    private static SilphSourcePackage PokemonLeagueSource(string root) => new(root,"maps/regions/pokemon-league");
    public static PretPokemonLeagueRegion Load(string root)
    {
        var source = PokemonLeagueSource(root);

        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original seven Pokemon League interiors; battles, Hall of Fame saving and credits remain separate runtime work.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("mapPath").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative , "PokemonLeague runtime certificate differs.");
            var payload = source.Read(relative);
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 7 && maps.Values.Sum(m => m.Width * m.Height) == 2032 && maps.Values.Sum(m => m.FieldWarps.Length) == 14,
            "PokemonLeague source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "PokemonLeague target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
