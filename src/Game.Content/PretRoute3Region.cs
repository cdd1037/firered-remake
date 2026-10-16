using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// A separately authenticated Route3 / Route4 / Mt. Moon approach Center increment. Source availability does not
/// certify script handlers, trainer battles, wild encounters, Cut, hidden items or audio.
/// </summary>
public sealed class PretRoute3Region
{
    public const string Profile = "firered-route3-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public ImmutableArray<PretPewterTrainerSource> Trainers { get; }
    public PretPewterHealLocation Healing { get; }
    public JsonElement MagikarpSale => Dependencies.GetProperty("magikarpSale");
    public JsonElement Encounters => Dependencies.GetProperty("encounters");
    public JsonElement ProgressionBoundary => Dependencies.GetProperty("progressionBoundary");

    public JsonElement Catalog { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretRoute3Region(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters = true,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        Trainers = dependencies.GetProperty("trainers").Deserialize<ImmutableArray<PretPewterTrainerSource>>(options);
        Healing = dependencies.GetProperty("healLocations")[0].GetProperty("table").Deserialize<PretPewterHealLocation>(options)
            ?? throw new InvalidDataException("Missing Route4 heal location.");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE3" => new("map.route-3"),
        "MAP_ROUTE4" => new("map.route-4"),
        "MAP_ROUTE4_POKEMON_CENTER_1F" => new("map.route-4-pokemon-center-1f"),
        "MAP_ROUTE4_POKEMON_CENTER_2F" => new("map.route-4-pokemon-center-2f"),
        "MAP_MT_MOON_1F" => new("map.mt-moon-1f"),
        "MAP_MT_MOON_B1F" => new("map.mt-moon-b1f"),
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        _ => PretRoute22Region.MapIdFromSourceSymbol(symbol)
    };

    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretRoute3Region Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/route3");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Route3 manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"maps/regions/route3/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate route3 output.");
        }
        using var catalog = JsonDocument.Parse(outputs["route3-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Route3 / Route4 / Mt. Moon approach Center; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("mapPath").GetString()!;
            if(runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!),out var compact) )
            {
                var runtimeMap=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"),MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id,runtimeMap),"Duplicate runtime map.");continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 3 && maps.Values.Sum(x => x.Width*x.Height) == 3990, "Incomplete route3 map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["TRAINER_YOUNGSTER_BEN"] == 89 && constants["TRAINER_LASS_ROBIN"] == 118 &&
            constants["HEAL_LOCATION_ROUTE4"] == 12 && constants["LOCALID_ROUTE4_NURSE"] == 1 && constants["MAGIKARP_PRICE"] == 500,
            "Route3 trainer/heal source constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 9 &&
            dependencies.RootElement.GetProperty("encounters").GetArrayLength() == 2 &&
            !dependencies.RootElement.GetProperty("progressionBoundary").GetProperty("enabled").GetBoolean(),
            "Route3 dependency scope differs.");
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe route3 path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route3 directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route3 file symlink is unsupported: " + path);
        }
        return result;
    }
}
