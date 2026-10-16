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
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public SevenIslandFieldData Field { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    private PretSevenIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");


        InitializeField(source);
        Field = LoadField(NewMaps, Contracts, constants);
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

    public static PretSevenIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root,"maps/regions/seven-island");

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SevenIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
