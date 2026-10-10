using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Fuchsia Gym source map, trainers, invisible walls and badge/TM06 script contracts.</summary>
public sealed class PretFuchsiaGymRegion
{
    public const string Profile = "firered-fuchsia-gym-region-import-v1";
    public const string ManifestSha256 = "1ffb8633d065f50274f0ba5476a38b2c580e380f09770d930b59851bbb5954cf";
    public const string LockSha256 = "7ff43ceca8a8b5a5311d9ab37d34c5d021ef39156903c1ad8ad5cc67afadc9e9";
    public const string CatalogSha256 = "7049816e9f3cf6cbe67d92e444e9f4db4a9631d8da4d938cb9cee79728a9d6de";
    public const string RawManifestSha256 = "0c18f1a68cab543b1fe3cbf2336d011cc2d2e8bae4b4b1efc057d724ec870b03";
    public const string FullSourceHash = "d73da74062e114ff9237e13e7ca695eb74ccda977842d6d3a3f82ebf48611037";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement BadgeRewards => Dependencies.GetProperty("badgeRewards");
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalFuchsiaEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretFuchsiaGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalFuchsiaEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalFuchsiaEvents = externalFuchsiaEvents;
        AdditionalSourceHashes = ["fuchsia-gym-region-manifest:" + ManifestSha256, "fuchsia-gym-region-lock:" + LockSha256,
            .. files.Select(f => "fuchsia-gym-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FUCHSIA_CITY_GYM" => new("map.fuchsia-city-gym"),
        _ => PretFuchsiaRouteRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretFuchsiaGymRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/fuchsia-gym-region");
        var rawDirectory = Path.Combine(project, "content/raw/fuchsia-gym-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var maximum = length ?? 2 * 1024 * 1024;
            Require(maximum is > 0 and <= 2 * 1024 * 1024 && PretRuntimeMapValidation.IsHash(sha), "Fuchsia Gym input bounds/hash differ.");
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), maximum);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned Fuchsia Gym bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/fuchsia-gym-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 95 && m.GetProperty("outputs").GetArrayLength() == 20 &&
            m.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree && m.GetProperty("lockSha256").GetString() == LockSha256 &&
            m.GetProperty("mapCount").GetInt32() == 1 && m.GetProperty("cellCount").GetInt32() == 345 &&
            m.GetProperty("borderCellCount").GetInt32() == 4 && m.GetProperty("scriptBlockCount").GetInt32() == 71 &&
            m.GetProperty("warpCount").GetInt32() == 3 && m.GetProperty("npcGraphicsCount").GetInt32() == 5 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Fuchsia Gym manifest identity differs.");
        var source = sourceLock.RootElement;
        Require(source.GetProperty("schemaVersion").GetInt32() == 1 && source.GetProperty("repository").GetString() == "pret/pokefirered" &&
            source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && source.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree &&
            source.GetProperty("profile").GetString() == Profile && source.GetProperty("gameVersion").GetString() == "FIRERED" &&
            source.GetProperty("revision").GetInt32() == 0 && source.GetProperty("files").GetArrayLength() == 95 &&
            source.GetProperty("maps").GetArrayLength() == 1 && source.GetProperty("maps")[0].GetString() == "FuchsiaCity_Gym" &&
            !source.GetProperty("completeGame").GetBoolean(), "Fuchsia Gym source lock identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "fuchsia-gym-region/" + path, output)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Fuchsia Gym output.");
        }
        Require(Hash(outputs["fuchsia-gym-catalog.json"]) == CatalogSha256, "Fuchsia Gym catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["fuchsia-gym-catalog.json"]);
        Require(catalog.RootElement.GetProperty("schemaVersion").GetInt32() == 1 && catalog.RootElement.GetProperty("profile").GetString() == Profile &&
            catalog.RootElement.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && catalog.RootElement.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.RootElement.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Fuchsia Gym source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Fuchsia Gym Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate Fuchsia Gym source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 95, "Incomplete Fuchsia Gym source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Fuchsia Gym source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Fuchsia Gym source profile; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing Fuchsia Gym document value.");
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
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Fuchsia Gym runtime map.");
                continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                    evidence.Sha256 == byPath[evidence.Path].Sha256, "Fuchsia Gym embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Fuchsia Gym source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Fuchsia Gym events differ from original source.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Fuchsia Gym map stable identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
                SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate Fuchsia Gym map.");
        }
        Require(maps.Count == 1 && maps.Values.Sum(x => x.Width*x.Height) == 345, "Incomplete Fuchsia Gym map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("profile").GetString() == Profile &&
            scriptInventory.RootElement.GetProperty("blocks").GetArrayLength() == 71 &&
            scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Fuchsia Gym script reference closure is incomplete.");
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
                Require(symbol.Length > 0, "Fuchsia Gym script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_BADGE05_GET"] == 0x824 && constants["FLAG_DEFEATED_KOGA"] == 0x4B4 &&
            constants["FLAG_GOT_TM06_FROM_KOGA"] == 0x259 && constants["ITEM_TM06"] == 294,
            "Fuchsia Gym badge/reward constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        var dependency = dependencies.RootElement;
        Require(dependency.GetProperty("schemaVersion").GetInt32() == 1 && dependency.GetProperty("profile").GetString() == Profile &&
            dependency.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && !dependency.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependency.GetProperty("trainers").GetArrayLength() == 7 &&
            dependency.GetProperty("badgeRewards").GetProperty("tm").GetString() == "ITEM_TM06" &&
            dependency.GetProperty("entry").GetProperty("sourceWarpId").GetInt32() == 4 &&
            dependency.GetProperty("entry").GetProperty("destinationWarpId").GetInt32() == 1,
            "Fuchsia Gym dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/FuchsiaCity/map.json"]);
        Require(npcSources.RootElement.GetProperty("schemaVersion").GetInt32() == 1 &&
            npcSources.RootElement.GetProperty("graphics").GetArrayLength() == 5, "Fuchsia Gym NPC inventory differs.");
        var gymEvents = maps[new MapId("map.fuchsia-city-gym")].Events;
        var cityEntry = cityEvents.RootElement.GetProperty("warp_events")[4];
        Require(cityEntry.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY_GYM" && cityEntry.GetProperty("dest_warp_id").GetString() == "1" &&
            JsonElement.DeepEquals(cityEntry, dependency.GetProperty("entry").GetProperty("source")) &&
            JsonElement.DeepEquals(gymEvents.GetProperty("warp_events")[1], dependency.GetProperty("entry").GetProperty("destination")),
            "Fuchsia Gym city entry differs from original source.");
        foreach (var trainer in dependency.GetProperty("trainers").EnumerateArray())
            Require(JsonElement.DeepEquals(trainer.GetProperty("object"), gymEvents.GetProperty("object_events")[trainer.GetProperty("localId").GetInt32() - 1]),
                "Fuchsia Gym trainer object differs from source.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.Width == 15 && data.Height == 23 && data.BorderWidth == 2 && data.BorderHeight == 2 &&
            data.TileSize == 16 && data.TechnicalSpawn is null && data.Events.GetProperty("object_events").GetArrayLength() == 8 &&
            data.Events.GetProperty("warp_events").GetArrayLength() == 3 && data.Events.GetProperty("warp_events").EnumerateArray().All(w =>
                w.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY" && w.GetProperty("dest_warp_id").GetString() == "4"), "Fuchsia Gym map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Fuchsia Gym layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_Building" => "primary/building",
            "gTileset_FuchsiaGym" => "secondary/fuchsia_gym",
            _ => throw new InvalidDataException("Unknown Fuchsia Gym region tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Fuchsia Gym cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Fuchsia Gym block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Fuchsia Gym metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Fuchsia Gym behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Fuchsia Gym metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Fuchsia Gym render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Fuchsia Gym path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Fuchsia Gym directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Fuchsia Gym file symlink is unsupported: " + path);
        }
        return result;
    }
}
