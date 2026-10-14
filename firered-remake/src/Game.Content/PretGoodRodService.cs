using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretGoodRodTablesDocument(int SchemaVersion, string ResourceId, string SourceCommit,
    string Rod, ImmutableArray<OldRodEncounterTable> Tables, ImmutableArray<OldRodEncounterTable> SourceOnlyTables);

/// <summary>Authenticates the original Good Rod house, key item and loaded ordinary Kanto tables. Admission belongs to the current composition.</summary>
public sealed class PretGoodRodService
{
    public const string ResourceId = "firered-good-rod-service-v1";
    public const string ManifestSha256 = "e093d3c2f38a311daa35a8313d7f3efe9fb5dc78765633e1c25cad655a3ff249";
    public const string LockSha256 = "58e10882454fa2fdf093be40c46d24c1aaad5e9ada3caaa9793cb88c4e2f5075";
    public const string FullSourceHash = "768dd8646bfd349cb1453f6befcab548ac403b059b9604f9889e9a3044058aa1";
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PewterMachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    /// <summary>Exactly 22 loaded ordinary Kanto tables in ordinal map-ID order, with original slot indexes 2, 3, 4.</summary>
    public ImmutableArray<OldRodEncounterTable> GoodRodTables { get; }
    /// <summary>Evidence only: four Safari tables and four unloaded Route19/20/Seafoam tables. Never use for ordinary admission.</summary>
    public ImmutableArray<OldRodEncounterTable> SourceOnlyTables { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretGoodRodService(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants, PretCityServiceItemsDocument items, PretGoodRodTablesDocument tables)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants;
        SourceFiles = source.Files; GeneratedDirectory = source.Directory; Items = items.Items; Icons = items.Icons;
        GoodRodTables = tables.Tables; SourceOnlyTables = tables.SourceOnlyTables;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["good-rod-service-manifest:" + ManifestSha256, "good-rod-service-lock:" + LockSha256,
            .. source.Files.Select(f => "good-rod-service:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FUCHSIA_CITY_HOUSE2" => HouseMapId,
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        _ => throw new InvalidDataException("Unknown Good Rod service map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Good Rod house must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretGoodRodService Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot, "good-rod-service", ResourceId,
            "scripts/good-rod-service.lock.json", LockSha256, 23816, ManifestSha256, 4116, 79, 18);
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Good Rod actor collection differs.");
        foreach (var texture in npc.EnumerateArray())
        {
            Need(texture.GetProperty("reuseExisting").GetBoolean() &&
                texture.GetProperty("graphicsId").GetString() is "OBJ_EVENT_GFX_FISHER" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Good Rod actors must reuse the original existing resources.");
            _ = SafariSource.Read(projectRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Good Rod catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Good Rod service resources; current-game admission is separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" &&
            hash == outputs[path].GetProperty("sha256").GetString() &&
            spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
            "Good Rod compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
            spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 10 && runtime.Height == 10,
            "Good Rod house dimensions differ.");
        foreach (var artifact in runtime.Artifacts)
            Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                "Good Rod house image binding differs.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 4 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0,
            "Good Rod original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("x").GetInt32() == 6 && actor.GetProperty("y").GetInt32() == 5 &&
            actor.GetProperty("script").GetString() == "FuchsiaCity_House2_EventScript_FishingGurusBrother",
            "Good Rod original actor differs.");
        var retained = source.Document("contracts.json").GetProperty("retainedCity");
        var city = retained.GetProperty("currentResource");
        _ = SafariSource.Read(projectRoot, retained.GetProperty("currentPackage").GetString() + "/" + city.GetProperty("path").GetString(),
            city.GetProperty("sha256").GetString()!, city.GetProperty("byteLength").GetInt32());
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 29 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Good Rod script closure differs.");
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
                Need(label.Length > 0, "Missing Good Rod script label.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["ITEM_GOOD_ROD"] == 263 && constants["FLAG_GOT_GOOD_ROD"] == 0x244 && constants["GOOD_ROD"] == 1,
            "Good Rod item, flag or rod constants differ.");
        var items = source.Parse<PretCityServiceItemsDocument>("items.json");
        Need(items.SchemaVersion == 1 && items.Profile == ResourceId && items.SourceCommit == PretWorldMap.SourceCommit &&
            items.Items.Length == 1 && items.Icons.Length == 1, "Good Rod item collection differs.");
        var item = items.Items[0].Item; var icon = items.Icons[0];
        Need(item.Id.Value == "pret:ITEM_GOOD_ROD" && item.SourceItemId == 263 && item.SecondaryId == 1 &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && item.Importance == 1 && item.Registrability == 1 &&
            item.FieldUseFunction == "FieldUseFunc_Rod" && item.BattleUsage == 0 && !item.CanToss && !item.CanSell && !item.CanGive,
            "Good Rod item declaration differs.");
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && Convert.FromBase64String(icon.RgbaBase64).Length == 24 * 24 * 4 &&
            SafariSource.Hash(Convert.FromBase64String(icon.RgbaBase64)) == icon.RgbaSha256 &&
            SafariSource.Hash(source.Outputs[icon.PngPath]) == icon.PngSha256, "Good Rod icon differs.");
        source.Reference(item.Source); source.Reference(items.Items[0].DescriptionSource);
        source.Reference(icon.PaletteSource); source.Reference(icon.BindingSource);
        Need(source.Files.Any(f => f.Path == icon.Source.Path && f.Sha256 == icon.Source.Sha256), "Good Rod icon source binding differs.");
        var tables = source.Parse<PretGoodRodTablesDocument>("good-rod-tables.json");
        ValidateTables(source, tables);
        return new(source, ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house), scripts.ToImmutable(), texts.ToImmutable(), constants, items, tables);
    }

    private static void ValidateTables(SilphSourcePackage source, PretGoodRodTablesDocument document)
    {
        Need(document.SchemaVersion == 1 && document.ResourceId == ResourceId && document.SourceCommit == PretWorldMap.SourceCommit &&
            document.Rod == "good" && document.Tables.Length == 22 && document.SourceOnlyTables.Length == 8,
            "Good Rod table collection differs.");
        using var wild = JsonDocument.Parse(source.Raw[OldRodEncounterRules.SourcePath]);
        var group = wild.RootElement.GetProperty("wild_encounter_groups").EnumerateArray().Single(g => g.GetProperty("label").GetString() == "gWildMonHeaders");
        var originals = group.GetProperty("encounters").EnumerateArray()
            .Where(e => e.GetProperty("base_label").GetString()!.EndsWith("_FireRed", StringComparison.Ordinal) && e.TryGetProperty("fishing_mons", out _))
            .ToDictionary(e => e.GetProperty("base_label").GetString()!, StringComparer.Ordinal);
        var all = document.Tables.Concat(document.SourceOnlyTables).ToArray();
        Need(all.Select(t => t.Map).Distinct().Count() == 30 && document.Tables.Select(t => t.Map.Value)
            .SequenceEqual(document.Tables.Select(t => t.Map.Value).Order(StringComparer.Ordinal)), "Good Rod table IDs/order differ.");
        foreach (var table in all)
        {
            Need(table.SourceCommit == PretWorldMap.SourceCommit && table.SourcePath == OldRodEncounterRules.SourcePath &&
                table.SourceSha256 == OldRodEncounterRules.SourceSha256 && table.Slots.Length == 3 &&
                table.Slots.Select(s => s.Index).SequenceEqual([2, 3, 4]) && table.Slots.Select(s => s.Weight).SequenceEqual([60, 20, 20]),
                "Good Rod source slot identity differs.");
            var original = originals[table.SourceSymbol];
            string stable = "map." + original.GetProperty("map").GetString()![4..].ToLowerInvariant().Replace('_', '-');
            stable = Regex.Replace(stable, @"route(\d+)", "route-$1");
            var fishing = original.GetProperty("fishing_mons");
            Need(table.Map.Value == stable && table.RawEncounterRate == fishing.GetProperty("encounter_rate").GetInt32(), "Good Rod original map/rate differs.");
            foreach (var slot in table.Slots)
            {
                var mon = fishing.GetProperty("mons")[slot.Index];
                Need(slot.Species.Value == "pret:" + mon.GetProperty("species").GetString() &&
                    slot.MinimumLevel == mon.GetProperty("min_level").GetInt32() && slot.MaximumLevel == mon.GetProperty("max_level").GetInt32(),
                    "Good Rod original species/levels differ.");
            }
        }
        Need(document.SourceOnlyTables.Count(t => t.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)) == 4 &&
            document.Tables.All(t => !t.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal) &&
                !t.Map.Value.StartsWith("map.seafoam-", StringComparison.Ordinal) && t.Map.Value is not "map.route-19" and not "map.route-20"),
            "Good Rod source-only map boundary differs.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
