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

    public JsonElement Contracts { get; }
    public JsonElement EncounterInventory { get; }
    public ImmutableDictionary<MapId, JsonElement> ReusedEvents { get; }
    public string GeneratedDirectory { get; }

    private PretSafariRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, ImmutableDictionary<MapId, JsonElement> reused, JsonElement contracts, JsonElement encounters)
    {
        GeneratedDirectory = directory; SourceMaps = maps; Constants = constants;
        ReusedEvents = reused;
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
    public static PretSafariRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/safari");

        byte[] Pinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(Safe(root, path));
            return bytes;
        }
        JsonElement Document(string path) { using var doc = JsonDocument.Parse(Pinned(directory, path)); return doc.RootElement.Clone(); }
        var maps = world.MapsInDirectory(directory);
        var constants = PretSharedConstants.ForPackage(directory);
        Need(constants["ITEM_HM03"] == 341 && constants["ITEM_HM04"] == 342 && constants["ITEM_GOLD_TEETH"] == 353 &&
            constants["FLAG_GOT_HM03"] == 0x239 && constants["FLAG_GOT_HM04"] == 0x23a && constants["FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH"] == 0x189,
            "Safari reward constants differ.");
        var reused = ImmutableDictionary<MapId, JsonElement>.Empty.Add(new("map.fuchsia-city"), world.Maps[new("map.fuchsia-city")].Events);
        var contracts = Document("contracts.json");
        Need(contracts.GetProperty("entryFee").GetInt32() == 500 && contracts.GetProperty("startingBalls").GetInt32() == 30 &&
            contracts.GetProperty("startingSteps").GetInt32() == 600 && contracts.GetProperty("pickups").GetArrayLength() == 13,
            "Safari source session/reward contract differs.");
        var gold = maps[new("map.safari-zone-west")].Events.GetProperty("object_events")[0];
        Need(gold.GetProperty("script").GetString() == "SafariZone_West_EventScript_ItemGoldTeeth" && gold.GetProperty("flag").GetString() == "FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH" &&
            gold.GetProperty("x").GetInt32() == 28 && gold.GetProperty("y").GetInt32() == 14, "Gold Teeth source object differs.");
        return new(directory, maps, constants,
            reused, contracts, Document("encounters.json"));
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
