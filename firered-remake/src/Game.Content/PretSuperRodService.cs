using System.Collections.Immutable;
using System.Text.Json;
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
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PewterMachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    /// <summary>All 46 ordinary FireRed tables in ordinal map-ID order, with original indexes 5 through 9.</summary>
    public ImmutableArray<OldRodEncounterTable> SuperRodTables { get; }
    /// <summary>Evidence only: four Safari tables. Never use for ordinary admission.</summary>
    public ImmutableArray<OldRodEncounterTable> SourceOnlyTables { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSuperRodService(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, PretCityServiceItemsDocument items, PretSuperRodTablesDocument tables)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; Items = items.Items; Icons = items.Icons;
        SuperRodTables = tables.Tables; SourceOnlyTables = tables.SourceOnlyTables;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
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
        var source = new SilphSourcePackage(projectRoot,"super-rod-service");
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Super Rod actor collection differs.");
        foreach (var texture in npc.EnumerateArray())
        {
            Need(texture.GetProperty("graphicsId").GetString() is "OBJ_EVENT_GFX_FISHER" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Super Rod actors must reuse the original existing resources.");
        }
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId &&
            catalog.GetProperty("maps").GetArrayLength() == 1 ,
            "Super Rod catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Super Rod service resources; current-game admission is separate.", ResourceId);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == HouseMapId.Value && path == relative + "/runtime-map.json" ,
            "Super Rod compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 11 && runtime.Height == 9,
            "Super Rod house dimensions differ.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 3 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 1,
            "Super Rod original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("x").GetInt32() == 4 && actor.GetProperty("y").GetInt32() == 4 &&
            actor.GetProperty("script").GetString() == "Route12_FishingHouse_EventScript_FishingGuruBrother",
            "Super Rod original actor differs.");
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
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && (source.Read(icon.PngPath)).Length > 0, "Super Rod icon differs.");
        var tables = source.Parse<PretSuperRodTablesDocument>("super-rod-tables.json");
        ValidateTables(source, tables);
        return new(source, ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house), constants, items, tables);
    }

    private static void ValidateTables(SilphSourcePackage source, PretSuperRodTablesDocument document)
    {
        Need(document.SchemaVersion == 1 && document.ResourceId == ResourceId &&
            document.Rod == "super" && document.Tables.Length == 46 && document.SourceOnlyTables.Length == 4,
            "Super Rod table collection differs.");
        var all = document.Tables.Concat(document.SourceOnlyTables).ToArray();
        Need(all.Select(t => t.Map).Distinct().Count() == 50 && document.Tables.Select(t => t.Map.Value)
            .SequenceEqual(document.Tables.Select(t => t.Map.Value).Order(StringComparer.Ordinal)), "Super Rod table IDs/order differ.");
        foreach (var table in all)
        {
            Need(table.SourcePath == OldRodEncounterRules.SourcePath && table.Slots.Length == 5 &&
                table.Slots.Select(s => s.Index).SequenceEqual([5, 6, 7, 8, 9]) && table.Slots.Select(s => s.Weight).SequenceEqual([40, 40, 15, 4, 1]),
                "Super Rod source slot identity differs.");
            Need(table.RawEncounterRate is >= 1 and <= 255 && table.Slots.All(slot =>
                slot.MinimumLevel is >= 1 and <= 100 && slot.MaximumLevel >= slot.MinimumLevel && slot.MaximumLevel <= 100),
                "Fishing rate or level range differs.");
        }
        Need(document.SourceOnlyTables.Count(t => t.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)) == 4 &&
            document.Tables.All(t => !t.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)),
            "Super Rod source-only map boundary differs.");
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
