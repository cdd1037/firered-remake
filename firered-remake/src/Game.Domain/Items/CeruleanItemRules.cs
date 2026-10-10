using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>The Cerulean profile appends one medicine and one Mart without changing historical profiles.</summary>
public static class CeruleanItemRules
{
    public const string Profile = "firered-cerulean-shop-v1";
    public const string CeruleanMart = "CeruleanCity_Mart_Items";
    public static ItemId SuperPotionId { get; } = new("pret:ITEM_SUPER_POTION");

    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 18 ||
            catalog.Shops.IsDefault || catalog.Shops.Length != 3)
            throw new NotSupportedException("Only the pinned Cerulean shop additions are supported.");
        MtMoonItemRules.ValidateShop(catalog with
        {
            Profile = EarlyShopInventoryRules.MtMoonProfile,
            Items = catalog.Items.Take(17).ToImmutableArray(),
            Shops = catalog.Shops.Take(2).ToImmutableArray()
        });
        ValidateSuperPotion(catalog.Items[17]);
        ValidateStock(catalog.Shops[2]);
    }

    public static void ValidateSuperPotion(EarlyShopItemDefinition item)
    {
        var metadata = item?.SourceMetadata;
        if (item is null || metadata is null || item.Id != SuperPotionId ||
            item.SourceItemSymbol != "ITEM_SUPER_POTION" || item.SourceItemId != 22 ||
            item.Name != "SUPER POTION" || item.Price != 700 || item.Importance != 0 ||
            item.Pocket != "POCKET_ITEMS" || string.IsNullOrWhiteSpace(item.Description) ||
            item.Type != "ITEM_TYPE_PARTY_MENU" || item.FieldUseFunction != "FieldUseFunc_Medicine" ||
            item.BattleUsage != 1 || item.BattleUseFunction != "BattleUseFunc_Medicine" ||
            item.HoldEffectParam != 50 || item.HealHp != 50 || item.CureStatus != 0 ||
            item.UseSupport != EarlyShopUseSupport.Potion || item.EffectBytes.IsDefault ||
            !item.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 4, 0, 50 }) ||
            item.Source is null || item.EffectSource is null ||
            metadata.Id != item.Id || metadata.SourceItemSymbol != item.SourceItemSymbol ||
            metadata.SourceItemId != item.SourceItemId || metadata.Name != item.Name ||
            metadata.Price != item.Price || metadata.Importance != item.Importance ||
            metadata.Pocket != item.Pocket || metadata.Type != item.Type ||
            metadata.FieldUseFunction != item.FieldUseFunction || metadata.BattleUsage != item.BattleUsage ||
            metadata.BattleUseFunction != item.BattleUseFunction || metadata.HoldEffect != "HOLD_EFFECT_NONE" ||
            metadata.HoldEffectParam != 50 || metadata.Registrability != 0 || metadata.SecondaryId != 0 ||
            !metadata.CanGive || !metadata.CanToss || !metadata.CanSell || metadata.Source != item.Source)
            throw new NotSupportedException("Unsupported Cerulean Super Potion metadata.");
    }

    public static void ValidateStock(EarlyShopStock stock)
    {
        string[] names = ["POKE_BALL", "SUPER_POTION", "POTION", "ANTIDOTE", "PARALYZE_HEAL",
            "AWAKENING", "BURN_HEAL", "ESCAPE_ROPE", "REPEL"];
        if (stock is null || stock.Id != CeruleanMart || stock.Source is null || stock.Items.IsDefault ||
            !stock.Items.SequenceEqual(names.Select(name => new ItemId("pret:ITEM_" + name))))
            throw new NotSupportedException("Unsupported Cerulean Mart stock/order/terminator.");
    }
}
