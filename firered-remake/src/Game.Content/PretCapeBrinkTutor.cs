using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Cape Brink and tutor house; source-authenticated teaching definitions and unchanged Two Island edge.</summary>
public sealed class PretCapeBrinkTutor
{
    public const string ResourceId = "firered-cape-brink-tutor-v1";
    public const string ManifestSha256 = "94fad389e227029032d0b1f6c0dd47f96b968d7325553f8e6fc50f3073682580";
    public const string LockSha256 = "d9e511b58010d02960ddfc4e07ad8e60482bf468e6ce2f22167d01ba2e573989";
    public const string FullSourceHash = "1495452181ed682cbc54cb395b7aa1b9af020d573aa3f5d9834f12acd37af501";
    public const string CapeScriptSha256 = "426e9e99b72d36fafcc318c25892429231009b30756c03cff72d36de04171543";
    public const string HouseScriptSha256 = "559e27f2985f58bb7dcf6e54fe25a1fdfbb0385040bc8f14c33df45c5dec9dba";
    public static MapId CapeMapId { get; } = new("map.two-island-cape-brink");
    public static MapId HouseMapId { get; } = new("map.two-island-cape-brink-house");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public JsonElement EncountersAndItems { get; }
    public JsonElement NativeInventory { get; }
    private readonly SilphSourcePackage _source;
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretCapeBrinkTutor(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        _source = source;
        EncountersAndItems = source.Document("encounters-items.json"); NativeInventory = source.Document("native.json");
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["cape-brink-tutor-manifest:" + ManifestSha256, "cape-brink-tutor-lock:" + LockSha256,
            .. source.Files.Select(f => "cape-brink-tutor:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TWO_ISLAND_CAPE_BRINK" => CapeMapId,
        "MAP_TWO_ISLAND_CAPE_BRINK_HOUSE" => HouseMapId,
        "MAP_TWO_ISLAND" => new("map.two-island"),
        _ => throw new InvalidDataException("Unknown Cape Brink map symbol: " + symbol)
    };

    public CapeBrinkTutorCatalog AuthenticateTutor(PokemonCatalog owned) => CapeBrinkTutorCatalog.Authenticate(owned,
        _source.Raw["src/field_specials.c"], _source.Raw["src/party_menu.c"], _source.Raw["data/scripts/move_tutors.inc"]);

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == CapeMapId || map.Id == HouseMapId, "Cape Brink map scope differs.");
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(
                c.GetProperty("direction").GetString() == "down" ? Direction.South : throw new InvalidDataException("Cape Brink direction differs."),
                c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretCapeBrinkTutor Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "cape-brink-tutor", ResourceId,
            "scripts/cape-brink-tutor.lock.json", LockSha256, 30702, ManifestSha256, 5450, 101, 26);
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_CHANNELER", "OBJ_EVENT_GFX_RED_NORMAL"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!) && texture.GetProperty("reuseExisting").GetBoolean(),
                "Cape Brink actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        Need(graphics.Count == 0, "Cape Brink actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 2 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Cape Brink catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Cape Brink and ultimate tutor house; current-game admission is separate.", ResourceId, source.Files);
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
                "Cape Brink compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
                spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Cape Brink source identity differs.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                    "Cape Brink image binding differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.FieldWarps.Length == 1 && map.Events.GetProperty("coord_events").GetArrayLength() == 0,
                "Cape Brink event scope differs.");
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals([CapeMapId, HouseMapId]), "Cape Brink map collection differs.");
        ValidateMaps(source, maps);
        ValidateAnchor(source, dependencyRoot);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 76 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Cape Brink script closure differs.");
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
                Need(label.Length > 0, "Missing Cape Brink script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["MOVETUTOR_FRENZY_PLANT"] == 15 && constants["MOVETUTOR_BLAST_BURN"] == 16 &&
            constants["MOVETUTOR_HYDRO_CANNON"] == 17 && constants["LOCALID_CAPE_BRINK_TUTOR"] == 1,
            "Cape Brink teaching indexes differ.");
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }

    private static void ValidateMaps(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap>.Builder maps)
    {
        var cape = maps[CapeMapId]; var house = maps[HouseMapId];
        Need(cape.Width == 24 && cape.Height == 40 && house.Width == 11 && house.Height == 9 &&
            cape.Cells.Length + house.Cells.Length == 1059 && cape.BorderCells.Length + house.BorderCells.Length == 8,
            "Cape Brink source geometry differs.");
        foreach (var map in maps.Values)
            Need(JsonElement.DeepEquals(map.Events, SilphSourcePackage.Json(source.Raw[map.SourceRef.Path])),
                "Cape Brink original event array differs.");
        Need(cape.Events.GetProperty("object_events").GetArrayLength() == 0 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            cape.Events.GetProperty("bg_events").GetArrayLength() == 2 && house.Events.GetProperty("bg_events").GetArrayLength() == 0,
            "Cape Brink original actor/item counts differ.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("local_id").GetString() == "LOCALID_CAPE_BRINK_TUTOR" &&
            actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_CHANNELER" &&
            actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("script").GetString() == "TwoIsland_CapeBrink_House_EventScript_StarterTutor", "Original Cape Brink actor differs.");
        Need(cape.Warps[0].DestinationMapId == HouseMapId && cape.Warps[0].DestinationWarpId == 0 &&
            house.Warps[0].DestinationMapId == CapeMapId && house.Warps[0].DestinationWarpId == 0,
            "Original Cape Brink reciprocal doors differ.");
        Need(ConnectedMap(cape).Connections.Length == 1 && ConnectedMap(house).Connections.IsEmpty,
            "Cape Brink connected map scope differs.");
    }

    private static void ValidateAnchor(SilphSourcePackage source, string dependencyRoot)
    {
        var anchors = source.Document("contracts.json").GetProperty("retainedAnchors");
        Need(anchors.GetArrayLength() == 1, "Cape Brink adjacent-map scope differs.");
        var retained = anchors[0]; string package = retained.GetProperty("currentPackage").GetString()!;
        _ = SafariSource.Read(dependencyRoot, package + "/manifest.json", retained.GetProperty("currentManifestSha256").GetString()!,
            retained.GetProperty("currentManifestByteLength").GetInt32());
        var resource = retained.GetProperty("currentResource");
        var current = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot, package + "/" + resource.GetProperty("path").GetString(),
            resource.GetProperty("sha256").GetString()!, resource.GetProperty("byteLength").GetInt32()));
        var events = current.GetProperty("events");
        Need(JsonElement.DeepEquals(events, SilphSourcePackage.Json(source.Raw["data/maps/TwoIsland/map.json"])),
            "Retained Two Island events differ from original source.");
        var edge = events.GetProperty("connections").EnumerateArray().Single(c => c.GetProperty("map").GetString() == "MAP_TWO_ISLAND_CAPE_BRINK");
        Need(JsonElement.DeepEquals(edge, retained.GetProperty("connection")) && edge.GetProperty("direction").GetString() == "up" &&
            edge.GetProperty("offset").GetInt32() == 24 && retained.GetProperty("geometryChanges").GetArrayLength() == 0 &&
            retained.GetProperty("eventChanges").GetArrayLength() == 0, "Original Two Island edge differs.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
