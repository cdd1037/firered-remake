using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Ordinary source Bag capacity/add transaction for the authenticated early item catalog.</summary>
public static class EarlyInventoryAddRules
{
    public static InventoryTransactionResult CheckSpace(EarlyShopCatalog catalog,InventoryState state,ItemId item,int quantity)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error)=>new(state,false,error);
        var error=EarlyShopInventoryRules.Validate(catalog,state);if(error is not null)return Fail(error);
        var definition=EarlyShopInventoryRules.Find(catalog,item);if(definition is null)return Fail("inventory_item_unsupported");
        if(quantity<1||quantity>catalog.Limits.MaximumStack)return Fail("inventory_gift_quantity");
        var pocket=EarlyShopInventoryRules.Pocket(state,definition);
        int count=OpeningInventoryRules.Quantity(pocket,item);
        if(count>catalog.Limits.MaximumStack-quantity)return Fail("inventory_stack_full");
        int capacity=definition.Pocket switch { "POCKET_POKE_BALLS" => catalog.QuestLimits.BallSlots, "POCKET_KEY_ITEMS" => catalog.QuestLimits.KeyItemSlots, "POCKET_BERRY_POUCH" => SsAnneItemRules.BerrySlots, _ => catalog.Limits.BagItemSlots };
        if(count==0&&pocket.Length>=capacity)return Fail("inventory_destination_full");
        return new(state,true,null);
    }
    public static InventoryTransactionResult Add(EarlyShopCatalog catalog,InventoryState state,ItemId item,int quantity)
    {
        var check=CheckSpace(catalog,state,item,quantity);if(!check.Succeeded)return check;
        var definition=EarlyShopInventoryRules.Find(catalog,item)!;
        int count=OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(state,definition),item);
        // Source AddBagItem inserts the Pouch atomically on the first berry. CheckBagHasSpace checks only its destination pocket.
        if (definition.Pocket == "POCKET_BERRY_POUCH" && OpeningInventoryRules.Quantity(state.KeyItems ?? [], SsAnneItemRules.BerryPouchId) == 0)
        {
            var keys = state.KeyItems ?? [];
            if (keys.Length >= catalog.QuestLimits.KeyItemSlots) return new(state, false, "inventory_key_items_full");
            state = state with { KeyItems = keys.Add(new(SsAnneItemRules.BerryPouchId, 1)) };
        }
        return new(EarlyShopInventoryRules.SetQuantity(state,definition,count+quantity),true,null);
    }
    /// <summary>Atomic source consumption; caller owns the accompanying reward/evolution transaction.</summary>
    public static InventoryTransactionResult Remove(EarlyShopCatalog catalog, InventoryState state, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        var error = EarlyShopInventoryRules.Validate(catalog, state);
        if (error is not null) return Fail(error);
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (definition is null) return Fail("inventory_item_unsupported");
        if (quantity < 1 || quantity > catalog.Limits.MaximumStack) return Fail("inventory_transfer_quantity");
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(state, definition), item);
        if (count < quantity) return Fail("inventory_insufficient_quantity");
        return new(EarlyShopInventoryRules.SetQuantity(state, definition, count - quantity), true, null);
    }

}
