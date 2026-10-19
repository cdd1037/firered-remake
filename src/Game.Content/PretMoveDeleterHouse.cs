using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Fuchsia City Move Deleter house, source scripts and existing town door.</summary>
public sealed class PretMoveDeleterHouse
{
    public const string ResourceId = "firered-move-deleter-v1";
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-3");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    private readonly string _projectRoot;

    private PretMoveDeleterHouse(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        GeneratedDirectory = Path.Combine(root, "content/generated/maps/regions/move-deleter");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        _projectRoot = root;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FUCHSIA_CITY_HOUSE3" => HouseMapId,
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        _ => throw new InvalidDataException("Unknown Move Deleter house map symbol: " + symbol)
    };



    /// <summary>Bind only after composition has constructed its final owned catalog. Reuses authenticated source bytes.</summary>
    public MoveDeleterCatalog Bind(PokemonCatalog owned, IReadOnlyDictionary<string, string>? sourceTexts = null) =>
        MoveDeleterCatalog.Authenticate(owned, PretMoveDeleterTexts.LoadProject(_projectRoot, sourceTexts));

    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretMoveDeleterHouse Load(PretCurrentWorld world) => new(world);
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
