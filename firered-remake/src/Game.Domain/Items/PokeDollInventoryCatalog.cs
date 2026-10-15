using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>One source-authenticated ordinary item; existing shared inventory transactions own all changes.</summary>
public sealed class PokeDollInventoryCatalog
{
    public const string Profile = "firered-poke-doll-inventory-v1";
    public static ItemId PokeDollId { get; } = new("pret:ITEM_POKE_DOLL");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, PokeDollInventoryCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public SeafoamShopCatalog Predecessor { get; }
    private PokeDollInventoryCatalog(EarlyShopCatalog catalog, SeafoamShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static PokeDollInventoryCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("poke_doll_inventory_authentication"); }
    public static PokeDollInventoryCatalog Authenticate(SeafoamShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SeafoamShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 128 || candidate.Items.IsDefault || candidate.Items.Length != 129 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) ||
            candidate.Items.Select(i => i.Id).Distinct().Count() != 129 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("poke_doll_inventory_predecessor");
        if (candidate.Items[128].Id != PokeDollId)
            throw new ArgumentException("poke_doll_inventory_source");
        var proof = new PokeDollInventoryCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
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
            if (pocket is {} stacks && !stacks.IsDefault && stacks.Any(s => s?.Item == PokeDollId))
                return "inventory_item_pocket";
        // Check original capacities before removing the one new ID for the immutable predecessor proof.
        // No mutation adapter or alternate transaction owner is introduced.
        return Predecessor.ValidateInventory(inventory with {
            BagItems = inventory.BagItems.RemoveAll(s => s.Item == PokeDollId),
            PcItems = inventory.PcItems.RemoveAll(s => s.Item == PokeDollId) });
    }
}
