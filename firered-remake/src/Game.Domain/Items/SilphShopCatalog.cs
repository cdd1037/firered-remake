using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Exact source-item extension over Safari shelves; inherited items and stock remain identical.</summary>
public sealed class SilphShopCatalog
{
    public const string Profile = "firered-silph-inventory-v1";
    public static ItemId CardKeyId { get; } = new("pret:ITEM_CARD_KEY");
    public static ItemId MasterBallId { get; } = new("pret:ITEM_MASTER_BALL");
    public static ItemId UltraBallId { get; } = new("pret:ITEM_ULTRA_BALL");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, SilphShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public EarlyShopCatalog Predecessor { get; }
    private SilphShopCatalog(EarlyShopCatalog catalog, EarlyShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static SilphShopCatalog Authenticate(EarlyShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        SafariShopCatalog.ValidateCatalog(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Profile != Profile || candidate.Items.IsDefault || candidate.Items.Length != predecessor.Items.Length + 12 ||
            candidate with { Profile = predecessor.Profile, Items = predecessor.Items } != predecessor ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) ||
            candidate.Items.Select(i => i.Id).Distinct().Count() != candidate.Items.Length)
            throw new ArgumentException("silph_shop_scope");
        for (int i = 0; i < predecessor.Items.Length; i++)
            if (!ReferenceEquals(candidate.Items[i], predecessor.Items[i])) throw new ArgumentException("silph_shop_predecessor");
        var proof = new SilphShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static SilphShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("silph_shop_authentication"); }
}
