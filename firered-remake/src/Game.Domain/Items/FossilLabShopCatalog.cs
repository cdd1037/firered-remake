using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The original Old Amber key item; all 124 predecessor items and every shop retain identity.</summary>
public sealed class FossilLabShopCatalog
{
    public const string Profile = "firered-fossil-lab-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "f2b58a53e9cc328adf77ab04a1a9d6a2721c0f5e50006169e5497596207453cb";
    public static ItemId OldAmberId { get; } = new("pret:ITEM_OLD_AMBER");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, FossilLabShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public TrainerTowerShopCatalog Predecessor { get; }
    private FossilLabShopCatalog(EarlyShopCatalog catalog, TrainerTowerShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static FossilLabShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("fossil_lab_shop_authentication"); }
    public static FossilLabShopCatalog Authenticate(TrainerTowerShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(TrainerTowerShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 124 || candidate.Items.IsDefault || candidate.Items.Length != 125 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) || candidate.Items.Select(i => i.Id).Distinct().Count() != 125 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("fossil_lab_shop_predecessor");
        if (candidate.Items[^1].Id != OldAmberId ||
            CampaignSourceFingerprint.Compute(candidate.Items.Skip(124).ToArray()) != AdditionalItemsSourceSha256)
            throw new ArgumentException("fossil_lab_shop_source");
        var proof = new FossilLabShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, bool keyItems)
        {
            if (stacks is not {} values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            if (!keyItems && values.Any(v => v.Item == OldAmberId)) return "inventory_item_pocket";
            return null;
        }
        if (Pocket(inventory.KeyItems, Catalog.QuestLimits.KeyItemSlots, true) is {} key) return key;
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots, false) is {} bag) return bag;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots, false) is {} pc) return pc;
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
        ImmutableArray<InventoryStack>? Merge(ImmutableArray<InventoryStack>? before, ImmutableArray<InventoryStack>? after)
        {
            if (before is not {} values) return after;
            var changedValues = after ?? []; var remaining = changedValues.ToDictionary(s => s.Item);
            var result = ImmutableArray.CreateBuilder<InventoryStack>();
            foreach (var stack in values)
                if (stack.Item == OldAmberId) result.Add(stack);
                else if (remaining.Remove(stack.Item, out var updated)) result.Add(updated);
            foreach (var stack in changedValues) if (remaining.ContainsKey(stack.Item)) result.Add(stack);
            return result.Count == 0 && after is null ? null : result.ToImmutable();
        }
        var restored = changed with { KeyItems = Merge(original.KeyItems, changed.KeyItems) };
        if (ValidateInventory(restored) is {} resultError) throw new ArgumentException(resultError, nameof(changed));
        return restored;
    }
    private static InventoryState Project(InventoryState inventory) => inventory with {
        KeyItems = inventory.KeyItems?.RemoveAll(s => s.Item == OldAmberId) };
}
