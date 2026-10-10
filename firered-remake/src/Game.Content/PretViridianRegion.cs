using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated source increment. Content availability does not certify runtime handlers.</summary>
public sealed class PretViridianRegion
{
    public const string Profile = "firered-viridian-region-import-v1";
    public const string ManifestSha256 = "4614b158318d117e82c0f61f72bf0f1a58b65616bdd65933e32df1e3f01b2568";
    public const string LockSha256 = "dd0045b11236a019539fdb57a80ff152fab493a87acec2c4a9eb53e2b2463fba";
    public const string CatalogSha256 = "1c2a18a575a93266014e5c416b904b44dd98d478c39b62483df5daa51418fd85";
    public const string WhiteoutLockSha256 = "28073ff8287489a2508cb0ca2ce695f469326b0ac040505f6f8cc55dda6a16d6";
    public const string FullSourceHash = "c4c7f01ac8170d3de7435c9f7b3bec8694f743ca7307a2526cc408d3854f3256";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretViridianRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, ImmutableArray<string> additionalSourceHashes, ImmutableDictionary<string, string> sourceUiTexts)
    { GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; AdditionalSourceHashes = additionalSourceHashes; SourceUiTexts = sourceUiTexts; }

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        var existing = PretWorldCatalog.Specifications.FirstOrDefault(x => x.SourceMapSymbol == symbol);
        if (existing is not null) return new(existing.StableId);
        return new(symbol switch
        {
            "MAP_ROUTE1" => "map.route-1", "MAP_VIRIDIAN_CITY" => "map.viridian-city",
            "MAP_VIRIDIAN_CITY_MART" => "map.viridian-mart", "MAP_ROUTE2" => "map.route-2",
            "MAP_ROUTE22" => "map.route-22", "MAP_ROUTE21_NORTH" => "map.route-21-north",
            "MAP_VIRIDIAN_CITY_GYM" => "map.viridian-gym", "MAP_VIRIDIAN_CITY_HOUSE" => "map.viridian-house",
            "MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F" => "map.viridian-pokemon-center-1f",
            "MAP_VIRIDIAN_CITY_SCHOOL" => "map.viridian-school",
            _ => throw new InvalidDataException("Unknown region source map symbol: " + symbol)
        });
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretViridianRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/viridian-region");
        var rawDirectory = Path.Combine(project, "content/raw/viridian-region");
        static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
        static string SafeFile(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
                throw new InvalidDataException("Unsafe region path: " + path);
            var result = root;
            for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
                if (cursor.LinkTarget is not null) throw new InvalidDataException("Region directory symlink is unsupported.");
            foreach (var segment in path.Split('/'))
            {
                result = Path.Combine(result, segment);
                if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                    throw new InvalidDataException("Region file symlink is unsupported: " + path);
            }
            return result;
        }
        byte[] ReadPinned(string root, string path, string sha, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == sha && (length is null || bytes.Length == length), "Pinned region bytes differ: " + path);
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/viridian-region.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("profile").GetString() == Profile && m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
            m.GetProperty("fullSourceHash").GetString() == FullSourceHash && m.GetProperty("inputCount").GetInt32() == 189 &&
            m.GetProperty("outputs").GetArrayLength() == 47, "Region manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"viridian-region/"+path,output))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32())), "Duplicate region output.");
        }
        Require(Hash(outputs["viridian-catalog.json"]) == CatalogSha256, "Region catalog identity differs.");
        using var catalog = JsonDocument.Parse(outputs["viridian-catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")), "Region source inventory differs from fixed lock.");
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
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Region Git blob differs: " + path);
            Require(raw.TryAdd(path, bytes), "Duplicate region source.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        Require(raw.Count == 189, "Incomplete region source inventory.");
        var material = string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == FullSourceHash, "Region source set hash differs.");
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Source material remains local; no asset distribution rights are inferred.", "Authenticated Viridian region increment; runtime support is separate.", Profile, inventory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing region document value.");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if(runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!),out var compact) && compact.FullSourceHash==FullSourceHash)
            {
                PretRuntimeMapIntegration.ValidateSource(compact,metadata,raw,spec);
                var runtimeMap=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),Path.Combine(project,"content/generated/pallet/player.png"),MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id,runtimeMap),"Duplicate runtime map.");continue;
            }
            using var document = JsonDocument.Parse(outputs[relative + "/world.json"]);
            var world = document.RootElement;
            var embedded = Decode<ImmutableArray<PretWorldEmbeddedSource>>(world.GetProperty("embeddedSource"));
            foreach (var evidence in embedded)
                Require(raw.TryGetValue(evidence.Path, out var bytes) && Convert.FromBase64String(evidence.Base64).AsSpan().SequenceEqual(bytes) && evidence.Sha256 == byPath[evidence.Path].Sha256,
                    "Region embedded source differs: " + evidence.Path);
            var sourceRef = Decode<SourceRef>(world.GetProperty("sourceRef"));
            using var events = JsonDocument.Parse(raw[sourceRef.Path]);
            Require(JsonElement.DeepEquals(world.GetProperty("events"), events.RootElement), "Region events differ from original source.");
            Require(MapIdFromSourceSymbol(sourceRef.Symbol).Value == world.GetProperty("stableId").GetString(), "Region map identity differs.");
            if (spec.GetProperty("role").GetString() != "newMap") continue;
            var data = new PretWorldDocument(world.GetProperty("schemaVersion").GetInt32(), world.GetProperty("stableId").GetString()!, sourceRef, metadata,
                world.GetProperty("width").GetInt32(), world.GetProperty("height").GetInt32(), world.GetProperty("tileSize").GetInt32(),
                world.GetProperty("borderWidth").GetInt32(), world.GetProperty("borderHeight").GetInt32(), world.GetProperty("texturePath").GetString()!,
                Decode<ImmutableArray<PretWorldArtifact>>(world.GetProperty("artifacts")), Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("cells")),
                Decode<ImmutableArray<PretWorldCell>>(world.GetProperty("borderCells")), embedded, null, world.GetProperty("events").Clone());
            var map = PretWorldMap.FromAuthenticatedRegion(data, SafeFile(directory, relative), Path.Combine(project, "content/generated/pallet/player.png"));
            maps.Add(map.Id, map);
        }
        Require(maps.Count == 3, "Region must add exactly three source maps.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name; var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else scripts.Add(new(path, text, new(SourceKind.Pret, path, "viridian-script-library", "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
        }
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var file in raw.Where(x => x.Key.StartsWith("include/constants/", StringComparison.Ordinal)))
            foreach (Match match in Regex.Matches(Encoding.UTF8.GetString(file.Value), @"^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                constants[match.Groups[1].Value] = match.Groups[2].Value.StartsWith("0x", StringComparison.Ordinal)
                    ? Convert.ToInt32(match.Groups[2].Value, 16) : int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        // A separate immutable source lock authenticates defeat/whiteout callbacks. The other C files
        // remain audit data; compiling every included file would pretend its runtime is implemented.
        using var whiteoutLock = JsonDocument.Parse(ReadPinned(project, "scripts/route1-whiteout-source.lock.json", WhiteoutLockSha256));
        var whiteoutHashes = ImmutableArray.CreateBuilder<string>();
        whiteoutHashes.Add("whiteout-lock:" + WhiteoutLockSha256);
        var whiteout = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var input in whiteoutLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var sha = input.GetProperty("sha256").GetString()!;
            var bytes = ReadPinned(Path.Combine(project, "content/raw/route1-whiteout"), path, sha, input.GetProperty("byteLength").GetInt32());
            var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == input.GetProperty("gitBlobSha1").GetString(), "Whiteout Git blob differs: " + path);
            Require(whiteout.TryAdd(path, bytes), "Duplicate whiteout source.");
            whiteoutHashes.Add("whiteout:" + path + ":" + sha);
            if (path == "data/scripts/white_out.inc") scripts.Add(new(path, Encoding.UTF8.GetString(bytes),
                new(SourceKind.Pret, path, "whiteout-script-library", "pret/pokefirered", PretWorldMap.SourceCommit, sha)));
            if (path == "data/text/white_out.inc") texts.Add(path, Encoding.UTF8.GetString(bytes));
        }
        Require(whiteout.Count == 17, "Incomplete whiteout source inventory.");
        var uiTexts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var homeText = Regex.Match(Encoding.UTF8.GetString(whiteout["src/strings.c"]),
            "const u8 gText_PlayerScurriedBackHome\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);", RegexOptions.CultureInvariant);
        Require(homeText.Success, "Missing source whiteout home text.");
        uiTexts.Add("gText_PlayerScurriedBackHome", homeText.Groups[1].Value + "$");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(), whiteoutHashes.ToImmutable(), uiTexts.ToImmutable());
    }
}
