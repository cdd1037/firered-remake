using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Personal PC transfers use the composed item catalogue. Deposit is locked to Items;
/// withdrawal returns existing PC stock to its original pocket. Both sides commit together.</summary>
public static class PersonalPcInventoryRules
{
    public static InventoryTransactionResult Transfer(InventoryCatalog catalog, InventoryState state,
        ItemId item, int quantity, bool withdraw)
    {
        InventoryTransactionResult No(string error) => new(state, false, error);
        if (catalog.Validate(state) is {} invalid) return No(invalid);
        if (!catalog.Items.TryGetValue(item, out var definition)) return No("inventory_item_unsupported");
        if (definition.Pocket is not ("POCKET_ITEMS" or "POCKET_POKE_BALLS" or "POCKET_BERRY_POUCH") ||
            !withdraw && definition.Pocket != "POCKET_ITEMS") return No("inventory_item_pocket");
        if (quantity < 1 || quantity > catalog.Limits.MaximumStack) return No("inventory_transfer_quantity");
        var bag = definition.Pocket switch {
            "POCKET_POKE_BALLS" => state.Balls ?? [], "POCKET_BERRY_POUCH" => state.Berries ?? [], _ => state.BagItems };
        var from = withdraw ? state.PcItems : bag;
        var to = withdraw ? bag : state.PcItems;
        int available = OpeningInventoryRules.Quantity(from, item), existing = OpeningInventoryRules.Quantity(to, item);
        if (available < quantity) return No("inventory_insufficient_quantity");
        if (existing > catalog.Limits.MaximumStack - quantity) return No("inventory_stack_full");
        int capacity = !withdraw ? catalog.Limits.PcItemSlots : definition.Pocket switch {
            "POCKET_POKE_BALLS" => catalog.QuestLimits.BallSlots,
            "POCKET_BERRY_POUCH" => SsAnneItemRules.BerrySlots, _ => catalog.Limits.BagItemSlots };
        if (existing == 0 && to.Length >= capacity) return No("inventory_destination_full");
        var changed = state;
        if (withdraw && definition.Pocket == "POCKET_BERRY_POUCH" &&
            OpeningInventoryRules.Quantity(state.KeyItems ?? [], SsAnneItemRules.BerryPouchId) == 0)
        {
            var keys = state.KeyItems ?? [];
            if (keys.Length >= catalog.QuestLimits.KeyItemSlots) return No("inventory_key_items_full");
            changed = changed with { KeyItems = keys.Add(new(SsAnneItemRules.BerryPouchId, 1)) };
        }
        var remaining = OpeningInventoryRules.SetQuantity(from, item, available - quantity);
        var received = OpeningInventoryRules.SetQuantity(to, item, existing + quantity);
        var bagResult = withdraw ? received : remaining;
        changed = definition.Pocket switch {
            "POCKET_POKE_BALLS" => changed with { Balls = bagResult },
            "POCKET_BERRY_POUCH" => changed with { Berries = bagResult }, _ => changed with { BagItems = bagResult } };
        changed = changed with { PcItems = withdraw ? remaining : received };
        return catalog.Validate(changed) is {} error ? No(error) : new(changed, true, null);
    }
}
