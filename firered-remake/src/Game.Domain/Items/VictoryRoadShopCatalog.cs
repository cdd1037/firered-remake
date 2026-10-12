using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Guard Spec. and Sitrus Berry only. Preserves all First-Sevii definitions, shops and limits.</summary>
public sealed class VictoryRoadShopCatalog
{
    public const string Profile = "firered-victory-road-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "8e9944abd036c5229342b1b0390836557749771b2baef1918cfb37d75ab825b3";
    public static ItemId GuardSpecId { get; } = new("pret:ITEM_GUARD_SPEC");
    public static ItemId SitrusBerryId { get; } = new("pret:ITEM_SITRUS_BERRY");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, VictoryRoadShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public FirstSeviiShopCatalog Predecessor { get; }
    private VictoryRoadShopCatalog(EarlyShopCatalog catalog, FirstSeviiShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static VictoryRoadShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("victory_road_shop_authentication"); }
    public static VictoryRoadShopCatalog Authenticate(FirstSeviiShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(FirstSeviiShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 98 || candidate.Items.IsDefault || candidate.Items.Length != 100 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) ||
            candidate.Items.Select(i => i.Id).Distinct().Count() != 100 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("victory_road_shop_predecessor");
        var fresh = candidate.Items.Skip(98).ToArray();
        if (!fresh.Select(i => i.Id).SequenceEqual([GuardSpecId, SitrusBerryId]) ||
            CampaignSourceFingerprint.Compute(fresh) != AdditionalItemsSourceSha256)
            throw new ArgumentException("victory_road_shop_source");
        var proof = new VictoryRoadShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }

    private static bool Additional(ItemId item) => item == GuardSpecId || item == SitrusBerryId;
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, string? pocket)
        {
            if (stacks is not { } values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            foreach (var stack in values.Where(v => Additional(v.Item)))
                if (pocket is not null && pocket != (stack.Item == GuardSpecId ? "POCKET_ITEMS" : "POCKET_BERRY_POUCH"))
                    return "inventory_item_pocket";
            return null;
        }
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots, "POCKET_ITEMS") is { } bag) return bag;
        if (Pocket(inventory.Berries, SsAnneItemRules.BerrySlots, "POCKET_BERRY_POUCH") is { } berry) return berry;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots, null) is { } pc) return pc;
        if (Pocket(inventory.KeyItems, Catalog.QuestLimits.KeyItemSlots, "POCKET_KEY_ITEMS") is { } key) return key;
        if (Pocket(inventory.Balls, Catalog.QuestLimits.BallSlots, "POCKET_POKE_BALLS") is { } ball) return ball;
        if (inventory.Machines is { } machines && (machines.IsDefault || machines.Any(m => m is null || Additional(m.Item))))
            return "inventory_item_pocket";
        if (inventory.Berries is { Length: > 0 } &&
            OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], SsAnneItemRules.BerryPouchId) != 1)
            return "inventory_berry_pouch_missing";
        return Predecessor.ValidateInventory(Project(inventory));
    }
    public InventoryState InventoryForPredecessor(InventoryState inventory)
    {
        if (ValidateInventory(inventory) is { } error) throw new ArgumentException(error, nameof(inventory));
        return Project(inventory);
    }
    public InventoryState RestoreFromPredecessor(InventoryState original, InventoryState changed)
    {
        _ = InventoryForPredecessor(original);
        if (Predecessor.ValidateInventory(changed) is { } error) throw new ArgumentException(error, nameof(changed));
        ImmutableArray<InventoryStack>? Merge(ImmutableArray<InventoryStack>? before, ImmutableArray<InventoryStack>? after)
        {
            if (before is not { } values) return after;
            var result = after ?? [];
            for (int i = 0; i < values.Length; i++)
                if (Additional(values[i].Item)) result = result.Insert(Math.Min(i, result.Length), values[i]);
            return result.IsEmpty && after is null ? null : result;
        }
        var result = changed with { BagItems = Merge(original.BagItems, changed.BagItems)!.Value,
            PcItems = Merge(original.PcItems, changed.PcItems)!.Value, Berries = Merge(original.Berries, changed.Berries) };
        if (ValidateInventory(result) is { } resultError) throw new ArgumentException(resultError, nameof(changed));
        return result;
    }
    private static InventoryState Project(InventoryState inventory) => inventory with {
        BagItems = inventory.BagItems.RemoveAll(s => Additional(s.Item)),
        PcItems = inventory.PcItems.RemoveAll(s => Additional(s.Item)),
        Berries = inventory.Berries?.RemoveAll(s => Additional(s.Item)) };
}
