using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original Super Rod gift service, house and key item. Encounter tables belong to the shared fishing resource.</summary>
public sealed class PretSuperRodService
{
    public const string ResourceId = "firered-super-rod-service-v1";
    public static MapId HouseMapId { get; } = new("map.route-12-fishing-house");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<MachineItem> Items { get; }
    public ImmutableArray<ItemIcon> Icons { get; }
    public string GeneratedDirectory { get; }

    private PretSuperRodService(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, PretCityServiceItemsDocument items)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; Items = items.Items; Icons = items.Icons;

    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE12_FISHING_HOUSE" => HouseMapId,
        "MAP_ROUTE12" => new("map.route-12"),
        _ => throw new InvalidDataException("Unknown Super Rod service map symbol: " + symbol)
    };



    public static PretSuperRodService Load(PretCurrentWorld world, string? dependencyRoot = null)
    {
        var projectRoot = world.ProjectRoot;
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/super-rod-service");
        var house = world.Maps[HouseMapId];
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
        return new(source, ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house), constants, items);
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
