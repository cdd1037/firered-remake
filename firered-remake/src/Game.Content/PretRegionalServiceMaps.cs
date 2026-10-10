using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretRegionalServiceMaps
{
    public const string Profile = "firered-regional-service-maps-import-v1";
    public const string ManifestSha256 = "a2c99c7d70d926cc0f67efdda254e78f58f3ebafe774c5b424cdda7ebcf8f2a3";
    public const string LockSha256 = "3b15d715a755329e7a0a4da5ee2a834af625fc2c1b16f4b2c1a1f3510c163db2";
    public const string FullSourceHash = "70e031f09ed7748f11035e85f45b68efbd739f951f7c5ae290f5fef9efc80101";
    public const string ConverterSha256 = "9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8";
    public static readonly MapId ElevatorMapId = new("map.celadon-city-department-store-elevator");
    /// <summary>All 17 authenticated source maps. Elevator events retain MAP_DYNAMIC / WARP_ID_DYNAMIC unchanged.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    /// <summary>16 maps with static warp records. Still require explicit field and service admission.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-5-south-entrance"), new("map.route-6-north-entrance"), new("map.route-7-east-entrance"), new("map.route-8-west-entrance")];
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

    private PretRegionalServiceMaps(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> sources,
        JsonElement catalog, JsonElement scriptInventory, JsonElement objects, JsonElement npcs,
        JsonElement availability, ImmutableDictionary<MapId, JsonElement> reused)
    {
        GeneratedDirectory = directory; SourceMaps = maps; NewMaps = maps.Remove(ElevatorMapId);
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = sources;
        Catalog = catalog; ScriptInventory = scriptInventory; ObjectTemplates = objects;
        NpcSources = npcs; Availability = availability; ReusedEvents = reused;
        AdditionalSourceHashes = ["regional-service-maps-manifest:" + ManifestSha256,
            "regional-service-maps-lock:" + LockSha256,
            .. sources.Select(s => "regional-service-maps:" + s.Path + ":" + s.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CELADON_CITY_CONDOMINIUMS_1F" => new("map.celadon-city-condominiums-1f"),
        "MAP_CELADON_CITY_CONDOMINIUMS_2F" => new("map.celadon-city-condominiums-2f"),
        "MAP_CELADON_CITY_CONDOMINIUMS_3F" => new("map.celadon-city-condominiums-3f"),
        "MAP_CELADON_CITY_CONDOMINIUMS_ROOF" => new("map.celadon-city-condominiums-roof"),
        "MAP_CELADON_CITY_CONDOMINIUMS_ROOF_ROOM" => new("map.celadon-city-condominiums-roof-room"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_1F" => new("map.celadon-city-department-store-1f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_2F" => new("map.celadon-city-department-store-2f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_3F" => new("map.celadon-city-department-store-3f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_4F" => new("map.celadon-city-department-store-4f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_5F" => new("map.celadon-city-department-store-5f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_ROOF" => new("map.celadon-city-department-store-roof"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_ELEVATOR" => new("map.celadon-city-department-store-elevator"),
        "MAP_CELADON_CITY_GAME_CORNER_PRIZE_ROOM" => new("map.celadon-city-game-corner-prize-room"),
        "MAP_CELADON_CITY_HOTEL" => new("map.celadon-city-hotel"),
        "MAP_CELADON_CITY_HOUSE1" => new("map.celadon-city-house-1"),
        "MAP_CELADON_CITY_RESTAURANT" => new("map.celadon-city-restaurant"),
        "MAP_LAVENDER_TOWN_HOUSE2" => new("map.lavender-town-house-2"),
        "MAP_ROUTE5_SOUTH_ENTRANCE" => new("map.route-5-south-entrance"),
        "MAP_ROUTE6_NORTH_ENTRANCE" => new("map.route-6-north-entrance"),
        "MAP_ROUTE7_EAST_ENTRANCE" => new("map.route-7-east-entrance"),
        "MAP_ROUTE8_WEST_ENTRANCE" => new("map.route-8-west-entrance"),
        _ => PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Checks the existing gate instances before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.TryGetValue(id, out var map), "Existing gate is missing: " + id.Value);
            Need(map!.Metadata.FullSourceHash == ReusedIdentity(id).Source &&
                JsonElement.DeepEquals(map.Events, ReusedEvents[id]), "Existing gate identity differs: " + id.Value);
        }
    }

    /// <summary>Default compact-file loading does not require any rich world.json. The adapter expands cells for existing Content APIs.</summary>
    public static PretRegionalServiceMaps Load(string projectRoot, bool compact = true)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/regional-service-maps");
        var rawDirectory = Path.Combine(project, "content/raw/regional-service-maps");
        byte[] Pinned(string root, string path, string sha, int? size = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(Safe(root, path), size ?? 2 * 1024 * 1024);
            Need(Hash(bytes) == sha && (size is null || bytes.Length == size), "Pinned regional service file differs: " + path);
            return bytes;
        }
        using var manifestDocument = JsonDocument.Parse(Pinned(directory, "manifest.json", ManifestSha256));
        using var lockDocument = JsonDocument.Parse(Pinned(project, "content/sources/regional-service-maps.lock.json", LockSha256));
        var manifest = manifestDocument.RootElement; var sourceLock = lockDocument.RootElement;
        Need(manifest.GetProperty("profile").GetString() == Profile && manifest.GetProperty("schemaVersion").GetInt32() == 1 &&
            manifest.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && manifest.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            manifest.GetProperty("inputCount").GetInt32() == 357 && manifest.GetProperty("mapCount").GetInt32() == 17 &&
            manifest.GetProperty("reusedMapCount").GetInt32() == 4 && manifest.GetProperty("cellCount").GetInt32() == 3241 &&
            manifest.GetProperty("outputs").GetArrayLength() == 206 && manifest.GetProperty("unresolvedLabelCount").GetInt32() == 0,
            "Regional service manifest scope differs.");
        var outputEntries = manifest.GetProperty("outputs").EnumerateArray().ToDictionary(x => x.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, entry) in outputEntries)
        {
            _ = Safe(directory, path);
            if (compact && path.EndsWith("/world.json", StringComparison.Ordinal)) continue;
            outputs.Add(path, Pinned(directory, path, entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32()));
        }
        JsonElement Document(string path) { using var doc = JsonDocument.Parse(outputs[path]); return doc.RootElement.Clone(); }
        var catalog = Document("regional-service-maps-catalog.json");
        Need(JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), sourceLock.GetProperty("files")) &&
            !catalog.GetProperty("runtimeIntegrated").GetBoolean(), "Regional source inventory/availability differs.");
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
        Need(raw.Count == 357 && Hash(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash,
            "Source-set inventory differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "17 source maps; four unchanged reused gates; services require explicit admission.", Profile, files);
        var runtimeCatalog = Document("runtime-catalog.json");
        Need(runtimeCatalog.GetProperty("converterSha256").GetString() == ConverterSha256 && runtimeCatalog.GetProperty("converterVersion").GetString() == PretRuntimeMapCatalog.ConverterVersion &&
            runtimeCatalog.GetProperty("maps").GetArrayLength() == 17, "Regional runtime converter differs.");
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
            maps.Add(new(stableId), PretWorldMap.FromAuthenticatedExtension(world, Safe(directory, relative), Safe(directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 17 && maps.Values.Sum(m => m.Width * m.Height) == 3241 && maps.Values.Sum(m => m.BorderWidth * m.BorderHeight) == 68, "Source map collection differs.");
        foreach (var map in maps.Values.Where(m => m.Id != ElevatorMapId))
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target))
                    Need(warp.DestinationWarpId < target.Events.GetProperty("warp_events").GetArrayLength(), "Loaded warp index differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var inventory = Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 442 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Script closure differs.");
        foreach (var property in inventory.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name; var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Missing script source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        var constants = Document("constants.json").GetProperty("values").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["ITEM_TEA"] == 369 && constants["FLAG_GOT_TEA"] == 678 && constants["FLAG_GOT_EEVEE"] == 611 && constants["LOCALID_EEVEE_POKEBALL"] == 2 && constants["SPECIES_EEVEE"] == 133, "Tea/Eevee source constants differ.");
        var reused = ImmutableDictionary.CreateBuilder<MapId, JsonElement>();
        foreach (var spec in catalog.GetProperty("reusedMaps").EnumerateArray())
        {
            var expectedIdentity = ReusedIdentity(new(spec.GetProperty("stableId").GetString()!));
            Need(spec.GetProperty("manifestSha256").GetString() == expectedIdentity.Manifest && spec.GetProperty("fullSourceHash").GetString() == expectedIdentity.Source && !spec.GetProperty("regenerate").GetBoolean(), "Reused source ownership differs.");
            var path = spec.GetProperty("sourceRef").GetProperty("path").GetString()!;
            using var events = JsonDocument.Parse(raw[path]); reused.Add(new(spec.GetProperty("stableId").GetString()!), events.RootElement.Clone());
        }
        Need(reused.Count == 4 && !maps.Keys.Any(reused.ContainsKey), "Reused maps must not be regenerated.");
        var availability = Document("availability.json");
        Need(!availability.GetProperty("runtimeIntegrated").GetBoolean() && !availability.GetProperty("completeGame").GetBoolean() && availability.GetProperty("enabledServices").GetArrayLength() == 0, "Source import cannot enable services.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants, files, catalog, inventory,
            Document("objects.json"), Document("npc-source.json"), availability, reused.ToImmutable());
    }
    private static (string Manifest, string Source) ReusedIdentity(MapId id) => id.Value switch
    {
        "map.route-5-south-entrance" or "map.route-6-north-entrance" => (PretVermilionRegion.ManifestSha256, PretVermilionRegion.FullSourceHash),
        "map.route-7-east-entrance" or "map.route-8-west-entrance" => (PretCeladonApproachRegion.ManifestSha256, PretCeladonApproachRegion.FullSourceHash),
        _ => throw new InvalidDataException("Unknown reused gate identity.")
    };
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Regional source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Regional source file symlink rejected.");
        }
        return result;
    }
}
