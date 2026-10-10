using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Celadon Gym source map, trainers, Cut trees and badge/TM19 script contracts.</summary>
public sealed class PretCeladonGymRegion
{
    public const string Profile = "firered-celadon-gym-region-import-v1";
    public const string ManifestSha256 = "91987e76510d0499fb043ab05abeac562d569911df0ec9c5375324cb74a97f76";
    public const string LockSha256 = "0237b9b50415fd60f6eb9fd4c7ba6adbababdfcddc085d8147c4d767dcf04be5";
    public const string CatalogSha256 = "bc9673850cf53f0ad648f5e053d23ff2ea9ece74f2b9afa195bb61cb23361087";
    public const string RawManifestSha256 = "88203dba57bb079d7b672e05506c60771bbbe53eb87e19b37b7eb23891e4aff3";
    public const string FullSourceHash = "ad4d0ea8ef39bb5a64f65ca53cb5c843d8cca4cef88a01f19031aa1bac14e746";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement CutTrees => Dependencies.GetProperty("cutTrees");
    public JsonElement BadgeRewards => Dependencies.GetProperty("badgeRewards");
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalCeladonEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCeladonGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalCeladonEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalCeladonEvents = externalCeladonEvents;
        AdditionalSourceHashes = ["celadon-gym-region-manifest:" + ManifestSha256, "celadon-gym-region-lock:" + LockSha256,
            .. files.Select(f => "celadon-gym-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CELADON_CITY_GYM" => new("map.celadon-city-gym"),
        _ => PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretCeladonGymRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/celadon-gym-region");
        var rawDirectory = Path.Combine(project, "content/raw/celadon-gym-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var maximum = length ?? 2 * 1024 * 1024;
            Require(maximum is > 0 and <= 2 * 1024 * 1024 && PretRuntimeMapValidation.IsHash(sha), "Celadon Gym input bounds/hash differ.");
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), maximum);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned Celadon Gym bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/celadon-gym-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 99 && m.GetProperty("outputs").GetArrayLength() == 22 &&
            m.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree && m.GetProperty("lockSha256").GetString() == LockSha256 &&
            m.GetProperty("mapCount").GetInt32() == 1 && m.GetProperty("cellCount").GetInt32() == 260 &&
            m.GetProperty("borderCellCount").GetInt32() == 4 && m.GetProperty("scriptBlockCount").GetInt32() == 81 &&
            m.GetProperty("warpCount").GetInt32() == 3 && m.GetProperty("npcGraphicsCount").GetInt32() == 7 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Celadon Gym manifest identity differs.");
        var source = sourceLock.RootElement;
        Require(source.GetProperty("schemaVersion").GetInt32() == 1 && source.GetProperty("repository").GetString() == "pret/pokefirered" &&
            source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && source.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree &&
            source.GetProperty("profile").GetString() == Profile && source.GetProperty("gameVersion").GetString() == "FIRERED" &&
            source.GetProperty("revision").GetInt32() == 0 && source.GetProperty("files").GetArrayLength() == 99 &&
            source.GetProperty("maps").GetArrayLength() == 1 && source.GetProperty("maps")[0].GetString() == "CeladonCity_Gym" &&
            !source.GetProperty("completeGame").GetBoolean(), "Celadon Gym source lock identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "celadon-gym-region/" + path, output)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Celadon Gym output.");
        }
        Require(Hash(outputs["celadon-gym-catalog.json"]) == CatalogSha256, "Celadon Gym catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["celadon-gym-catalog.json"]);
        Require(catalog.RootElement.GetProperty("schemaVersion").GetInt32() == 1 && catalog.RootElement.GetProperty("profile").GetString() == Profile &&
            catalog.RootElement.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && catalog.RootElement.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.RootElement.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Celadon Gym source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Celadon Gym Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate Celadon Gym source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 99, "Incomplete Celadon Gym source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Celadon Gym source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Celadon Gym source profile; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing Celadon Gym document value.");
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
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Celadon Gym runtime map.");
                continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                    evidence.Sha256 == byPath[evidence.Path].Sha256, "Celadon Gym embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Celadon Gym source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Celadon Gym events differ from original source.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Celadon Gym map stable identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
                SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate Celadon Gym map.");
        }
        Require(maps.Count == 1 && maps.Values.Sum(x => x.Width*x.Height) == 260, "Incomplete Celadon Gym map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("profile").GetString() == Profile &&
            scriptInventory.RootElement.GetProperty("blocks").GetArrayLength() == 81 &&
            scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Celadon Gym script reference closure is incomplete.");
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
                Require(symbol.Length > 0, "Celadon Gym script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_TEMP_12"] == 18 && constants["FLAG_TEMP_13"] == 19 && constants["FLAG_TEMP_14"] == 20 &&
            constants["FLAG_BADGE04_GET"] == 0x823 && constants["FLAG_DEFEATED_ERIKA"] == 0x4B3 &&
            constants["FLAG_GOT_TM19_FROM_ERIKA"] == 0x293 && constants["ITEM_TM19"] == 307,
            "Celadon Gym badge/Cut/reward constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        var dependency = dependencies.RootElement;
        Require(dependency.GetProperty("schemaVersion").GetInt32() == 1 && dependency.GetProperty("profile").GetString() == Profile &&
            dependency.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && !dependency.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependency.GetProperty("trainers").GetArrayLength() == 8 && dependency.GetProperty("cutTrees").GetArrayLength() == 3 &&
            dependency.GetProperty("badgeRewards").GetProperty("tm").GetString() == "ITEM_TM19" &&
            dependency.GetProperty("entry").GetProperty("sourceWarpId").GetInt32() == 6 &&
            dependency.GetProperty("entry").GetProperty("destinationWarpId").GetInt32() == 1,
            "Celadon Gym dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/CeladonCity/map.json"]);
        Require(npcSources.RootElement.GetProperty("schemaVersion").GetInt32() == 1 &&
            npcSources.RootElement.GetProperty("graphics").GetArrayLength() == 7, "Celadon Gym NPC inventory differs.");
        var gymEvents = maps[new MapId("map.celadon-city-gym")].Events;
        var cityEntry = cityEvents.RootElement.GetProperty("warp_events")[6];
        Require(cityEntry.GetProperty("dest_map").GetString() == "MAP_CELADON_CITY_GYM" && cityEntry.GetProperty("dest_warp_id").GetString() == "1" &&
            JsonElement.DeepEquals(cityEntry, dependency.GetProperty("entry").GetProperty("source")) &&
            JsonElement.DeepEquals(gymEvents.GetProperty("warp_events")[1], dependency.GetProperty("entry").GetProperty("destination")),
            "Celadon Gym city entry differs from original source.");
        foreach (var tree in dependency.GetProperty("cutTrees").EnumerateArray())
            Require(JsonElement.DeepEquals(tree.GetProperty("object"), gymEvents.GetProperty("object_events")[tree.GetProperty("localId").GetInt32() - 1]),
                "Celadon Gym Cut object differs from source.");
        foreach (var trainer in dependency.GetProperty("trainers").EnumerateArray())
            Require(JsonElement.DeepEquals(trainer.GetProperty("object"), gymEvents.GetProperty("object_events")[trainer.GetProperty("localId").GetInt32() - 1]),
                "Celadon Gym trainer object differs from source.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.Width == 13 && data.Height == 20 && data.BorderWidth == 2 && data.BorderHeight == 2 &&
            data.TileSize == 16 && data.TechnicalSpawn is null && data.Events.GetProperty("object_events").GetArrayLength() == 11 &&
            data.Events.GetProperty("warp_events").GetArrayLength() == 3 && data.Events.GetProperty("warp_events").EnumerateArray().All(w =>
                w.GetProperty("dest_map").GetString() == "MAP_CELADON_CITY" && w.GetProperty("dest_warp_id").GetString() == "6"), "Celadon Gym map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Celadon Gym layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_Building" => "primary/building",
            "gTileset_CeladonGym" => "secondary/celadon_gym",
            _ => throw new InvalidDataException("Unknown Celadon Gym region tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Celadon Gym cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Celadon Gym block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Celadon Gym metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Celadon Gym behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Celadon Gym metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Celadon Gym render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Celadon Gym path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Celadon Gym directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Celadon Gym file symlink is unsupported: " + path);
        }
        return result;
    }
}
