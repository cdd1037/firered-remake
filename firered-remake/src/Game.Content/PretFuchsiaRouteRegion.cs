using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretFuchsiaRouteRegion
{
    public const string Profile = "firered-fuchsia-route-region-import-v1";
    public const string ManifestSha256 = "0dfe16640308b507cfeb0ee4e9dd6082fcbc7ac2f2c07da2ab83614c0a6bd152";
    public const string LockSha256 = "aedbfd9c715a6c3cad72608e00d8a04f284c2366556374a56e50195722b1ded6";
    public const string FullSourceHash = "6240ed767915bbe8a8f9f6477f6cb2b3bda0d4774325b3e412ed3d01550973ed";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string RawManifestSha256 = "d7500f01d6a88fe1955163d5d426d78836b5879bb2c6a06384e896990251987b";
    public const string ConverterSha256 = "9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8";
    public static readonly MapId CenterMapId = new("map.fuchsia-city-pokemon-center-1f");
    public const string HealingLocation = "HEAL_LOCATION_FUCHSIA_CITY";
    /// <summary>Six complete authenticated maps. Loading alone admits no field action or service.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps => SourceMaps;
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-12")];
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Catalog { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Availability { get; }
    public ImmutableDictionary<MapId, JsonElement> ReusedEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretFuchsiaRouteRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> sources,
        JsonElement catalog, JsonElement scriptInventory, JsonElement objects, JsonElement npcs,
        JsonElement availability, ImmutableDictionary<MapId, JsonElement> reused)
    {
        GeneratedDirectory = directory; SourceMaps = maps;
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = sources;
        Catalog = catalog; ScriptInventory = scriptInventory; ObjectTemplates = objects;
        NpcSources = npcs; Availability = availability; ReusedEvents = reused;
        AdditionalSourceHashes = ["fuchsia-route-region-manifest:" + ManifestSha256,
            "fuchsia-route-region-lock:" + LockSha256,
            .. sources.Select(s => "fuchsia-route-region:" + s.Path + ":" + s.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE13" => new("map.route-13"),
        "MAP_ROUTE14" => new("map.route-14"),
        "MAP_ROUTE15" => new("map.route-15"),
        "MAP_ROUTE15_WEST_ENTRANCE_1F" => new("map.route-15-west-entrance-1f"),
        "MAP_ROUTE15_WEST_ENTRANCE_2F" => new("map.route-15-west-entrance-2f"),
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        "MAP_FUCHSIA_CITY_POKEMON_CENTER_1F" => new("map.fuchsia-city-pokemon-center-1f"),
        "MAP_FUCHSIA_CITY_POKEMON_CENTER_2F" => new("map.fuchsia-city-pokemon-center-2f"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE" => new("map.fuchsia-city-safari-zone-entrance"),
        "MAP_FUCHSIA_CITY_WARDENS_HOUSE" => new("map.fuchsia-city-wardens-house"),
        "MAP_FUCHSIA_CITY_MART" => new("map.fuchsia-city-mart"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_OFFICE" => new("map.fuchsia-city-safari-zone-office"),
        "MAP_FUCHSIA_CITY_GYM" => new("map.fuchsia-city-gym"),
        "MAP_FUCHSIA_CITY_HOUSE1" => new("map.fuchsia-city-house-1"),
        "MAP_FUCHSIA_CITY_HOUSE2" => new("map.fuchsia-city-house-2"),
        "MAP_FUCHSIA_CITY_HOUSE3" => new("map.fuchsia-city-house-3"),
        "MAP_ROUTE18" => new("map.route-18"),
        "MAP_ROUTE19" => new("map.route-19"),
        _ => PretSnorlaxRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Keep source connections, including unavailable neighbors, so callers can reject before mutation.</summary>
    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unknown Fuchsia route connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Only a source-package boundary check; not an admission of interactions, trainer approaches or encounters.</summary>
    public bool ContainsDestination(MapId destination) => SourceMaps.ContainsKey(destination) || ReusedMapIds.Contains(destination);
    public bool AllowsNewMapWarp(MapId source, int index)
    {
        if (!SourceMaps.TryGetValue(source, out var map) || index < 0 || index >= map.FieldWarps.Length) return false;
        return ContainsDestination(map.FieldWarps[index].DestinationMapId);
    }
    public JsonElement Route12Events => ReusedEvents[new("map.route-12")];

    /// <summary>Checks the existing Route12 instance before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.TryGetValue(id, out var map), "Existing Route12 is missing: " + id.Value);
            Need(map!.Metadata.FullSourceHash == ReusedIdentity(id).Source &&
                JsonElement.DeepEquals(map.Events, ReusedEvents[id]), "Existing Route12 identity differs: " + id.Value);
        }
    }

    /// <summary>Default compact-file loading does not require any rich world.json. The compact adapter retains packed blocks and expands legacy cells only on demand.</summary>
    public static PretFuchsiaRouteRegion Load(string projectRoot, bool compact = true)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/fuchsia-route-region");
        var rawDirectory = Path.Combine(project, "content/raw/fuchsia-route-region");
        byte[] Pinned(string root, string path, string sha, int? size = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(Safe(root, path), size ?? 2 * 1024 * 1024);
            Need(Hash(bytes) == sha && (size is null || bytes.Length == size), "Pinned Fuchsia route file differs: " + path);
            return bytes;
        }
        using var manifestDocument = JsonDocument.Parse(Pinned(directory, "manifest.json", ManifestSha256));
        using var lockDocument = JsonDocument.Parse(Pinned(project, "content/sources/fuchsia-route-region.lock.json", LockSha256));
        using var rawManifestDocument = JsonDocument.Parse(Pinned(rawDirectory, "manifest.json", RawManifestSha256));
        var rawManifest = rawManifestDocument.RootElement;
        Need(rawManifest.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && rawManifest.GetProperty("tree").GetString() == SourceTree &&
            rawManifest.GetProperty("inputCount").GetInt32() == 208 && rawManifest.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            rawManifest.GetProperty("lockSha256").GetString() == LockSha256, "Fuchsia raw receipt differs.");
        var manifest = manifestDocument.RootElement; var sourceLock = lockDocument.RootElement;
        Need(sourceLock.GetProperty("profile").GetString() == Profile && sourceLock.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            sourceLock.GetProperty("tree").GetString() == SourceTree && sourceLock.GetProperty("files").GetArrayLength() == 208 &&
            manifest.GetProperty("lockSha256").GetString() == LockSha256 && manifest.GetProperty("sourceTree").GetString() == SourceTree,
            "Fuchsia source-lock identity differs.");
        Need(manifest.GetProperty("profile").GetString() == Profile && manifest.GetProperty("schemaVersion").GetInt32() == 1 &&
            manifest.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && manifest.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            manifest.GetProperty("inputCount").GetInt32() == 208 && manifest.GetProperty("mapCount").GetInt32() == 6 &&
            manifest.GetProperty("reusedMapCount").GetInt32() == 1 && manifest.GetProperty("cellCount").GetInt32() == 6546 &&
            manifest.GetProperty("outputs").GetArrayLength() == 98 && manifest.GetProperty("unresolvedLabelCount").GetInt32() == 0,
            "Fuchsia route manifest scope differs.");
        var outputEntries = manifest.GetProperty("outputs").EnumerateArray().ToDictionary(x => x.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, entry) in outputEntries)
        {
            _ = Safe(directory, path);
            if (compact && path.EndsWith("/world.json", StringComparison.Ordinal)) continue;
            outputs.Add(path, Pinned(directory, path, entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32()));
        }
        JsonElement Document(string path) { using var doc = JsonDocument.Parse(outputs[path]); return doc.RootElement.Clone(); }
        var catalog = Document("fuchsia-route-region-catalog.json");
        Need(JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), sourceLock.GetProperty("files")) &&
            !catalog.GetProperty("runtimeIntegrated").GetBoolean(), "Fuchsia source inventory/availability differs.");
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var sources = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in sourceLock.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!; var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!; var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = Pinned(rawDirectory, path, sha, length);
            Need(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == blob, "Git blob differs: " + path);
            raw.Add(path, bytes); sources.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var files = sources.ToImmutable(); var byPath = files.ToDictionary(s => s.Path, StringComparer.Ordinal);
        Need(raw.Count == 208 && Hash(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash,
            "Source-set inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Six source maps; unchanged Route12 reuse; services require explicit admission.", Profile, files);
        var runtimeCatalog = Document("runtime-catalog.json");
        Need(runtimeCatalog.GetProperty("converterSha256").GetString() == ConverterSha256 && runtimeCatalog.GetProperty("converterVersion").GetString() == PretRuntimeMapCatalog.ConverterVersion &&
            runtimeCatalog.GetProperty("maps").GetArrayLength() == 6, "Fuchsia runtime converter differs.");
        var runtimeEntries = runtimeCatalog.GetProperty("maps").EnumerateArray().ToDictionary(e => e.GetProperty("stableId").GetString()!, StringComparer.Ordinal);
        using var layoutsDocument = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layouts = layoutsDocument.RootElement.GetProperty("layouts").EnumerateArray().Where(x => x.TryGetProperty("id", out _)).ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = runtimeEntries[stableId];
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" &&
                runtimeEntry.GetProperty("sourcePath").GetString() == relative + "/world.json" &&
                runtimeEntry.GetProperty("sourceWorldSha256").GetString() == outputEntries[relative + "/world.json"].GetProperty("sha256").GetString() &&
                runtimeEntry.GetProperty("sha256").GetString() == outputEntries[relative + "/runtime-map.json"].GetProperty("sha256").GetString(), "Runtime source certificate differs.");
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
            if (!compact)
            {
                using var richDocument = JsonDocument.Parse(outputs[relative + "/world.json"]); var rich = richDocument.RootElement;
                Need(JsonElement.DeepEquals(rich.GetProperty("events"), data.Events) &&
                    PretRuntimeMapValidation.Decode<ImmutableArray<PretWorldCell>>(Encoding.UTF8.GetBytes(rich.GetProperty("cells").GetRawText())).SequenceEqual(cells) &&
                    PretRuntimeMapValidation.Decode<ImmutableArray<PretWorldCell>>(Encoding.UTF8.GetBytes(rich.GetProperty("borderCells").GetRawText())).SequenceEqual(border), "Rich/compact cell parity differs.");
            }
            if (compact)
            {
                var runtime = PretRuntimeMapData.Decode(outputs[relative + "/runtime-map.json"], new(stableId,
                    relative + "/runtime-map.json", outputs[relative + "/runtime-map.json"].Length,
                    runtimeEntry.GetProperty("sha256").GetString()!, relative + "/world.json",
                    outputEntries[relative + "/world.json"].GetProperty("byteLength").GetInt32(),
                    data.SourceWorldSha256, FullSourceHash));
                maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), Safe(directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
            }
            else maps.Add(new(stableId), PretWorldMap.FromAuthenticatedExtension(world, Safe(directory, relative), Safe(directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 6 && maps.Values.Sum(m => m.Width * m.Height) == 6546 && maps.Values.Sum(m => m.BorderWidth * m.BorderHeight) == 24, "Source map collection differs.");
        foreach (var map in maps.Values)
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target))
                    Need(warp.DestinationWarpId < target.Events.GetProperty("warp_events").GetArrayLength(), "Loaded warp index differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var inventory = Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 451 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Script closure differs.");
        foreach (var property in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            var path = property.GetString()!; var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing script source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        var constants = Document("constants.json").GetProperty("values").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants[HealingLocation] == 8 && constants["LOCALID_FUCHSIA_NURSE"] == 1 && constants["LOCALID_ROUTE14_BORDER_TREE"] == 13 && constants["SPECIES_KABUTO"] == 140 && constants["SPECIES_OMANYTE"] == 138, "Fuchsia source constants differ.");
        var reused = ImmutableDictionary.CreateBuilder<MapId, JsonElement>();
        foreach (var spec in catalog.GetProperty("reusedMaps").EnumerateArray())
        {
            var expectedIdentity = ReusedIdentity(new(spec.GetProperty("stableId").GetString()!));
            Need(spec.GetProperty("manifestSha256").GetString() == expectedIdentity.Manifest && spec.GetProperty("fullSourceHash").GetString() == expectedIdentity.Source && !spec.GetProperty("regenerate").GetBoolean(), "Reused source ownership differs.");
            var path = spec.GetProperty("sourceRef").GetProperty("path").GetString()!;
            using var events = JsonDocument.Parse(raw[path]); reused.Add(new(spec.GetProperty("stableId").GetString()!), events.RootElement.Clone());
        }
        Need(reused.Count == 1 && !maps.Keys.Any(reused.ContainsKey), "Reused maps must not be regenerated.");
        var availability = Document("availability.json");
        Need(!availability.GetProperty("runtimeIntegrated").GetBoolean() && !availability.GetProperty("completeGame").GetBoolean() && availability.GetProperty("enabledServices").GetArrayLength() == 0, "Source import cannot enable services.");
        var center = maps[CenterMapId];
        var nurse = center.Events.GetProperty("object_events")[0];
        Need(nurse.GetProperty("local_id").GetString() == "LOCALID_FUCHSIA_NURSE" && nurse.GetProperty("x").GetInt32() == 7 &&
            nurse.GetProperty("y").GetInt32() == 2 && nurse.GetProperty("script").GetString() == "FuchsiaCity_PokemonCenter_1F_EventScript_Nurse",
            "Fuchsia nurse declaration differs.");
        var fuchsia = maps[new("map.fuchsia-city")];
        Need(fuchsia.Warps[6].DestinationMapId == CenterMapId && fuchsia.Warps[6].DestinationWarpId == 1 &&
            fuchsia.Warps[6].X == 25 && fuchsia.Warps[6].Y == 31 && center.Warps[1].DestinationWarpId == 6,
            "Fuchsia Center entrance differs.");
        Need(center.GetSourceCell(6, 8)!.BehaviorId == 0 && center.GetSourceCell(7, 8)!.BehaviorId == 0x65 &&
            center.GetSourceCell(8, 8)!.BehaviorId == 0, "Center extra warp records must not become wider exits.");
        var clone = maps[new("map.route-15")].Events.GetProperty("object_events")[13];
        var borderTree = maps[new("map.route-14")].Events.GetProperty("object_events")[12];
        Need(clone.GetProperty("type").GetString() == "clone" && clone.GetProperty("x").GetInt32() == 73 && clone.GetProperty("y").GetInt32() == 7 &&
            clone.GetProperty("target_map").GetString() == "MAP_ROUTE14" && clone.GetProperty("target_local_id").GetString() == "LOCALID_ROUTE14_BORDER_TREE" &&
            borderTree.GetProperty("local_id").GetString() == "LOCALID_ROUTE14_BORDER_TREE" && borderTree.GetProperty("x").GetInt32() == 1 && borderTree.GetProperty("y").GetInt32() == 47,
            "Route15 cross-map tree clone differs.");
        var npcs = Document("npc-source.json");
        var dynamic = npcs.GetProperty("dynamicGraphics");
        Need(dynamic.GetArrayLength() == 1 && dynamic[0].GetProperty("sourceGraphics").GetString() == "OBJ_EVENT_GFX_VAR_0" &&
            dynamic[0].GetProperty("whenSet").GetString() == "OBJ_EVENT_GFX_OMANYTE" && dynamic[0].GetProperty("whenUnset").GetString() == "OBJ_EVENT_GFX_KABUTO" &&
            fuchsia.Events.GetProperty("object_events")[8].GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_VAR_0",
            "Fuchsia fossil graphics branch differs.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants, files, catalog, inventory,
            Document("objects.json"), npcs, availability, reused.ToImmutable());
    }
    private static (string Manifest, string Source) ReusedIdentity(MapId id) => id.Value == "map.route-12"
        ? (PretSnorlaxRegion.ManifestSha256, PretSnorlaxRegion.FullSourceHash)
        : throw new InvalidDataException("Unknown reused Fuchsia route identity.");
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Fuchsia source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Fuchsia source file symlink rejected.");
        }
        return result;
    }
}
