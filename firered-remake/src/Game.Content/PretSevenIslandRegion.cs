using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Eighteen exact Seven Island maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretSevenIslandRegion
{
    public const string Profile = "firered-seven-island-region-v1";
    public const string FullSourceHash = "cbfe0ba113e65db478cfac3c8bcdbbb8adfda1212cf976a192e162dbe67806dc";
    public const string ManifestSha256 = "4d6c432e83937e3c659dd534dd1f4bc52340665b205bfb4634fc53620dc66180";
    public const string LockSha256 = "54e4c4776c2d20d3d08b7063b7c2040ad44d96a387e58c20bdfc59aaaae0a2ba";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Geometry { get; }
    public JsonElement Items { get; }
    public SevenIslandFieldData Field { get; }
    public ImmutableDictionary<string, ImmutableArray<byte>> RawSources { get; }
    public JsonElement NpcSources { get; }
    public ImmutableDictionary<string, JsonElement> ReusedAnchorEvents { get; }
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSevenIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); Geometry = source.Document("geometry.json"); Items = source.Document("items.json");
        RawSources = source.Raw.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
        ReusedAnchorEvents = new[] { "VermilionCity" }.ToImmutableDictionary(n => n, n => SilphSourcePackage.Json(source.Raw["data/maps/" + n + "/map.json"]), StringComparer.Ordinal); NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["seven-island-region-manifest:" + ManifestSha256, "seven-island-region-lock:" + LockSha256,
            .. source.Files.Select(f => "seven-island-region:" + f.Path + ":" + f.Sha256)];
        InitializeField(source);
        Field = LoadField(source, constants);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SEVEN_ISLAND" => new("map.seven-island"),
        "MAP_SEVEN_ISLAND_HARBOR" => new("map.seven-island-harbor"),
        "MAP_SEVEN_ISLAND_HOUSE_ROOM1" => new("map.seven-island-house-room1"),
        "MAP_SEVEN_ISLAND_HOUSE_ROOM2" => new("map.seven-island-house-room2"),
        "MAP_SEVEN_ISLAND_MART" => new("map.seven-island-mart"),
        "MAP_SEVEN_ISLAND_POKEMON_CENTER_1F" => new("map.seven-island-pokemon-center-1f"),
        "MAP_SEVEN_ISLAND_POKEMON_CENTER_2F" => new("map.seven-island-pokemon-center-2f"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON" => new("map.seven-island-sevault-canyon"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON_ENTRANCE" => new("map.seven-island-sevault-canyon-entrance"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON_HOUSE" => new("map.seven-island-sevault-canyon-house"),
        "MAP_SEVEN_ISLAND_SEVAULT_CANYON_TANOBY_KEY" => new("map.seven-island-sevault-canyon-tanoby-key"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS" => new("map.seven-island-tanoby-ruins"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_DILFORD_CHAMBER" => new("map.seven-island-tanoby-ruins-dilford-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_LIPTOO_CHAMBER" => new("map.seven-island-tanoby-ruins-liptoo-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_MONEAN_CHAMBER" => new("map.seven-island-tanoby-ruins-monean-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_RIXY_CHAMBER" => new("map.seven-island-tanoby-ruins-rixy-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_SCUFIB_CHAMBER" => new("map.seven-island-tanoby-ruins-scufib-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_VIAPOIS_CHAMBER" => new("map.seven-island-tanoby-ruins-viapois-chamber"),
        "MAP_SEVEN_ISLAND_TANOBY_RUINS_WEEPTH_CHAMBER" => new("map.seven-island-tanoby-ruins-weepth-chamber"),
        "MAP_SEVEN_ISLAND_TRAINER_TOWER" => new("map.seven-island-trainer-tower"),
        "MAP_TRAINER_TOWER_LOBBY" => new("map.trainer-tower-lobby"),
        _ => PretSixIslandRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Seven Island connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSevenIslandRegion Load(string root, bool compact = true)
    {
        var source = new SilphSourcePackage(root, "seven-island-region", Profile,
            "content/sources/seven-island-region.lock.json", LockSha256, 93542, ManifestSha256, 36106, 296, 198);
        var raw = source.Raw; var files = source.Files; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outputs = source.Outputs; var directory = source.Directory;
        var outputEntries = source.Manifest.GetProperty("outputs").EnumerateArray().ToDictionary(e => e.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash && JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")) &&
            !catalog.GetProperty("runtimeIntegrated").GetBoolean() && catalog.GetProperty("maps").GetArrayLength() == 18, "SevenIsland source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Original Seven Island source maps; application admission remains separate.", Profile, files);
        using var layoutsDocument = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layouts = layoutsDocument.RootElement.GetProperty("layouts").EnumerateArray().Where(x => x.TryGetProperty("id", out _)).ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" &&
                runtimeEntry.GetProperty("sha256").GetString() == outputEntries[relative + "/runtime-map.json"].GetProperty("sha256").GetString(), "SevenIsland runtime certificate differs.");
            var data = PretRuntimeMapValidation.Decode<PretRuntimeMapDocument>(outputs[relative + "/runtime-map.json"]);
            Need(data.SchemaVersion == 1 && data.ConverterVersion == PretRuntimeMapCatalog.ConverterVersion && data.SourceCommit == PretWorldMap.SourceCommit &&
                data.FullSourceHash == FullSourceHash && data.SourceWorldSha256 == runtimeEntry.GetProperty("sourceWorldSha256").GetString() &&
                data.StableId == stableId && MapIdFromSourceSymbol(data.SourceRef.Symbol).Value == stableId && data.TechnicalSpawn is null && data.TileSize == 16,
                "Compact map identity differs.");
            Need(data.SourceRef.Kind == SourceKind.Pret && data.SourceRef.Repository == "pret/pokefirered" && data.SourceRef.Commit == PretWorldMap.SourceCommit &&
                data.SourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                data.SourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && data.SourceRef.Sha256 == byPath[data.SourceRef.Path].Sha256, "Map source reference differs.");
            using var originalEvents = JsonDocument.Parse(raw[data.SourceRef.Path]);
            Need(JsonElement.DeepEquals(data.Events, originalEvents.RootElement), "Map events differ from source.");
            var layout = layouts[data.Events.GetProperty("layout").GetString()!];
            Need((data.Width, data.Height, data.BorderWidth, data.BorderHeight) ==
                (layout.GetProperty("width").GetInt32(), layout.GetProperty("height").GetInt32(), layout.GetProperty("border_width").GetInt32(), layout.GetProperty("border_height").GetInt32()), "Map dimensions differ.");
            var bases = new[] { "primary_tileset", "secondary_tileset" }.Select(k => "data/tilesets/" + catalog.GetProperty("tilesets").GetProperty(layout.GetProperty(k).GetString()!).GetString()).ToArray();
            var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]), @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
                .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
            var metatiles = data.Metatiles.ToDictionary(r => checked((int)r[0])); var behaviors = data.Behaviors.ToDictionary(r => r.Id, r => r.Name);
            ImmutableArray<PretWorldCell> Cells(string encoded, string path, int width, int height)
            {
                var blocks = Convert.FromBase64String(encoded);
                Need(Convert.ToBase64String(blocks) == encoded && blocks.AsSpan().SequenceEqual(raw[path]) && blocks.Length == width * height * 2, "Compact block source differs.");
                var result = ImmutableArray.CreateBuilder<PretWorldCell>(width * height);
                for (var i = 0; i < width * height; i++)
                {
                    var block = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(i * 2)); var (mid, collision, elevation) = PretWorldDecoder.DecodeBlock(block);
                    var row = metatiles[mid]; Need(row.Length == 3 && row[2] < data.AttributeSources.Length, "Invalid metatile table.");
                    var source = data.AttributeSources[(int)row[2]]; var local = mid < 640 ? mid : mid - 640;
                    Need(source.Path == bases[mid < 640 ? 0 : 1] + "/metatile_attributes.bin" && source.Sha256 == byPath[source.Path].Sha256, "Metatile source differs.");
                    var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[source.Path].AsSpan(local * 4)); var a = PretWorldDecoder.DecodeAttributes(attribute);
                    Need(row[1] == attribute && a.LayerType <= 2 && behaviors[a.BehaviorId] == behaviorNames.GetValueOrDefault(a.BehaviorId, $"UNKNOWN_0x{a.BehaviorId:X3}"), "Metatile attributes/behavior differ.");
                    result.Add(new(i % width, i / width, block, mid, collision, elevation, attribute, a.BehaviorId, behaviors[a.BehaviorId], a.Terrain,
                        a.Attribute2, a.Attribute3, a.EncounterType, a.Attribute5, a.LayerType, a.Attribute7, PretWorldDecoder.Movement(a.BehaviorId),
                        new(SourceKind.Pret, source.Path, $"metatile_{local:x3}", "pret/pokefirered", PretWorldMap.SourceCommit, source.Sha256)));
                }
                return result.MoveToImmutable();
            }
            var cells = Cells(data.Blocks, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Height);
            var border = Cells(data.BorderBlocks, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderHeight);
            Need(metatiles.Keys.ToHashSet().SetEquals(cells.Concat(border).Select(c => c.MetatileId)) && behaviors.Keys.ToHashSet().SetEquals(cells.Concat(border).Select(c => c.BehaviorId)), "Extra/missing compact table entries.");
            var embedded = ImmutableArray.CreateBuilder<PretWorldEmbeddedSource>();
            foreach (var script in data.Scripts)
            {
                Need(script.Path.StartsWith(data.SourceRef.Path[..^"map.json".Length], StringComparison.Ordinal) && script.Sha256 == byPath[script.Path].Sha256 &&
                    Encoding.UTF8.GetBytes(script.Text).AsSpan().SequenceEqual(raw[script.Path]), "Live script differs.");
                embedded.Add(new(script.Path, script.Sha256, Convert.ToBase64String(raw[script.Path])));
            }
            Need(data.Scripts.Any(s => s.Path.EndsWith("/scripts.inc", StringComparison.Ordinal)), "Map script missing.");
            Need(data.TexturePath == "map.png" && data.Artifacts.Length == 8, "Artifact inventory differs.");
            foreach (var art in data.Artifacts)
            {
                Need(outputEntries[relative + "/" + art.Path].GetProperty("sha256").GetString() == art.Sha256 &&
                    (art.Path.StartsWith("border", StringComparison.Ordinal) ? (art.Width, art.Height) == (data.BorderWidth * 16, data.BorderHeight * 16) : (art.Width, art.Height) == (data.Width * 16, data.Height * 16)), "Map artifact source differs.");
            }
            var world = new PretWorldDocument(1, stableId, data.SourceRef, metadata, data.Width, data.Height, data.TileSize, data.BorderWidth,
                data.BorderHeight, data.TexturePath, data.Artifacts, cells, border, embedded.ToImmutable(), null, data.Events.Clone());
            if (compact)
            {
                var runtime = PretRuntimeMapData.Decode(outputs[relative + "/runtime-map.json"], new(stableId,
                    relative + "/runtime-map.json", outputs[relative + "/runtime-map.json"].Length,
                    runtimeEntry.GetProperty("sha256").GetString()!, relative + "/world.json",
                    spec.GetProperty("sourceWorldByteLength").GetInt32(),
                    data.SourceWorldSha256, FullSourceHash));
                maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), Safe(directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
            }
            else maps.Add(new(stableId), PretWorldMap.FromAuthenticatedExtension(world, Safe(directory, relative), Safe(directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 18 && maps.Values.Sum(m => m.Width * m.Height) == 14685 && maps.Values.Sum(m => m.FieldWarps.Length) == 29,
            "SevenIsland source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SevenIsland target warp index differs.");
        var inventory = source.Document("scripts.json"); var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Need(inventory.GetProperty("blocks").GetArrayLength() == 425 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "SevenIsland source script closure differs.");
        foreach (var row in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = row.GetString()!; var text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing SevenIsland script label."); scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_labels.h"]),
            @"^#define\s+(METATILE_\w+)\s+0x([0-9A-Fa-f]+)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            Need(constants.TryGetValue(match.Groups[1].Value, out var value) && value ==
                int.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                "Generated SevenIsland metatile constant differs from source.");
        ValidateContracts(source, maps.ToImmutable(), constants);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
