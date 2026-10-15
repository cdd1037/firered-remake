using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// Authenticated Cerulean City and ten city interiors. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretCeruleanRegion
{
    public const string Profile = "firered-cerulean-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Healing => Dependencies.GetProperty("healLocation");
    public JsonElement Route4Connection => Dependencies.GetProperty("route4Connection");

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalRoute9Events { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCeruleanRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement npcSources, JsonElement externalRoute9Events)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        NpcSources = npcSources; ExternalRoute9Events = externalRoute9Events;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        "MAP_CERULEAN_CITY_POKEMON_CENTER_1F" => new("map.cerulean-city-pokemon-center-1f"),
        "MAP_CERULEAN_CITY_POKEMON_CENTER_2F" => new("map.cerulean-city-pokemon-center-2f"),
        "MAP_CERULEAN_CITY_MART" => new("map.cerulean-city-mart"),
        "MAP_CERULEAN_CITY_GYM" => new("map.cerulean-city-gym"),
        "MAP_CERULEAN_CITY_BIKE_SHOP" => new("map.cerulean-city-bike-shop"),
        "MAP_CERULEAN_CITY_HOUSE1" => new("map.cerulean-city-house-1"),
        "MAP_CERULEAN_CITY_HOUSE2" => new("map.cerulean-city-house-2"),
        "MAP_CERULEAN_CITY_HOUSE3" => new("map.cerulean-city-house-3"),
        "MAP_CERULEAN_CITY_HOUSE4" => new("map.cerulean-city-house-4"),
        "MAP_CERULEAN_CITY_HOUSE5" => new("map.cerulean-city-house-5"),
        "MAP_ROUTE4" => new("map.route-4"),
        "MAP_ROUTE5" => new("map.route-5"),
        "MAP_ROUTE9" => new("map.route-9"),
        "MAP_ROUTE24" => new("map.route-24"),
        "MAP_CERULEAN_CAVE_1F" => new("map.cerulean-cave-1f"),
        "MAP_UNION_ROOM" => new("map.union-room"),
        "MAP_TRADE_CENTER" => new("map.trade-center"),
        _ => PretMtMoonRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretCeruleanRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/cerulean-region");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Cerulean manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "cerulean-region/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate cerulean output.");
        }
        using var catalog = JsonDocument.Parse(outputs["cerulean-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();

        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Cerulean / city interiors; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact))
            {

                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Cerulean runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_BADGE02_GET"] > 0 && constants["TRAINER_LEADER_MISTY"] > 0 &&
            constants["HEAL_LOCATION_CERULEAN_CITY"] == 4, "Cerulean trainer/heal constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 7 &&
            !dependencies.RootElement.GetProperty("route4Connection").GetProperty("enabled").GetBoolean(),
            "Cerulean dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var route9Events = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/Route9/map.json"));
        return new(directory, maps.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), route9Events.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe cerulean path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Cerulean directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Cerulean file symlink is unsupported: " + path);
        }
        return result;
    }
}
