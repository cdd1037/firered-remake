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
/// Authenticated Route5/6, Underground Path and Vermilion maps, actors, services and source boundaries. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretVermilionRegion
{
    public const string Profile = "firered-vermilion-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement CeruleanSouthConnection => Dependencies.GetProperty("ceruleanSouthConnection");
    public JsonElement HealLocation => Dependencies.GetProperty("healLocation");
    public JsonElement Mart => Dependencies.GetProperty("mart");
    public JsonElement Encounters { get; }

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalCeruleanEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretVermilionRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog, JsonElement npcSources, JsonElement externalCeruleanEvents, JsonElement encounters)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        Encounters = encounters;
        NpcSources = npcSources; ExternalCeruleanEvents = externalCeruleanEvents;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE5" => new("map.route-5"),
        "MAP_ROUTE5_SOUTH_ENTRANCE" => new("map.route-5-south-entrance"),
        "MAP_UNDERGROUND_PATH_NORTH_ENTRANCE" => new("map.underground-path-north-entrance"),
        "MAP_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL" => new("map.underground-path-north-south-tunnel"),
        "MAP_UNDERGROUND_PATH_SOUTH_ENTRANCE" => new("map.underground-path-south-entrance"),
        "MAP_ROUTE6_NORTH_ENTRANCE" => new("map.route-6-north-entrance"),
        "MAP_ROUTE6" => new("map.route-6"),
        "MAP_VERMILION_CITY" => new("map.vermilion-city"),
        "MAP_VERMILION_CITY_POKEMON_CENTER_1F" => new("map.vermilion-city-pokemon-center-1f"),
        "MAP_VERMILION_CITY_MART" => new("map.vermilion-city-mart"),
        "MAP_ROUTE5_POKEMON_DAY_CARE" => new("map.route-5-pokemon-day-care"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_SAFFRON_CITY_CONNECTION" => new("map.saffron-city-connection"),
        "MAP_ROUTE11" => new("map.route-11"),
        "MAP_SSANNE_EXTERIOR" => new("map.ssanne-exterior"),
        "MAP_VERMILION_CITY_HOUSE1" => new("map.vermilion-city-house-1"),
        "MAP_VERMILION_CITY_HOUSE2" => new("map.vermilion-city-house2"),
        "MAP_VERMILION_CITY_HOUSE3" => new("map.vermilion-city-house-3"),
        "MAP_VERMILION_CITY_POKEMON_FAN_CLUB" => new("map.vermilion-city-pokemon-fan-club"),
        "MAP_VERMILION_CITY_GYM" => new("map.vermilion-city-gym"),
        "MAP_VERMILION_CITY_POKEMON_CENTER_2F" => new("map.vermilion-city-pokemon-center-2f"),
        _ => PretBillRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretVermilionRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/vermilion");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Vermilion manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "maps/regions/vermilion/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate vermilion output.");
        }
        using var catalog = JsonDocument.Parse(outputs["vermilion-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Route5/6, Underground Path and Vermilion; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("mapPath").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact) )
            {
                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Vermilion runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 10 && maps.Values.Sum(x => x.Width*x.Height) == 6011, "Incomplete vermilion map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_TEA"] > 0 && constants["LOCALID_VERMILION_NURSE"] == 1 &&
            constants["HEAL_LOCATION_VERMILION_CITY"] == 6, "Vermilion progression/local actor constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 6 &&
            dependencies.RootElement.GetProperty("mart").GetProperty("items").GetArrayLength() == 8 &&
            !dependencies.RootElement.GetProperty("ceruleanSouthConnection").GetProperty("enabled").GetBoolean(),
            "Vermilion dependency scope differs.");
        using var encounters = JsonDocument.Parse(outputs["encounters.json"]);
        Require(encounters.RootElement.GetProperty("gameVersion").GetString() == "FIRERED" &&
            encounters.RootElement.GetProperty("maps").GetArrayLength() == 2, "Vermilion encounter scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/CeruleanCity/map.json"));
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), cityEvents.RootElement.Clone(), encounters.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe vermilion path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Vermilion directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Vermilion file symlink is unsupported: " + path);
        }
        return result;
    }
}
