using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Exact source-item extension over Silph inventory; inherited items and stock remain identical; only the source Saffron Mart is appended.</summary>
public sealed class SaffronGymShopCatalog
{
    public const string Profile = "firered-saffron-gym-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "654373c204e139b5c370e14c845d55a42d7dbb985b65186ac2a277fcea4c7241";
    public const string MartSourceSha256 = "344acbecfe31ecf3e9a7c62c1cbf2a92c69a80539111c047586fe4727b9069fd";
    public static ItemId Tm04Id { get; } = new("pret:ITEM_TM04");
    public static ItemId MaxRepelId { get; } = new("pret:ITEM_MAX_REPEL");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, SaffronGymShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public EarlyShopCatalog Predecessor { get; }
    private SaffronGymShopCatalog(EarlyShopCatalog catalog, EarlyShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static SaffronGymShopCatalog Authenticate(EarlyShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        SilphShopCatalog.ValidateCatalog(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Profile != Profile || candidate.Items.IsDefault || candidate.Items.Length != predecessor.Items.Length + 1 ||
            candidate.Shops.IsDefault || candidate.Shops.Length != predecessor.Shops.Length + 1 ||
            candidate with { Profile = predecessor.Profile, Items = predecessor.Items, Shops = predecessor.Shops } != predecessor ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) ||
            candidate.Items.Select(i => i.Id).Distinct().Count() != candidate.Items.Length)
            throw new ArgumentException("saffron_gym_shop_scope");
        for (int i = 0; i < predecessor.Items.Length; i++)
            if (!ReferenceEquals(candidate.Items[i], predecessor.Items[i])) throw new ArgumentException("saffron_gym_shop_predecessor");
        for (int i = 0; i < predecessor.Shops.Length; i++)
            if (!ReferenceEquals(candidate.Shops[i], predecessor.Shops[i])) throw new ArgumentException("saffron_gym_shop_stock_predecessor");
        if (candidate.Shops[^1] is null || candidate.Shops[^1].Items.IsDefault ||
            CampaignSourceFingerprint.Compute(candidate.Shops[^1]) != MartSourceSha256)
            throw new ArgumentException("saffron_gym_shop_stock_source");
        if (CampaignSourceFingerprint.Compute(candidate.Items.Skip(predecessor.Items.Length).ToArray()) != AdditionalItemsSourceSha256)
            throw new ArgumentException("saffron_gym_shop_source");
        var proof = new SaffronGymShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static SaffronGymShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("saffron_gym_shop_authentication"); }
}
