// Recovery candidate reconstructed from retained context, 2026-10-10.
// Source pins are original. This C# file's original byte identity is not known.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Celadon approach source data; no gameplay-handler registration.</summary>
public sealed class PretCeladonApproachRegion
{
    public const string Profile = "firered-celadon-approach-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Centers => Dependencies.GetProperty("centers");
    public JsonElement Mart => Dependencies.GetProperty("mart");
    public JsonElement Guards => Dependencies.GetProperty("guards");
    public JsonElement VolunteerHouse => Dependencies.GetProperty("volunteerHouse");
    public JsonElement CutTrees => Dependencies.GetProperty("cutTrees");
    public JsonElement Items => Dependencies.GetProperty("items");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public ImmutableArray<MapId> ReusedMapIds => [new("map.lavender-town")];

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalLavenderEvents { get; }
    public JsonElement ExternalRoute16Events { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCeladonApproachRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> files,
        JsonElement dependencies, JsonElement catalog, JsonElement npcSources,
        JsonElement lavenderEvents, JsonElement route16Events)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        NpcSources = npcSources;
        ExternalLavenderEvents = lavenderEvents; ExternalRoute16Events = route16Events;
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CELADON_CITY" => new("map.celadon-city"),
        "MAP_CELADON_CITY_CONDOMINIUMS_1F" => new("map.celadon-city-condominiums-1f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_1F" => new("map.celadon-city-department-store-1f"),
        "MAP_CELADON_CITY_GAME_CORNER" => new("map.celadon-city-game-corner"),
        "MAP_CELADON_CITY_GAME_CORNER_PRIZE_ROOM" => new("map.celadon-city-game-corner-prize-room"),
        "MAP_CELADON_CITY_GYM" => new("map.celadon-city-gym"),
        "MAP_CELADON_CITY_HOTEL" => new("map.celadon-city-hotel"),
        "MAP_CELADON_CITY_HOUSE1" => new("map.celadon-city-house-1"),
        "MAP_CELADON_CITY_POKEMON_CENTER_1F" => new("map.celadon-city-pokemon-center-1f"),
        "MAP_CELADON_CITY_POKEMON_CENTER_2F" => new("map.celadon-city-pokemon-center-2f"),
        "MAP_CELADON_CITY_RESTAURANT" => new("map.celadon-city-restaurant"),
        "MAP_LAVENDER_TOWN" => new("map.lavender-town"),
        "MAP_LAVENDER_TOWN_HOUSE1" => new("map.lavender-town-house-1"),
        "MAP_LAVENDER_TOWN_MART" => new("map.lavender-town-mart"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_1F" => new("map.lavender-town-pokemon-center-1f"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_2F" => new("map.lavender-town-pokemon-center-2f"),
        "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE" => new("map.lavender-town-volunteer-pokemon-house"),
        "MAP_ROUTE16" => new("map.route-16"),
        "MAP_ROUTE7" => new("map.route-7"),
        "MAP_ROUTE7_EAST_ENTRANCE" => new("map.route-7-east-entrance"),
        "MAP_ROUTE8" => new("map.route-8"),
        "MAP_ROUTE8_WEST_ENTRANCE" => new("map.route-8-west-entrance"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_SAFFRON_CITY_CONNECTION" => new("map.saffron-city-connection"),
        "MAP_TRADE_CENTER" => new("map.trade-center"),
        "MAP_UNDERGROUND_PATH_EAST_ENTRANCE" => new("map.underground-path-east-entrance"),
        "MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL" => new("map.underground-path-east-west-tunnel"),
        "MAP_UNDERGROUND_PATH_WEST_ENTRANCE" => new("map.underground-path-west-entrance"),
        "MAP_UNION_ROOM" => new("map.union-room"),
        _ => PretRockTunnelRegion.MapIdFromSourceSymbol(symbol)
    };
    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var connections = source.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            source.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), connections);
    }
    public static PretCeladonApproachRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/celadon-approach-region");
        
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Celadon approach manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "celadon-approach-region/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate output.");
        }
        using var catalog = JsonDocument.Parse(outputs["celadon-approach-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();

        var inventory = files.ToImmutable(); var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated CeladonApproach source profile; runtime certification is separate.", Profile, inventory);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact))
            {

                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative), PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate runtime map."); continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_TEA"] == 0x2A6 && constants["FLAG_RESCUED_MR_FUJI"] == 0x23C && constants["HEAL_LOCATION_LAVENDER_TOWN"] == 5 && constants["HEAL_LOCATION_CELADON_CITY"] == 7, "Service requirements differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]); var d = dependencies.RootElement;
        Require(!d.GetProperty("runtimeIntegrated").GetBoolean() && d.GetProperty("trainers").GetArrayLength() == 13 &&
            d.GetProperty("trainers").EnumerateArray().Count(x => x.GetProperty("battleCommand").GetString() == "trainerbattle_double") == 2 &&
            d.GetProperty("pickups").GetArrayLength() == 13 && d.GetProperty("centers").GetArrayLength() == 2 && d.GetProperty("guards").GetArrayLength() == 2 &&
            d.GetProperty("mart").GetProperty("stock").GetArrayLength() == 9 && d.GetProperty("cutTrees").GetArrayLength() == 6 &&
            d.GetProperty("volunteerHouse").GetProperty("mrFujiActor").GetProperty("flag").GetString() == "FLAG_HIDE_POKEHOUSE_FUJI", "Dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/LavenderTown/map.json"));
        using var route16Events = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/Route16/map.json"));
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), inventory,
            d.Clone(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), cityEvents.RootElement.Clone(), route16Events.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or "..")) throw new InvalidDataException("Unsafe Celadon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent) if (cursor.LinkTarget is not null) throw new InvalidDataException("Directory symlink unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null) throw new InvalidDataException("File symlink unsupported: " + path);
        }
        return result;
    }
}
