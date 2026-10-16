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
/// A separately authenticated Route22 / League gate / Route23 increment. Source availability does not
/// certify script handlers, trainer battles, wild encounters, Cut, hidden items or audio.
/// </summary>
public sealed class PretRoute22Region
{
    public const string Profile = "firered-route22-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement EarlyRivalTrainers => Dependencies.GetProperty("earlyRivalTrainers");
    public JsonElement Encounters => Dependencies.GetProperty("encounters");
    public JsonElement FirstBadgeBoundary => Dependencies.GetProperty("firstBadgeBoundary");
    public JsonElement MissingAudio => Dependencies.GetProperty("audio");

    public ImmutableHashSet<(int X, int Y)> FirstBadgeRoute23SourceStops { get; }
    public JsonElement Catalog { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretRoute22Region(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        FirstBadgeRoute23SourceStops = FirstBadgeBoundary.GetProperty("stableRows").EnumerateArray()
            .SelectMany(row => row.GetProperty("x").EnumerateArray().Select(x => (x.GetInt32(), row.GetProperty("y").GetInt32())))
            .ToImmutableHashSet();
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE22" => new("map.route-22"),
        "MAP_ROUTE22_NORTH_ENTRANCE" => new("map.route-22-north-entrance"),
        "MAP_ROUTE23" => new("map.route-23"),
        "MAP_VICTORY_ROAD_1F" => new("map.victory-road-1f"),
        "MAP_VICTORY_ROAD_2F" => new("map.victory-road-2f"),
        "MAP_INDIGO_PLATEAU_EXTERIOR" => new("map.indigo-plateau-exterior"),
        _ => PretPewterRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretRoute22Region Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/route22");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Route22 manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"maps/regions/route22/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate route22 output.");
        }
        using var catalog = JsonDocument.Parse(outputs["route22-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Route22 / League gate / Route23; runtime certification is separate.", Profile);
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
        Require(maps.Count == 3 && maps.Values.Sum(x => x.Width*x.Height) == 5172, "Incomplete route22 map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["TRAINER_RIVAL_ROUTE22_EARLY_SQUIRTLE"] == 329 &&
            constants["TRAINER_RIVAL_ROUTE22_EARLY_BULBASAUR"] == 330 &&
            constants["TRAINER_RIVAL_ROUTE22_EARLY_CHARMANDER"] == 331 &&
            constants["STDSTRING_BOULDER_BADGE"] == 15 && constants["STDSTRING_CASCADE_BADGE"] == 16,
            "Route22 trainer/badge string source constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("earlyRivalTrainers").GetArrayLength() == 3 &&
            dependencies.RootElement.GetProperty("firstBadgeBoundary").GetProperty("stableReachableCellCount").GetInt32() == 51 &&
            dependencies.RootElement.GetProperty("coordinateEvents").EnumerateArray().All(e => !e.GetProperty("enabled").GetBoolean()),
            "Route22 dependency scope differs.");
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe route22 path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route22 directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route22 file symlink is unsupported: " + path);
        }
        return result;
    }
}
