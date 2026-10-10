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
/// A separately authenticated Pokemon Center 1F source increment. Source availability does not
/// certify script handlers, healing animation, audio, PC storage, upstairs or link play.
/// </summary>
public sealed class PretViridianCenter
{
    public const string Profile = "firered-viridian-center-import-v1";
    public const string ManifestSha256 = "1d76f02b48e690edfbad0f9b1f8f3a1875d4cf39a78f5fcc911fc33ddf985963";
    public const string LockSha256 = "3a933ce9e3b542e328887410dfd9d6a8091fd243987f0d408fee9745240a7abb";
    public const string CatalogSha256 = "c2ce7469ac2e77bce6278d89f5432372f36fdaff49a2daa537ef254adafc54de";
    public const string FullSourceHash = "6a9b0808f6b28adda753582c1480f9417dc3c745b09f8d15766b7e5083f4bbd3";
    public const string StableMapId = "map.viridian-pokemon-center-1f";
    public PretWorldMap Map { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string ServiceSourcePath => Path.Combine(GeneratedDirectory, "service.json");

    private PretViridianCenter(string directory, PretWorldMap map, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, ImmutableDictionary<string, string> uiTexts)
    {
        GeneratedDirectory = directory; Map = map; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; SourceUiTexts = uiTexts;
        AdditionalSourceHashes = ["viridian-center-manifest:" + ManifestSha256, "viridian-center-lock:" + LockSha256,
            .. files.Select(f => "viridian-center:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F" => new(StableMapId),
        "MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F" => new("map.viridian-pokemon-center-2f"),
        _ => PretViridianRegion.MapIdFromSourceSymbol(symbol)
    };

    public static PretViridianCenter Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/viridian-center");
        var rawDirectory = Path.Combine(project, "content/raw/viridian-center");
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned center bytes differ: " + path);
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/viridian-center.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 88 && m.GetProperty("outputs").GetArrayLength() == 15 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Center manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"viridian-center/"+path,output))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate center output.");
        }
        Require(Hash(outputs["center-catalog.json"]) == CatalogSha256, "Center catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["center-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")),
            "Center source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Center Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate center source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 88, "Incomplete center source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Center source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Viridian Pokemon Center 1F; runtime certification is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing center document value.");
        var spec = catalog.RootElement.GetProperty("map");
        var relative = spec.GetProperty("directory").GetString()!;
        PretWorldMap map;
        if(runtimeMaps is not null)
        {
            var compact=runtimeMaps.Maps[new MapId(spec.GetProperty("stableId").GetString()!)];
            PretRuntimeMapIntegration.ValidateSource(compact,metadata,raw,spec);
            ValidateMap(compact.ToWorldDocument(metadata),raw,byPath);
            map=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),SafeFile(directory,"npc/red-normal.png"),MapIdFromSourceSymbol);
        }
        else
        {
        using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
        var world = document.RootElement;
        var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
        foreach (var evidence in embedded)
            Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) &&
                evidence.Sha256 == byPath[evidence.Path].Sha256, "Center embedded source differs: " + evidence.Path);
        var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
        Require(sourceRef.Kind == SourceKind.Pret && sourceRef.Path == "data/maps/ViridianCity_PokemonCenter_1F/map.json" &&
            sourceRef.Symbol == "MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F" && sourceRef.Commit == PretWorldMap.SourceCommit &&
            sourceRef.Repository == "pret/pokefirered" && sourceRef.Sha256 == byPath[sourceRef.Path].Sha256, "Center source identity differs.");
        using var events = JsonDocument.Parse(raw[sourceRef.Path]);
        Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Center events differ from original source.");
        var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
            world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
            world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
            Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
            Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
        ValidateMap(data, raw, byPath);
        map = PretWorldMap.FromAuthenticatedExtension(data, SafeFile(directory, relative),
            SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
        }
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Center script reference closure is incomplete.");
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
                Require(symbol.Length > 0, "Center script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        using var constantDocument = JsonDocument.Parse(outputs["constants.json"]);
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in constantDocument.RootElement.GetProperty("values").EnumerateObject())
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        Require(constants["HEAL_LOCATION_VIRIDIAN_CITY"] == 2 && constants["LOCALID_VIRIDIAN_NURSE"] == 1 &&
            constants["FLDEFF_POKECENTER_HEAL"] == 25 && constants["SCR_MENU_CANCEL"] == 127, "Center required source constants differ.");
        var uiTexts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var centerText = Regex.Match(Encoding.UTF8.GetString(raw["src/strings.c"]),
            "const u8 gText_PlayerScurriedToCenter\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);", RegexOptions.CultureInvariant);
        Require(centerText.Success, "Missing source center whiteout message.");
        uiTexts.Add("gText_PlayerScurriedToCenter", centerText.Groups[1].Value + "$");
        return new(directory, map, scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(), inventory, uiTexts.ToImmutable());
    }

    private static void ValidateMap(PretWorldDocument data, Dictionary<string, byte[]> raw, Dictionary<string, PretWorldSourceFile> files)
    {
        Require(data.SchemaVersion == 1 && data.StableId == StableMapId && data.Width == 15 && data.Height == 10 &&
            data.TileSize == 16 && data.BorderWidth == 2 && data.BorderHeight == 2 && data.TechnicalSpawn is null, "Center map dimensions/identity differ.");
        using var layouts = JsonDocument.Parse(raw["data/layouts/layouts.json"]);
        var layout = layouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l =>
            l.TryGetProperty("id", out var id) && id.GetString() == data.Events.GetProperty("layout").GetString());
        Require(layout.GetProperty("id").GetString() == "LAYOUT_POKEMON_CENTER_1F" && layout.GetProperty("width").GetInt32() == data.Width &&
            layout.GetProperty("height").GetInt32() == data.Height && layout.GetProperty("primary_tileset").GetString() == "gTileset_Building" &&
            layout.GetProperty("secondary_tileset").GetString() == "gTileset_PokemonCenter", "Center layout differs.");
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void Cells(ImmutableArray<PretWorldCell> cells, string path, int width, int expectedCount)
        {
            var bytes = raw[path];
            Require(!cells.IsDefault && cells.Length == expectedCount && bytes.Length == expectedCount * 2, "Center cell inventory differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
                var decoded = PretWorldDecoder.DecodeBlock(block);
                Require(cell.X == i % width && cell.Y == i / width && cell.RawBlock == block &&
                    (cell.MetatileId, cell.Collision, cell.Elevation) == decoded, "Center block decoding differs.");
                var local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
                var attrPath = cell.MetatileId < 640 ? "data/tilesets/primary/building/metatile_attributes.bin" :
                    "data/tilesets/secondary/pokemon_center/metatile_attributes.bin";
                var attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[attrPath].AsSpan(local * 4));
                Require(cell.RawAttributes == attribute && PretWorldDecoder.DecodeAttributes(attribute) ==
                    new PretWorldAttributes(cell.BehaviorId, cell.Terrain, cell.Attribute2, cell.Attribute3, cell.EncounterType, cell.Attribute5, cell.LayerType, cell.Attribute7),
                    "Center metatile attributes differ.");
                Require(cell.BehaviorName == behaviorNames.GetValueOrDefault(cell.BehaviorId, $"UNKNOWN_0x{cell.BehaviorId:X3}") &&
                    cell.Movement == PretWorldDecoder.Movement(cell.BehaviorId) && cell.LayerType is >= 0 and <= 2, "Center behavior/layer differs.");
                Require(cell.SourceRef.Kind == SourceKind.Pret && cell.SourceRef.Path == attrPath && cell.SourceRef.Symbol == $"metatile_{local:x3}" &&
                    cell.SourceRef.Repository == "pret/pokefirered" && cell.SourceRef.Commit == PretWorldMap.SourceCommit &&
                    cell.SourceRef.Sha256 == files[attrPath].Sha256, "Center metatile source differs.");
            }
        }
        Cells(data.Cells, layout.GetProperty("blockdata_filepath").GetString()!, data.Width, 150);
        Cells(data.BorderCells, layout.GetProperty("border_filepath").GetString()!, data.BorderWidth, 4);
        Require(data.TexturePath == "map.png" && data.Artifacts.Length == 4 &&
            data.Artifacts.Select(a => a.Path).ToHashSet(StringComparer.Ordinal).SetEquals(["map.png", "bottom.png", "middle.png", "top.png"]) &&
            data.Artifacts.All(a => a.Width == 240 && a.Height == 160), "Center render artifact inventory differs.");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe center path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Center directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Center file symlink is unsupported: " + path);
        }
        return result;
    }
}
