using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Four authenticated source maps. Loading is not admission of scripts or unavailable road content.</summary>
public sealed class PretSnorlaxRegion
{
    public const string Profile = "firered-snorlax-region-import-v1";
    public const string ManifestSha256 = "84560ac286661008a9fe28be0d58d42065583e69671b5281fbf5b73c6e477d42";
    public const string LockSha256 = "3c2a69e3c26854af0924b50da06d236a4c1d3a7fc3e0bcb8ae0d45625b838cd1";
    public const string CatalogSha256 = "73be9e1f39eb703040063101db97ab7c3effeeb5e794b693fd8ee20496994683";
    public const string RawManifestSha256 = "ddf8ba0bbdbebf003335f707e087cd9349944881d47e1d393c9df09ec31a6485";
    public const string FullSourceHash = "9cfcd4821eec1a5fef64e2ee420db1ce79165d02d98e31d58837790c27b60de0";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Boundaries { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSnorlaxRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> files,
        JsonElement boundaries, JsonElement catalog, JsonElement inventory, JsonElement npcs)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Boundaries = boundaries; Catalog = catalog;
        ScriptInventory = inventory; NpcSources = npcs;
        AdditionalSourceHashes = ["snorlax-region-manifest:" + ManifestSha256, "snorlax-region-lock:" + LockSha256,
            .. files.Select(f => "snorlax-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE12" => new("map.route-12"),
        "MAP_ROUTE12_NORTH_ENTRANCE_1F" => new("map.route-12-north-entrance-1f"),
        "MAP_ROUTE12_NORTH_ENTRANCE_2F" => new("map.route-12-north-entrance-2f"),
        "MAP_ROUTE12_FISHING_HOUSE" => new("map.route-12-fishing-house"),
        "MAP_ROUTE13" => new("map.route-13"),
        "MAP_ROUTE16" => new("map.route-16"),
        "MAP_ROUTE16_NORTH_ENTRANCE_1F" => new("map.route-16-north-entrance-1f"),
        "MAP_ROUTE16_NORTH_ENTRANCE_2F" => new("map.route-16-north-entrance-2f"),
        "MAP_ROUTE16_HOUSE" => new("map.route-16-house"),
        "MAP_ROUTE17" => new("map.route-17"),
        _ => PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Retains external source connections. Availability must be checked before committing a transition.</summary>
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

    /// <summary>Pure preflight. This never changes source collision or substitutes for ordinary movement checks.</summary>
    public bool AllowsFootDestination(MapId map, int x, int y) => Boundaries.GetProperty("maps").EnumerateArray()
        .Where(m => m.GetProperty("map").GetString() == map.Value)
        .SelectMany(m => m.GetProperty("admittedFootRows").EnumerateArray())
        .Any(r => r.GetProperty("y").GetInt32() == y && r.GetProperty("x").EnumerateArray().Any(v => v.GetInt32() == x));

    public bool AllowsWarp(MapId map, int sourceWarpId)
    {
        if (!NewMaps.TryGetValue(map, out var source) || sourceWarpId < 0 || sourceWarpId >= source.Events.GetProperty("warp_events").GetArrayLength()) return false;
        var w = source.Events.GetProperty("warp_events")[sourceWarpId];
        var b = Boundaries.GetProperty("maps").EnumerateArray().Single(m => m.GetProperty("map").GetString() == map.Value);
        return !b.GetProperty("rejectedWarps").EnumerateArray().Any(r => r.GetProperty("index").GetInt32() == sourceWarpId) &&
            AllowsFootDestination(map, w.GetProperty("x").GetInt32(), w.GetProperty("y").GetInt32());
    }

    public static PretSnorlaxRegion Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/snorlax-region");
        var rawDirectory = Path.Combine(project, "content/raw/snorlax-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned Snorlax region bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "content/sources/snorlax-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 217 && m.GetProperty("outputs").GetArrayLength() == 53 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Snorlax region identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32())), "Duplicate Snorlax output.");
        }
        Require(Hash(outputs["snorlax-catalog.json"]) == CatalogSha256, "Snorlax catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["snorlax-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")), "Source inventory differs from fixed lock.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!; var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!; var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length); var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob && raw.TryAdd(path, bytes), "Source blob differs/duplicates: " + path);
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 217, "Incomplete Snorlax region sources.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Snorlax source set differs.");
        var inventory = files.ToImmutable(); var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Bounded source import; application and native acceptance remain separate.", Profile, inventory);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing document value.");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]); var w = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(w.GetProperty("embeddedSource"));
            foreach (var e in embedded) Require(raw.TryGetValue(e.Path, out var bytes) && Convert.FromBase64String(e.Base64).AsSpan().SequenceEqual(bytes) && e.Sha256 == byPath[e.Path].Sha256, "Embedded source differs: " + e.Path);
            var sourceRef = Decode<SourceRef>(w.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Map source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(w.GetProperty("events"), events.RootElement), "Original map objects/events changed.");
            var data = new PretWorldDocument(w.GetProperty("schemaVersion").GetInt32(), w.GetProperty("stableId").GetString()!, sourceRef, metadata,
                w.GetProperty("width").GetInt32(), w.GetProperty("height").GetInt32(), w.GetProperty("tileSize").GetInt32(), w.GetProperty("borderWidth").GetInt32(), w.GetProperty("borderHeight").GetInt32(), w.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(w.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(w.GetProperty("cells")), Decode<ImmutableArray<PretWorldCell>>(w.GetProperty("borderCells")), embedded, null, w.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Map stable ID differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative), SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate map.");
        }
        Require(maps.Count == 4 && maps.Values.Sum(x => x.Width * x.Height) == 4217 && maps.Values.Sum(x => x.Events.GetProperty("object_events").GetArrayLength()) == 27, "Four-map scope differs.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0 && scriptInventory.RootElement.GetProperty("blocks").GetArrayLength() == 206, "Incomplete script closure.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name; var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "Script lacks source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]); var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var p in constantDocument.RootElement.GetProperty("values").EnumerateObject()) constants.Add(p.Name, p.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_HIDE_ROUTE_12_SNORLAX"] == 84 && constants["FLAG_HIDE_ROUTE_16_SNORLAX"] == 128 && constants["FLAG_WOKE_UP_ROUTE_12_SNORLAX"] == 595 && constants["FLAG_SYS_SPECIAL_WILD_BATTLE"] == 2055 && constants["CRY_MODE_ENCOUNTER"] == 2, "Snorlax constants differ.");
        using var boundaries = JsonDocument.Parse(outputs["boundaries.json"]); using var npcs = JsonDocument.Parse(outputs["npc-source.json"]);
        Require(!boundaries.RootElement.GetProperty("runtimeIntegrated").GetBoolean() && boundaries.RootElement.GetProperty("maps").GetArrayLength() == 4, "Boundary scope differs.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(), inventory,
            boundaries.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(), npcs.RootElement.Clone());
    }
    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.TileSize == 16 && data.TechnicalSpawn is null, "Snorlax region map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Snorlax region layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_General" => "primary/general",
            "gTileset_LavenderTown" => "secondary/lavender_town",
            "gTileset_CeladonCity" => "secondary/celadon_city",
            "gTileset_Building" => "primary/building",
            "gTileset_GenericBuilding2" => "secondary/generic_building_2",
            _ => throw new InvalidDataException("Unknown Snorlax region region tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Snorlax region cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Snorlax region block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Snorlax region metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Snorlax region behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Snorlax region metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Snorlax region render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Snorlax region path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Snorlax region directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Snorlax region file symlink is unsupported: " + path);
        }
        return result;
    }
}
