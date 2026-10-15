using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Four authenticated source maps. Loading is not admission of scripts or unavailable road content.</summary>
public sealed class PretSnorlaxRegion
{
    public const string Profile = "firered-snorlax-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Boundaries { get; }

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSnorlaxRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> files,
        JsonElement boundaries, JsonElement catalog, JsonElement npcs)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; SourceFiles = files; Boundaries = boundaries; Catalog = catalog;
        NpcSources = npcs;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE12" => new("map.route-12"),
        "MAP_ROUTE12_NORTH_ENTRANCE_1F" => new("map.route-12-north-entrance-1f"),
        "MAP_ROUTE12_NORTH_ENTRANCE_2F" => new("map.route-12-north-entrance-2f"),
        "MAP_ROUTE12_FISHING_HOUSE" => new("map.route-12-fishing-house"),
        "MAP_ROUTE13" => new("map.route-13"),
        "MAP_ROUTE16" => new("map.route-16"),
        "MAP_ROUTE16_NORTH_ENTRANCE_1F" => new("map.route-16-north-entrance-1f"),
        "MAP_ROUTE16_NORTH_ENTRANCE_2F" => new("map.route-16-north-entrance-2f"),
        "MAP_ROUTE16_HOUSE" => new("map.route-16-house"),
        "MAP_ROUTE17" => new("map.route-17"),
        _ => PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Retains external source connections. Availability must be checked before committing a transition.</summary>
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

    /// <summary>Pure preflight. This never changes source collision or substitutes for ordinary movement checks.</summary>
    public bool AllowsFootDestination(MapId map, int x, int y) => Boundaries.GetProperty("maps").EnumerateArray()
        .Where(m => m.GetProperty("map").GetString() == map.Value)
        .SelectMany(m => m.GetProperty("admittedFootRows").EnumerateArray())
        .Any(r => r.GetProperty("y").GetInt32() == y && r.GetProperty("x").EnumerateArray().Any(v => v.GetInt32() == x));

    public bool AllowsWarp(MapId map, int sourceWarpId)
    {
        if (!NewMaps.TryGetValue(map, out var source) || sourceWarpId < 0 || sourceWarpId >= source.Events.GetProperty("warp_events").GetArrayLength()) return false;
        var w = source.Events.GetProperty("warp_events")[sourceWarpId];
        var b = Boundaries.GetProperty("maps").EnumerateArray().Single(m => m.GetProperty("map").GetString() == map.Value);
        return !b.GetProperty("rejectedWarps").EnumerateArray().Any(r => r.GetProperty("index").GetInt32() == sourceWarpId) &&
            AllowsFootDestination(map, w.GetProperty("x").GetInt32(), w.GetProperty("y").GetInt32());
    }

    public static PretSnorlaxRegion Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/snorlax-region");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Snorlax region identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Snorlax output.");
        }
        using var catalog = JsonDocument.Parse(outputs["snorlax-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();

        var inventory = files.ToImmutable(); var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Bounded source import; application and native acceptance remain separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 4 && maps.Values.Sum(x => x.Width * x.Height) == 4217 && maps.Values.Sum(x => x.Events.GetProperty("object_events").GetArrayLength()) == 27, "Four-map scope differs.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_HIDE_ROUTE_12_SNORLAX"] == 84 && constants["FLAG_HIDE_ROUTE_16_SNORLAX"] == 128 && constants["FLAG_WOKE_UP_ROUTE_12_SNORLAX"] == 595 && constants["FLAG_SYS_SPECIAL_WILD_BATTLE"] == 2055 && constants["CRY_MODE_ENCOUNTER"] == 2, "Snorlax constants differ.");
        using var boundaries = JsonDocument.Parse(outputs["boundaries.json"]); using var npcs = JsonDocument.Parse(outputs["npc-source.json"]);
        Require(!boundaries.RootElement.GetProperty("runtimeIntegrated").GetBoolean() && boundaries.RootElement.GetProperty("maps").GetArrayLength() == 4, "Boundary scope differs.");
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), inventory,
            boundaries.RootElement.Clone(), catalog.RootElement.Clone(), npcs.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Snorlax region path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Snorlax region directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Snorlax region file symlink is unsupported: " + path);
        }
        return result;
    }
}
