using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original Four Island Day Care, source scripts and existing town door.</summary>
public sealed class PretFourIslandDayCare
{
    public const string ResourceId = "firered-four-island-daycare-v1";
    public const string ManifestSha256 = "56dd8087f99f9d31f63b768ca7bd3675d7ca435dc32da6fc730c0678ef5bc7e7";
    public const string LockSha256 = "dded46c8749c613209f5a867ec578faca6b8fd706bcfef20d293acb5d0fae7c3";
    public const string FullSourceHash = "cd27ff7a7df3fa4046f26a81be83bb7f7e25db91fcbe8b9440fcda24c4790de0";
    public static MapId HouseMapId { get; } = new("map.four-island-pokemon-day-care");
    private readonly ImmutableDictionary<string, ImmutableArray<byte>> _sourceBytes;
    public static MapId DayCareMapId => HouseMapId;
    public const string EntryScript = "FourIsland_PokemonDayCare_EventScript_DaycareWoman";
    public const string ManEntryScript = "FourIsland_EventScript_DaycareMan";
    public const string PendingEggPositionScript = "FourIsland_EventScript_TrySetDayCareManPos";
    public const string ExteriorScriptSha256 = "e9d41ce813c9c853df0ccd4d54dc03f3b0bd2a18f75645f32fa19785372d7e76";
    public const string MapScriptSha256 = "422d698fd61cc5283326504d2f180c2e3d7118a2ecb62b76e3b4491818fa4717";
    public const string SharedScriptSha256 = "cefd9875f288efb807dc38a4d4dbd50dd75cb34bcb132e1f24e13d8d721705a8";
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

