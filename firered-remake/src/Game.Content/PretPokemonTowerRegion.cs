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
    public static MapId HouseMapId => new("map.lavender-town-volunteer-pokemon-house");
    public static MapId FloorMapId(int floor) => floor is >= 1 and <= 7 ? new("map.pokemon-tower-" + floor + "f") : throw new ArgumentOutOfRangeException(nameof(floor));
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public PretRuntimeMapData ExistingHouse { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
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
    private readonly ImmutableHashSet<string> _outputs;
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
            using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
            Need(m.GetProperty("schemaVersion").GetInt32() == 1 , "Tower region identity differs.");
        var outputs = m.GetProperty("outputs").EnumerateArray()
            .Select(x => x.GetProperty("path").GetString()!).ToImmutableHashSet(StringComparer.Ordinal);
        byte[] ReadOutput(string path)
        {
            if (!outputs.Contains(path)) throw new InvalidDataException("Missing region output: " + path);
            return ReadPinned(directory, path);
        }
        JsonElement Document(string path) { using var d = JsonDocument.Parse(ReadOutput(path)); return d.RootElement.Clone(); }
            using var sourceLock = JsonDocument.Parse(ReadPinned(project,"scripts/pokemon-tower-region.lock.json"));
        var files = sourceLock.RootElement.GetProperty("files").EnumerateArray().Select(f => new PretWorldSourceFile(f.GetProperty("path").GetString()!,            $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{f.GetProperty("path").GetString()}")).ToImmutableArray();
        var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal); var rawDirectory = Path.Combine(project, PretSourcePaths.FireRedRawDirectory);
            byte[] ReadSource(string path) { var f = byPath[path]; return ReadPinned(rawDirectory,path); }
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, "Local authenticated source data.",
            "Source contracts only; executable progression must be registered by Application.", Profile, files);
        var spec = Document("runtime.json"); var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        Need(!spec.GetProperty("runtimeIntegrated").GetBoolean(), "Map data cannot claim campaign integration.");
        foreach (var s in spec.GetProperty("maps").EnumerateArray())
        {
            var id = new MapId(s.GetProperty("stableId").GetString()!); var compact = runtime.Maps[id]; var isNew = s.GetProperty("isNew").GetBoolean();
                Need(compact.SourceRef.Symbol == s.GetProperty("sourceMapSymbol").GetString() &&
                MapIdFromSourceSymbol(compact.SourceRef.Symbol) == id, "Tower map identity differs.");
                if (!isNew) { Need(id == HouseMapId , "Existing house provenance changed."); continue; }
            var relative = s.GetProperty("directory").GetString()!;
            foreach (var artifact in compact.Artifacts)
            {
                var b = ReadOutput(relative + "/" + artifact.Path);
                    Need(b.Length > 24 && BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16, 4)) == artifact.Width &&
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
                scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(directory);
        var contract = Document("pokemon-tower.json");
            Need(!contract.GetProperty("runtimeIntegrated").GetBoolean() &&
            contract.GetProperty("metatilePatches").GetArrayLength() == 0, "Tower scene contract differs.");
        using var city = JsonDocument.Parse(ReadSource("data/maps/LavenderTown/map.json"));
        Need(JsonElement.DeepEquals(runtime.Maps[new("map.lavender-town")].Events, city.RootElement), "Existing Lavender events differ.");
        foreach (var entry in contract.GetProperty("lavenderEntries").EnumerateArray())
            Need(JsonElement.DeepEquals(city.RootElement.GetProperty("warp_events")[entry.GetProperty("sourceWarpId").GetInt32()], entry.GetProperty("source")), "Lavender entrance contract differs.");
        foreach (var e in outputs.Where(e => e.StartsWith("npc/", StringComparison.Ordinal) || e.StartsWith("presentation/", StringComparison.Ordinal))) _ = ReadOutput(e);
        return new(directory, rawDirectory, maps.ToImmutable(), runtime.Maps[HouseMapId], files, scripts.ToImmutable(), texts.ToImmutable(), constants, outputs, contract, city.RootElement.Clone());
    }
    private JsonElement ReadDocument(string path)
    {
            if (!_outputs.Contains(path)) throw new InvalidDataException("Missing region output: " + path); using var document = JsonDocument.Parse(ReadPinned(GeneratedDirectory,path)); return document.RootElement.Clone();
    }
    private static byte[] ReadPinned(string root, string path)
    {
        PretRocketHideoutRuntimeMaps.RequireNoLinks(root, path);
        return File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, path));
    }
    private static void Need(bool ok, string message) => PretRuntimeMapValidation.Require(ok, message);
}
