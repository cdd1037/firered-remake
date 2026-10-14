using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Fuchsia City Move Deleter house, source scripts and existing town door.</summary>
public sealed class PretMoveDeleterHouse
{
    public const string ResourceId = "firered-move-deleter-v1";
    public const string ManifestSha256 = "15719cb2f96419e7815c06ecef8ac73e4f75c9a4091d118c0184e7a8e5aa0fc3";
    public const string LockSha256 = "6fc42e3dc12d3d917d8c917b3b289e8de19b5157079791cac39efdb95da1beae";
    public const string FullSourceHash = "de0038bed8db60932e2d30352820d85779a2e352ce9c9336fc187b5cac9206ac";
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-3");
    private readonly byte[] _specialSource;
    private readonly byte[] _summarySource;
    private readonly byte[] _scriptSource;
    private readonly byte[] _stringsSource;
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
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretMoveDeleterHouse(SilphSourcePackage source, PretWorldMap house,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        _specialSource = source.Raw["src/party_menu_specials.c"];
        _summarySource = source.Raw["src/pokemon_summary_screen.c"];
        _scriptSource = source.Raw["data/maps/FuchsiaCity_House3/scripts.inc"];
        _stringsSource = source.Raw["src/strings.c"];
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house);
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["move-deleter-manifest:" + ManifestSha256, "move-deleter-lock:" + LockSha256,
            .. source.Files.Select(f => "move-deleter:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FUCHSIA_CITY_HOUSE3" => HouseMapId,
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        _ => throw new InvalidDataException("Unknown Move Deleter house map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Move Deleter house must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Bind only after composition has constructed its final owned catalog. Reuses authenticated source bytes.</summary>
    public MoveDeleterCatalog Bind(PokemonCatalog owned) => MoveDeleterCatalog.Authenticate(owned,
        _specialSource, _summarySource, _scriptSource, _stringsSource);

    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretMoveDeleterHouse Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "move-deleter", ResourceId,
            "scripts/move-deleter.lock.json", LockSha256, 21910, ManifestSha256, 3466, 72, 15);
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Move Deleter house actor collection differs.");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in npc.EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && texture.GetProperty("reuseExisting").GetBoolean() &&
                graphic is "OBJ_EVENT_GFX_BALDING_MAN" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Move Deleter house actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Move Deleter house catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Fuchsia City Move Deleter house; service rules and current-game admission are separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" &&
            hash == outputs[path].GetProperty("sha256").GetString() &&
            spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
            "Move Deleter house compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
            spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 11 && runtime.Height == 9 &&
            runtime.Events.GetProperty("layout").GetString() == "LAYOUT_HOUSE1", "Move Deleter house layout differs.");
        foreach (var artifact in runtime.Artifacts)
            Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                "Move Deleter house image binding differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 1 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Move Deleter original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_BALDING_MAN" &&
            actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("movement_type").GetString() == "MOVEMENT_TYPE_FACE_RIGHT" &&
            actor.GetProperty("script").GetString() == "FuchsiaCity_House3_EventScript_MoveDeleter", "Move Deleter original actor differs.");
        var door = house.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == 4 && door.GetProperty("y").GetInt32() == 7 &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY" &&
            door.GetProperty("dest_warp_id").GetString() == "10", "Move Deleter original exit differs.");
        ValidateTown(source, dependencyRoot);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 17 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Move Deleter house script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string sourcePath = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(raw[sourcePath]);
            if (sourcePath.EndsWith("/text.inc", StringComparison.Ordinal) || sourcePath.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(sourcePath, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing Move Deleter house script label.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["PARTY_SIZE"] == 6 && constants["MAX_MON_MOVES"] == 4 &&
            constants["YES"] == 1 && constants["MUS_MOVE_DELETED"] == 270,
            "Move Deleter source party/move/fanfare constants differ.");
        return new(source, house, scripts.ToImmutable(), texts.ToImmutable(), constants);
    }

    private static void ValidateTown(SilphSourcePackage source, string dependencyRoot)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedTown");
        string package = retained.GetProperty("currentPackage").GetString()!;
        _ = SafariSource.Read(dependencyRoot, package + "/manifest.json", retained.GetProperty("currentManifestSha256").GetString()!,
            retained.GetProperty("currentManifestByteLength").GetInt32());
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot, package + "/" + town.GetProperty("path").GetString(),
            town.GetProperty("sha256").GetString()!, town.GetProperty("byteLength").GetInt32()));
        Need(JsonElement.DeepEquals(data.GetProperty("events"), SilphSourcePackage.Json(source.Raw["data/maps/FuchsiaCity/map.json"])),
            "Retained Fuchsia City events differ from the original source.");
        var entrance = data.GetProperty("events").GetProperty("warp_events")[10];
        Need(entrance.GetProperty("x").GetInt32() == 19 && entrance.GetProperty("y").GetInt32() == 31 &&
            entrance.GetProperty("elevation").GetInt32() == 0 && entrance.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY_HOUSE3" &&
            entrance.GetProperty("dest_warp_id").GetString() == "0", "Original Fuchsia City house entrance differs.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Move Deleter house must not rewrite its original town entrance.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
