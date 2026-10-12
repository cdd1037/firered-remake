using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Thirteen exact source maps. This package does not activate scripts, rewards or field transitions.</summary>
public sealed partial class PretSilphRegion
{
    public const string Profile = "firered-silph-region-import-v1";
    public const string FullSourceHash = "92b22ea64df5ebefcf3ad6246db7a7939c903cb79f4cfc51844004ea71c89fba";
    public const string ManifestSha256 = "5a2ccb95fd88fa7d6232703cd65756f77ffaa04dd202ee8621c88e830cf16331";
    public const string LockSha256 = "1dac7a359927d4ff07f80cde18427eb189af281cc1a379024e8fbc7ba45e9467";
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
    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalSaffronEvents => Catalog.GetProperty("externalSaffronEvents");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSilphRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        Catalog = source.Document("silph-region-catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); ObjectTemplates = source.Document("objects.json"); NpcSources = source.Document("npc-source.json");
        InitializeField(source);
        AdditionalSourceHashes = ["silph-region-manifest:" + ManifestSha256, "silph-region-lock:" + LockSha256,
            .. source.Files.Select(f => "silph-region:" + f.Path + ":" + f.Sha256)];
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol.StartsWith("MAP_SILPH_CO_", StringComparison.Ordinal) || symbol.StartsWith("MAP_SAFFRON_CITY", StringComparison.Ordinal)
        ? new("map." + symbol[4..].ToLowerInvariant().Replace('_', '-'))
        : symbol == "MAP_DYNAMIC" ? PretRocketHideoutRegion.DynamicMapId : PretRegionalServiceMaps.MapIdFromSourceSymbol(symbol);
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown Silph connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSilphRegion Load(string root, bool compact = true)
    {
        var source = new SilphSourcePackage(root, "silph-region", Profile, "content/sources/silph-region.lock.json", LockSha256, 59199, ManifestSha256, 23421,193,140);
        var raw = source.Raw; var files = source.Files; var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outputs = source.Outputs; var directory = source.Directory;
        var outputEntries = source.Manifest.GetProperty("outputs").EnumerateArray().ToDictionary(e => e.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var catalog = source.Document("silph-region-catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash && JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")) &&
            !catalog.GetProperty("runtimeIntegrated").GetBoolean() && catalog.GetProperty("maps").GetArrayLength() == 13, "Silph source inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Saffron approach and12 Silph floors/elevator; application admission remains separate.", Profile, files);
        using var layoutsDocument = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layouts = layoutsDocument.RootElement.GetProperty("layouts").EnumerateArray().Where(x => x.TryGetProperty("id", out _)).ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" &&
                runtimeEntry.GetProperty("sha256").GetString() == outputEntries[relative + "/runtime-map.json"].GetProperty("sha256").GetString(), "Silph runtime certificate differs.");
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
        Need(maps.Count == 13 && maps.Values.Sum(m => m.Width * m.Height) == 10644 && maps.Values.Sum(m => m.FieldWarps.Length) == 84,
            "Silph source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "Silph target warp index differs.");
        var inventory = source.Document("scripts.json"); var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Need(inventory.GetProperty("blocks").GetArrayLength() == 510 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Silph source script closure differs.");
        foreach (var row in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = row.GetString()!; var text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing Silph script label."); scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        // Door scripts use labels from this already authenticated source header.
        foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_labels.h"]),
            @"^#define\s+(METATILE_SilphCo_\w+)\s+0x([0-9A-Fa-f]+)\b", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            constants = constants.Add(match.Groups[1].Value,
                int.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture));
        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("doors").GetArrayLength() == 20 && contracts.GetProperty("pads").GetArrayLength() == 34 &&
            contracts.GetProperty("elevatorFloors").GetArrayLength() == 11 && contracts.GetProperty("pickups").GetArrayLength() == 17 &&
            contracts.GetProperty("hiddenItems").GetArrayLength() == 12, "Silph field source scope differs.");
        foreach (var door in contracts.GetProperty("doors").EnumerateArray())
        {
            var map = maps[new(door.GetProperty("map").GetString()!)]; var label = door.GetProperty("script").GetString();
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Any(e => e.TryGetProperty("script", out var v) && v.GetString() == label), "Silph door lacks source event.");
        }
        foreach (var pad in contracts.GetProperty("pads").EnumerateArray())
        {
            var map = maps[new(pad.GetProperty("map").GetString()!)]; var warp = map.FieldWarps[pad.GetProperty("warpIndex").GetInt32()];
            Need(map.SourceBehaviorAt(warp.X, warp.Y) == 103 && warp.X == pad.GetProperty("x").GetInt32() && warp.Y == pad.GetProperty("y").GetInt32() &&
                warp.DestinationMapId == MapIdFromSourceSymbol(pad.GetProperty("destinationMap").GetString()!) && warp.DestinationWarpId == pad.GetProperty("destinationWarp").GetInt32(), "Silph pad source endpoint differs.");
        }
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
