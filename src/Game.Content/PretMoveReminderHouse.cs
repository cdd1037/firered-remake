using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Two Island Move Reminder house, source scripts and existing town door.</summary>
public sealed class PretMoveReminderHouse
{
    public const string ResourceId = "firered-move-reminder-house-v1";
    public static MapId HouseMapId { get; } = new("map.two-island-house");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretMoveReminderHouse(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/move-reminder-house");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TWO_ISLAND_HOUSE" => HouseMapId,
        "MAP_TWO_ISLAND" => new("map.two-island"),
        _ => throw new InvalidDataException("Unknown Move Reminder house map symbol: " + symbol)
    };



    /// <summary>dependencyRoot optionally supplies the unchanged town and existing NPC atlas resources.</summary>
    public static PretMoveReminderHouse Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
