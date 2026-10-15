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
    public static MapId HouseMapId { get; } = new("map.route-12-fishing-house");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
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
        var source = new SilphSourcePackage(projectRoot,"super-rod-service",            "scripts/super-rod-service.lock.json");
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Super Rod actor collection differs.");
        foreach (var texture in npc.EnumerateArray())
        {
            Need(texture.GetProperty("reuseExisting").GetBoolean() &&
                texture.GetProperty("graphicsId").GetString() is "OBJ_EVENT_GFX_FISHER" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Super Rod actors must reuse the original existing resources.");
        }
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("maps").GetArrayLength() == 1 ,
            "Super Rod catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Super Rod service resources; current-game admission is separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" ,
            "Super Rod compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Outputs[path], id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 11 && runtime.Height == 9,
            "Super Rod house dimensions differ.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 3 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 1,
            "Super Rod original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 &&
            actor.GetProperty("script").GetString() == "Route12_FishingHouse_EventScript_FishingGuruBrother",
            "Super Rod original actor differs.");
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
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret,sourcePath,label,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["ITEM_SUPER_ROD"] == 264 && constants["FLAG_GOT_SUPER_ROD"] == 0x255 && constants["SUPER_ROD"] == 2 && constants["VAR_MAGIKARP_SIZE_RECORD"] == 0x4040 &&
            constants["FLAG_GOT_RECORD_SETTING_MAGIKARP"] == 0x29C,
            "Super Rod item, flag or rod constants differ.");
        var items = source.Parse<PretCityServiceItemsDocument>("items.json");
        Need(items.SchemaVersion == 1 &&
            items.Items.Length == 1 && items.Icons.Length == 1, "Super Rod item collection differs.");
        var item = items.Items[0].Item; var icon = items.Icons[0];
        Need(item.Id.Value == "pret:ITEM_SUPER_ROD" && item.SourceItemId == 264 && item.SecondaryId == 2 &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && item.Importance == 1 && item.Registrability == 1 &&
            item.FieldUseFunction == "FieldUseFunc_Rod" && item.BattleUsage == 0 && !item.CanToss && !item.CanSell && !item.CanGive,
            "Super Rod item declaration differs.");
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && PngRgba8.Decode(source.Outputs[icon.PngPath], icon.Width, icon.Height).Length == 24 * 24 * 4 , "Super Rod icon differs.");
        var tables = source.Parse<PretSuperRodTablesDocument>("super-rod-tables.json");
        ValidateTables(source, tables);
        return new(source, ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house), scripts.ToImmutable(), texts.ToImmutable(), constants, items, tables);
    }

    private static void ValidateTables(SilphSourcePackage source, PretSuperRodTablesDocument document)
    {
        Need(document.SchemaVersion == 1 && document.ResourceId == ResourceId &&
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
            Need(table.SourcePath == OldRodEncounterRules.SourcePath && table.Slots.Length == 5 &&
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
