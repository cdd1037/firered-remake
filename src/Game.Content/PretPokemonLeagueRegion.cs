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
    public string GeneratedDirectory { get; }
    private PretPokemonLeagueRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; SourceMaps = maps; NewMaps = maps; Constants = constants;

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

    private static SilphSourcePackage PokemonLeagueSource(string root) => new(root,"maps/regions/pokemon-league");
    public static PretPokemonLeagueRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = PokemonLeagueSource(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "PokemonLeague target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
