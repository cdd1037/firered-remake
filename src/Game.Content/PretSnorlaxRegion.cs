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
    public JsonElement Boundaries { get; }

    public string GeneratedDirectory { get; }

    private PretSnorlaxRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, JsonElement boundaries)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Boundaries = boundaries;

    }

    /// <summary>Retains external source connections. Availability must be checked before committing a transition.</summary>


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

    public static PretSnorlaxRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/snorlax");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
            return bytes;
        }
        var maps = world.MapsInDirectory(directory);
        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_HIDE_ROUTE_12_SNORLAX"] == 84 && constants["FLAG_HIDE_ROUTE_16_SNORLAX"] == 128 && constants["FLAG_WOKE_UP_ROUTE_12_SNORLAX"] == 595 && constants["FLAG_SYS_SPECIAL_WILD_BATTLE"] == 2055 && constants["CRY_MODE_ENCOUNTER"] == 2, "Snorlax constants differ.");
        using var boundaries = JsonDocument.Parse(ReadPinned(directory, "boundaries.json"));
        Require(boundaries.RootElement.GetProperty("maps").GetArrayLength() == 4, "Boundary scope differs.");
        return new(directory, maps, constants.ToImmutable(),
            boundaries.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
}