    private PretFourIslandDayCare(SilphSourcePackage source, PretWorldMap house,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        _sourceBytes = source.Raw.ToImmutableDictionary(p => p.Key,
            p => ImmutableArray.CreateRange(p.Value), StringComparer.Ordinal);
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house);
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["four-island-daycare-manifest:" + ManifestSha256, "four-island-daycare-lock:" + LockSha256,
            .. source.Files.Select(f => "four-island-daycare:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" => HouseMapId,
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        _ => throw new InvalidDataException("Unknown Four Island Day Care map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Four Island Day Care must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Returns a copy of a source blob already authenticated by this package.</summary>
    public byte[] CopySource(string sourcePath) => _sourceBytes.TryGetValue(sourcePath, out var bytes)
        ? bytes.ToArray() : throw new InvalidDataException("Source is outside Four Island Day Care: " + sourcePath);

    /// <summary>Load map/script evidence before compiling campaign scripts. dependencyRoot supplies unchanged town/NPC resources.</summary>
    public static PretFourIslandDayCare Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "four-island-daycare", ResourceId,
            "scripts/four-island-daycare.lock.json", LockSha256, 24986, ManifestSha256, 3591, 82, 15);
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 3, "Four Island Day Care actor collection differs.");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in npc.EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && texture.GetProperty("reuseExisting").GetBoolean() &&
                graphic is "OBJ_EVENT_GFX_OLD_WOMAN" or "OBJ_EVENT_GFX_OLD_MAN_1" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Four Island Day Care actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Four Island Day Care catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Four Island Day Care; service rules and current-game admission are separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" &&
            hash == outputs[path].GetProperty("sha256").GetString() &&
            spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
            "Four Island Day Care compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
            spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 12 && runtime.Height == 9 &&
            runtime.Events.GetProperty("layout").GetString() == "LAYOUT_FOUR_ISLAND_POKEMON_DAY_CARE", "Four Island Day Care layout differs.");
        foreach (var artifact in runtime.Artifacts)
            Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                "Four Island Day Care image binding differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 1 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Four Island Day Care original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_OLD_WOMAN" &&
            actor.GetProperty("x").GetInt32() == 2 && actor.GetProperty("y").GetInt32() == 2 && actor.GetProperty("elevation").GetInt32() == 3 &&
            actor.GetProperty("movement_type").GetString() == "MOVEMENT_TYPE_FACE_DOWN" &&
            actor.GetProperty("script").GetString() == EntryScript, "Four Island Day Care original actor differs.");
        var door = house.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == 4 && door.GetProperty("y").GetInt32() == 7 &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == "MAP_FOUR_ISLAND" &&
            door.GetProperty("dest_warp_id").GetString() == "1", "Four Island Day Care original exit differs.");
        ValidateTown(source, dependencyRoot);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 58 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Four Island Day Care script closure differs.");
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
                Need(label.Length > 0, "Missing Four Island Day Care script label.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["PARTY_SIZE"] == 6 && constants["GAME_STAT_USED_DAYCARE"] == 47 &&
            constants["YES"] == 1 && constants["MUS_SEVII_45"] == 336 && constants["FLAG_PENDING_DAYCARE_EGG"] == 614 &&
            constants["LOCALID_FOUR_ISLAND_DAYCARE_MAN"] == 1 && constants["LOCALID_FOUR_ISLAND_DAYCARE_WOMAN"] == 1 &&
            constants["DAYCARE_NO_MONS"] == 0 && constants["DAYCARE_EGG_WAITING"] == 1 &&
            constants["DAYCARE_ONE_MON"] == 2 && constants["DAYCARE_TWO_MONS"] == 3 && constants["DAYCARE_EXITED_LEVEL_MENU"] == 2,
            "Four Island Day Care source party/stat/music constants differ.");
        Need(inventory.GetProperty("futureUnsupportedRoots")[0].GetString() == "EventScript_EggHatch" &&
            inventory.GetProperty("futureUnsupportedSpecials")[0].GetString() == "EggHatch" &&
            !source.Document("contracts.json").GetProperty("service").GetProperty("eggHatchingAdmitted").GetBoolean(),
            "Shared egg-hatch evidence must remain outside this map package admission.");
        var closure = inventory.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("label").GetString()!).ToHashSet(StringComparer.Ordinal);
        Need(closure.Contains(EntryScript) && closure.Contains(ManEntryScript) && closure.Contains(PendingEggPositionScript) &&
            inventory.GetProperty("excludedRoots").EnumerateArray().All(r => !closure.Contains(r.GetString()!)),
            "Four Island Day Care closure admits an unrelated entry.");
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
        Need(JsonElement.DeepEquals(data.GetProperty("events"), SilphSourcePackage.Json(source.Raw["data/maps/FourIsland/map.json"])),
            "Retained Four Island events differ from the original source.");
        var entrance = data.GetProperty("events").GetProperty("warp_events")[1];
        Need(entrance.GetProperty("x").GetInt32() == 12 && entrance.GetProperty("y").GetInt32() == 13 &&
            entrance.GetProperty("elevation").GetInt32() == 0 && entrance.GetProperty("dest_map").GetString() == "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" &&
            entrance.GetProperty("dest_warp_id").GetString() == "0", "Original Four Island house entrance differs.");
        var man = data.GetProperty("events").GetProperty("object_events")[0];
        Need(man.GetProperty("local_id").GetString() == "LOCALID_FOUR_ISLAND_DAYCARE_MAN" &&
            man.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_OLD_MAN_1" &&
            man.GetProperty("x").GetInt32() == 16 && man.GetProperty("y").GetInt32() == 13 &&
            man.GetProperty("elevation").GetInt32() == 3 && man.GetProperty("script").GetString() == ManEntryScript &&
            JsonElement.DeepEquals(man, retained.GetProperty("dayCareMan").GetProperty("actor")),
            "Original exterior Day Care man differs.");
        Need(retained.GetProperty("dayCareMan").GetProperty("pendingEggPosition").GetProperty("x").GetInt32() == 16 &&
            retained.GetProperty("dayCareMan").GetProperty("pendingEggPosition").GetProperty("y").GetInt32() == 14,
            "Original pending-egg exterior position differs.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Four Island Day Care must not rewrite its original town entrance.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
