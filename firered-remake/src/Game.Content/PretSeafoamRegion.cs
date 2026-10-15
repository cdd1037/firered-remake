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
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Encounters { get; }
    public JsonElement Items { get; }
    public JsonElement Trainers { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private readonly GeneratedFieldTilesets _fieldTilesets;

    private PretSeafoamRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, PretWorldMap> variants, ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; LayoutVariants = variants; Constants = constants;
         GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        Encounters = source.Document("encounters.json"); Items = source.Document("items.json"); Trainers = source.Document("trainers.json");
        _fieldTilesets = GeneratedFieldTilesets.ForPackage(source.Directory);
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SEAFOAM_ISLANDS_1F" => new("map.seafoam-islands-1f"),
        "MAP_SEAFOAM_ISLANDS_B1F" => new("map.seafoam-islands-b1f"),
        "MAP_SEAFOAM_ISLANDS_B2F" => new("map.seafoam-islands-b2f"),
        "MAP_SEAFOAM_ISLANDS_B3F" => new("map.seafoam-islands-b3f"),
        "MAP_SEAFOAM_ISLANDS_B4F" => new("map.seafoam-islands-b4f"),
        "MAP_ROUTE19" => new("map.route-19"), "MAP_ROUTE20" => new("map.route-20"),
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"), "MAP_CINNABAR_ISLAND" => new("map.cinnabar-island"),
        _ => throw new InvalidDataException("Unknown Seafoam map symbol: " + symbol)
    };

    public PretWorldMap SeafoamMap(MapId map, bool currentStopped) => currentStopped && map.Value is "map.seafoam-islands-b3f" or "map.seafoam-islands-b4f"
        ? LayoutVariants[map.Value == "map.seafoam-islands-b3f" ? "LAYOUT_SEAFOAM_ISLANDS_B3F_CURRENT_STOPPED" : "LAYOUT_SEAFOAM_ISLANDS_B4F_CURRENT_STOPPED"]
        : NewMaps[map];

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Seafoam route connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Complete original attributes, including CalmWater used by B4F OnLoad.</summary>
    public FieldMetatileDefinition? MetatileDefinition(MapId map, int metatile)
    {
        return NewMaps.TryGetValue(map, out var field) ? _fieldTilesets.Find(field.Events.GetProperty("layout").GetString()!, metatile) : null;
    }

    public static PretSeafoamRegion Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"seafoam-resources");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("maps").GetArrayLength() == 7 && catalog.GetProperty("layoutVariants").GetArrayLength() == 2 , "Seafoam catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Seafoam resources; current-game admission is separate.", ResourceId, ImmutableArray<PretWorldSourceFile>.Empty);
        string player = PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png");
        PretWorldMap LoadMap(JsonElement spec, bool variant)
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" , "Seafoam compact binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Seafoam map identity differs.");
            var layout = spec.GetProperty(variant ? "sourceLayout" : "layout");
            Need(runtime.Width == layout.GetProperty("width").GetInt32() && runtime.Height == layout.GetProperty("height").GetInt32(), "Seafoam layout dimensions differ.");
            return PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative), player, MapIdFromSourceSymbol);
        }
        var maps = catalog.GetProperty("maps").EnumerateArray().ToImmutableDictionary(s => new MapId(s.GetProperty("stableId").GetString()!), s => LoadMap(s, false));
        var variants = catalog.GetProperty("layoutVariants").EnumerateArray().ToImmutableDictionary(s => s.GetProperty("layoutSymbol").GetString()!, s => LoadMap(s, true), StringComparer.Ordinal);
        Need(maps.Count == 7 && variants.Count == 2 && maps.Values.Sum(m => m.Events.GetProperty("object_events").GetArrayLength()) == 43, "Seafoam map/object collection differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps, variants, constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
