using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Twenty-four exact event-island maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretEventIslandRegion
{
    public const string Profile = "firered-event-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public EventIslandFieldData Field { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    private PretEventIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");


        Field = LoadField(source, constants);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_BIRTH_ISLAND_EXTERIOR" => new("map.birth-island-exterior"),
        "MAP_BIRTH_ISLAND_HARBOR" => new("map.birth-island-harbor"),
        "MAP_NAVEL_ROCK_1F" => new("map.navel-rock-1f"),
        "MAP_NAVEL_ROCK_B1F" => new("map.navel-rock-b1f"),
        "MAP_NAVEL_ROCK_BASE" => new("map.navel-rock-base"),
        "MAP_NAVEL_ROCK_BASE_PATH_B10F" => new("map.navel-rock-base-path-b10f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B11F" => new("map.navel-rock-base-path-b11f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B1F" => new("map.navel-rock-base-path-b1f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B2F" => new("map.navel-rock-base-path-b2f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B3F" => new("map.navel-rock-base-path-b3f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B4F" => new("map.navel-rock-base-path-b4f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B5F" => new("map.navel-rock-base-path-b5f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B6F" => new("map.navel-rock-base-path-b6f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B7F" => new("map.navel-rock-base-path-b7f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B8F" => new("map.navel-rock-base-path-b8f"),
        "MAP_NAVEL_ROCK_BASE_PATH_B9F" => new("map.navel-rock-base-path-b9f"),
        "MAP_NAVEL_ROCK_EXTERIOR" => new("map.navel-rock-exterior"),
        "MAP_NAVEL_ROCK_FORK" => new("map.navel-rock-fork"),
        "MAP_NAVEL_ROCK_HARBOR" => new("map.navel-rock-harbor"),
        "MAP_NAVEL_ROCK_SUMMIT" => new("map.navel-rock-summit"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_2F" => new("map.navel-rock-summit-path-2f"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_3F" => new("map.navel-rock-summit-path-3f"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_4F" => new("map.navel-rock-summit-path-4f"),
        "MAP_NAVEL_ROCK_SUMMIT_PATH_5F" => new("map.navel-rock-summit-path-5f"),
        _ => PretSevenIslandRegion.MapIdFromSourceSymbol(symbol)
    };

    public static PretEventIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root,"maps/regions/event-island");

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "EventIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
