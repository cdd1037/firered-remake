using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

public enum InventoryOrderPocket { Items, KeyItems, Balls, Pc }

/// <summary>Source SELECT inserts the selected stack before a list boundary (including CANCEL).
/// Quantities and all stack identities are preserved; Berry Pouch ordering is not user-controlled.</summary>
public static class InventoryOrderRules
{
    public static ImmutableArray<InventoryStack> Pocket(InventoryState state, InventoryOrderPocket pocket) => pocket switch {
        InventoryOrderPocket.Items => state.BagItems, InventoryOrderPocket.KeyItems => state.KeyItems ?? [],
        InventoryOrderPocket.Balls => state.Balls ?? [], InventoryOrderPocket.Pc => state.PcItems,
        _ => throw new ArgumentOutOfRangeException(nameof(pocket)) };

    public static InventoryTransactionResult Move(InventoryCatalog catalog, InventoryState state,
        InventoryOrderPocket pocket, int source, int destination, ItemId expectedItem)
    {
        InventoryTransactionResult No(string error) => new(state, false, error);
        if (catalog.Validate(state) is {} invalid) return No(invalid);
        if (!Enum.IsDefined(pocket)) return No("inventory_move_pocket");
        var stacks = Pocket(state, pocket);
        if (source < 0 || source >= stacks.Length || destination < 0 || destination > stacks.Length)
            return No("inventory_move_index");
        if (stacks[source].Item != expectedItem) return No("inventory_move_stale");
        var moved = MoveBefore(stacks, source, destination);
        return new(pocket switch {
            InventoryOrderPocket.Items => state with { BagItems = moved },
            InventoryOrderPocket.KeyItems => state with { KeyItems = moved },
            InventoryOrderPocket.Balls => state with { Balls = moved },
            _ => state with { PcItems = moved } }, true, null);
    }

    public static ImmutableArray<T> MoveBefore<T>(ImmutableArray<T> entries, int source, int destination)
    {
        if (entries.IsDefault || source < 0 || source >= entries.Length || destination < 0 || destination > entries.Length)
            throw new ArgumentOutOfRangeException(nameof(source));
        if (source == destination || source + 1 == destination) return entries;
        return entries.RemoveAt(source).Insert(source < destination ? destination - 1 : destination, entries[source]);
    }
}
