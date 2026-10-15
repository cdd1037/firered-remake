using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Opt-in future inventory profile. Does not relax OpeningInventoryRules or alter serialized defaults.</summary>
public static class EarlyShopInventoryRules
{
    public const string MtMoonProfile = "firered-mtmoon-shop-v1";
    public const string Profile = "firered-viridian-pewter-shop-v1";
    public const string ViridianMart = "ViridianCity_Mart_Items", PewterMart = "PewterCity_Mart_Items";
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Inventory is {} inventory)
        {
            if (catalog.Items.IsDefault || catalog.Shops.IsDefault || catalog.MaximumTransactionQuantity < 1 ||
                catalog.MaximumMoney < 1 || catalog.Items.Select(i => i.Id).Distinct().Count() != catalog.Items.Length ||
                catalog.Items.Any(i => i.Item is null || i.Price < 0 || !Enum.IsDefined(i.UseSupport) || i.EffectBytes.IsDefault ||
                    i.EffectBytes.Any(value => value is < 0 or > 255) || !inventory.Items.TryGetValue(i.Id, out var item) || item != i.Item) ||
                catalog.Shops.Any(s => s.Items.IsDefault || s.Items.Any(id => !catalog.Items.Any(i => i.Id == id && i.Price > 0))))
                throw new NotSupportedException("Invalid shop item or stock scope.");
            return;
        }
        if (catalog.Items.IsDefault || catalog.Shops.IsDefault || catalog.MaximumTransactionQuantity < 1 || catalog.MaximumMoney < 1 ||
            catalog.Items.Select(i => i.Id).Distinct().Count() != catalog.Items.Length ||
            catalog.Items.Any(i => i.Item is null || i.Price < 0 || !Enum.IsDefined(i.UseSupport) || i.EffectBytes.IsDefault) ||
            catalog.Shops.Any(shop => shop.Items.IsDefault || shop.Items.Any(id => !catalog.Items.Any(i => i.Id == id && i.Price > 0))))
            throw new NotSupportedException("Invalid item catalog or stock.");
    }

    public static string? Validate(EarlyShopCatalog catalog, InventoryState? state)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Inventory is {} current) return current.Validate(state);
        throw new InvalidOperationException("Inventory operations require the current inventory catalog.");
    }

    internal static EarlyShopItemDefinition? Find(EarlyShopCatalog catalog, ItemId item) => catalog.Items.FirstOrDefault(i => i.Id == item);
    internal static ImmutableArray<InventoryStack> Pocket(InventoryState inventory, EarlyShopItemDefinition item)
        => item.Pocket switch { "POCKET_POKE_BALLS" => inventory.Balls ?? [], "POCKET_KEY_ITEMS" => inventory.KeyItems ?? [], "POCKET_BERRY_POUCH" => inventory.Berries ?? [], _ => inventory.BagItems };
    internal static InventoryState SetQuantity(InventoryState inventory, EarlyShopItemDefinition item, int quantity)
    {
        var changed = OpeningInventoryRules.SetQuantity(Pocket(inventory, item), item.Id, quantity);
        return item.Pocket switch { "POCKET_POKE_BALLS" => inventory with { Balls = changed },
            "POCKET_KEY_ITEMS" => inventory with { KeyItems = changed }, "POCKET_BERRY_POUCH" => inventory with { Berries = changed }, _ => inventory with { BagItems = changed } };
    }
}
