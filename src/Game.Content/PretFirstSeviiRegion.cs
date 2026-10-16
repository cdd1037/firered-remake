using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Original first-Sevii maps and script-selected layouts. Application admission remains separate.</summary>
public sealed partial class PretFirstSeviiRegion
{
    public const string Profile = "firered-first-sevii-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, PretWorldMap> LayoutVariants { get; private init; } = ImmutableDictionary<string, PretWorldMap>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<Game.Domain.Encounters.EarlyLandEncounterTable> LandEncounters { get; private init; }
    public ImmutableArray<Game.Domain.Encounters.WaterEncounterTable> WaterEncounters { get; private init; }
    public ImmutableArray<FieldItemServiceDefinition> ItemServices { get; private init; }
    public string GeneratedDirectory { get; }
    private PretFirstSeviiRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;

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

    public static PretFirstSeviiRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = FirstSeviiSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(directory);
        var variants = world.VariantsInDirectory(directory);
        var constants = PretSharedConstants.ForPackage(directory);
        using var encounters = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/first-sevii/encounters.json")));
        var land = PretWildEncounterTables.Land(encounters.RootElement.GetProperty("admitted"), MapIdFromSourceSymbol);
        var water = PretWildEncounterTables.Water(encounters.RootElement.GetProperty("admitted"), MapIdFromSourceSymbol);
        Need(land.All(t => maps.ContainsKey(t.Map)) && water.All(t => maps.ContainsKey(t.Map)), "Encounter map is not mounted.");
        using var serviceData = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/first-sevii/services.json")));
        var services = PretFieldItemServices.Decode(serviceData.RootElement.GetProperty("serviceBindings"), maps, constants);
        return new(source, maps, constants) { LayoutVariants = variants, LandEncounters = land, WaterEncounters = water, ItemServices = services };
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
