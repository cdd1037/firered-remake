using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original Seafoam and entrance routes. Loading source resources does not admit gameplay.</summary>
public sealed class PretSeafoamRegion
{
    public const string ResourceId = "firered-seafoam-resources-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    /// <summary>Two alternate original layouts; their map IDs and event headers remain the base map's.</summary>
    public ImmutableDictionary<string, PretWorldMap> LayoutVariants { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Contracts { get; }
    public JsonElement Items { get; }
    public string GeneratedDirectory { get; }
    private readonly GeneratedFieldTilesets _fieldTilesets;

    private PretSeafoamRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, PretWorldMap> variants, ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; LayoutVariants = variants; Constants = constants;
         GeneratedDirectory = source.Directory;
        Contracts = source.Document("contracts.json");

         Items = source.Document("items.json");
        _fieldTilesets = GeneratedFieldTilesets.ForPackage(source.Directory);
    }

    public PretWorldMap SeafoamMap(MapId map, bool currentStopped) => currentStopped && map.Value is "map.seafoam-islands-b3f" or "map.seafoam-islands-b4f"
        ? LayoutVariants[map.Value == "map.seafoam-islands-b3f" ? "LAYOUT_SEAFOAM_ISLANDS_B3F_CURRENT_STOPPED" : "LAYOUT_SEAFOAM_ISLANDS_B4F_CURRENT_STOPPED"]
        : NewMaps[map];



    /// <summary>Complete original attributes, including CalmWater used by B4F OnLoad.</summary>
    public FieldMetatileDefinition? MetatileDefinition(MapId map, int metatile)
    {
        return NewMaps.TryGetValue(map, out var field) ? _fieldTilesets.Find(field.Events.GetProperty("layout").GetString()!, metatile) : null;
    }

    public static PretSeafoamRegion Load(PretCurrentWorld world, string? dependencyRoot = null)
    {
        var projectRoot = world.ProjectRoot;
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/seafoam");
        var maps = world.MapsInDirectory(source.Directory);
        var variants = world.VariantsInDirectory(source.Directory);
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps, variants, constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
