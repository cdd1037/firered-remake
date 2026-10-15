using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Saffron Trainer Fan Club, with authenticated source scripts and unchanged town entrance.</summary>
public sealed class PretTrainerFanClub
{
    public const string ResourceId = "firered-trainer-fan-club-v1";
    public const string ManifestSha256 = "bac08ccc65eadfa6192c81695afde2aa20c3df5a8d0f2900f0f06276186127a3";
    public const string LockSha256 = "a7019beb3d8a98c72ce9430c1d374fe88a38f946c9e322edce45e2a493db8e8e";
    public const string FullSourceHash = "29bd935a65fc2966a3149479f61afff74003a6303f3181004a5ebd01331120a8";
    public const string ScriptPath = "data/maps/SaffronCity_PokemonTrainerFanClub/scripts.inc";
    public const string ScriptSha256 = "fea2976333d6d2f0a348c5511ec34afe1c29cf7c1d5fb087da0b1a2401022d68";
    public const string ImporterSha256 = "321d5229729bcdf0f59b2788c96e13a107149dd479289b7e3c7d120033871dcb";
    public const string RulesImplementationSha256 = "cf392f23992daa3a1742f86880084c8367ef7fe9035091628e50216c5b30952f";
    public static MapId RoomMapId { get; } = new("map.saffron-city-pokemon-trainer-fan-club");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, string> AuthenticatedMapScriptHashes { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretTrainerFanClub(SilphSourcePackage source, PretWorldMap map,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(map.Id, map);
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        AuthenticatedMapScriptHashes = ImmutableDictionary<string, string>.Empty.Add(ScriptPath, ScriptSha256);
        AdditionalSourceHashes = ["trainer-fan-club-manifest:" + ManifestSha256,
            "trainer-fan-club-lock:" + LockSha256, "trainer-fan-club-importer:" + ImporterSha256,
            "trainer-fan-club-domain-implementation:" + RulesImplementationSha256,
            .. source.Files.Select(f => "trainer-fan-club:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB" => RoomMapId,
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Trainer Fan Club map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == RoomMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Trainer Fan Club must retain its disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>dependencyRoot supplies unchanged Saffron City and the nine existing NPC sheets.</summary>
    public static PretTrainerFanClub Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "trainer-fan-club", ResourceId,
            "scripts/trainer-fan-club.lock.json", LockSha256, 27327, ManifestSha256, 3580, 90, 15);
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_BEAUTY", "OBJ_EVENT_GFX_BLACK_BELT",
            "OBJ_EVENT_GFX_CRUSH_GIRL", "OBJ_EVENT_GFX_GENTLEMAN", "OBJ_EVENT_GFX_LITTLE_GIRL",
            "OBJ_EVENT_GFX_RED_NORMAL", "OBJ_EVENT_GFX_ROCKER", "OBJ_EVENT_GFX_WOMAN_3",
            "OBJ_EVENT_GFX_YOUNGSTER"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!) && texture.GetProperty("reuseExisting").GetBoolean(),
                "Trainer Fan Club actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        Need(graphics.Count == 0, "Trainer Fan Club actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("profile").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Trainer Fan Club catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Saffron Trainer Fan Club and original shared script closure.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
        Need(id == RoomMapId.Value && path == relative + "/runtime-map.json" &&
            hash == outputs[path].GetProperty("sha256").GetString() &&
            spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
            "Trainer Fan Club compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
            spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Trainer Fan Club map identity differs.");
        foreach (var artifact in runtime.Artifacts)
            Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                "Trainer Fan Club image binding differs.");
        var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/trainer-tower-region/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(map.Width == 11 && map.Height == 17 && map.FieldWarps.Length == 1 &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null &&
            map.Events.GetProperty("object_events").GetArrayLength() == 10 &&
            map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
            map.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            map.Events.GetProperty("map_type").GetString() == "MAP_TYPE_INDOOR" &&
            map.Events.GetProperty("music").GetString() == "MUS_PEWTER" &&
            !map.Events.GetProperty("allow_cycling").GetBoolean() &&
            !map.Events.GetProperty("allow_escaping").GetBoolean() && !map.Events.GetProperty("allow_running").GetBoolean(),
            "Trainer Fan Club original map/event collection differs.");
        ValidateTopology(source, dependencyRoot, map);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 103 &&
            inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Trainer Fan Club script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        Need(files[ScriptPath].Sha256 == ScriptSha256, "Trainer Fan Club script source differs.");
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string sourcePath = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(raw[sourcePath]);
            if (sourcePath.EndsWith("/text.inc", StringComparison.Ordinal) || sourcePath.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(sourcePath, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing Trainer Fan Club script label.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label,
                    "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["NUM_TRAINER_FAN_CLUB_MEMBERS"] == 8 && constants["(NUM_TRAINER_FAN_CLUB_MEMBERS - 1)"] == 7 &&
            constants["VAR_FANCLUB_FAN_COUNTER"] == 0x4038 && constants["VAR_FANCLUB_LOSE_FAN_TIMER"] == 0x4039 &&
            constants["VAR_MAP_SCENE_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB"] == 0x4073 && constants["FAMECHECKER_BRUNO"] == 10,
            "Trainer Fan Club original constants differ.");
        return new(source, map, scripts.ToImmutable(), texts.ToImmutable(), constants);
    }

    private static void ValidateTopology(SilphSourcePackage source, string dependencyRoot, PretWorldMap room)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedAnchor");
        string package = retained.GetProperty("currentPackage").GetString()!;
        _ = SafariSource.Read(dependencyRoot, package + "/manifest.json", retained.GetProperty("currentManifestSha256").GetString()!, 23421);
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot, package + "/" + town.GetProperty("path").GetString(),
            town.GetProperty("sha256").GetString()!, town.GetProperty("byteLength").GetInt32()));
        Need(JsonElement.DeepEquals(data.GetProperty("events"), SilphSourcePackage.Json(source.Raw["data/maps/SaffronCity/map.json"])),
            "Retained Saffron City events differ from the original source.");
        var entrance = data.GetProperty("events").GetProperty("warp_events")[14];
        var exit = room.Events.GetProperty("warp_events")[0];
        Need(Warp(entrance, 47, 21, 0, "MAP_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB", "0") &&
            Warp(exit, 5, 15, 3, "MAP_SAFFRON_CITY", "14") &&
            retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Trainer Fan Club must retain its original reciprocal town door.");
    }

    private static bool Warp(JsonElement value, int x, int y, int elevation, string map, string index) =>
        value.GetProperty("x").GetInt32() == x && value.GetProperty("y").GetInt32() == y &&
        value.GetProperty("elevation").GetInt32() == elevation && value.GetProperty("dest_map").GetString() == map &&
        value.GetProperty("dest_warp_id").GetString() == index;

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
