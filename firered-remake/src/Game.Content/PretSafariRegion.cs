using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretSafariRegion
{
    public const string Profile = "firered-safari-region-import-v1";
    public static readonly MapId EntranceMapId = new("map.fuchsia-city-safari-zone-entrance");
    public static readonly MapId CenterMapId = new("map.safari-zone-center");
    public static readonly MapId SecretHouseMapId = new("map.safari-zone-secret-house");
    public static readonly MapId WardenHouseMapId = new("map.fuchsia-city-wardens-house");
    /// <summary>Twelve complete authenticated maps. Loading alone admits no field action or service.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps => SourceMaps;
    public ImmutableArray<MapId> ReusedMapIds => [new("map.fuchsia-city")];
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }

    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Availability { get; }
    public JsonElement Contracts { get; }
    public JsonElement EncounterInventory { get; }
    public ImmutableDictionary<MapId, JsonElement> ReusedEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSafariRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, JsonElement catalog, JsonElement objects, JsonElement npcs,
        JsonElement availability, ImmutableDictionary<MapId, JsonElement> reused, JsonElement contracts, JsonElement encounters)
    {
        GeneratedDirectory = directory; SourceMaps = maps; Constants = constants;
        Catalog = catalog; ObjectTemplates = objects;
        NpcSources = npcs; Availability = availability; ReusedEvents = reused;
        Contracts = contracts; EncounterInventory = encounters;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE" => new("map.fuchsia-city-safari-zone-entrance"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_OFFICE" => new("map.fuchsia-city-safari-zone-office"),
        "MAP_SAFARI_ZONE_CENTER" => new("map.safari-zone-center"),
        "MAP_SAFARI_ZONE_EAST" => new("map.safari-zone-east"),
        "MAP_SAFARI_ZONE_NORTH" => new("map.safari-zone-north"),
        "MAP_SAFARI_ZONE_WEST" => new("map.safari-zone-west"),
        "MAP_SAFARI_ZONE_CENTER_REST_HOUSE" => new("map.safari-zone-center-rest-house"),
        "MAP_SAFARI_ZONE_EAST_REST_HOUSE" => new("map.safari-zone-east-rest-house"),
        "MAP_SAFARI_ZONE_NORTH_REST_HOUSE" => new("map.safari-zone-north-rest-house"),
        "MAP_SAFARI_ZONE_WEST_REST_HOUSE" => new("map.safari-zone-west-rest-house"),
        "MAP_SAFARI_ZONE_SECRET_HOUSE" => new("map.safari-zone-secret-house"),
        "MAP_FUCHSIA_CITY_WARDENS_HOUSE" => new("map.fuchsia-city-wardens-house"),
        _ => PretFuchsiaRouteRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Keep source connections, including unavailable neighbors, so callers can reject before mutation.</summary>
    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unknown Safari region connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.CreateConnectedCells(), edges);
    }

    /// <summary>Only a source-package boundary check; not an admission of interactions, trainer approaches or encounters.</summary>
    public bool ContainsDestination(MapId destination) => SourceMaps.ContainsKey(destination) || ReusedMapIds.Contains(destination);
    public bool AllowsNewMapWarp(MapId source, int index)
    {
        if (!SourceMaps.TryGetValue(source, out var map) || index < 0 || index >= map.FieldWarps.Length) return false;
        return ContainsDestination(map.FieldWarps[index].DestinationMapId);
    }
    public JsonElement FuchsiaCityEvents => ReusedEvents[new("map.fuchsia-city")];

    /// <summary>Checks the existing FuchsiaCity instance before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.TryGetValue(id, out var map), "Existing FuchsiaCity is missing: " + id.Value);
            Need(                JsonElement.DeepEquals(map!.Events, ReusedEvents[id]), "Existing FuchsiaCity identity differs: " + id.Value);
        }
    }

    /// <summary>Maps retain packed blocks and expand source cells only on demand.</summary>
    public static PretSafariRegion Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/safari");

        byte[] Pinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(Safe(root, path));
            return bytes;
        }
        using var manifestDocument = JsonDocument.Parse(Pinned(directory,"manifest.json"));
        var manifest = manifestDocument.RootElement;
        Need(manifest.GetProperty("schemaVersion").GetInt32() == 1,
            "Safari region manifest scope differs.");
        var outputEntries = manifest.GetProperty("outputs").EnumerateArray().ToDictionary(x => x.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, entry) in outputEntries)
        {
            if (path == "scripts.json") continue;
            _ = Safe(directory, path);
            outputs.Add(path, Pinned(directory,path));
        }
        JsonElement Document(string path) { using var doc = JsonDocument.Parse(outputs[path]); return doc.RootElement.Clone(); }
        var catalog = Document("safari-region-catalog.json");


        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Twelve source maps; unchanged FuchsiaCity reuse; services require explicit admission.", Profile);
        var runtimeCatalog = Document("runtime-catalog.json");
        var runtimeEntries = runtimeCatalog.GetProperty("maps").EnumerateArray().ToDictionary(e => e.GetProperty("stableId").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("mapPath").GetString()!;
            var runtimeEntry = runtimeEntries[stableId];
            Need(runtimeEntry.GetProperty("path").GetString() == relative , "Runtime source certificate differs.");
            var payload = outputs[relative];
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 12 && maps.Values.Sum(m => m.Width * m.Height) == 8893 && maps.Values.Sum(m => m.BorderWidth * m.BorderHeight) == 56, "Source map collection differs.");
        foreach (var map in maps.Values)
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target))
                    Need(warp.DestinationWarpId < target.Events.GetProperty("warp_events").GetArrayLength(), "Loaded warp index differs.");

        var constants = PretSharedConstants.ForPackage(directory);
        Need(constants["ITEM_HM03"] == 341 && constants["ITEM_HM04"] == 342 && constants["ITEM_GOLD_TEETH"] == 353 &&
            constants["FLAG_GOT_HM03"] == 0x239 && constants["FLAG_GOT_HM04"] == 0x23a && constants["FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH"] == 0x189,
            "Safari reward constants differ.");
        var reused = ImmutableDictionary.CreateBuilder<MapId, JsonElement>();
        foreach (var spec in catalog.GetProperty("reusedMaps").EnumerateArray())
        {
            Need(!spec.GetProperty("regenerate").GetBoolean(), "Reused source ownership differs.");
            var path = spec.GetProperty("sourceRef").GetProperty("path").GetString()!;
            using var events = JsonDocument.Parse(GeneratedRegionEvents.Read(project, path)); reused.Add(new(spec.GetProperty("stableId").GetString()!), events.RootElement.Clone());
        }
        Need(reused.Count == 1 && !maps.Keys.Any(reused.ContainsKey), "Reused maps must not be regenerated.");
        var availability = Document("availability.json");
        Need(!availability.GetProperty("completeGame").GetBoolean() && availability.GetProperty("enabledServices").GetArrayLength() == 0, "Source import cannot enable services.");
        var contracts = Document("contracts.json");
        Need(contracts.GetProperty("entryFee").GetInt32() == 500 && contracts.GetProperty("startingBalls").GetInt32() == 30 &&
            contracts.GetProperty("startingSteps").GetInt32() == 600 && contracts.GetProperty("pickups").GetArrayLength() == 13,
            "Safari source session/reward contract differs.");
        var gold = maps[new("map.safari-zone-west")].Events.GetProperty("object_events")[0];
        Need(gold.GetProperty("script").GetString() == "SafariZone_West_EventScript_ItemGoldTeeth" && gold.GetProperty("flag").GetString() == "FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH" &&
            gold.GetProperty("x").GetInt32() == 28 && gold.GetProperty("y").GetInt32() == 14, "Gold Teeth source object differs.");
        var npcs = Document("npc-source.json");
        Need(npcs.GetProperty("graphics").GetArrayLength() == 15 && npcs.GetProperty("dynamicGraphics").GetArrayLength() == 0, "Safari NPC inventory differs.");
        return new(directory, maps.ToImmutable(), constants, catalog,
            Document("objects.json"), npcs, availability, reused.ToImmutable(), contracts, Document("encounters.json"));
    }
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Safari source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Safari source file symlink rejected.");
        }
        return result;
    }
}
