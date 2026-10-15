using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Original seven-item Vermilion stock; all prior item definitions remain unchanged.</summary>
public static class VermilionItemRules
{
    public const string Profile = "firered-vermilion-shop-v1";
    public const string Mart = "VermilionCity_Mart_Items";
    public static ImmutableArray<ItemId> Stock { get; } =
        new[] { "POKE_BALL", "SUPER_POTION", "ANTIDOTE", "PARALYZE_HEAL", "AWAKENING", "ICE_HEAL", "REPEL" }
        .Select(symbol => new ItemId("pret:ITEM_" + symbol)).ToImmutableArray();

    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Shops.IsDefault || catalog.Shops.Length != 4)
            throw new NotSupportedException("Exact Vermilion shop profile required.");
        BillItemRules.ValidateShop(catalog with { Profile = BillItemRules.Profile, Shops = catalog.Shops.Take(3).ToImmutableArray() });
        var shop = catalog.Shops[3];
        if (shop is null || shop.Id != Mart || shop.Items.IsDefault || !shop.Items.SequenceEqual(Stock) ||
            shop.Source is not { Path: "data/maps/VermilionCity_Mart/scripts.inc", Symbol: Mart, StartLine: 24, EndLine: 32 })
            throw new NotSupportedException("Vermilion source stock differs.");
    }
}
