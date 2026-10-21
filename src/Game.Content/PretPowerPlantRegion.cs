using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated original Power Plant resources. This is not a game profile or runtime admission.</summary>
public sealed class PretPowerPlantRegion
{
    public const string ResourceId = "firered-power-plant-resources-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }

    public JsonElement Items { get; }
    public string GeneratedDirectory { get; }

    private PretPowerPlantRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants; GeneratedDirectory = source.Directory;
        Contracts = source.Document("contracts.json");

         Items = source.Document("items.json");

    }

    public static PretPowerPlantRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/power-plant");
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();

        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
