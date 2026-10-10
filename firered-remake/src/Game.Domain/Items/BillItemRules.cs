using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>The Bill route appends three source quest items without changing older shops or pockets.</summary>
public static class BillItemRules
{
    public const string Profile = "firered-bill-shop-v1";
    public static ItemId NuggetId { get; } = new("pret:ITEM_NUGGET");
    public static ItemId FameCheckerId { get; } = new("pret:ITEM_FAME_CHECKER");
    public static ItemId SsTicketId { get; } = new("pret:ITEM_SS_TICKET");

    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 21 ||
            catalog.Shops.IsDefault || catalog.Shops.Length != 3)
            throw new NotSupportedException("Only the pinned Bill shop additions are supported.");
        CeruleanItemRules.ValidateShop(catalog with
        {
            Profile = CeruleanItemRules.Profile,
            Items = catalog.Items.Take(18).ToImmutableArray()
        });
        ValidateItem(catalog.Items[18], "NUGGET", 110, "NUGGET", 10000, false, false, "FieldUseFunc_OakStopsYou");
        ValidateItem(catalog.Items[19], "FAME_CHECKER", 363, "FAME CHECKER", 0, true, true, "FieldUseFunc_FameChecker");
        ValidateItem(catalog.Items[20], "SS_TICKET", 265, "S.S. TICKET", 0, true, false, "FieldUseFunc_OakStopsYou");
    }

    private static void ValidateItem(EarlyShopItemDefinition item, string symbol, int id, string name,
        int price, bool key, bool registrable, string fieldUse)
    {
        var metadata = item?.SourceMetadata;
        if (item is null || metadata is null || item.Id != new ItemId("pret:ITEM_" + symbol) ||
            item.SourceItemSymbol != "ITEM_" + symbol || item.SourceItemId != id || item.Name != name ||
            item.Price != price || item.Importance != (key ? 1 : 0) ||
            item.Pocket != (key ? "POCKET_KEY_ITEMS" : "POCKET_ITEMS") || string.IsNullOrWhiteSpace(item.Description) ||
            item.Type != "ITEM_TYPE_BAG_MENU" || item.FieldUseFunction != fieldUse ||
            item.BattleUsage != 0 || item.BattleUseFunction != "NULL" || item.HoldEffectParam != 0 ||
            item.HealHp != 0 || item.CureStatus != 0 || item.UseSupport != EarlyShopUseSupport.NoFieldUse ||
            item.EffectBytes.IsDefault || item.EffectBytes.Length != 0 || item.EffectSource is not null || item.Source is null ||
            metadata.Id != item.Id || metadata.SourceItemSymbol != item.SourceItemSymbol ||
            metadata.SourceItemId != item.SourceItemId || metadata.Name != item.Name || metadata.Price != item.Price ||
            metadata.Importance != item.Importance || metadata.Pocket != item.Pocket || metadata.Type != item.Type ||
            metadata.FieldUseFunction != item.FieldUseFunction || metadata.BattleUsage != item.BattleUsage ||
            metadata.BattleUseFunction != item.BattleUseFunction || metadata.HoldEffect != "HOLD_EFFECT_NONE" ||
            metadata.HoldEffectParam != 0 || metadata.Registrability != (registrable ? 1 : 0) || metadata.SecondaryId != 0 ||
            metadata.CanGive != !key || metadata.CanToss != !key || metadata.CanSell != !key || metadata.Source != item.Source)
            throw new NotSupportedException("Unsupported Bill item metadata: " + symbol);
    }
}
