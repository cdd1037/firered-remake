using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated original Saffron Fighting Dojo resources. This is not a game profile or runtime admission.</summary>
public sealed class PretFightingDojoRegion
{
    public const string ResourceId = "firered-fighting-dojo-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }

    public string GeneratedDirectory { get; }
    /// <summary>Contains authenticated project-relative bindings to existing NPC textures.</summary>

    private PretFightingDojoRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants; GeneratedDirectory = source.Directory;


    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_DOJO" => new("map.saffron-city-dojo"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Saffron Fighting Dojo map symbol: " + symbol)
    };



    public static PretFightingDojoRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/fighting-dojo");
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.ContainsKey(new("map.saffron-city-dojo")) && maps.Values.Single().FieldWarps.Length == 3,
            "Saffron Fighting Dojo map/warp collection differs.");

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["LOCALID_KARATE_MASTER"] == 5 && constants["FLAG_GOT_HITMON_FROM_DOJO"] == 0x278 &&
            constants["FLAG_HIDE_DOJO_HITMONLEE_BALL"] == 0x60 && constants["FLAG_HIDE_DOJO_HITMONCHAN_BALL"] == 0x61 &&
            constants["SPECIES_HITMONLEE"] == 106 && constants["SPECIES_HITMONCHAN"] == 107,
            "Dojo actor and gift constants differ.");
        var events = maps.Values.Single().Events;
        Need(events.GetProperty("object_events").GetArrayLength() == 7 && events.GetProperty("coord_events").GetArrayLength() == 2 &&
            events.GetProperty("bg_events").GetArrayLength() == 4, "Dojo original event cohort differs.");
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
