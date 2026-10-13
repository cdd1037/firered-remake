using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated resources for three cave maps. This is not a game profile or runtime admission.</summary>
public sealed class PretCeruleanCaveRegion
{
    public const string ResourceId = "firered-cerulean-cave-resources-v1";
    public const string ManifestSha256 = "7861f71f2f1dae78cef47248f13999f12bc862a1ff086378a66826311d487016";
    public const string LockSha256 = "a58256d497d92e1c97e48f9f5ae63c64566609ffc88c16337d086757efae8e59";
    public const string FullSourceHash = "88522e728fd860a8144071dc1f78223ccb33a32d12d50970ac4d572b6cd6f083";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Encounters { get; }
    public JsonElement Items { get; }
    public JsonElement Reachability { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretCeruleanCaveRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants;
        SourceFiles = source.Files; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        Encounters = source.Document("encounters.json"); Items = source.Document("items.json");
        Reachability = source.Document("reachability.json");
        AdditionalSourceHashes = ["cerulean-cave-manifest:" + ManifestSha256, "cerulean-cave-lock:" + LockSha256,
            .. source.Files.Select(f => "cerulean-cave:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CERULEAN_CAVE_1F" => new("map.cerulean-cave-1f"),
        "MAP_CERULEAN_CAVE_2F" => new("map.cerulean-cave-2f"),
        "MAP_CERULEAN_CAVE_B1F" => new("map.cerulean-cave-b1f"),
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        _ => throw new InvalidDataException("Unknown Cerulean Cave map symbol: " + symbol)
    };

    public static PretCeruleanCaveRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot, "cerulean-cave", ResourceId,
            "scripts/cerulean-cave-resources.lock.json", LockSha256, 27806,
            ManifestSha256, 7550, 91, 40);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Cerulean Cave catalog source identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original cave resources; current-game admission is separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
            Need(path == relative + "/runtime-map.json" && hash == outputs[path].GetProperty("sha256").GetString() &&
                spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
                "Cerulean Cave compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
                spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 40 && runtime.Height == 23,
                "Cerulean Cave map identity/dimensions differ.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                    "Cerulean Cave image binding differs.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata,
                PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(source.Directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Keys.ToHashSet().SetEquals(new[] { new MapId("map.cerulean-cave-1f"), new MapId("map.cerulean-cave-2f"), new MapId("map.cerulean-cave-b1f") }) &&
            maps.Values.Sum(m => m.FieldWarps.Length) == 15, "Cerulean Cave map/warp collection differs.");
        // The existing city already has the original cave entrance and guard.
        // Authenticate that retained map instead of constructing a competing copy.
        var entrance = source.Document("contracts.json").GetProperty("cityEntrance");
        var city = entrance.GetProperty("currentResource");
        _ = SafariSource.Read(projectRoot, entrance.GetProperty("currentPackage").GetString() + "/" + city.GetProperty("path").GetString(),
            city.GetProperty("sha256").GetString()!, city.GetProperty("byteLength").GetInt32());
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 80 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Cerulean Cave script inventory differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing Cerulean Cave script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
