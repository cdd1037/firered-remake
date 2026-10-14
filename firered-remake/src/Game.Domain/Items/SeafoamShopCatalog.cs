using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Original Water Stone pickup/storage admission; all 127 predecessor items and shops retain identity.</summary>
public sealed class SeafoamShopCatalog
{
    public const string Profile = "firered-seafoam-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "7ef8568f9a50617b77170edc01e3d82795a6aff4d94e1898f2d08c2840d8b2ef";
    public static ItemId WaterStoneId { get; } = new("pret:ITEM_WATER_STONE");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, SeafoamShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public NpcTradeShopCatalog Predecessor { get; }
    private SeafoamShopCatalog(EarlyShopCatalog catalog, NpcTradeShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static SeafoamShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("seafoam_shop_authentication"); }
    public static SeafoamShopCatalog Authenticate(NpcTradeShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(NpcTradeShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 127 || candidate.Items.IsDefault || candidate.Items.Length != 128 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) || candidate.Items.Select(i => i.Id).Distinct().Count() != 128 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("seafoam_shop_predecessor");
        if (candidate.Items[127].Id != WaterStoneId || candidate.Items[127].UseSupport != EarlyShopUseSupport.NoFieldUse ||
            CampaignSourceFingerprint.Compute(candidate.Items.Skip(127).ToArray()) != AdditionalItemsSourceSha256)
            throw new ArgumentException("seafoam_shop_source");
        var proof = new SeafoamShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, bool canContainAdditions)
        {
            if (stacks is not {} values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            if (!canContainAdditions && values.Any(v => IsAddition(v.Item))) return "inventory_item_pocket";
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
                if (IsAddition(stack.Item)) result.Add(stack);
                else if (remaining.Remove(stack.Item, out var updated)) result.Add(updated);
            foreach (var stack in after) if (remaining.ContainsKey(stack.Item)) result.Add(stack);
            return result.ToImmutable();
        }
        var restored = changed with { BagItems = Merge(original.BagItems, changed.BagItems), PcItems = Merge(original.PcItems, changed.PcItems) };
        if (ValidateInventory(restored) is {} resultError) throw new ArgumentException(resultError, nameof(changed));
        return restored;
    }
    private static bool IsAddition(ItemId item) => item == WaterStoneId;
    private static InventoryState Project(InventoryState inventory) => inventory with {
        BagItems = inventory.BagItems.RemoveAll(s => IsAddition(s.Item)), PcItems = inventory.PcItems.RemoveAll(s => IsAddition(s.Item)) };
}
