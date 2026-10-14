using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretSuperRodTablesDocument(int SchemaVersion, string ResourceId, string SourceCommit,
    string Rod, ImmutableArray<OldRodEncounterTable> Tables, ImmutableArray<OldRodEncounterTable> SourceOnlyTables);

/// <summary>Authenticates the original Super Rod house, key item and all ordinary FireRed tables. Admission belongs to the current composition.</summary>
public sealed class PretSuperRodService
{
    public const string ResourceId = "firered-super-rod-service-v1";
    public const string ManifestSha256 = "1c029101a64510b38b4c2f32443f8008c450e95ab74356ef881aed8490e12e4a";
    public const string LockSha256 = "ba9db3c59015d5af1fd0b8620dcb03ff5ec05e31444b87b70e103ab681841436";
    public const string FullSourceHash = "5449e91aef58005f8cba02609ca42a192d32bd2dae0e0065b12e9cb92686792b";
    public static MapId HouseMapId { get; } = new("map.route-12-fishing-house");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PewterMachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    /// <summary>All 46 ordinary FireRed tables in ordinal map-ID order, with original indexes 5 through 9.</summary>
    public ImmutableArray<OldRodEncounterTable> SuperRodTables { get; }
    /// <summary>Evidence only: four Safari tables. Never use for ordinary admission.</summary>
    public ImmutableArray<OldRodEncounterTable> SourceOnlyTables { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSuperRodService(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, PretCityServiceItemsDocument items, PretSuperRodTablesDocument tables)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants;
        SourceFiles = source.Files; GeneratedDirectory = source.Directory; Items = items.Items; Icons = items.Icons;
        SuperRodTables = tables.Tables; SourceOnlyTables = tables.SourceOnlyTables;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["super-rod-service-manifest:" + ManifestSha256, "super-rod-service-lock:" + LockSha256,
            .. source.Files.Select(f => "super-rod-service:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE12_FISHING_HOUSE" => HouseMapId,
        "MAP_ROUTE12" => new("map.route-12"),
        _ => throw new InvalidDataException("Unknown Super Rod service map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Super Rod house must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretSuperRodService Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "super-rod-service", ResourceId,
            "scripts/super-rod-service.lock.json", LockSha256, 24495, ManifestSha256, 4139, 81, 18);
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Super Rod actor collection differs.");
        foreach (var texture in npc.EnumerateArray())
        {
            Need(texture.GetProperty("reuseExisting").GetBoolean() &&
                texture.GetProperty("graphicsId").GetString() is "OBJ_EVENT_GFX_FISHER" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Super Rod actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Super Rod catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Super Rod service resources; current-game admission is separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" &&
            hash == outputs[path].GetProperty("sha256").GetString() &&
            spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
            "Super Rod compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
            spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 11 && runtime.Height == 9,
            "Super Rod house dimensions differ.");
        foreach (var artifact in runtime.Artifacts)
            Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                "Super Rod house image binding differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 3 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 1,
            "Super Rod original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 &&
            actor.GetProperty("script").GetString() == "Route12_FishingHouse_EventScript_FishingGuruBrother",
            "Super Rod original actor differs.");
        var retained = source.Document("contracts.json").GetProperty("retainedCity");
        var city = retained.GetProperty("currentResource");
        _ = SafariSource.Read(dependencyRoot, retained.GetProperty("currentPackage").GetString() + "/" + city.GetProperty("path").GetString(),
            city.GetProperty("sha256").GetString()!, city.GetProperty("byteLength").GetInt32());
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 60 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Super Rod script closure differs.");
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
                Need(label.Length > 0, "Missing Super Rod script label.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["ITEM_SUPER_ROD"] == 264 && constants["FLAG_GOT_SUPER_ROD"] == 0x255 && constants["SUPER_ROD"] == 2 && constants["VAR_MAGIKARP_SIZE_RECORD"] == 0x4040 &&
            constants["FLAG_GOT_RECORD_SETTING_MAGIKARP"] == 0x29C,
            "Super Rod item, flag or rod constants differ.");
        var items = source.Parse<PretCityServiceItemsDocument>("items.json");
        Need(items.SchemaVersion == 1 && items.Profile == ResourceId && items.SourceCommit == PretWorldMap.SourceCommit &&
            items.Items.Length == 1 && items.Icons.Length == 1, "Super Rod item collection differs.");
        var item = items.Items[0].Item; var icon = items.Icons[0];
        Need(item.Id.Value == "pret:ITEM_SUPER_ROD" && item.SourceItemId == 264 && item.SecondaryId == 2 &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && item.Importance == 1 && item.Registrability == 1 &&
            item.FieldUseFunction == "FieldUseFunc_Rod" && item.BattleUsage == 0 && !item.CanToss && !item.CanSell && !item.CanGive,
            "Super Rod item declaration differs.");
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && Convert.FromBase64String(icon.RgbaBase64).Length == 24 * 24 * 4 &&
            SafariSource.Hash(Convert.FromBase64String(icon.RgbaBase64)) == icon.RgbaSha256 &&
            SafariSource.Hash(source.Outputs[icon.PngPath]) == icon.PngSha256, "Super Rod icon differs.");
        source.Reference(item.Source); source.Reference(items.Items[0].DescriptionSource);
        source.Reference(icon.PaletteSource); source.Reference(icon.BindingSource);
        Need(source.Files.Any(f => f.Path == icon.Source.Path && f.Sha256 == icon.Source.Sha256), "Super Rod icon source binding differs.");
        var tables = source.Parse<PretSuperRodTablesDocument>("super-rod-tables.json");
        ValidateTables(source, tables);
        return new(source, ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house), scripts.ToImmutable(), texts.ToImmutable(), constants, items, tables);
    }

