using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original Good Rod gift service, house and key item. Encounter tables belong to the shared fishing resource.</summary>
public sealed class PretGoodRodService
{
    public const string ResourceId = "firered-good-rod-service-v1";
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<MachineItem> Items { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretGoodRodService(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, PretCityServiceItemsDocument items)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; Items = items.Items; Icons = items.Icons;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
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
            map.CreateConnectedCells(), []);
    }

    public static PretGoodRodService Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"maps/regions/good-rod-service");
        var npc = source.Document("npc-source.json").GetProperty("graphics");
        Need(npc.GetArrayLength() == 2, "Good Rod actor collection differs.");
        foreach (var texture in npc.EnumerateArray())
        {
            Need(texture.GetProperty("graphicsId").GetString() is "OBJ_EVENT_GFX_FISHER" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Good Rod actors must reuse the original existing resources.");
        }
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Good Rod catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Good Rod service resources; current-game admission is separate.", ResourceId);
        var spec = catalog.GetProperty("maps")[0];
        string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("mapPath").GetString()!;
        string path = spec.GetProperty("path").GetString()!;
        Need(id == HouseMapId.Value && path == relative ,
            "Good Rod compact map binding differs.");
        var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
        Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id && runtime.Width == 10 && runtime.Height == 10,
            "Good Rod house dimensions differ.");
        var house = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
        Need(house.FieldWarps.Length == 4 && house.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            house.Events.GetProperty("coord_events").GetArrayLength() == 0 && house.Events.GetProperty("bg_events").GetArrayLength() == 0,
            "Good Rod original event collection differs.");
        var actor = house.Events.GetProperty("object_events")[0];
        Need(actor.GetProperty("x").GetInt32() == 6 && actor.GetProperty("y").GetInt32() == 5 &&
            actor.GetProperty("script").GetString() == "FuchsiaCity_House2_EventScript_FishingGurusBrother",
            "Good Rod original actor differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["ITEM_GOOD_ROD"] == 263 && constants["FLAG_GOT_GOOD_ROD"] == 0x244 && constants["GOOD_ROD"] == 1,
            "Good Rod item, flag or rod constants differ.");
        var items = source.Parse<PretCityServiceItemsDocument>("items.json");
        Need(items.SchemaVersion == 1 &&
            items.Items.Length == 1 && items.Icons.Length == 1, "Good Rod item collection differs.");
        var item = items.Items[0].Item; var icon = items.Icons[0];
        Need(item.Id.Value == "pret:ITEM_GOOD_ROD" && item.SourceItemId == 263 && item.SecondaryId == 1 &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && item.Importance == 1 && item.Registrability == 1 &&
            item.FieldUseFunction == "FieldUseFunc_Rod" && item.BattleUsage == 0 && !item.CanToss && !item.CanSell && !item.CanGive,
            "Good Rod item declaration differs.");
        Need(icon.Item == item.Id && icon.Width == 24 && icon.Height == 24 && (source.Read(icon.PngPath)).Length > 0, "Good Rod icon differs.");
        return new(source, ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house), constants, items);
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
