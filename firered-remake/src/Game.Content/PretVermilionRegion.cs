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
/// Authenticated Route5/6, Underground Path and Vermilion maps, actors, services and source boundaries. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretVermilionRegion
{
    public const string RuntimeManifestSha256 = "2ce94d941d01764791f0211c3dcbcd7245f8a9fff90f7662283b30751e74562b";
    public const string RuntimeConverterSha256 = "da6f7a39b12f53282dbabfbac1066ad2cf01be531eba8d2aa1bd9e156b1c1527";
    public const string Profile = "firered-vermilion-region-import-v1";
    public const string ManifestSha256 = "a82c5a5c5c6c04c743fc39d4bdef8e017a796a3f78a60867bdecfc767540d910";
    public const string LockSha256 = "e46077d2d1a90afa843f2aaa7e4ad9428056f146860ddd306badc2ab395abc27";
    public const string CatalogSha256 = "fa45e447dd04a75c7082091b8971d6b94a4fd767eb0cd9178ec0579806a279db";
    public const string RawManifestSha256 = "eaaefd492a5c7b7520e8ac48daaa1c583bc6ef43225ffb063e2aace993d290b1";
    public const string FullSourceHash = "fffd904ff456351bef0ed808b1043c968f2b03db122a10e056ac3971b88f1759";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement CeruleanSouthConnection => Dependencies.GetProperty("ceruleanSouthConnection");
    public JsonElement HealLocation => Dependencies.GetProperty("healLocation");
    public JsonElement Mart => Dependencies.GetProperty("mart");
    public JsonElement Encounters { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalCeruleanEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretVermilionRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalCeruleanEvents, JsonElement encounters)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        Encounters = encounters;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalCeruleanEvents = externalCeruleanEvents;
        AdditionalSourceHashes = ["vermilion-region-manifest:" + ManifestSha256, "vermilion-region-lock:" + LockSha256,
            .. files.Select(f => "vermilion-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE5" => new("map.route-5"),
        "MAP_ROUTE5_SOUTH_ENTRANCE" => new("map.route-5-south-entrance"),
        "MAP_UNDERGROUND_PATH_NORTH_ENTRANCE" => new("map.underground-path-north-entrance"),
        "MAP_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL" => new("map.underground-path-north-south-tunnel"),
        "MAP_UNDERGROUND_PATH_SOUTH_ENTRANCE" => new("map.underground-path-south-entrance"),
        "MAP_ROUTE6_NORTH_ENTRANCE" => new("map.route-6-north-entrance"),
        "MAP_ROUTE6" => new("map.route-6"),
        "MAP_VERMILION_CITY" => new("map.vermilion-city"),
        "MAP_VERMILION_CITY_POKEMON_CENTER_1F" => new("map.vermilion-city-pokemon-center-1f"),
        "MAP_VERMILION_CITY_MART" => new("map.vermilion-city-mart"),
        "MAP_ROUTE5_POKEMON_DAY_CARE" => new("map.route-5-pokemon-day-care"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_SAFFRON_CITY_CONNECTION" => new("map.saffron-city-connection"),
        "MAP_ROUTE11" => new("map.route-11"),
        "MAP_SSANNE_EXTERIOR" => new("map.ssanne-exterior"),
        "MAP_VERMILION_CITY_HOUSE1" => new("map.vermilion-city-house-1"),
        "MAP_VERMILION_CITY_HOUSE2" => new("map.vermilion-city-house2"),
        "MAP_VERMILION_CITY_HOUSE3" => new("map.vermilion-city-house-3"),
        "MAP_VERMILION_CITY_POKEMON_FAN_CLUB" => new("map.vermilion-city-pokemon-fan-club"),
        "MAP_VERMILION_CITY_GYM" => new("map.vermilion-city-gym"),
        "MAP_VERMILION_CITY_POKEMON_CENTER_2F" => new("map.vermilion-city-pokemon-center-2f"),
        _ => PretBillRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretVermilionRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/vermilion-region");
        var rawDirectory = Path.Combine(project, "content/raw/vermilion-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned vermilion bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/vermilion-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 236 && m.GetProperty("outputs").GetArrayLength() == 127 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Vermilion manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "vermilion-region/" + path, output)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate vermilion output.");
        }
        Require(Hash(outputs["vermilion-catalog.json"]) == CatalogSha256, "Vermilion catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["vermilion-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Vermilion source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Vermilion Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate vermilion source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 236, "Incomplete Vermilion source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Vermilion source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Route5/6, Underground Path and Vermilion; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing vermilion document value.");
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
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Vermilion runtime map.");
                continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                    evidence.Sha256 == byPath[evidence.Path].Sha256, "Vermilion embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Vermilion source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Vermilion events differ from original source.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Vermilion map stable identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
                SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate vermilion map.");
        }
        Require(maps.Count == 10 && maps.Values.Sum(x => x.Width*x.Height) == 6011, "Incomplete vermilion map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Vermilion script reference closure is incomplete.");
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
                Require(symbol.Length > 0, "Vermilion script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_GOT_TEA"] > 0 && constants["LOCALID_VERMILION_NURSE"] == 1 &&
            constants["HEAL_LOCATION_VERMILION_CITY"] == 6, "Vermilion progression/local actor constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 6 &&
            dependencies.RootElement.GetProperty("mart").GetProperty("items").GetArrayLength() == 8 &&
            !dependencies.RootElement.GetProperty("ceruleanSouthConnection").GetProperty("enabled").GetBoolean(),
            "Vermilion dependency scope differs.");
        using var encounters = JsonDocument.Parse(outputs["encounters.json"]);
        Require(encounters.RootElement.GetProperty("gameVersion").GetString() == "FIRERED" &&
            encounters.RootElement.GetProperty("maps").GetArrayLength() == 2, "Vermilion encounter scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/CeruleanCity/map.json"]);
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone(), encounters.RootElement.Clone());
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.TileSize == 16 && data.TechnicalSpawn is null, "Vermilion map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Vermilion layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_General" => "primary/general",
            "gTileset_Building" => "primary/building",
            "gTileset_CeruleanCity" => "secondary/cerulean_city",
            "gTileset_VermilionCity" => "secondary/vermilion_city",
            "gTileset_GenericBuilding1" => "secondary/generic_building_1",
            "gTileset_GenericBuilding2" => "secondary/generic_building_2",
            "gTileset_UndergroundPath" => "secondary/underground_path",
            "gTileset_PokemonCenter" => "secondary/pokemon_center",
            "gTileset_Mart" => "secondary/mart",
            _ => throw new InvalidDataException("Unknown Vermilion region tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Vermilion cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Vermilion block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Vermilion metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Vermilion behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Vermilion metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Vermilion render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe vermilion path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Vermilion directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Vermilion file symlink is unsupported: " + path);
        }
        return result;
    }
}
