using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretFuchsiaRouteRegion
{
    public const string Profile = "firered-fuchsia-route-region-import-v1";
    public static readonly MapId CenterMapId = new("map.fuchsia-city-pokemon-center-1f");
    public const string HealingLocation = "HEAL_LOCATION_FUCHSIA_CITY";
    /// <summary>Complete authenticated southern-route maps. Loading alone admits no field action or service.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps => SourceMaps;
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-12")];
    public ImmutableDictionary<string, int> Constants { get; }

    public ImmutableDictionary<MapId, JsonElement> ReusedEvents { get; }
    public string GeneratedDirectory { get; }

    private PretFuchsiaRouteRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, ImmutableDictionary<MapId, JsonElement> reused)
    {
        GeneratedDirectory = directory; SourceMaps = maps; Constants = constants;
        ReusedEvents = reused;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE13" => new("map.route-13"),
        "MAP_ROUTE14" => new("map.route-14"),
        "MAP_ROUTE15" => new("map.route-15"),
        "MAP_ROUTE15_WEST_ENTRANCE_1F" => new("map.route-15-west-entrance-1f"),
        "MAP_ROUTE15_WEST_ENTRANCE_2F" => new("map.route-15-west-entrance-2f"),
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        "MAP_FUCHSIA_CITY_POKEMON_CENTER_1F" => new("map.fuchsia-city-pokemon-center-1f"),
        "MAP_FUCHSIA_CITY_POKEMON_CENTER_2F" => new("map.fuchsia-city-pokemon-center-2f"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE" => new("map.fuchsia-city-safari-zone-entrance"),
        "MAP_FUCHSIA_CITY_WARDENS_HOUSE" => new("map.fuchsia-city-wardens-house"),
        "MAP_FUCHSIA_CITY_MART" => new("map.fuchsia-city-mart"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_OFFICE" => new("map.fuchsia-city-safari-zone-office"),
        "MAP_FUCHSIA_CITY_GYM" => new("map.fuchsia-city-gym"),
        "MAP_FUCHSIA_CITY_HOUSE1" => new("map.fuchsia-city-house-1"),
        "MAP_FUCHSIA_CITY_HOUSE2" => new("map.fuchsia-city-house-2"),
        "MAP_FUCHSIA_CITY_HOUSE3" => new("map.fuchsia-city-house-3"),
        "MAP_ROUTE18" => new("map.route-18"),
        "MAP_ROUTE19" => new("map.route-19"),
        _ => PretSnorlaxRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Keep source connections, including unavailable neighbors, so callers can reject before mutation.</summary>


    /// <summary>Only a source-package boundary check; not an admission of interactions, trainer approaches or encounters.</summary>
    public bool ContainsDestination(MapId destination) => SourceMaps.ContainsKey(destination) || ReusedMapIds.Contains(destination);
    public bool AllowsNewMapWarp(MapId source, int index)
    {
        if (!SourceMaps.TryGetValue(source, out var map) || index < 0 || index >= map.FieldWarps.Length) return false;
        return ContainsDestination(map.FieldWarps[index].DestinationMapId);
    }
    public JsonElement Route12Events => ReusedEvents[new("map.route-12")];

    /// <summary>Checks the existing Route12 instance before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.ContainsKey(id), "Existing Route12 is missing: " + id.Value);
        }
    }

    /// <summary>Maps retain packed blocks and expand source cells only on demand.</summary>
    public static PretFuchsiaRouteRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/fuchsia-route");

        var maps = world.MapsInDirectory(directory);
        var constants = PretSharedConstants.ForPackage(directory);
        Need(constants[HealingLocation] == 8 && constants["LOCALID_FUCHSIA_NURSE"] == 1 && constants["LOCALID_ROUTE14_BORDER_TREE"] == 13 && constants["SPECIES_KABUTO"] == 140 && constants["SPECIES_OMANYTE"] == 138, "Fuchsia source constants differ.");
        var reused = ImmutableDictionary<MapId, JsonElement>.Empty.Add(new("map.route-12"), world.Maps[new("map.route-12")].Events);
        var center = maps[CenterMapId];
        var nurse = center.Events.GetProperty("object_events")[0];
        Need(nurse.GetProperty("local_id").GetString() == "LOCALID_FUCHSIA_NURSE" && nurse.GetProperty("x").GetInt32() == 7 &&
            nurse.GetProperty("y").GetInt32() == 2 && nurse.GetProperty("script").GetString() == "FuchsiaCity_PokemonCenter_1F_EventScript_Nurse",
            "Fuchsia nurse declaration differs.");
        var fuchsia = maps[new("map.fuchsia-city")];
        Need(fuchsia.Warps[6].DestinationMapId == CenterMapId && fuchsia.Warps[6].DestinationWarpId == 1 &&
            fuchsia.Warps[6].X == 25 && fuchsia.Warps[6].Y == 31 && center.Warps[1].DestinationWarpId == 6,
            "Fuchsia Center entrance differs.");
        Need(center.GetSourceCell(6, 8)!.BehaviorId == 0 && center.GetSourceCell(7, 8)!.BehaviorId == 0x65 &&
            center.GetSourceCell(8, 8)!.BehaviorId == 0, "Center extra warp records must not become wider exits.");
        var clone = maps[new("map.route-15")].Events.GetProperty("object_events")[13];
        var borderTree = maps[new("map.route-14")].Events.GetProperty("object_events")[12];
        Need(clone.GetProperty("type").GetString() == "clone" && clone.GetProperty("x").GetInt32() == 73 && clone.GetProperty("y").GetInt32() == 7 &&
            clone.GetProperty("target_map").GetString() == "MAP_ROUTE14" && clone.GetProperty("target_local_id").GetString() == "LOCALID_ROUTE14_BORDER_TREE" &&
            borderTree.GetProperty("local_id").GetString() == "LOCALID_ROUTE14_BORDER_TREE" && borderTree.GetProperty("x").GetInt32() == 1 && borderTree.GetProperty("y").GetInt32() == 47,
            "Route15 cross-map tree clone differs.");
        return new(directory, maps, constants, reused);
    }
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Fuchsia source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Fuchsia source file symlink rejected.");
        }
        return result;
    }
}
