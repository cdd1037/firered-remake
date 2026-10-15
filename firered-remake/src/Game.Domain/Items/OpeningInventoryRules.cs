using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Bounded deterministic storage transactions. No file IO, RNG, UI or partial commits.</summary>
public static class OpeningInventoryRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile = "firered-opening-potion-v1";
    public static ItemId PotionId { get; } = new("pret:ITEM_POTION");
    public static InventoryLimits SourceLimits { get; } = new(42, 30, 999);

    public static void ValidateCatalog(OpeningItemsCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != Profile || catalog.CompleteItemCatalog)
            throw new NotSupportedException("Only the pinned opening Potion profile is supported.");
        var p = catalog.Potion;
        if (catalog.Limits != SourceLimits || catalog.InitialPcItems.IsDefault ||
            catalog.InitialPcItems.Length != 1 || catalog.InitialPcItems[0] != new InventoryStack(PotionId, 1) ||
            p is null || p.Id != PotionId || p.SourceItemId != 13 || p.Name != "POTION" || p.Price != 300 ||
            p.Pocket != "POCKET_ITEMS" || p.Type != "ITEM_TYPE_PARTY_MENU" ||
            p.FieldUseFunction != "FieldUseFunc_Medicine" || p.BattleUsage != 1 ||
            p.BattleUseFunction != "BattleUseFunc_Medicine" || p.HealHp != 20 ||
            p.EffectBytes.IsDefault || !p.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 4, 0, 20 }))
            throw new NotSupportedException("Unsupported inventory or Potion data in the opening profile.");
    }

    public static InventoryState NewGame(OpeningItemsCatalog catalog)
    {
        ValidateCatalog(catalog);
        return new([], catalog.InitialPcItems, catalog.Limits);
    }

    public static string? Validate(OpeningItemsCatalog catalog, InventoryState? state)
    {
        ValidateCatalog(catalog);
        if (state is null) return "inventory_missing";
        if (state.Limits != catalog.Limits) return "inventory_limits";
        return ValidateStacks(state.BagItems, state.Limits.BagItemSlots, state.Limits.MaximumStack, "bag") ??
            ValidateStacks(state.PcItems, state.Limits.PcItemSlots, state.Limits.MaximumStack, "pc");
    }

    private static string? ValidateStacks(ImmutableArray<InventoryStack> stacks, int capacity, int maximum, string store)
    {
        if (stacks.IsDefault || stacks.Length > capacity) return "inventory_" + store + "_capacity";
        var seen = new HashSet<ItemId>();
        foreach (var stack in stacks)
        {
            if (stack is null) return "inventory_stack_missing";
            if (stack.Item != PotionId) return "inventory_item_unsupported";
            if (stack.Quantity <= 0 || stack.Quantity > maximum) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }

    public static int Quantity(ImmutableArray<InventoryStack> stacks, ItemId item)
        => stacks.FirstOrDefault(x => x.Item == item)?.Quantity ?? 0;

    public static InventoryTransactionResult Withdraw(OpeningItemsCatalog catalog, InventoryState state,
        ItemId item, int quantity) => Transfer(catalog, state, item, quantity, true);

    public static InventoryTransactionResult Deposit(OpeningItemsCatalog catalog, InventoryState state,
        ItemId item, int quantity) => Transfer(catalog, state, item, quantity, false);

    private static InventoryTransactionResult Transfer(OpeningItemsCatalog catalog, InventoryState state,
        ItemId item, int quantity, bool withdrawing)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        var error = Validate(catalog, state);
        if (error is not null) return Fail(error);
        if (item != PotionId) return Fail("inventory_item_unsupported");
        // The original menus restrict quantity to 1..available (itself <=999). Reject unchecked
        // source helper calls such as count=0 or a new stack >999 at the transaction boundary.
        if (quantity <= 0 || quantity > state.Limits.MaximumStack) return Fail("inventory_transfer_quantity");
        var from = withdrawing ? state.PcItems : state.BagItems;
        var to = withdrawing ? state.BagItems : state.PcItems;
        int available = Quantity(from, item), existing = Quantity(to, item);
        if (available < quantity) return Fail("inventory_insufficient_quantity");
        if (existing > state.Limits.MaximumStack - quantity) return Fail("inventory_stack_full");
        int capacity = withdrawing ? state.Limits.BagItemSlots : state.Limits.PcItemSlots;
        if (existing == 0 && to.Length >= capacity) return Fail("inventory_destination_full");
        var remaining = SetQuantity(from, item, available - quantity);
        var received = SetQuantity(to, item, existing + quantity);
        return new(withdrawing ? state with { BagItems = received, PcItems = remaining } :
            state with { BagItems = remaining, PcItems = received }, true, null);
    }

    public static ImmutableArray<InventoryStack> SetQuantity(ImmutableArray<InventoryStack> stacks, ItemId item, int value)
    {
        int index = -1;
        for (int i = 0; i < stacks.Length; i++) if (stacks[i].Item == item) { index = i; break; }
        if (index < 0) return value == 0 ? stacks : stacks.Add(new(item, value));
        return value == 0 ? stacks.RemoveAt(index) : stacks.SetItem(index, new(item, value));
    }
}
