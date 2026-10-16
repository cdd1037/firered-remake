using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated original Cinnabar fossil lab resources. This is not a game profile or runtime admission.</summary>
public sealed class PretFossilLabRegion
{
    public const string ResourceId = "firered-fossil-lab-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }

    public string GeneratedDirectory { get; }
    /// <summary>Contains authenticated project-relative bindings to existing NPC textures.</summary>

    private PretFossilLabRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants; GeneratedDirectory = source.Directory;

    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM" => new("map.cinnabar-island-pokemon-lab-experiment-room"),
        "MAP_CINNABAR_ISLAND" => new("map.cinnabar-island"),
        // These are original, preserved warp destinations. Resolving their IDs
        // does not add them to NewMaps or admit traversal into missing rooms.
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE" => new("map.cinnabar-island-pokemon-lab-lounge"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM" => new("map.cinnabar-island-pokemon-lab-research-room"),
        _ => throw new InvalidDataException("Unknown Cinnabar fossil lab map symbol: " + symbol)
    };



    public static PretFossilLabRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/fossil-lab");
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.Keys.ToHashSet().SetEquals(new[] { new MapId("map.cinnabar-island-pokemon-lab-entrance"),new MapId("map.cinnabar-island-pokemon-lab-experiment-room") }) ,
            "Cinnabar fossil lab map/warp collection differs.");

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["HELIX_FOSSIL"] == 1 && constants["DOME_FOSSIL"] == 2 && constants["OLD_AMBER"] == 3 &&
            constants["VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_WHICH_FOSSIL"] == 0x4069 &&
            constants["VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_REVIVE_STATE"] == 0x406A &&
            constants["FLAG_REVIVED_DOME"] == 0x2EC && constants["FLAG_REVIVED_HELIX"] == 0x2ED && constants["FLAG_REVIVED_AMBER"] == 0x2EE &&
            constants["SPECIES_OMANYTE"] == 138 && constants["SPECIES_KABUTO"] == 140 && constants["SPECIES_AERODACTYL"] == 142,
            "Fossil selection, state and gift constants differ.");
        var entranceEvents = maps[new("map.cinnabar-island-pokemon-lab-entrance")].Events;
        var experimentEvents = maps[new("map.cinnabar-island-pokemon-lab-experiment-room")].Events;
        Need(entranceEvents.GetProperty("object_events").GetArrayLength() == 1 &&
            entranceEvents.GetProperty("coord_events").GetArrayLength() == 0 && entranceEvents.GetProperty("bg_events").GetArrayLength() == 4 &&
            experimentEvents.GetProperty("object_events").GetArrayLength() == 2 &&
            experimentEvents.GetProperty("coord_events").GetArrayLength() == 0 && experimentEvents.GetProperty("bg_events").GetArrayLength() == 0,
            "Fossil lab original event cohort differs.");
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
