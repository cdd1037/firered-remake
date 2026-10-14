using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Source-authenticated ordinary additions, independent of region and shared transaction ownership.</summary>
public sealed class OrdinaryItemInventoryCatalog
{
    public const string Profile = "firered-ordinary-item-supplement-v1";
    public const string AdditionalItemsSourceSha256 = "f20a8a5042374657bf7ef37efb7b6f9f474376f844aa7662b867af3a2366e9dd";
    public static ItemId PpMaxId { get; } = new("pret:ITEM_PP_MAX");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, OrdinaryItemInventoryCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public EarlyShopCatalog Predecessor { get; }
    public ImmutableArray<EarlyShopItemDefinition> Additions { get; }
    private OrdinaryItemInventoryCatalog(EarlyShopCatalog predecessor, ImmutableArray<EarlyShopItemDefinition> additions)
    {
        Predecessor = predecessor; Additions = additions;
        Catalog = predecessor with { Profile = Profile, Items = predecessor.Items.AddRange(additions) };
    }
    public static OrdinaryItemInventoryCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static EarlyShopCatalog BaseCatalog(EarlyShopCatalog catalog) => For(catalog)?.Predecessor ?? catalog;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("ordinary_item_inventory_authentication"); }
    public static OrdinaryItemInventoryCatalog Authenticate(EarlyShopCatalog predecessor, ImmutableArray<EarlyShopItemDefinition> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        EarlyShopInventoryRules.ValidateCatalog(predecessor);
        if (predecessor.SourceCommit != OpeningInventoryRules.SourceCommit || For(predecessor) is not null ||
            additions.IsDefault || additions.Length != 1 || additions[0] is not { Pocket: "POCKET_ITEMS", Importance: 0 } item ||
            item.Id != PpMaxId || item.EffectBytes.IsDefault || predecessor.Items.Any(i => i.Id == item.Id) ||
            CampaignSourceFingerprint.Compute(additions.ToArray()) != AdditionalItemsSourceSha256)
            throw new ArgumentException("ordinary_item_inventory_source");
        var proof = new OrdinaryItemInventoryCatalog(predecessor, additions);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public bool IsAddition(ItemId item) => Additions.Any(i => i.Id == item);
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack> stacks, int capacity)
        {
            if (stacks.IsDefault || stacks.Length > capacity) return "inventory_pocket_capacity";
            if (stacks.Any(s => s is null)) return "inventory_stack_missing";
            if (stacks.Any(s => s.Quantity < 1 || s.Quantity > Catalog.Limits.MaximumStack)) return "inventory_stack_quantity";
            if (stacks.Select(s => s.Item).Distinct().Count() != stacks.Length) return "inventory_duplicate_stack";
            return null;
        }
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots) is {} bag) return bag;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots) is {} pc) return pc;
        foreach (var pocket in new[] { inventory.KeyItems, inventory.Balls, inventory.Berries, inventory.Machines })
            if (pocket is {} stacks && !stacks.IsDefault && stacks.Any(s => s is not null && IsAddition(s.Item)))
                return "inventory_item_pocket";
        // Preserve original capacities and reject malformed full state before narrowing to the source proof.
        return EarlyShopInventoryRules.Validate(Predecessor, inventory with {
            BagItems = inventory.BagItems.RemoveAll(s => IsAddition(s.Item)),
            PcItems = inventory.PcItems.RemoveAll(s => IsAddition(s.Item)) });
    }
}
