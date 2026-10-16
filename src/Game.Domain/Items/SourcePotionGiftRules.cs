using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>The pinned ordinary Bag check/add contract, bounded to the imported Potion.
/// The story interpreter owns the source gift flag and dialogue order.</summary>
public static class SourcePotionGiftRules
{
    public static InventoryTransactionResult CheckSpace(OpeningItemsCatalog catalog, InventoryState state,
        ItemId item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        var error = OpeningInventoryRules.Validate(catalog, state);
        if (error is not null) return Fail(error);
        if (item != OpeningInventoryRules.PotionId) return Fail("inventory_item_unsupported");
        if (quantity <= 0 || quantity > state.Limits.MaximumStack) return Fail("inventory_gift_quantity");
        var existing = OpeningInventoryRules.Quantity(state.BagItems, item);
        if (existing > state.Limits.MaximumStack - quantity) return Fail("inventory_stack_full");
        if (existing == 0 && state.BagItems.Length >= state.Limits.BagItemSlots)
            return Fail("inventory_destination_full");
        return new(state, true, null);
    }

    public static InventoryTransactionResult Add(OpeningItemsCatalog catalog, InventoryState state,
        ItemId item, int quantity = 1)
    {
        var check = CheckSpace(catalog, state, item, quantity);
        if (!check.Succeeded) return check;
        var total = OpeningInventoryRules.Quantity(state.BagItems, item) + quantity;
        return new(state with { BagItems = OpeningInventoryRules.SetQuantity(state.BagItems, item, total) }, true, null);
    }
}