    private static void ValidateTables(SilphSourcePackage source, PretSuperRodTablesDocument document)
    {
        Need(document.SchemaVersion == 1 && document.ResourceId == ResourceId && document.SourceCommit == PretWorldMap.SourceCommit &&
            document.Rod == "super" && document.Tables.Length == 46 && document.SourceOnlyTables.Length == 4,
            "Super Rod table collection differs.");
        using var wild = JsonDocument.Parse(source.Raw[OldRodEncounterRules.SourcePath]);
        var group = wild.RootElement.GetProperty("wild_encounter_groups").EnumerateArray().Single(g => g.GetProperty("label").GetString() == "gWildMonHeaders");
        var originals = group.GetProperty("encounters").EnumerateArray()
            .Where(e => e.GetProperty("base_label").GetString()!.EndsWith("_FireRed", StringComparison.Ordinal) && e.TryGetProperty("fishing_mons", out _))
            .ToDictionary(e => e.GetProperty("base_label").GetString()!, StringComparer.Ordinal);
        var all = document.Tables.Concat(document.SourceOnlyTables).ToArray();
        Need(all.Select(t => t.Map).Distinct().Count() == 50 && document.Tables.Select(t => t.Map.Value)
            .SequenceEqual(document.Tables.Select(t => t.Map.Value).Order(StringComparer.Ordinal)), "Super Rod table IDs/order differ.");
        foreach (var table in all)
        {
            Need(table.SourceCommit == PretWorldMap.SourceCommit && table.SourcePath == OldRodEncounterRules.SourcePath &&
                table.SourceSha256 == OldRodEncounterRules.SourceSha256 && table.Slots.Length == 5 &&
                table.Slots.Select(s => s.Index).SequenceEqual([5, 6, 7, 8, 9]) && table.Slots.Select(s => s.Weight).SequenceEqual([40, 40, 15, 4, 1]),
                "Super Rod source slot identity differs.");
            var original = originals[table.SourceSymbol];
            string stable = "map." + original.GetProperty("map").GetString()![4..].ToLowerInvariant().Replace('_', '-');
            stable = Regex.Replace(stable, @"route(\d+)", "route-$1");
            var fishing = original.GetProperty("fishing_mons");
            Need(table.Map.Value == stable && table.RawEncounterRate == fishing.GetProperty("encounter_rate").GetInt32(), "Super Rod original map/rate differs.");
            foreach (var slot in table.Slots)
            {
                var mon = fishing.GetProperty("mons")[slot.Index];
                Need(slot.Species.Value == "pret:" + mon.GetProperty("species").GetString() &&
                    slot.MinimumLevel == mon.GetProperty("min_level").GetInt32() && slot.MaximumLevel == mon.GetProperty("max_level").GetInt32(),
                    "Super Rod original species/levels differ.");
            }
        }
        Need(document.SourceOnlyTables.Count(t => t.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)) == 4 &&
            document.Tables.All(t => !t.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)),
            "Super Rod source-only map boundary differs.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
