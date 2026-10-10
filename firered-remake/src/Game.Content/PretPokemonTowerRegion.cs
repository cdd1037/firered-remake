using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated seven-floor source increment. Existing Fuji house bytes and all progression remain owned by the caller.</summary>
public sealed partial class PretPokemonTowerRegion
{
    public const string Profile = "firered-pokemon-tower-region-v1";
    public const string FullSourceHash = "b70e9238770d5952aff6c8a075a5de1447e947cf6c31428b54bb8d77a7b03e51";
    public const string LockSha256 = "35ee161c56cb74091f695faa506adc1443ac700e2165705db380c0d12cbf76e6";
    public const string ManifestSha256 = "c590a16d66a62b1d29236e84b5a737920189ac1cd2ccce989f0d64d37e2416d5";
    public static MapId HouseMapId => new("map.lavender-town-volunteer-pokemon-house");
    public static MapId FloorMapId(int floor) => floor is >= 1 and <= 7 ? new("map.pokemon-tower-" + floor + "f") : throw new ArgumentOutOfRangeException(nameof(floor));
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public PretRuntimeMapData ExistingHouse { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<int> InitiallySetFlags { get; }
    public JsonElement SourceContract { get; }
    public JsonElement ExternalLavenderEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");
    public bool RuntimeIntegrated => false;
    public JsonElement Dependencies => ReadDocument("dependencies.json");
    public JsonElement Mechanics => ReadDocument("mechanics.json");
    public JsonElement ScriptInventory => ReadDocument("scripts.json");
    public JsonElement NpcSources => ReadDocument("npc-source.json");
    public JsonElement ConstantSources => ReadDocument("constants-source.json");
    private readonly ImmutableDictionary<string, (string Hash, int Length)> _outputs;
    private readonly string _rawDirectory;

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        var m = Regex.Match(symbol, @"^MAP_POKEMON_TOWER_([1-7])F$", RegexOptions.CultureInvariant);
        return m.Success ? FloorMapId(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : PretRocketHideoutRegion.MapIdFromSourceSymbol(symbol);
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        Need(source.ValueKind == JsonValueKind.Null, "Tower maps must retain their original disconnected indoor topology.");
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretPokemonTowerRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot); var directory = Path.Combine(project, "content/generated/pokemon-tower-region");
        var runtime = runtimeMaps ?? PretPokemonTowerRuntimeMaps.Load(project);
        Need(runtime.ManifestSha256 == PretPokemonTowerRuntimeMaps.ManifestSha256, "Tower requires the authenticated 127-map catalog.");
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256, 128 * 1024));
        var m = manifest.RootElement;
        Need(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("sourceLockSha256").GetString() == LockSha256, "Tower region identity differs.");
        var outputs = m.GetProperty("outputs").EnumerateArray().ToImmutableDictionary(x => x.GetProperty("path").GetString()!,
            x => (Hash: x.GetProperty("sha256").GetString()!, Length: x.GetProperty("byteLength").GetInt32()), StringComparer.Ordinal);
        byte[] ReadOutput(string path) { var e = outputs[path]; return ReadPinned(directory, path, e.Hash, e.Length, e.Length); }
        JsonElement Document(string path) { using var d = JsonDocument.Parse(ReadOutput(path)); return d.RootElement.Clone(); }
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/pokemon-tower-region.lock.json", LockSha256, 256 * 1024));
        var files = sourceLock.RootElement.GetProperty("files").EnumerateArray().Select(f => new PretWorldSourceFile(f.GetProperty("path").GetString()!,
            f.GetProperty("gitBlobSha1").GetString()!, f.GetProperty("byteLength").GetInt32(), f.GetProperty("sha256").GetString()!,
            $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{f.GetProperty("path").GetString()}")).ToImmutableArray();
        Need(files.Length == 244 && PretRuntimeMapValidation.Hash(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash, "Tower source inventory differs.");
        var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal); var rawDirectory = Path.Combine(project, "content/raw/pokemon-tower-region");
        byte[] ReadSource(string path) { var f = byPath[path]; return ReadPinned(rawDirectory, path, f.Sha256, f.ByteLength, f.ByteLength); }
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash, "Local authenticated source data.",
            "Source contracts only; executable progression must be registered by Application.", Profile, files);
        var spec = Document("runtime.json"); var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        Need(!spec.GetProperty("runtimeIntegrated").GetBoolean(), "Map data cannot claim campaign integration.");
        foreach (var s in spec.GetProperty("maps").EnumerateArray())
        {
            var id = new MapId(s.GetProperty("stableId").GetString()!); var compact = runtime.Maps[id]; var isNew = s.GetProperty("isNew").GetBoolean();
            Need(compact.SourceRef.Sha256 == byPath[compact.SourceRef.Path].Sha256 && compact.SourceRef.Symbol == s.GetProperty("sourceMapSymbol").GetString() &&
                MapIdFromSourceSymbol(compact.SourceRef.Symbol) == id, "Tower map identity differs.");
            using var original = JsonDocument.Parse(ReadSource(compact.SourceRef.Path));
            Need(JsonElement.DeepEquals(original.RootElement, compact.Events), "Tower original events differ.");
            foreach (var script in compact.Scripts)
                Need(script.Sha256 == byPath[script.Path].Sha256 && Encoding.UTF8.GetBytes(script.Text).AsSpan().SequenceEqual(ReadSource(script.Path)), "Tower embedded script differs.");
            if (!isNew) { Need(id == HouseMapId && compact.FullSourceHash == PretCeladonApproachRegion.FullSourceHash, "Existing house provenance changed."); continue; }
            Need(compact.FullSourceHash == FullSourceHash, "New Tower map source provenance differs.");
            var relative = s.GetProperty("directory").GetString()!;
            foreach (var artifact in compact.Artifacts)
            {
                var b = ReadOutput(relative + "/" + artifact.Path);
                Need(PretRuntimeMapValidation.Hash(b) == artifact.Sha256 && b.Length > 24 && BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16, 4)) == artifact.Width &&
                    BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20, 4)) == artifact.Height, "Tower PNG differs.");
            }
            maps.Add(id, PretWorldMap.FromRuntime(compact, metadata, PretRuntimeMapValidation.SafeFile(directory, relative),
                PretRuntimeMapValidation.SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 7 && maps.Values.All(x => x.Width == 24 && x.Height == 20) && maps.Values.Sum(x => x.Warps.Length) == 15, "Tower seven-floor geometry differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var p in spec.GetProperty("scriptPaths").EnumerateArray())
        {
            var path = p.GetString()!; var text = Encoding.UTF8.GetString(ReadSource(path));
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length > 0, "Tower script source has no original label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, symbol, "pret/pokefirered", PretWorldMap.SourceCommit, byPath[path].Sha256)));
            }
        }
        var constants = Document("constants.json").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
        var contract = Document("pokemon-tower.json");
        Need(contract.GetProperty("fullSourceHash").GetString() == FullSourceHash && !contract.GetProperty("runtimeIntegrated").GetBoolean() &&
            contract.GetProperty("metatilePatches").GetArrayLength() == 0, "Tower scene contract differs.");
        using var city = JsonDocument.Parse(ReadSource("data/maps/LavenderTown/map.json"));
        Need(JsonElement.DeepEquals(runtime.Maps[new("map.lavender-town")].Events, city.RootElement), "Existing Lavender events differ.");
        foreach (var entry in contract.GetProperty("lavenderEntries").EnumerateArray())
            Need(JsonElement.DeepEquals(city.RootElement.GetProperty("warp_events")[entry.GetProperty("sourceWarpId").GetInt32()], entry.GetProperty("source")), "Lavender entrance contract differs.");
        foreach (var e in outputs.Where(e => e.Key.StartsWith("npc/", StringComparison.Ordinal) || e.Key.StartsWith("presentation/", StringComparison.Ordinal))) _ = ReadOutput(e.Key);
        return new(directory, rawDirectory, maps.ToImmutable(), runtime.Maps[HouseMapId], files, scripts.ToImmutable(), texts.ToImmutable(), constants, outputs, contract, city.RootElement.Clone());
    }

    // Optional complete developer provenance audit, separate from compact map loading.
    public void VerifyAllSourceFiles()
    {
        foreach (var f in SourceFiles) _ = ReadPinned(_rawDirectory, f.Path, f.Sha256, f.ByteLength, f.ByteLength);
    }
    private JsonElement ReadDocument(string path)
    {
        var e = _outputs[path]; using var document = JsonDocument.Parse(ReadPinned(GeneratedDirectory, path, e.Hash, e.Length, e.Length)); return document.RootElement.Clone();
    }
    private static byte[] ReadPinned(string root, string path, string hash, int maximum, int? length = null)
    {
        PretRocketHideoutRuntimeMaps.RequireNoLinks(root, path);
        Need(maximum is > 0 and <= 16 * 1024 * 1024 && PretRuntimeMapValidation.IsHash(hash), "Tower file bounds/hash differ.");
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(root, path), maximum);
        Need(PretRuntimeMapValidation.Hash(bytes) == hash && (length is null || bytes.Length == length), "Tower pinned bytes differ: " + path); return bytes;
    }
    private static void Need(bool ok, string message) => PretRuntimeMapValidation.Require(ok, message);
}
