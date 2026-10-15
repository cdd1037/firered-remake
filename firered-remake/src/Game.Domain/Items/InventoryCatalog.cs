using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>The admitted inventory items and pocket limits of one composed game.</summary>
public sealed class InventoryCatalog
{
    public ImmutableDictionary<ItemId, QuestItemDefinition> Items { get; }
    public InventoryLimits Limits { get; }
    public QuestInventoryLimits QuestLimits { get; }
    public int MachineSlots { get; }
    public ImmutableHashSet<ItemId> Singletons { get; }

    public InventoryCatalog(IEnumerable<QuestItemDefinition> items, InventoryLimits limits,
        QuestInventoryLimits questLimits, int machineSlots, IEnumerable<ItemId> singletons)
    {
        var definitions = ImmutableDictionary.CreateBuilder<ItemId, QuestItemDefinition>();
        foreach (var item in items)
        {
            if (definitions.TryGetValue(item.Id, out var existing) && existing != item)
                throw new ArgumentException("inventory_definition_conflict: " + item.Id.Value);
            definitions[item.Id] = item;
        }
        Items = definitions.ToImmutable();
        Limits = limits; QuestLimits = questLimits; MachineSlots = machineSlots;
        Singletons = singletons.ToImmutableHashSet();
        if (!Singletons.IsSubsetOf(Items.Keys)) throw new ArgumentException("inventory_singleton_not_admitted");
    }

    public string? Validate(InventoryState? state)
    {
        if (state is null) return "inventory_missing";
        if (state.Limits != Limits) return "inventory_limits";
        var error = Pocket(state.BagItems, Limits.BagItemSlots, "POCKET_ITEMS") ??
            Pocket(state.PcItems, Limits.PcItemSlots, null) ??
            Optional(state.KeyItems, QuestLimits.KeyItemSlots, "POCKET_KEY_ITEMS") ??
            Optional(state.Balls, QuestLimits.BallSlots, "POCKET_POKE_BALLS") ??
            Optional(state.Berries, SsAnneItemRules.BerrySlots, "POCKET_BERRY_POUCH") ??
            Optional(state.Machines, MachineSlots, "POCKET_TM_CASE");
        if (error is not null) return error;
        int cases = OpeningInventoryRules.Quantity(state.KeyItems ?? [], MachineRules.TmCaseId);
        if (cases > 1) return "inventory_tm_case_quantity";
        if ((state.Machines?.Length ?? 0) > 0 && cases != 1) return "inventory_tm_case_missing";
        int pouches = OpeningInventoryRules.Quantity(state.KeyItems ?? [], SsAnneItemRules.BerryPouchId);
        if (pouches > 1) return "inventory_berry_pouch_quantity";
        if ((state.Berries?.Length ?? 0) > 0 && pouches != 1) return "inventory_berry_pouch_missing";
        return null;
    }

    private string? Optional(ImmutableArray<InventoryStack>? stacks, int capacity, string pocket)
        => stacks is null ? null : Pocket(stacks.Value, capacity, pocket);

    private string? Pocket(ImmutableArray<InventoryStack> stacks, int capacity, string? pocket)
    {
        if (stacks.IsDefault || stacks.Length > capacity) return "inventory_pocket_capacity";
        var seen = new HashSet<ItemId>();
        foreach (var stack in stacks)
        {
            if (stack is null) return "inventory_stack_missing";
            if (!Items.TryGetValue(stack.Item, out var item)) return "inventory_item_unsupported";
            if (pocket is null ? item.Pocket is "POCKET_KEY_ITEMS" or "POCKET_TM_CASE" : item.Pocket != pocket)
                return "inventory_item_pocket";
            if (stack.Quantity < 1 || stack.Quantity > Limits.MaximumStack ||
                (Singletons.Contains(stack.Item) && stack.Quantity != 1)) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }
}
