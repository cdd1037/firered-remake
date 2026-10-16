using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Five exact Five Island and Rocket Warehouse maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFiveIslandRegion
{
    public const string Profile = "firered-five-island-region-v1";
    // Authenticated source spans preserve the unloaded original connections.
    private const int WaterLabyrinthConnectionWidth = 72;
    private const int MemorialPillarConnectionHeight = 60;
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public string GeneratedDirectory { get; }
    private PretFiveIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");

        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FIVE_ISLAND" => new("map.five-island"),
        "MAP_FIVE_ISLAND_HARBOR" => new("map.five-island-harbor"),
        "MAP_FIVE_ISLAND_HOUSE1" => new("map.five-island-house1"),
        "MAP_FIVE_ISLAND_HOUSE2" => new("map.five-island-house2"),
        "MAP_FIVE_ISLAND_MEADOW" => new("map.five-island-meadow"),
        "MAP_FIVE_ISLAND_MEMORIAL_PILLAR" => new("map.five-island-memorial-pillar"),
        "MAP_FIVE_ISLAND_POKEMON_CENTER_1F" => new("map.five-island-pokemon-center-1f"),
        "MAP_FIVE_ISLAND_POKEMON_CENTER_2F" => new("map.five-island-pokemon-center-2f"),
        "MAP_FIVE_ISLAND_ROCKET_WAREHOUSE" => new("map.five-island-rocket-warehouse"),
        "MAP_FIVE_ISLAND_WATER_LABYRINTH" => new("map.five-island-water-labyrinth"),
        _ => PretSixIslandRegion.MapIdFromSourceSymbol(symbol)
    };

    public static PretFiveIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = FiveIslandSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FiveIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
