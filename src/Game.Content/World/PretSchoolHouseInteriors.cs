using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Source-selected ordinary interiors. Geometry, actors and reciprocal doors are catalog data.</summary>
public sealed class PretSchoolHouseInteriors
{
    public const string ResourceId = "firered-school-house-interiors-v1";
    public static MapId SchoolMapId { get; } = new("map.viridian-school");
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-1");
    public static MapId ThreeIslandHouseMapId { get; } = new("map.three-island-house2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }

    private PretSchoolHouseInteriors(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        Func<string, MapId> resolve)
    {
        NewMaps = maps;
        Constants = PretSharedConstants.ForPackage(source.Directory);
        GeneratedDirectory = source.Directory;
    }



    public static PretSchoolHouseInteriors Load(PretCurrentWorld world, string? dependencyRoot = null)
    {
        var projectRoot = world.ProjectRoot;
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "maps/regions/school-house-interiors");
        var maps = world.MapsInDirectory(source.Directory);
        foreach (var map in maps.Values)
        {
            Need(map.FieldWarps.Length > 0 && map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
                map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Ordinary interior event scope differs.");
        }
        var result = new PretSchoolHouseInteriors(source, maps, world.Resolve);
        result.ValidateReusedMaps(world.Maps);
        return result;
    }

    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var room in NewMaps.Values)
        foreach (var exit in room.FieldWarps)
        {
            if (!existing.TryGetValue(exit.DestinationMapId, out var exterior))
                throw new InvalidDataException("Ordinary interior exterior is not mounted.");
            Need(exit.DestinationWarpId >= 0 && exit.DestinationWarpId < exterior.FieldWarps.Length, "Exterior entrance index is invalid.");
            var entrance = exterior.FieldWarps[exit.DestinationWarpId];
            Need(entrance.DestinationMapId == room.Id && entrance.DestinationWarpId >= 0 &&
                entrance.DestinationWarpId < room.FieldWarps.Length, "Exterior entrance does not reach the interior.");
            var returned = room.FieldWarps[entrance.DestinationWarpId];
            Need(returned.DestinationMapId == exterior.Id && returned.DestinationWarpId == exit.DestinationWarpId,
                "Interior door is not reciprocal.");
        }
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
