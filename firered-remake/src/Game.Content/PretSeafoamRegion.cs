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
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Encounters { get; }
    public JsonElement Items { get; }
    public JsonElement Trainers { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private readonly ImmutableDictionary<MapId, (byte[] Primary, byte[] Secondary)> _attributes;

    private PretSeafoamRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, PretWorldMap> variants, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; LayoutVariants = variants; Scripts = scripts; TextSources = texts; Constants = constants;
        SourceFiles = source.Files; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        Encounters = source.Document("encounters.json"); Items = source.Document("items.json"); Trainers = source.Document("trainers.json");
        _attributes = Catalog.GetProperty("maps").EnumerateArray().ToImmutableDictionary(s => new MapId(s.GetProperty("stableId").GetString()!), s =>
        {
            var layout = s.GetProperty("layout");
            byte[] Bytes(string kind) => source.Raw["data/tilesets/" + Catalog.GetProperty("tilesets").GetProperty(layout.GetProperty(kind).GetString()!).GetString() + "/metatile_attributes.bin"];
            return (Bytes("primary_tileset"), Bytes("secondary_tileset"));
        });
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
        if (!_attributes.TryGetValue(map, out var pair) || metatile is < 0 or > 1023) return null;
        var bytes = metatile < 640 ? pair.Primary : pair.Secondary; int offset = (metatile < 640 ? metatile : metatile - 640) * 4;
        if (offset + 4 > bytes.Length) return null;
        var a = PretWorldDecoder.DecodeAttributes(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
        return new(metatile, a.BehaviorId, a.Terrain, a.EncounterType, a.LayerType);
    }

    public static PretSeafoamRegion Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"seafoam-resources",            "scripts/seafoam-resources.lock.json");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("maps").GetArrayLength() == 7 && catalog.GetProperty("layoutVariants").GetArrayLength() == 2 , "Seafoam catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Seafoam resources; current-game admission is separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        string player = PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png");
        PretWorldMap LoadMap(JsonElement spec, bool variant)
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" , "Seafoam compact binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Seafoam map identity differs.");
            var layout = spec.GetProperty(variant ? "sourceLayout" : "layout");
            using var originalLayouts = JsonDocument.Parse(source.Raw["data/layouts/layouts.json"]);
            var original = originalLayouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l => l.TryGetProperty("id", out var v) && v.GetString() == layout.GetProperty("id").GetString());
            Need(JsonElement.DeepEquals(layout, original), "Seafoam original layout differs.");
            Need(runtime.Width == layout.GetProperty("width").GetInt32() && runtime.Height == layout.GetProperty("height").GetInt32(), "Seafoam layout dimensions differ.");
            return PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative), player, MapIdFromSourceSymbol);
        }
        var maps = catalog.GetProperty("maps").EnumerateArray().ToImmutableDictionary(s => new MapId(s.GetProperty("stableId").GetString()!), s => LoadMap(s, false));
        var variants = catalog.GetProperty("layoutVariants").EnumerateArray().ToImmutableDictionary(s => s.GetProperty("layoutSymbol").GetString()!, s => LoadMap(s, true), StringComparer.Ordinal);
        Need(maps.Count == 7 && variants.Count == 2 && maps.Values.Sum(m => m.Events.GetProperty("object_events").GetArrayLength()) == 43, "Seafoam map/object collection differs.");
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 270 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Seafoam script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing Seafoam script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret,path,label,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps, variants, scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
