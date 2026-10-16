using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Encounters;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Exact Six Island source maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretSixIslandRegion
{
    public const string Profile = "firered-six-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public ImmutableArray<EarlyLandEncounterTable> LandEncounters { get; }
    public string GeneratedDirectory { get; }
    private PretSixIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");

        LandEncounters = PretWildEncounterTables.Land(source.Document("encounters.json"), MapIdFromSourceSymbol);
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SIX_ISLAND" => new("map.six-island"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_1F" => new("map.six-island-dotted-hole-1f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B1F" => new("map.six-island-dotted-hole-b1f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B2F" => new("map.six-island-dotted-hole-b2f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B3F" => new("map.six-island-dotted-hole-b3f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B4F" => new("map.six-island-dotted-hole-b4f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_SAPPHIRE_ROOM" => new("map.six-island-dotted-hole-sapphire-room"),
        "MAP_SIX_ISLAND_ALTERING_CAVE" => new("map.six-island-altering-cave"),
        "MAP_SIX_ISLAND_PATTERN_BUSH" => new("map.six-island-pattern-bush"),
        "MAP_SIX_ISLAND_OUTCAST_ISLAND" => new("map.six-island-outcast-island"),
        "MAP_SIX_ISLAND_GREEN_PATH" => new("map.six-island-green-path"),
        "MAP_SIX_ISLAND_HARBOR" => new("map.six-island-harbor"),
        "MAP_SIX_ISLAND_HOUSE" => new("map.six-island-house"),
        "MAP_SIX_ISLAND_MART" => new("map.six-island-mart"),
        "MAP_SIX_ISLAND_POKEMON_CENTER_1F" => new("map.six-island-pokemon-center-1f"),
        "MAP_SIX_ISLAND_POKEMON_CENTER_2F" => new("map.six-island-pokemon-center-2f"),
        "MAP_SIX_ISLAND_RUIN_VALLEY" => new("map.six-island-ruin-valley"),
        "MAP_SIX_ISLAND_WATER_PATH" => new("map.six-island-water-path"),
        "MAP_SIX_ISLAND_WATER_PATH_HOUSE1" => new("map.six-island-water-path-house1"),
        "MAP_SIX_ISLAND_WATER_PATH_HOUSE2" => new("map.six-island-water-path-house2"),
        _ => PretFourIslandRegion.MapIdFromSourceSymbol(symbol)
    };

    public static PretSixIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = SixIslandSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.Count > 0, "Empty Six Island map collection.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SixIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
