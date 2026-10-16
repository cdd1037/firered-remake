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
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");
    public bool RuntimeIntegrated => false;
    public JsonElement Dependencies => ReadDocument("dependencies.json");
    public JsonElement Mechanics => ReadDocument("mechanics.json");

    public JsonElement NpcSources => ReadDocument("npc-source.json");

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        var m = Regex.Match(symbol, @"^MAP_POKEMON_TOWER_([1-7])F$", RegexOptions.CultureInvariant);
        return m.Success ? FloorMapId(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : PretRocketHideoutRegion.MapIdFromSourceSymbol(symbol);
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        Need(source.ValueKind == JsonValueKind.Null, "Tower maps must retain their original disconnected indoor topology.");
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), []);
    }

    public static PretPokemonTowerRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot); var directory = Path.Combine(project, "content/generated/maps/regions/pokemon-tower");
        var runtime = runtimeMaps ?? PretPokemonTowerRuntimeMaps.Load(project);
        JsonElement Document(string path) { using var d = JsonDocument.Parse(ReadPinned(directory, path)); return d.RootElement.Clone(); }

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, "Local authenticated source data.",
            "Source contracts only; executable progression must be registered by Application.", Profile);
        var spec = Document("runtime.json"); var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();

        foreach (var s in spec.GetProperty("maps").EnumerateArray())
        {
            var id = new MapId(s.GetProperty("stableId").GetString()!); var compact = runtime.Maps[id]; var isNew = s.GetProperty("isNew").GetBoolean();
                Need(compact.SourceMapSymbol == s.GetProperty("sourceMapSymbol").GetString() &&
                MapIdFromSourceSymbol(compact.SourceMapSymbol) == id, "Tower map identity differs.");
                if (!isNew) { Need(id == HouseMapId , "Existing house provenance changed."); continue; }
            var relative = s.GetProperty("mapPath").GetString()!;
            maps.Add(id, PretWorldMap.FromRuntime(compact, metadata, PretRuntimeMapValidation.SafeFile(directory, relative),
                PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 7 && maps.Values.All(x => x.Width == 24 && x.Height == 20) && maps.Values.Sum(x => x.Warps.Length) == 15, "Tower seven-floor geometry differs.");

        var constants = PretSharedConstants.ForPackage(directory);
        var contract = Document("pokemon-tower.json");
            Need(contract.GetProperty("metatilePatches").GetArrayLength() == 0, "Tower scene contract differs.");
        using var city = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/LavenderTown/map.json"));
        return new(directory, maps.ToImmutable(), runtime.Maps[HouseMapId], constants, contract, city.RootElement.Clone());
    }
    private JsonElement ReadDocument(string path)
    {
            using var document = JsonDocument.Parse(ReadPinned(GeneratedDirectory,path)); return document.RootElement.Clone();
    }
    private static byte[] ReadPinned(string root, string path)
    {
        PretRocketHideoutRuntimeMaps.RequireNoLinks(root, path);
        return File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, path));
    }
    private static void Need(bool ok, string message) => PretRuntimeMapValidation.Require(ok, message);
}
