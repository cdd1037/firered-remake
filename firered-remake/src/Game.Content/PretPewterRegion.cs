using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// A separately authenticated Pewter City / gym / services source increment. Source availability does not
/// certify script handlers, trainer battles, wild encounters, Cut, hidden items or audio.
/// </summary>
public sealed class PretPewterRegion
{
    public const string Profile = "firered-pewter-region-import-v1";
    public const string ManifestSha256 = "f06eec631c567cbc299068fa9648081ec4f4db29545807bc370af6af99f68c87";
    public const string LockSha256 = "9597dbf6fa1a7bee3b2a8dd131bc51138ad10fac97f4baaa33d3e85c035f0621";
    public const string CatalogSha256 = "15f753bb14733a2e7e7f1de55f2b5418b22ebfb5e40d2da94a798765492f7dbb";
    public const string RawManifestSha256 = "dd4687d124e2744c5cd04883dad4757de4d810e4b8d396dfdc8ded584b863a50";
    public const string FullSourceHash = "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    public ImmutableArray<PretPewterTrainerSource> Trainers { get; }
    public ImmutableArray<string> MartItems { get; }
    public PretPewterHealLocation Healing { get; }
    public JsonElement GymReward => Dependencies.GetProperty("gymReward");
    public JsonElement Catalog { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretPewterRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        Trainers = dependencies.GetProperty("trainers").Deserialize<ImmutableArray<PretPewterTrainerSource>>(options);
        MartItems = dependencies.GetProperty("martInventories")[0].GetProperty("items").Deserialize<ImmutableArray<string>>(options);
        Healing = dependencies.GetProperty("healLocations")[0].GetProperty("table").Deserialize<PretPewterHealLocation>(options)
            ?? throw new InvalidDataException("Missing Pewter heal location.");
        AdditionalSourceHashes = ["pewter-region-manifest:" + ManifestSha256, "pewter-region-lock:" + LockSha256,
            .. files.Select(f => "pewter-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_PEWTER_CITY" => new("map.pewter-city"),
        "MAP_PEWTER_CITY_GYM" => new("map.pewter-gym"),
        "MAP_PEWTER_CITY_MART" => new("map.pewter-mart"),
        "MAP_PEWTER_CITY_POKEMON_CENTER_1F" => new("map.pewter-pokemon-center-1f"),
        "MAP_PEWTER_CITY_HOUSE1" => new("map.pewter-house-1"),
        "MAP_PEWTER_CITY_HOUSE2" => new("map.pewter-house-2"),
        "MAP_PEWTER_CITY_MUSEUM_1F" => new("map.pewter-museum-1f"),
        "MAP_PEWTER_CITY_MUSEUM_2F" => new("map.pewter-museum-2f"),
        "MAP_PEWTER_CITY_POKEMON_CENTER_2F" => new("map.pewter-pokemon-center-2f"),
        "MAP_ROUTE3" => new("map.route-3"),
        _ => PretForestRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretPewterRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/pewter-region");
        var rawDirectory = Path.Combine(project, "content/raw/pewter-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned pewter bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/pewter-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 257 && m.GetProperty("outputs").GetArrayLength() == 103 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Pewter manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"pewter-region/"+path,output))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate pewter output.");
        }
        Require(Hash(outputs["pewter-catalog.json"]) == CatalogSha256, "Pewter catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["pewter-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Pewter source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Pewter Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate pewter source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 257, "Incomplete pewter source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Pewter source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Pewter City / gym / services; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing pewter document value.");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if(runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!),out var compact) && compact.FullSourceHash==FullSourceHash)
            {
                PretRuntimeMapIntegration.ValidateSource(compact,metadata,raw,spec);
                ValidateMap(compact.ToWorldDocument(metadata),raw,byPath);
                var runtimeMap=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),SafeFile(directory,"npc/red-normal.png"),MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id,runtimeMap),"Duplicate runtime map.");continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                    evidence.Sha256 == byPath[evidence.Path].Sha256, "Pewter embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Pewter source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Pewter events differ from original source.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Pewter map stable identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
                SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate pewter map.");
        }
        Require(maps.Count == 8 && maps.Values.Sum(x => x.Width*x.Height) == 3073, "Incomplete pewter map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Pewter script reference closure is incomplete.");
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
                Require(symbol.Length > 0, "Pewter script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["TRAINER_LEADER_BROCK"] == 414 && constants["TRAINER_CAMPER_LIAM"] == 142,
            "Pewter trainer source constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 2 &&
            dependencies.RootElement.GetProperty("encounters").GetArrayLength() == 0, "Pewter dependency scope differs.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone());
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.TileSize == 16 && data.TechnicalSpawn is null, "Pewter map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height &&
            layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight,
            "Pewter layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_General" => "primary/general", "gTileset_Building" => "primary/building",
            "gTileset_PewterCity" => "secondary/pewter_city", "gTileset_PewterGym" => "secondary/pewter_gym",
            "gTileset_Mart" => "secondary/mart", "gTileset_PokemonCenter" => "secondary/pokemon_center",
            "gTileset_GenericBuilding2" => "secondary/generic_building_2", "gTileset_Museum" => "secondary/museum",
            _ => throw new InvalidDataException("Unknown Pewter tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Pewter cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Pewter block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Pewter metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Pewter behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Pewter metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) &&
            data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Pewter render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe pewter path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Pewter directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Pewter file symlink is unsupported: " + path);
        }
        return result;
    }
}

/// <summary>Original ordered custom moves include MOVE_NONE slots; no battle behavior is certified.</summary>
public sealed record PretPewterTrainerMonSource(string Species, int SourceSpeciesId, int Level,
    int SourceIv, string HeldItem, ImmutableArray<string> CustomMoves, string SpeciesName);

public sealed record PretPewterTrainerSource(string Symbol, int SourceTrainerId,
    ImmutableDictionary<string, string> Declaration, string Name, ImmutableArray<int> EncodedName,
    int BaseMoney, ImmutableArray<PretPewterTrainerMonSource> Party, PretPewterSourceLocation PartySourceRef,
    PretPewterSourceLocation SourceRef, int SourceTrainerClass, int SourceTrainerPic, int EncounterMusicGender,
    int AiFlags, int PartyFlags, string ScriptEntry, string IntroText, string DefeatText,
    string? PostBattleScript, string? MusicOption, PretPewterSourceLocation ScriptSourceRef, string RuntimeStatus);

public sealed record PretPewterHealLocation(string Id, string Map, int X, int Y,
    [property: JsonPropertyName("respawn_map")] string RespawnMap,
    [property: JsonPropertyName("respawn_npc")] string RespawnNpc);

/// <summary>Original definition line retained alongside the immutable source-file identity.</summary>
public sealed record PretPewterSourceLocation(SourceKind Kind, string Path, string Symbol,
    string Repository, string Commit, string Sha256, int Line)
{
    public SourceRef Reference => new(Kind, Path, Symbol, Repository, Commit, Sha256);
}
