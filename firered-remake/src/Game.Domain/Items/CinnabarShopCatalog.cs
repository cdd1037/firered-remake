using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Authenticated Secret Key and Cinnabar Mart extension; inherited items and shops retain identity.</summary>
public sealed class CinnabarShopCatalog
{
    public const string Profile = "firered-cinnabar-inventory-v1";
    public static ItemId SecretKeyId { get; } = new("pret:ITEM_SECRET_KEY");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, CinnabarShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public SaffronGymShopCatalog Predecessor { get; }
    private CinnabarShopCatalog(EarlyShopCatalog catalog, SaffronGymShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }

    public static CinnabarShopCatalog Authenticate(SaffronGymShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SaffronGymShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            candidate.Items.IsDefault || candidate.Items.Length != 88 || old.Items.Length != 87 ||
            candidate.Shops.IsDefault || candidate.Shops.Length != old.Shops.Length + 1 ||
            candidate with { Profile = old.Profile, Items = old.Items, Shops = old.Shops } != old ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) || candidate.Items.Select(i => i.Id).Distinct().Count() != 88)
            throw new ArgumentException("cinnabar_shop_scope");
        for (int i = 0; i < old.Items.Length; i++)
            if (!ReferenceEquals(candidate.Items[i], old.Items[i])) throw new ArgumentException("cinnabar_shop_predecessor");
        for (int i = 0; i < old.Shops.Length; i++)
            if (!ReferenceEquals(candidate.Shops[i], old.Shops[i])) throw new ArgumentException("cinnabar_shop_stock_predecessor");
        if (candidate.Shops[^1] is null || candidate.Shops[^1].Items.IsDefault)
            throw new ArgumentException("cinnabar_shop_source");
        var proof = new CinnabarShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static CinnabarShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("cinnabar_shop_authentication"); }

    public string? ValidateInventory(InventoryState? inventory)
    {
        ValidateCatalog(Catalog);
        if (inventory is null) return "inventory_missing";
        if (inventory.KeyItems is { } keys)
        {
            if (keys.IsDefault || keys.Length > Catalog.QuestLimits.KeyItemSlots) return "inventory_key_items_capacity";
            if (keys.Any(k => k is null)) return "inventory_stack_missing";
            if (keys.Any(k => k.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (keys.Select(k => k.Item).Distinct().Count() != keys.Length) return "inventory_duplicate_stack";
        }
        return EarlyShopInventoryRules.Validate(Predecessor.Catalog, WithoutSecretKey(inventory));
    }

    /// <summary>Validate the entire current inventory before projecting the one new key out of historical rules.</summary>
    public InventoryState InventoryForPredecessor(InventoryState inventory)
    {
        if (ValidateInventory(inventory) is { } error) throw new ArgumentException(error, nameof(inventory));
        return WithoutSecretKey(inventory);
    }

    /// <summary>Merge a validated historical transaction, preserving the protected key and its relative position.</summary>
    public InventoryState RestoreFromPredecessor(InventoryState original, InventoryState changed)
    {
        _ = InventoryForPredecessor(original);
        if (EarlyShopInventoryRules.Validate(Predecessor.Catalog, changed) is { } error)
            throw new ArgumentException(error, nameof(changed));
        var keys = original.KeyItems;
        int index = keys is { } values ? Enumerable.Range(0, values.Length).FirstOrDefault(i => values[i].Item == SecretKeyId, -1) : -1;
        var result = index < 0 ? changed : changed with { KeyItems = (changed.KeyItems ?? []).Insert(Math.Min(index, changed.KeyItems?.Length ?? 0), keys!.Value[index]) };
        if (ValidateInventory(result) is { } resultError) throw new ArgumentException(resultError, nameof(changed));
        return result;
    }
    private static InventoryState WithoutSecretKey(InventoryState inventory) =>
        inventory.KeyItems is { } keys && keys.Any(k => k.Item == SecretKeyId)
            ? inventory with { KeyItems = keys.RemoveAll(k => k.Item == SecretKeyId) } : inventory;
}
