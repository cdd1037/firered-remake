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
    public static PretFirstSeviiRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = FirstSeviiSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(directory);
        var variants = world.VariantsInDirectory(directory);
        var constants = PretSharedConstants.ForPackage(directory);
        using var encounters = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/first-sevii/encounters.json")));
        var land = PretWildEncounterTables.Land(encounters.RootElement.GetProperty("admitted"), world.Resolve);
        var water = PretWildEncounterTables.Water(encounters.RootElement.GetProperty("admitted"), world.Resolve);
        Need(land.All(t => maps.ContainsKey(t.Map)) && water.All(t => maps.ContainsKey(t.Map)), "Encounter map is not mounted.");
        using var serviceData = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/first-sevii/services.json")));
        var services = PretFieldItemServices.Decode(serviceData.RootElement.GetProperty("serviceBindings"), maps, constants);
        return new(source, maps, constants) { LayoutVariants = variants, LandEncounters = land, WaterEncounters = water, ItemServices = services };
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
