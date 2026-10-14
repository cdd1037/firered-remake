using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The original held Stick; all 125 predecessor items and every shop retain identity.</summary>
public sealed class NpcTradeShopCatalog
{
    public const string Profile = "firered-npc-trade-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "a336ed043054a7dfd075c88671001e879bea668fcf1baf9d2e4e27c0f59393b6";
    public static ItemId StickId { get; } = new("pret:ITEM_STICK");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, NpcTradeShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public FossilLabShopCatalog Predecessor { get; }
    private NpcTradeShopCatalog(EarlyShopCatalog catalog, FossilLabShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static NpcTradeShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("npc_trade_shop_authentication"); }
    public static NpcTradeShopCatalog Authenticate(FossilLabShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(FossilLabShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 125 || candidate.Items.IsDefault || candidate.Items.Length != 126 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) || candidate.Items.Select(i => i.Id).Distinct().Count() != 126 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("npc_trade_shop_predecessor");
        if (candidate.Items[^1].Id != StickId ||
            CampaignSourceFingerprint.Compute(candidate.Items.Skip(125).ToArray()) != AdditionalItemsSourceSha256)
            throw new ArgumentException("npc_trade_shop_source");
        var proof = new NpcTradeShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, bool canContainStick)
        {
            if (stacks is not {} values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            if (!canContainStick && values.Any(v => v.Item == StickId)) return "inventory_item_pocket";
            return null;
        }
        if (Pocket(inventory.KeyItems, Catalog.QuestLimits.KeyItemSlots, false) is {} key) return key;
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots, true) is {} bag) return bag;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots, true) is {} pc) return pc;
        if (Pocket(inventory.Balls, Catalog.QuestLimits.BallSlots, false) is {} ball) return ball;
        if (Pocket(inventory.Berries, SsAnneItemRules.BerrySlots, false) is {} berry) return berry;
        if (Pocket(inventory.Machines, 58, false) is {} machine) return machine;
        return Predecessor.ValidateInventory(Project(inventory));
    }
    public InventoryState InventoryForPredecessor(InventoryState inventory)
    {
        if (ValidateInventory(inventory) is {} error) throw new ArgumentException(error, nameof(inventory));
        return Project(inventory);
    }
    public InventoryState RestoreFromPredecessor(InventoryState original, InventoryState changed)
    {
        _ = InventoryForPredecessor(original);
        if (Predecessor.ValidateInventory(changed) is {} error) throw new ArgumentException(error, nameof(changed));
        static ImmutableArray<InventoryStack> Merge(ImmutableArray<InventoryStack> before, ImmutableArray<InventoryStack> after)
        {
            var remaining = after.ToDictionary(s => s.Item); var result = ImmutableArray.CreateBuilder<InventoryStack>();
            foreach (var stack in before)
                if (stack.Item == StickId) result.Add(stack);
                else if (remaining.Remove(stack.Item, out var updated)) result.Add(updated);
            foreach (var stack in after) if (remaining.ContainsKey(stack.Item)) result.Add(stack);
            return result.ToImmutable();
        }
        var restored = changed with { BagItems = Merge(original.BagItems, changed.BagItems), PcItems = Merge(original.PcItems, changed.PcItems) };
        if (ValidateInventory(restored) is {} resultError) throw new ArgumentException(resultError, nameof(changed));
        return restored;
    }
    private static InventoryState Project(InventoryState inventory) => inventory with {
        BagItems = inventory.BagItems.RemoveAll(s => s.Item == StickId), PcItems = inventory.PcItems.RemoveAll(s => s.Item == StickId) };
}
