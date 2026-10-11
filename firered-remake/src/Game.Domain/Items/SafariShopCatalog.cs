using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Game.Domain.Items;

/// <summary>Exact twelve-item Safari extension, preserving the authenticated old shelves.</summary>
public sealed class SafariShopCatalog
{
    public const string Profile = "firered-safari-inventory-v1";
    public const string AdditionalItemsSha256 = "7c4ca11a5e7276f6da90dd081a34a9836918a97ead2015f4227697627e61008b";
    private static readonly ConditionalWeakTable<EarlyShopCatalog, SafariShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public EarlyShopCatalog Predecessor { get; }
    private SafariShopCatalog(EarlyShopCatalog catalog, EarlyShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static SafariShopCatalog Authenticate(EarlyShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        FuchsiaGymItemRules.ValidateShop(predecessor);
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Profile != Profile || candidate.Items.IsDefault ||
            candidate.Items.Length != predecessor.Items.Length + 12 ||
            candidate with { Profile = predecessor.Profile, Items = predecessor.Items } != predecessor ||
            candidate.Items.Select(i => i.Id).Distinct().Count() != candidate.Items.Length)
            throw new ArgumentException("safari_shop_scope");
        for (int i = 0; i < predecessor.Items.Length; i++)
            if (!ReferenceEquals(candidate.Items[i], predecessor.Items[i]))
                throw new ArgumentException("safari_shop_predecessor");
        var added = candidate.Items.Skip(predecessor.Items.Length).ToArray();
        if (Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(added))) != AdditionalItemsSha256)
            throw new ArgumentException("safari_shop_source");
        var proof = new SafariShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static SafariShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("safari_shop_authentication"); }
}
