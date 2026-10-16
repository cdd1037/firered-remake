using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original two-floor Saffron Copycat house; item and tutor admission are separate.</summary>
public sealed class PretCopycatHouse
{
    public const string ResourceId = "firered-copycat-house-v1";
    public static MapId Floor1MapId { get; } = new("map.saffron-city-copycats-house-1f");
    public static MapId Floor2MapId { get; } = new("map.saffron-city-copycats-house-2f");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretCopycatHouse(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/copycat-house");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_COPYCATS_HOUSE_1F" => Floor1MapId,
        "MAP_SAFFRON_CITY_COPYCATS_HOUSE_2F" => Floor2MapId,
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Copycat house map symbol: " + symbol)
    };



    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged town and actor resources.</summary>
    public static PretCopycatHouse Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
