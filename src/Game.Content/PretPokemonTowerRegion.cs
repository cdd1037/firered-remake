using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated seven-floor source increment. Existing Fuji house bytes and all progression remain owned by the caller.</summary>
public sealed partial class PretPokemonTowerRegion
{
    public const string Profile = "firered-pokemon-tower-region-v1";
    public static MapId HouseMapId => new("map.lavender-town-volunteer-pokemon-house");
    public static MapId FloorMapId(int floor) => floor is >= 1 and <= 7 ? new("map.pokemon-tower-" + floor + "f") : throw new ArgumentOutOfRangeException(nameof(floor));
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public PretRuntimeMapData ExistingHouse { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<int> InitiallySetFlags { get; }
    public JsonElement SourceContract { get; }
    public JsonElement ExternalLavenderEvents { get; }
    public string GeneratedDirectory { get; }
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");
    public bool RuntimeIntegrated => false;
    public JsonElement Dependencies => ReadDocument("dependencies.json");
    public JsonElement Mechanics => ReadDocument("mechanics.json");


    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        var m = Regex.Match(symbol, @"^MAP_POKEMON_TOWER_([1-7])F$", RegexOptions.CultureInvariant);
        return m.Success ? FloorMapId(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : PretRocketHideoutRegion.MapIdFromSourceSymbol(symbol);
    }



    public static PretPokemonTowerRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot); var directory = Path.Combine(project, "content/generated/maps/regions/pokemon-tower");
        var runtime = world.RuntimeMaps;
        JsonElement Document(string path) { using var d = JsonDocument.Parse(ReadPinned(directory, path)); return d.RootElement.Clone(); }

        var maps = world.MapsInDirectory(directory);

        var constants = PretSharedConstants.ForPackage(directory);
        var contract = Document("pokemon-tower.json");
        using var city = JsonDocument.Parse(world.ReadEvents("data/maps/LavenderTown/map.json"));
        return new(directory, maps, runtime.Maps[HouseMapId], constants, contract, city.RootElement.Clone());
    }
    private JsonElement ReadDocument(string path)
    {
            using var document = JsonDocument.Parse(ReadPinned(GeneratedDirectory,path)); return document.RootElement.Clone();
    }
    private static byte[] ReadPinned(string root, string path)
    {
        PretRuntimeMapValidation.RequireNoLinks(root, path);
        return File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, path));
    }
    private static void Need(bool ok, string message) => PretRuntimeMapValidation.Require(ok, message);
}
