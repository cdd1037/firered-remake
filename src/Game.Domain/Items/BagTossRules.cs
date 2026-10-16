using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Original field Bag/Pouch disposal, using the existing inventory owner.
/// TM CASE has USE/GIVE/EXIT and does not offer TOSS even for expendable TMs.</summary>
public static class BagTossRules
{
    public static bool CanToss(EarlyShopCatalog catalog, ItemId item) =>
        EarlyShopInventoryRules.Find(catalog, item) is { Importance: 0 } definition &&
        definition.Pocket is "POCKET_ITEMS" or "POCKET_POKE_BALLS" or "POCKET_BERRY_POUCH";

    public static int Available(EarlyShopCatalog catalog, InventoryState inventory, ItemId item) =>
        EarlyShopInventoryRules.Find(catalog, item) is {} definition && CanToss(catalog, item)
            ? OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item) : 0;

    public static InventoryTransactionResult Toss(EarlyShopCatalog catalog, InventoryState inventory,
        ItemId item, string pocket, int quantity)
    {
        InventoryTransactionResult No(string error) => new(inventory, false, error);
        if (EarlyShopInventoryRules.Validate(catalog, inventory) is {} invalid) return No(invalid);
        if (!CanToss(catalog, item)) return No("bag_toss_item_not_tossable");
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        if (definition.Pocket != pocket) return No("bag_toss_pocket_invalid");
        // TOSS uses the full 1..999 owned stack, not the shop's 99-item buy limit.
        if (quantity < 1 || quantity > inventory.Limits.MaximumStack) return No("bag_toss_quantity_invalid");
        int available = Available(catalog, inventory, item);
        if (quantity > available) return No("inventory_insufficient_quantity");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, available - quantity), true, null);
    }
}
