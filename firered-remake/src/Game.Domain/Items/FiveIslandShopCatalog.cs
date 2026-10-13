using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Original Big Pearl, Up-Grade and Sapphire additions; all108 inherited items and shop stocks retain identity.</summary>
public sealed class FiveIslandShopCatalog
{
    public const string Profile = "firered-five-island-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "563c01cecd18ee0f1ea0ecec18b07df525b3013358e9eb621541884fbe19f53c";
    public static ItemId BigPearlId { get; } = new("pret:ITEM_BIG_PEARL");
    public static ItemId UpGradeId { get; } = new("pret:ITEM_UP_GRADE");
    public static ItemId SapphireId { get; } = new("pret:ITEM_SAPPHIRE");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, FiveIslandShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public SixIslandShopCatalog Predecessor { get; }
    private FiveIslandShopCatalog(EarlyShopCatalog catalog, SixIslandShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static FiveIslandShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("five_island_shop_authentication"); }
    public static FiveIslandShopCatalog Authenticate(SixIslandShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SixIslandShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 108 || candidate.Items.IsDefault || candidate.Items.Length != 111 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) ||
            candidate.Items.Select(i => i.Id).Distinct().Count() != 111 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("five_island_shop_predecessor");
        var fresh = candidate.Items.Skip(108).ToArray();
        if (!fresh.Select(i => i.Id).SequenceEqual([BigPearlId, UpGradeId, SapphireId]) ||
            CampaignSourceFingerprint.Compute(fresh) != AdditionalItemsSourceSha256)
            throw new ArgumentException("five_island_shop_source");
        var proof = new FiveIslandShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    private static bool Additional(ItemId item) => item == BigPearlId || item == UpGradeId || item == SapphireId;
    private static bool Key(ItemId item) => item == SapphireId;
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, string? pocket)
        {
            if (stacks is not {} values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            foreach (var stack in values.Where(v => Additional(v.Item)))
                if (pocket is null ? Key(stack.Item) : pocket != (Key(stack.Item) ? "POCKET_KEY_ITEMS" : "POCKET_ITEMS"))
                    return "inventory_item_pocket";
            return null;
        }
        if (Pocket(inventory.KeyItems, Catalog.QuestLimits.KeyItemSlots, "POCKET_KEY_ITEMS") is {} key) return key;
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots, "POCKET_ITEMS") is {} bag) return bag;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots, null) is {} pc) return pc;
        if (Pocket(inventory.Balls, Catalog.QuestLimits.BallSlots, "POCKET_POKE_BALLS") is {} ball) return ball;
        if (Pocket(inventory.Berries, SsAnneItemRules.BerrySlots, "POCKET_BERRY_POUCH") is {} berry) return berry;
        if (Pocket(inventory.Machines, 58, "POCKET_TM_CASE") is {} machine) return machine;
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
            var changedValues = after ?? [];
            var remaining = changedValues.ToDictionary(s => s.Item);
            var result = ImmutableArray.CreateBuilder<InventoryStack>();
            // Source remove compacts the pocket. Merge surviving original slots in
            // their original order, rather than inserting extras at stale indexes
            // after an earlier ordinary stack has disappeared.
            foreach (var stack in values)
                if (Additional(stack.Item)) result.Add(stack);
                else if (remaining.Remove(stack.Item, out var changedStack)) result.Add(changedStack);
            foreach (var stack in changedValues)
                if (remaining.ContainsKey(stack.Item)) result.Add(stack);
            return result.Count == 0 && after is null ? null : result.ToImmutable();
        }
        var restored = changed with {
            KeyItems = Merge(original.KeyItems, changed.KeyItems),
            BagItems = Merge(original.BagItems, changed.BagItems)!.Value,
            PcItems = Merge(original.PcItems, changed.PcItems)!.Value };
        if (ValidateInventory(restored) is {} resultError) throw new ArgumentException(resultError, nameof(changed));
        return restored;
    }
    private static InventoryState Project(InventoryState inventory) => inventory with {
        KeyItems = inventory.KeyItems?.RemoveAll(s => Additional(s.Item)),
        BagItems = inventory.BagItems.RemoveAll(s => Additional(s.Item)),
        PcItems = inventory.PcItems.RemoveAll(s => Additional(s.Item)) };
}
