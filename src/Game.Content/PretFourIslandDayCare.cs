using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Four Island Day Care, source scripts and existing town door.</summary>
public sealed class PretFourIslandDayCare
{
    public const string ResourceId = "firered-four-island-daycare-v1";
    public static MapId HouseMapId { get; } = new("map.four-island-pokemon-day-care");
    public static MapId DayCareMapId => HouseMapId;
    public const string EntryScript = "FourIsland_PokemonDayCare_EventScript_DaycareWoman";
    public const string ManEntryScript = "FourIsland_EventScript_DaycareMan";
    public const string PendingEggPositionScript = "FourIsland_EventScript_TrySetDayCareManPos";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretFourIslandDayCare(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/four-island-daycare");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" => HouseMapId,
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        _ => throw new InvalidDataException("Unknown Four Island Day Care map symbol: " + symbol)
    };



    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretFourIslandDayCare Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
