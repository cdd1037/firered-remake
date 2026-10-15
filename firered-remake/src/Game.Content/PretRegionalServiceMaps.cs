using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretRegionalServiceMaps
{
    public const string Profile = "firered-regional-service-maps-import-v1";
    public static readonly MapId ElevatorMapId = new("map.celadon-city-department-store-elevator");
    /// <summary>All 17 authenticated source maps. Elevator events retain MAP_DYNAMIC / WARP_ID_DYNAMIC unchanged.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    /// <summary>16 maps with static warp records. Still require explicit field and service admission.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-5-south-entrance"), new("map.route-6-north-entrance"), new("map.route-7-east-entrance"), new("map.route-8-west-entrance")];
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Availability { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretRegionalServiceMaps(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants,
        JsonElement catalog, JsonElement objects, JsonElement npcs,
        JsonElement availability)
    {
        GeneratedDirectory = directory; SourceMaps = maps; NewMaps = maps.Remove(ElevatorMapId);
        Constants = constants; Catalog = catalog; ObjectTemplates = objects;
        NpcSources = npcs; Availability = availability; }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_DYNAMIC" => new("map.dynamic"),
        "MAP_CELADON_CITY_CONDOMINIUMS_1F" => new("map.celadon-city-condominiums-1f"),
        "MAP_CELADON_CITY_CONDOMINIUMS_2F" => new("map.celadon-city-condominiums-2f"),
        "MAP_CELADON_CITY_CONDOMINIUMS_3F" => new("map.celadon-city-condominiums-3f"),
        "MAP_CELADON_CITY_CONDOMINIUMS_ROOF" => new("map.celadon-city-condominiums-roof"),
        "MAP_CELADON_CITY_CONDOMINIUMS_ROOF_ROOM" => new("map.celadon-city-condominiums-roof-room"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_1F" => new("map.celadon-city-department-store-1f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_2F" => new("map.celadon-city-department-store-2f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_3F" => new("map.celadon-city-department-store-3f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_4F" => new("map.celadon-city-department-store-4f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_5F" => new("map.celadon-city-department-store-5f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_ROOF" => new("map.celadon-city-department-store-roof"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_ELEVATOR" => new("map.celadon-city-department-store-elevator"),
        "MAP_CELADON_CITY_GAME_CORNER_PRIZE_ROOM" => new("map.celadon-city-game-corner-prize-room"),
        "MAP_CELADON_CITY_HOTEL" => new("map.celadon-city-hotel"),
        "MAP_CELADON_CITY_HOUSE1" => new("map.celadon-city-house-1"),
        "MAP_CELADON_CITY_RESTAURANT" => new("map.celadon-city-restaurant"),
        "MAP_LAVENDER_TOWN_HOUSE2" => new("map.lavender-town-house-2"),
        "MAP_ROUTE5_SOUTH_ENTRANCE" => new("map.route-5-south-entrance"),
        "MAP_ROUTE6_NORTH_ENTRANCE" => new("map.route-6-north-entrance"),
        "MAP_ROUTE7_EAST_ENTRANCE" => new("map.route-7-east-entrance"),
        "MAP_ROUTE8_WEST_ENTRANCE" => new("map.route-8-west-entrance"),
        _ => PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Checks the existing gate instances before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.TryGetValue(id, out var map), "Existing gate is missing: " + id.Value);
        }
    }

    /// <summary>Maps use the compact representation; source cells expand only on request.</summary>
    public static PretRegionalServiceMaps Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/regional-service-maps");
        byte[] Pinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(Safe(root, path));
            return bytes;
        }
        using var manifestDocument = JsonDocument.Parse(Pinned(directory,"manifest.json"));
        var manifest = manifestDocument.RootElement;
        Need(manifest.GetProperty("schemaVersion").GetInt32() == 1 && manifest.GetProperty("unresolvedLabelCount").GetInt32() == 0,
            "Regional service manifest scope differs.");
        var outputEntries = manifest.GetProperty("outputs").EnumerateArray().ToDictionary(x => x.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, entry) in outputEntries)
        {
            _ = Safe(directory, path);
            outputs.Add(path, Pinned(directory,path));
        }
        JsonElement Document(string path) { using var doc = JsonDocument.Parse(outputs[path]); return doc.RootElement.Clone(); }
        var catalog = Document("regional-service-maps-catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean(), "Regional source inventory/availability differs.");
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "17 source maps; four unchanged reused gates; services require explicit admission.", Profile, files);
        var runtimeCatalog = Document("runtime-catalog.json");
        Need(runtimeCatalog.GetProperty("converterVersion").GetString() == PretRuntimeMapCatalog.ConverterVersion &&
            runtimeCatalog.GetProperty("maps").GetArrayLength() == 17, "Regional runtime converter differs.");
        var runtimeEntries = runtimeCatalog.GetProperty("maps").EnumerateArray().ToDictionary(e => e.GetProperty("stableId").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = runtimeEntries[stableId];
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "Runtime source certificate differs.");
            var payload = outputs[relative + "/runtime-map.json"];
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 17 && maps.Values.Sum(m => m.Width * m.Height) == 3241 && maps.Values.Sum(m => m.BorderWidth * m.BorderHeight) == 68, "Source map collection differs.");
        foreach (var map in maps.Values.Where(m => m.Id != ElevatorMapId))
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target))
                    Need(warp.DestinationWarpId < target.Events.GetProperty("warp_events").GetArrayLength(), "Loaded warp index differs.");
        var constants = PretSharedConstants.ForPackage(directory);
        Need(constants["ITEM_TEA"] == 369 && constants["FLAG_GOT_TEA"] == 678 && constants["FLAG_GOT_EEVEE"] == 611 && constants["LOCALID_EEVEE_POKEBALL"] == 2 && constants["SPECIES_EEVEE"] == 133, "Tea/Eevee source constants differ.");
        var availability = Document("availability.json");
        Need(!availability.GetProperty("runtimeIntegrated").GetBoolean() && !availability.GetProperty("completeGame").GetBoolean() && availability.GetProperty("enabledServices").GetArrayLength() == 0, "Source import cannot enable services.");
        return new(directory, maps.ToImmutable(), constants, catalog,
            Document("objects.json"), Document("npc-source.json"), availability);
    }
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Regional source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Regional source file symlink rejected.");
        }
        return result;
    }
}
