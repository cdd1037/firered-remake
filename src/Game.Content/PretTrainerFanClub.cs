using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Saffron Trainer Fan Club, with authenticated source scripts and unchanged town entrance.</summary>
public sealed class PretTrainerFanClub
{
    public const string ResourceId = "firered-trainer-fan-club-v1";
    public const string ScriptPath = "data/maps/SaffronCity_PokemonTrainerFanClub/scripts.inc";
    public static MapId RoomMapId { get; } = new("map.saffron-city-pokemon-trainer-fan-club");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretTrainerFanClub(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/trainer-fan-club");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB" => RoomMapId,
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Trainer Fan Club map symbol: " + symbol)
    };



    /// <summary>dependencyRoot supplies unchanged Saffron City and the nine existing NPC sheets.</summary>
    public static PretTrainerFanClub Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
