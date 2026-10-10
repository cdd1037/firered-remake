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
/// Authenticated Vermilion Gym map, actors, two-switch puzzle, barrier metatiles, Surge and field Cut source contracts. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretVermilionGymRegion
{
    public const string Profile = "firered-vermilion-gym-region-import-v1";
    public const string ManifestSha256 = "b98bb7d97c8a0af7f154aa1a640cd172696651bd2909ece7a884b3a607d3f894";
    public const string LockSha256 = "569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8";
    public const string CatalogSha256 = "98f87daa845b8a5dd31b084877c28f4bdf793dc3297bee02139ead6acbb6d5c8";
    public const string RawManifestSha256 = "68ba55c192cedfed9fabaa48ff5abb090265985645a2d01488e23c2825edbbab";
    public const string FullSourceHash = "86541efd4d1371314ad44f526ea95ec019665aa66f4a2bd2203c4ccbb51e8e39";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Puzzle => Dependencies.GetProperty("puzzle");
    public JsonElement Cut => Dependencies.GetProperty("cut");
    public JsonElement Surge => Dependencies.GetProperty("surge");
    public JsonElement BeamStates { get; }
    public ImmutableDictionary<int, PretMartMetatileDefinition> BeamDefinitions { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalVermilionEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretVermilionGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalVermilionEvents, JsonElement beamStates,
        ImmutableDictionary<int, PretMartMetatileDefinition> beamDefinitions)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalVermilionEvents = externalVermilionEvents;
        BeamStates = beamStates; BeamDefinitions = beamDefinitions;
        AdditionalSourceHashes = ["vermilion-gym-region-manifest:" + ManifestSha256, "vermilion-gym-region-lock:" + LockSha256,
            .. files.Select(f => "vermilion-gym-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VERMILION_CITY_GYM" => new("map.vermilion-city-gym"),
        _ => PretVermilionRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretVermilionGymRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/vermilion-gym-region");
        var rawDirectory = Path.Combine(project, "content/raw/vermilion-gym-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned Vermilion Gym bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/vermilion-gym-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 121 && m.GetProperty("outputs").GetArrayLength() == 110 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Vermilion Gym manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "vermilion-gym-region/" + path, output)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Vermilion Gym output.");
        }
        Require(Hash(outputs["vermilion-gym-catalog.json"]) == CatalogSha256, "Vermilion Gym catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["vermilion-gym-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Vermilion Gym source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Vermilion Gym Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate Vermilion Gym source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 121, "Incomplete Vermilion Gym source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Vermilion Gym source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Vermilion Gym source profile; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing Vermilion Gym document value.");
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
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Vermilion Gym runtime map.");
                continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                    evidence.Sha256 == byPath[evidence.Path].Sha256, "Vermilion Gym embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Vermilion Gym source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Vermilion Gym events differ from original source.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Vermilion Gym map stable identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
                SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate Vermilion Gym map.");
        }
        Require(maps.Count == 1 && maps.Values.Sum(x => x.Width*x.Height) == 231, "Incomplete Vermilion Gym map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Vermilion Gym script reference closure is incomplete.");
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
                Require(symbol.Length > 0, "Vermilion Gym script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_TEMP_1"] == 1 && constants["FLAG_TEMP_12"] == 18 &&
            constants["FOUND_FIRST_SWITCH"] == 1 && constants["SWITCH1_ID"] == 0x8004 &&
            constants["SWITCH2_ID"] == 0x8005 && constants["TRASH_CAN_ID"] == 0x8008 &&
            constants["VAR_TEMP_0"] == 0x4000 && constants["VAR_TEMP_1"] == 0x4001,
            "Vermilion Gym puzzle/Cut constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 4 &&
            dependencies.RootElement.GetProperty("puzzle").GetProperty("rngDrawCount").GetInt32() == 2 &&
            dependencies.RootElement.GetProperty("cut").GetProperty("localId").GetInt32() == 7 &&
            dependencies.RootElement.GetProperty("surge").GetProperty("rewardItem").GetString() == "ITEM_TM34",
            "Vermilion Gym dependency scope differs.");
        using var beams = JsonDocument.Parse(outputs["beam-metatiles.json"]);
        var beamDefinitions = ReadBeams(beams.RootElement, raw, outputs, directory);
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/VermilionCity/map.json"]);
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone(),
            beams.RootElement.GetProperty("states").Clone(), beamDefinitions);
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.TileSize == 16 && data.TechnicalSpawn is null, "Vermilion Gym map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Vermilion Gym layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_Building" => "primary/building",
            "gTileset_VermilionGym" => "secondary/vermilion_gym",
            _ => throw new InvalidDataException("Unknown Vermilion Gym region tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Vermilion Gym cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Vermilion Gym block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Vermilion Gym metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Vermilion Gym behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Vermilion Gym metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Vermilion Gym render artifact inventory differs.");
    }

    private static ImmutableDictionary<int, PretMartMetatileDefinition> ReadBeams(JsonElement beams,
        Dictionary<string, byte[]> raw, Dictionary<string, byte[]> outputs, string directory)
    {
        Require(beams.GetProperty("schemaVersion").GetInt32() == 1 && beams.GetProperty("profile").GetString() == Profile &&
            beams.GetProperty("mapId").GetString() == "map.vermilion-city-gym" && beams.GetProperty("preserveSourceElevation").GetBoolean(),
            "Vermilion Gym beam identity differs.");
        var definitions = ImmutableDictionary.CreateBuilder<int, PretMartMetatileDefinition>();
        var attributes = raw["data/tilesets/secondary/vermilion_gym/metatile_attributes.bin"];
        var sourceTiles = raw["data/tilesets/secondary/vermilion_gym/metatiles.bin"];
        var names = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        foreach (var tile in beams.GetProperty("definitions").EnumerateArray())
        {
            var id = tile.GetProperty("rawMetatileId").GetInt32();
            var local = id - 640;
            Require(local >= 0 && local < attributes.Length / 4, "Vermilion Gym beam metatile is out of bounds.");
            var attribute = BinaryPrimitives.ReadUInt32LittleEndian(attributes.AsSpan(local * 4));
            var behavior = (int)(attribute & 511);
            var terrain = (int)((attribute >> 9) & 31);
            var encounter = (int)((attribute >> 24) & 7);
            var layer = (int)((attribute >> 29) & 3);
            Require(tile.GetProperty("rawAttributes").GetUInt32() == attribute && tile.GetProperty("behavior").GetInt32() == behavior &&
                tile.GetProperty("terrain").GetInt32() == terrain && tile.GetProperty("encounterType").GetInt32() == encounter &&
                tile.GetProperty("layerType").GetInt32() == layer && layer is >= 0 and <= 2, "Vermilion Gym beam attributes differ.");
            var values = tile.GetProperty("tileEntries").EnumerateArray().Select(e => e.GetUInt16()).ToImmutableArray();
            Require(values.Length == 8, "Vermilion Gym beam tile count differs.");
            for (var i = 0; i < 8; i++)
                Require(values[i] == BinaryPrimitives.ReadUInt16LittleEndian(sourceTiles.AsSpan(local * 16 + i * 2)), "Vermilion Gym beam tile bytes differ.");
            string Texture(string name)
            {
                var path = tile.GetProperty(name + "TexturePath").GetString()!;
                Require(path == $"beam-metatiles/{id:x3}-{name}.png", "Vermilion Gym beam image path differs.");
                var png = outputs[path];
                Require(png.Length > 33 && png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
                    BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)) == 16 && BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)) == 16 &&
                    png[24] == 8 && png[25] == 6, "Vermilion Gym beam image format differs.");
                return SafeFile(directory, path);
            }
            definitions.Add(id, new(id, attribute, behavior, names[behavior], terrain, encounter, layer, values,
                Texture("bottom"), Texture("middle"), Texture("top")));
        }
        Require(definitions.Count == 29, "Vermilion Gym beam inventory differs.");
        var states = beams.GetProperty("states").EnumerateArray().ToArray();
        Require(states.Select(s => s.GetProperty("state").GetString()).SequenceEqual(["on", "half", "off"]), "Vermilion Gym beam state order differs.");
        var sourceBlocks = raw["data/layouts/VermilionCity_Gym/map.bin"];
        foreach (var state in states)
        {
            var patches = state.GetProperty("patches").EnumerateArray().ToArray();
            Require(patches.Length == 10, "Vermilion Gym beam state must replace ten cells.");
            var coordinates = new HashSet<(int, int)>();
            foreach (var patch in patches)
            {
                int x = patch.GetProperty("x").GetInt32(), y = patch.GetProperty("y").GetInt32();
                int id = patch.GetProperty("rawMetatileId").GetInt32(), collision = patch.GetProperty("collision").GetInt32();
                Require(x is >= 3 and <= 7 && y is 6 or 7 && coordinates.Add((x, y)) && definitions.ContainsKey(id), "Vermilion Gym beam coordinates differ.");
                var block = BinaryPrimitives.ReadUInt16LittleEndian(sourceBlocks.AsSpan((y * 11 + x) * 2));
                Require(collision == (patch.GetProperty("isImpassable").GetBoolean() ? 3 : 0) &&
                    patch.GetProperty("sourceRawBlock").GetUInt16() == block && patch.GetProperty("sourceElevation").GetInt32() == block >> 12 &&
                    patch.GetProperty("resultingRawBlock").GetUInt16() == ((block & 0xf000) | id | (collision << 10)), "Vermilion Gym beam block semantics differ.");
                Require((state.GetProperty("state").GetString() == "off" && x is >= 4 and <= 6) ? collision == 0 : collision == 3,
                    "Vermilion Gym barrier opening differs.");
            }
        }
        return definitions.ToImmutable();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Vermilion Gym path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Vermilion Gym directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Vermilion Gym file symlink is unsupported: " + path);
        }
        return result;
    }
}
