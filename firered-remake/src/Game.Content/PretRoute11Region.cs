using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// Authenticated Route11, Digletts Cave, east Route2 and aides source contracts.
/// Registers no gameplay handlers. Runtime and native certification remain separate.
/// </summary>
public sealed class PretRoute11Region
{
    public const string Profile = "firered-route11-region-import-v1";
    public const string ManifestSha256 = "69bcb11e2647356e22c6a0e295b4a605d1468d1d81d430ad805b3359334eaded";
    public const string LockSha256 = "6fcb8bd7fc9402db22376e642252b4fad9fcfed927aeec48db5d2d4b669b4304";
    public const string CatalogSha256 = "ef5887516f7103da508a5ffbd9b28d2ebe104af6ae1ebb180d44774564edc693";
    public const string RawManifestSha256 = "3650520b057e94ba07da2fb3ca4c83d35b451418b34b1d46ab6bba72fc44ddb1";
    public const string FullSourceHash = "e85f9c8575479f09000b712a953e86203f62e64dfd844725c2fb349b3347c77f";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Aides => Dependencies.GetProperty("aides");
    public JsonElement CutTrees => Dependencies.GetProperty("cutTrees");
    public JsonElement Items => Dependencies.GetProperty("items");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-2")];
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalRoute2Events { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretRoute11Region(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalRoute2Events)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalRoute2Events = externalRoute2Events;
        AdditionalSourceHashes = ["route11-region-manifest:" + ManifestSha256, "route11-region-lock:" + LockSha256,
            .. files.Select(f => "route11-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE11" => new("map.route-11"),
        "MAP_DIGLETTS_CAVE_SOUTH_ENTRANCE" => new("map.digletts-cave-south-entrance"),
        "MAP_DIGLETTS_CAVE_B1F" => new("map.digletts-cave-b1f"),
        "MAP_DIGLETTS_CAVE_NORTH_ENTRANCE" => new("map.digletts-cave-north-entrance"),
        "MAP_ROUTE2_EAST_BUILDING" => new("map.route-2-east-building"),
        "MAP_ROUTE2_HOUSE" => new("map.route-2-house"),
        "MAP_ROUTE11_EAST_ENTRANCE_1F" => new("map.route-11-east-entrance-1f"),
        "MAP_ROUTE11_EAST_ENTRANCE_2F" => new("map.route-11-east-entrance-2f"),
        "MAP_ROUTE12" => new("map.route-12"),
        _ => PretSsAnneRegion.MapIdFromSourceSymbol(symbol)
    };

    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretRoute11Region Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/route11-region");
        var rawDirectory = Path.Combine(project, "content/raw/route11-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned Route11/Cave bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "content/sources/route11-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 256 && m.GetProperty("outputs").GetArrayLength() == 96 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Route11/Cave manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "route11-region/" + path, output)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Route11/Cave output.");
        }
        Require(Hash(outputs["route11-catalog.json"]) == CatalogSha256, "Route11/Cave catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["route11-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Route11/Cave source inventory differs from fixed lock.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Route11/Cave Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate Route11/Cave source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 256, "Incomplete Route11/Cave source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Route11/Cave source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Route11/Cave source profile; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing Route11/Cave document value.");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact) && compact.FullSourceHash == FullSourceHash)
            {
                PretRuntimeMapIntegration.ValidateSource(compact, metadata, raw, spec);
                ValidateMap(compact.ToWorldDocument(metadata), raw, byPath);
                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Route11/Cave runtime map.");
                continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                    evidence.Sha256 == byPath[evidence.Path].Sha256, "Route11/Cave embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Route11/Cave source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Route11/Cave events differ from original source.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Route11/Cave map stable identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
                SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate Route11/Cave map.");
        }
        Require(maps.Count == 8 && maps.Values.Sum(x => x.Width*x.Height) == 8978, "Incomplete Route11/Cave map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Route11/Cave script reference closure is incomplete.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name;
            var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "Route11/Cave script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_GOT_HM05"] > 0 && constants["FLAG_GOT_ITEMFINDER"] > 0 &&
            constants["REQUIRED_SEEN_MONS"] == 10 && constants["REQUIRED_CAUGHT_MONS"] == 30,
            "Route11/Cave aide thresholds differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 10 &&
            dependencies.RootElement.GetProperty("pickups").GetArrayLength() == 6 &&
            dependencies.RootElement.GetProperty("aides").GetArrayLength() == 2 &&
            dependencies.RootElement.GetProperty("blockedMaps")[0].GetString() == "MAP_ROUTE12",
            "Route11/Cave dependency scope differs.");
        // Existing Forest owns Route2: authenticate its certificate and preserve the original map identity.
        var reused = catalog.RootElement.GetProperty("reusedMaps");
        Require(reused.GetArrayLength() == 1 && reused[0].GetProperty("stableId").GetString() == "map.route-2" &&
            reused[0].GetProperty("manifestSha256").GetString() == PretForestRegion.ManifestSha256 &&
            reused[0].GetProperty("fullSourceHash").GetString() == PretForestRegion.FullSourceHash,
            "Route11/Cave reused Route2 certificate differs.");
        var forestDirectory = Path.Combine(project, "content/generated/forest-region");
        _ = ReadPinned(forestDirectory, "manifest.json", PretForestRegion.ManifestSha256);
        foreach (var output in reused[0].GetProperty("outputs").EnumerateArray())
        {
            var relative = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "forest-region/" + relative, output)) continue;
            var bytes = ReadPinned(forestDirectory, relative, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
            if (relative == "route-2/world.json")
            {
                using var world = JsonDocument.Parse(bytes);
                using var original = JsonDocument.Parse(raw["data/maps/Route2/map.json"]);
                Require(JsonElement.DeepEquals(world.RootElement.GetProperty("events"), original.RootElement), "Reused Route2 events differ.");
            }
        }
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/Route2/map.json"]);
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.TileSize == 16 && data.TechnicalSpawn is null, "Route11/Cave map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Route11/Cave layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_General" => "primary/general",
            "gTileset_VermilionCity" => "secondary/vermilion_city",
            "gTileset_DiglettsCave" => "secondary/digletts_cave",
            "gTileset_Building" => "primary/building",
            "gTileset_GenericBuilding2" => "secondary/generic_building_2",
            _ => throw new InvalidDataException("Unknown Route11/Cave region tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Route11/Cave cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Route11/Cave block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Route11/Cave metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Route11/Cave behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Route11/Cave metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Route11/Cave render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Route11/Cave path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route11/Cave directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route11/Cave file symlink is unsupported: " + path);
        }
        return result;
    }
}
