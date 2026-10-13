using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated original Saffron Fighting Dojo resources. This is not a game profile or runtime admission.</summary>
public sealed class PretFightingDojoRegion
{
    public const string ResourceId = "firered-fighting-dojo-region-v1";
    public const string ManifestSha256 = "7cf0235aee05b38e7a65edac1b39d00cb783b51ce81f216df60aa26330c22c33";
    public const string LockSha256 = "ce0b4cf909b30f0d1d22cc2466b2c284756a762eeba43a2209add7950debc441";
    public const string FullSourceHash = "8a0e9fc0af0b9ec99d3c22feb4fd19f60b9ac904df7a215f7fd2c2c6f7ca8518";
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
    public string GeneratedDirectory { get; }
    /// <summary>Contains authenticated project-relative bindings to existing NPC textures.</summary>
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretFightingDojoRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants;
        SourceFiles = source.Files; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["fighting-dojo-region-manifest:" + ManifestSha256, "fighting-dojo-region-lock:" + LockSha256,
            .. source.Files.Select(f => "fighting-dojo-region:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_DOJO" => new("map.saffron-city-dojo"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Saffron Fighting Dojo map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == new MapId("map.saffron-city-dojo") && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Saffron Fighting Dojo must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretFightingDojoRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot, "fighting-dojo-region", ResourceId,
            "scripts/fighting-dojo-region.lock.json", LockSha256, 23429,
            ManifestSha256, 3468, 77, 15);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(texture.GetProperty("reuseExisting").GetBoolean(), "Dojo NPC textures must use existing resources.");
            _ = SafariSource.Read(projectRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Saffron Fighting Dojo catalog source identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Saffron Fighting Dojo resources; current-game admission is separate.", ResourceId, source.Files);
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
                "Saffron Fighting Dojo compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
                spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 13 && runtime.Height == 16,
                "Saffron Fighting Dojo map identity/dimensions differ.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                    "Saffron Fighting Dojo image binding differs.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata,
                PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/silph-region/npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Count == 1 && maps.ContainsKey(new("map.saffron-city-dojo")) && maps.Values.Single().FieldWarps.Length == 3,
            "Saffron Fighting Dojo map/warp collection differs.");
        var retained = source.Document("contracts.json").GetProperty("retainedCity");
        var route = retained.GetProperty("currentResource");
        _ = SafariSource.Read(projectRoot, retained.GetProperty("currentPackage").GetString() + "/" + route.GetProperty("path").GetString(),
            route.GetProperty("sha256").GetString()!, route.GetProperty("byteLength").GetInt32());
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 67 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Saffron Fighting Dojo script inventory differs.");
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
                Need(label.Length > 0, "Missing Saffron Fighting Dojo script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["LOCALID_KARATE_MASTER"] == 5 && constants["FLAG_GOT_HITMON_FROM_DOJO"] == 0x278 &&
            constants["FLAG_HIDE_DOJO_HITMONLEE_BALL"] == 0x60 && constants["FLAG_HIDE_DOJO_HITMONCHAN_BALL"] == 0x61 &&
            constants["SPECIES_HITMONLEE"] == 106 && constants["SPECIES_HITMONCHAN"] == 107,
            "Dojo actor and gift constants differ.");
        var events = maps.Values.Single().Events;
        Need(events.GetProperty("object_events").GetArrayLength() == 7 && events.GetProperty("coord_events").GetArrayLength() == 2 &&
            events.GetProperty("bg_events").GetArrayLength() == 4, "Dojo original event cohort differs.");
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
