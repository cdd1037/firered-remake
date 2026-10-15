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
/// Authenticated Route9, Route10, RockTunnel, Lavender arrival and Center source source contracts.
/// Registers no gameplay handlers. Runtime and native certification remain separate.
/// </summary>
public sealed class PretRockTunnelRegion
{
    public const string Profile = "firered-rocktunnel-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.cerulean-city")];

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalCeruleanEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretRockTunnelRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement catalog, JsonElement npcSources, JsonElement externalCeruleanEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Catalog = catalog;
        NpcSources = npcSources; ExternalCeruleanEvents = externalCeruleanEvents;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE9" => new("map.route-9"),
        "MAP_ROUTE10" => new("map.route-10"),
        "MAP_ROCK_TUNNEL_1F" => new("map.rock-tunnel-1f"),
        "MAP_ROCK_TUNNEL_B1F" => new("map.rock-tunnel-b1f"),
        "MAP_ROUTE10_POKEMON_CENTER_1F" => new("map.route-10-pokemon-center-1f"),
        "MAP_ROUTE10_POKEMON_CENTER_2F" => new("map.route-10-pokemon-center-2f"),
        "MAP_LAVENDER_TOWN" => new("map.lavender-town"),
        "MAP_LAVENDER_TOWN_HOUSE1" => new("map.lavender-town-house-1"),
        "MAP_LAVENDER_TOWN_HOUSE2" => new("map.lavender-town-house-2"),
        "MAP_LAVENDER_TOWN_MART" => new("map.lavender-town-mart"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_1F" => new("map.lavender-town-pokemon-center-1f"),
        "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE" => new("map.lavender-town-volunteer-pokemon-house"),
        "MAP_POKEMON_TOWER_1F" => new("map.pokemon-tower-1f"),
        "MAP_POWER_PLANT" => new("map.power-plant"),
        "MAP_ROUTE8" => new("map.route-8"),
        _ => PretRoute11Region.MapIdFromSourceSymbol(symbol)
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

    public static PretRockTunnelRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/rocktunnel");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "RockTunnel manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "maps/regions/rocktunnel/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate RockTunnel output.");
        }
        using var catalog = JsonDocument.Parse(outputs["rocktunnel-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated RockTunnel source profile; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("mapPath").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact) )
            {
                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate RockTunnel runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 7 && maps.Values.Sum(x => x.Width*x.Height) == 7980, "Incomplete RockTunnel map set.");

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_SYS_FLASH_ACTIVE"] == 0x806 && constants["FLAG_BADGE01_GET"] == 0x820 &&
            constants["REQUIRED_OWNED_MONS"] == 20 && constants["LOCALID_ROUTE9_CUT_TREE"] == 10,
            "RockTunnel field requirements differ.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/CeruleanCity/map.json"));
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe RockTunnel path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("RockTunnel directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("RockTunnel file symlink is unsupported: " + path);
        }
        return result;
    }
}
