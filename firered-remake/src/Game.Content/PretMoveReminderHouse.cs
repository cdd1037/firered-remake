using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Two Island Move Reminder house, source scripts and existing town door.</summary>
public sealed class PretMoveReminderHouse
{
    public const string ResourceId = "firered-move-reminder-house-v1";
    public const string ManifestSha256 = "98898a42e59d260e8a4bd1c852de74fcd1199e4d05323f15e1fc685ca142be85";
    public const string LockSha256 = "a9c05702e03c978df40968c4412119701356edb8ff53973a01654cd3d08a8f20";
    public const string FullSourceHash = "8286d1e7a5a2ffb7103d75d374848acf244f7ff121f109f57d07fef978a24ba1";
    public static MapId HouseMapId { get; } = new("map.two-island-house");
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
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretMoveReminderHouse(SilphSourcePackage source, PretWorldMap house,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house);
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; Catalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["move-reminder-house-manifest:" + ManifestSha256, "move-reminder-house-lock:" + LockSha256,
            .. source.Files.Select(f => "move-reminder-house:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TWO_ISLAND_HOUSE" => HouseMapId,
        "MAP_TWO_ISLAND" => new("map.two-island"),
        _ => throw new InvalidDataException("Unknown Move Reminder house map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Move Reminder house must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>dependencyRoot optionally supplies the unchanged town and existing NPC atlas resources.</summary>
    public static PretMoveReminderHouse Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "move-reminder-house", ResourceId,
            "scripts/move-reminder-house.lock.json", LockSha256, 20410, ManifestSha256, 3446, 67, 15);
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Move Reminder house actor collection differs.");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in npc.EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && texture.GetProperty("reuseExisting").GetBoolean() &&
                graphic is "OBJ_EVENT_GFX_FAT_MAN" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Move Reminder house actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Move Reminder house catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Two Island Move Reminder house; service rules and current-game admission are separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" &&
            hash == outputs[path].GetProperty("sha256").GetString() &&
            spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
            "Move Reminder house compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
            spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 11 && runtime.Height == 9 &&
            runtime.Events.GetProperty("layout").GetString() == "LAYOUT_HOUSE3", "Move Reminder house layout differs.");
        foreach (var artifact in runtime.Artifacts)
            Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                "Move Reminder house image binding differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 1 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Move Reminder original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_FAT_MAN" &&
            actor.GetProperty("x").GetInt32() == 7 && actor.GetProperty("y").GetInt32() == 5 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("movement_type").GetString() == "MOVEMENT_TYPE_FACE_DOWN" &&
            actor.GetProperty("script").GetString() == "TwoIsland_House_EventScript_MoveManiac", "Move Reminder original actor differs.");
        var door = house.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == 4 && door.GetProperty("y").GetInt32() == 7 &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == "MAP_TWO_ISLAND" &&
            door.GetProperty("dest_warp_id").GetString() == "1", "Move Reminder original exit differs.");
        ValidateTown(source, dependencyRoot);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 31 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Move Reminder house script closure differs.");
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
                Need(label.Length > 0, "Missing Move Reminder house script label.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["CHECKED_MUSHROOMS"] == 1 && constants["HAS_BIG_MUSHROOM"] == 2 &&
            constants["HAS_TINY_MUSHROOMS"] == 3 && constants["HAS_BOTH_MUSHROOMS"] == 4 &&
            constants["MULTICHOICE_MUSHROOMS"] == 51 && constants["PARTY_SIZE"] == 6,
            "Move Reminder original temporary flags/menu constants differ.");
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
        Need(JsonElement.DeepEquals(data.GetProperty("events"), SilphSourcePackage.Json(source.Raw["data/maps/TwoIsland/map.json"])),
            "Retained Two Island events differ from the original source.");
        var entrance = data.GetProperty("events").GetProperty("warp_events")[1];
        Need(entrance.GetProperty("x").GetInt32() == 33 && entrance.GetProperty("y").GetInt32() == 9 &&
            entrance.GetProperty("elevation").GetInt32() == 0 && entrance.GetProperty("dest_map").GetString() == "MAP_TWO_ISLAND_HOUSE" &&
            entrance.GetProperty("dest_warp_id").GetString() == "0", "Original Two Island house entrance differs.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Move Reminder house must not rewrite its original town entrance.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
