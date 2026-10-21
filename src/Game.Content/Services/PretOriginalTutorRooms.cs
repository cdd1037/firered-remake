using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Two original tutor interiors and their unchanged adjacent entrances; teaching admission is separate.</summary>
public sealed class PretOriginalTutorRooms
{
    public const string ResourceId = "firered-original-tutor-rooms-v1";
    public static MapId BodySlamMapId { get; } = new("map.four-island-house1");
    public static MapId MetronomeMapId { get; } = new("map.cinnabar-island-pokemon-lab-research-room");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretOriginalTutorRooms(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/original-tutor-rooms");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND_HOUSE1" => BodySlamMapId,
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM" => MetronomeMapId,
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        _ => throw new InvalidDataException("Unknown original tutor room map symbol: " + symbol)
    };



    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretOriginalTutorRooms Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
