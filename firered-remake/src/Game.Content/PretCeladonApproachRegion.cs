// Recovery candidate reconstructed from retained context, 2026-10-10.
// Source pins are original. This C# file's original byte identity is not known.
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Celadon approach source data; no gameplay-handler registration.</summary>
public sealed class PretCeladonApproachRegion
{
    public const string Profile = "firered-celadon-approach-region-import-v1";
    public const string ManifestSha256 = "f487d67103ec1db904fb0bee6caa95f1b1f96f5725ad850e83e42b6f3f868d3f";
    public const string LockSha256 = "b09ce111e7a091e7cc551f0d4660fb468f9a805340af0f80056b12d1db00242a";
    public const string CatalogSha256 = "b36aa010a286c7f04de62cd3d805fdaff2a4d6ab1194cbda49dc4923277e9c96";
    public const string RawManifestSha256 = "695fed4f152b1ecef3702de664886dac14786e65bbda7c054492b49d8e7587d0";
    public const string FullSourceHash = "756a0c0bfe4ab147b9443e71a8937a5b2a661a65e192919c5ac2691b89589f89";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Centers => Dependencies.GetProperty("centers");
    public JsonElement Mart => Dependencies.GetProperty("mart");
    public JsonElement Guards => Dependencies.GetProperty("guards");
    public JsonElement VolunteerHouse => Dependencies.GetProperty("volunteerHouse");
    public JsonElement CutTrees => Dependencies.GetProperty("cutTrees");
    public JsonElement Items => Dependencies.GetProperty("items");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public ImmutableArray<MapId> ReusedMapIds => [new("map.lavender-town")];
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalLavenderEvents { get; }
    public JsonElement ExternalRoute16Events { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCeladonApproachRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> files,
        JsonElement dependencies, JsonElement catalog, JsonElement scriptInventory, JsonElement npcSources,
        JsonElement lavenderEvents, JsonElement route16Events)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptInventory; NpcSources = npcSources;
        ExternalLavenderEvents = lavenderEvents; ExternalRoute16Events = route16Events;
        AdditionalSourceHashes = ["celadon-approach-region-manifest:" + ManifestSha256,
            "celadon-approach-region-lock:" + LockSha256,
            .. files.Select(f => "celadon-approach-region:" + f.Path + ":" + f.Sha256)];
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CELADON_CITY" => new("map.celadon-city"),
        "MAP_CELADON_CITY_CONDOMINIUMS_1F" => new("map.celadon-city-condominiums-1f"),
        "MAP_CELADON_CITY_DEPARTMENT_STORE_1F" => new("map.celadon-city-department-store-1f"),
        "MAP_CELADON_CITY_GAME_CORNER" => new("map.celadon-city-game-corner"),
        "MAP_CELADON_CITY_GAME_CORNER_PRIZE_ROOM" => new("map.celadon-city-game-corner-prize-room"),
        "MAP_CELADON_CITY_GYM" => new("map.celadon-city-gym"),
        "MAP_CELADON_CITY_HOTEL" => new("map.celadon-city-hotel"),
        "MAP_CELADON_CITY_HOUSE1" => new("map.celadon-city-house-1"),
        "MAP_CELADON_CITY_POKEMON_CENTER_1F" => new("map.celadon-city-pokemon-center-1f"),
        "MAP_CELADON_CITY_POKEMON_CENTER_2F" => new("map.celadon-city-pokemon-center-2f"),
        "MAP_CELADON_CITY_RESTAURANT" => new("map.celadon-city-restaurant"),
        "MAP_LAVENDER_TOWN" => new("map.lavender-town"),
        "MAP_LAVENDER_TOWN_HOUSE1" => new("map.lavender-town-house-1"),
        "MAP_LAVENDER_TOWN_MART" => new("map.lavender-town-mart"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_1F" => new("map.lavender-town-pokemon-center-1f"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_2F" => new("map.lavender-town-pokemon-center-2f"),
        "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE" => new("map.lavender-town-volunteer-pokemon-house"),
        "MAP_ROUTE16" => new("map.route-16"),
        "MAP_ROUTE7" => new("map.route-7"),
        "MAP_ROUTE7_EAST_ENTRANCE" => new("map.route-7-east-entrance"),
        "MAP_ROUTE8" => new("map.route-8"),
        "MAP_ROUTE8_WEST_ENTRANCE" => new("map.route-8-west-entrance"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_SAFFRON_CITY_CONNECTION" => new("map.saffron-city-connection"),
        "MAP_TRADE_CENTER" => new("map.trade-center"),
        "MAP_UNDERGROUND_PATH_EAST_ENTRANCE" => new("map.underground-path-east-entrance"),
        "MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL" => new("map.underground-path-east-west-tunnel"),
        "MAP_UNDERGROUND_PATH_WEST_ENTRANCE" => new("map.underground-path-west-entrance"),
        "MAP_UNION_ROOM" => new("map.union-room"),
        _ => PretRockTunnelRegion.MapIdFromSourceSymbol(symbol)
    };
    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var connections = source.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            source.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), connections);
    }
    public static PretCeladonApproachRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/celadon-approach-region");
        var rawDirectory = Path.Combine(project, "content/raw/celadon-approach-region");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned Celadon approach bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "content/sources/celadon-approach-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 418 && m.GetProperty("outputs").GetArrayLength() == 184 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Celadon approach manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "celadon-approach-region/" + path, output)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32())), "Duplicate output.");
        }
        Require(Hash(outputs["celadon-approach-catalog.json"]) == CatalogSha256, "Celadon catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["celadon-approach-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")), "Source inventory differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!; var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!; var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 418, "Incomplete source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Source-set hash differs.");
        var inventory = files.ToImmutable(); var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated CeladonApproach source profile; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing document value.");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact) && compact.FullSourceHash == FullSourceHash)
            {
                PretRuntimeMapIntegration.ValidateSource(compact, metadata, raw, spec);
                ValidateMap(compact.ToWorldDocument(metadata), raw, byPath);
                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative), SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate runtime map."); continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]); var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) && evidence.Sha256 == byPath[evidence.Path].Sha256, "Embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/" + spec.GetProperty("sourceName").GetString() + "/map.json" &&
                sourceRef.Symbol == spec.GetProperty("sourceMapSymbol").GetString() && sourceRef.Commit == PretWorldMap.SourceCommit &&
                sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Map source identity differs.");
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Map events differ.");
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            Require(data.StableId == spec.GetProperty("stableId").GetString() && MapIdFromSourceSymbol(sourceRef.Symbol).Value == data.StableId, "Stable map identity differs.");
            ValidateMap(data, raw, byPath);
            var map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative), SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(maps.TryAdd(map.Id, map), "Duplicate map.");
        }
        Require(maps.Count == 15 && maps.Values.Sum(x => x.Width * x.Height) == 6280, "Incomplete map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Script reference closure incomplete.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name; var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "Script lacks original label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]); var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject()) constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["FLAG_GOT_TEA"] == 0x2A6 && constants["FLAG_RESCUED_MR_FUJI"] == 0x23C && constants["HEAL_LOCATION_LAVENDER_TOWN"] == 5 && constants["HEAL_LOCATION_CELADON_CITY"] == 7, "Service requirements differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]); var d = dependencies.RootElement;
        Require(!d.GetProperty("runtimeIntegrated").GetBoolean() && d.GetProperty("trainers").GetArrayLength() == 13 &&
            d.GetProperty("trainers").EnumerateArray().Count(x => x.GetProperty("battleCommand").GetString() == "trainerbattle_double") == 2 &&
            d.GetProperty("pickups").GetArrayLength() == 13 && d.GetProperty("centers").GetArrayLength() == 2 && d.GetProperty("guards").GetArrayLength() == 2 &&
            d.GetProperty("mart").GetProperty("stock").GetArrayLength() == 9 && d.GetProperty("cutTrees").GetArrayLength() == 6 &&
            d.GetProperty("volunteerHouse").GetProperty("mrFujiActor").GetProperty("flag").GetString() == "FLAG_HIDE_POKEHOUSE_FUJI", "Dependency scope differs.");
        var reused = catalog.RootElement.GetProperty("reusedMaps");
        Require(reused.GetArrayLength() == 1 && reused[0].GetProperty("stableId").GetString() == "map.lavender-town" &&
            reused[0].GetProperty("manifestSha256").GetString() == PretRockTunnelRegion.ManifestSha256 && reused[0].GetProperty("fullSourceHash").GetString() == PretRockTunnelRegion.FullSourceHash, "Reused Lavender certificate differs.");
        var lavenderDirectory = Path.Combine(project, "content/generated/rocktunnel-region");
        _ = ReadPinned(lavenderDirectory, "manifest.json", PretRockTunnelRegion.ManifestSha256);
        foreach (var output in reused[0].GetProperty("outputs").EnumerateArray())
        {
            var relative = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "rocktunnel-region/" + relative, output)) continue;
            var bytes = ReadPinned(lavenderDirectory, relative, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
            if (relative == "lavender-town/world.json")
            { using var world = JsonDocument.Parse(bytes); using var original = JsonDocument.Parse(raw["data/maps/LavenderTown/map.json"]); Require(JsonElement.DeepEquals(world.RootElement.GetProperty("events"), original.RootElement), "Reused Lavender events differ."); }
        }
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/LavenderTown/map.json"]);
        using var route16Events = JsonDocument.Parse(raw["data/maps/Route16/map.json"]);
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(), inventory,
            d.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(), npcSources.RootElement.Clone(), cityEvents.RootElement.Clone(), route16Events.RootElement.Clone());
    }
    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.TileSize == 16 && data.TechnicalSpawn is null, "Map schema differs.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l => l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("width").GetInt32() == data.Width && layout.GetProperty("height").GetInt32() == data.Height && layout.GetProperty("border_width").GetInt32() == data.BorderWidth && layout.GetProperty("border_height").GetInt32() == data.BorderHeight, "Layout dimensions differ.");
        string Tileset(string symbol) => symbol switch
        {
            "gTileset_General" => "primary/general", "gTileset_Building" => "primary/building",
            "gTileset_PokemonCenter" => "secondary/pokemon_center", "gTileset_GenericBuilding1" => "secondary/generic_building_1",
            "gTileset_GenericBuilding2" => "secondary/generic_building_2", "gTileset_FanClubDaycare" => "secondary/fan_club_daycare",
            "gTileset_Mart" => "secondary/mart", "gTileset_LavenderTown" => "secondary/lavender_town",
            "gTileset_CeladonCity" => "secondary/celadon_city", "gTileset_UndergroundPath" => "secondary/underground_path",
            _ => throw new InvalidDataException("Unknown Celadon approach tileset.")
        };
        var primaryBase = "data/tilesets/" + Tileset(layout.GetProperty("primary_tileset").GetString()!);
        var secondaryBase = "data/tilesets/" + Tileset(layout.GetProperty("secondary_tileset").GetString()!);
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]), @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant).ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path]; Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i]; var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2)); var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block && (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = (cell.MetatileId < 640 ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) == new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7), "Metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") && cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" && cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit && cell.SourceRef.Sha256 == files[attrPath].Sha256, "Metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, data.Width * data.Height);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, data.BorderWidth * data.BorderHeight);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 8 && data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png", "border.png", "border-bottom.png", "border-middle.png", "border-top.png"]) && data.Artifacts.All(a => a.Path.StartsWith("border", StringComparison.Ordinal) ? a.Width == data.BorderWidth * 16 && a.Height == data.BorderHeight * 16 : a.Width == data.Width * 16 && a.Height == data.Height * 16), "Render artifact inventory differs.");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or "..")) throw new InvalidDataException("Unsafe Celadon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent) if (cursor.LinkTarget is not null) throw new InvalidDataException("Directory symlink unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null) throw new InvalidDataException("File symlink unsupported: " + path);
        }
        return result;
    }
}
