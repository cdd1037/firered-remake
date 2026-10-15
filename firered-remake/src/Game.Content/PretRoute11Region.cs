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
/// Authenticated Route11, Digletts Cave, east Route2 and aides source contracts.
/// Registers no gameplay handlers. Runtime and native certification remain separate.
/// </summary>
public sealed class PretRoute11Region
{
    public const string Profile = "firered-route11-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Aides => Dependencies.GetProperty("aides");
    public JsonElement CutTrees => Dependencies.GetProperty("cutTrees");
    public JsonElement Items => Dependencies.GetProperty("items");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-2")];

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalRoute2Events { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretRoute11Region(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement npcSources, JsonElement externalRoute2Events)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        NpcSources = npcSources; ExternalRoute2Events = externalRoute2Events;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE11" => new("map.route-11"),
        "MAP_DIGLETTS_CAVE_SOUTH_ENTRANCE" => new("map.digletts-cave-south-entrance"),
        "MAP_DIGLETTS_CAVE_B1F" => new("map.digletts-cave-b1f"),
        "MAP_DIGLETTS_CAVE_NORTH_ENTRANCE" => new("map.digletts-cave-north-entrance"),
        "MAP_ROUTE2_EAST_BUILDING" => new("map.route-2-east-building"),
        "MAP_ROUTE2_HOUSE" => new("map.route-2-house"),
        "MAP_ROUTE11_EAST_ENTRANCE_1F" => new("map.route-11-east-entrance-1f"),
        "MAP_ROUTE11_EAST_ENTRANCE_2F" => new("map.route-11-east-entrance-2f"),
        "MAP_ROUTE12" => new("map.route-12"),
        _ => PretSsAnneRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretRoute11Region Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/route11-region");
        
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Route11/Cave manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "route11-region/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Route11/Cave output.");
        }
        using var catalog = JsonDocument.Parse(outputs["route11-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();

        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Route11/Cave source profile; runtime certification is separate.", Profile, inventory);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact) )
            {
                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Route11/Cave runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 8 && maps.Values.Sum(x => x.Width*x.Height) == 8978, "Incomplete Route11/Cave map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_HM05"] > 0 && constants["FLAG_GOT_ITEMFINDER"] > 0 &&
            constants["REQUIRED_SEEN_MONS"] == 10 && constants["REQUIRED_CAUGHT_MONS"] == 30,
            "Route11/Cave aide thresholds differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 10 &&
            dependencies.RootElement.GetProperty("pickups").GetArrayLength() == 6 &&
            dependencies.RootElement.GetProperty("aides").GetArrayLength() == 2 &&
            dependencies.RootElement.GetProperty("blockedMaps")[0].GetString() == "MAP_ROUTE12",
            "Route11/Cave dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/Route2/map.json"));
        return new(directory, maps.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Route11/Cave path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route11/Cave directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route11/Cave file symlink is unsupported: " + path);
        }
        return result;
    }
}
