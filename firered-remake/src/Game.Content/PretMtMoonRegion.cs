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
/// A separately authenticated Mt. Moon three-floor increment. Source availability does not
/// certify script handlers, trainer battles, wild encounters, fossils, hidden items or audio.
/// </summary>
public sealed class PretMtMoonRegion
{
    public const string Profile = "firered-mtmoon-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public ImmutableArray<PretPewterTrainerSource> Trainers { get; }
    public JsonElement FossilChoice => Dependencies.GetProperty("fossilChoice");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public JsonElement Encounters => Dependencies.GetProperty("encounters");
    public JsonElement ProgressionBoundary => Dependencies.GetProperty("progressionBoundary");

    public JsonElement Catalog { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretMtMoonRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters = true,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        Trainers = dependencies.GetProperty("trainers").Deserialize<ImmutableArray<PretPewterTrainerSource>>(options);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_MT_MOON_1F" => new("map.mt-moon-1f"),
        "MAP_MT_MOON_B1F" => new("map.mt-moon-b1f"),
        "MAP_MT_MOON_B2F" => new("map.mt-moon-b2f"),
        _ => PretRoute3Region.MapIdFromSourceSymbol(symbol)
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

    public static PretMtMoonRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/mtmoon");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "MtMoon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"maps/regions/mtmoon/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate mtmoon output.");
        }
        using var catalog = JsonDocument.Parse(outputs["mtmoon-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated MtMoon / three-floor source; runtime certification is separate.", Profile);
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
        Require(maps.Count == 3 && maps.Values.Sum(x => x.Width*x.Height) == 5800, "Incomplete mtmoon map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["TRAINER_SUPER_NERD_MIGUEL"] == 170 && constants["TRAINER_HIKER_MARCOS"] == 181 &&
            constants["LOCALID_DOME_FOSSIL"] == 1 && constants["LOCALID_HELIX_FOSSIL"] == 2 && constants["LOCALID_MIGUEL"] == 3 &&
            constants["ITEM_DOME_FOSSIL"] == 358 && constants["ITEM_HELIX_FOSSIL"] == 357,
            "Mt. Moon trainer/fossil source constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 12 &&
            dependencies.RootElement.GetProperty("encounters").GetArrayLength() == 3 &&
            dependencies.RootElement.GetProperty("pickups").GetArrayLength() == 18 &&
            !dependencies.RootElement.GetProperty("progressionBoundary").GetProperty("enabled").GetBoolean(),
            "MtMoon dependency scope differs.");
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe mtmoon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("MtMoon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("MtMoon file symlink is unsupported: " + path);
        }
        return result;
    }
}
