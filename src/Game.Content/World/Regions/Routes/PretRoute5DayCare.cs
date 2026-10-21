using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Route 5 Day Care, source scripts and existing town door.</summary>
public sealed class PretRoute5DayCare
{
    public const string ResourceId = "firered-route5-daycare-v1";
    public static MapId HouseMapId { get; } = new("map.route-5-pokemon-day-care");
    public static MapId DayCareMapId => HouseMapId;
    public const string EntryScript = "Route5_PokemonDayCare_EventScript_DaycareMan";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretRoute5DayCare(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/route5-daycare");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE5_POKEMON_DAY_CARE" => HouseMapId,
        "MAP_ROUTE5" => new("map.route-5"),
        _ => throw new InvalidDataException("Unknown Route 5 Day Care map symbol: " + symbol)
    };



    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretRoute5DayCare Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
