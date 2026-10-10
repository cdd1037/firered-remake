using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Seven authenticated Lavender additions; validates the catalog.
/// Application profiles explicitly register supported item effects.</summary>
public static class LavenderItemRules
{
    public const string Profile = "firered-lavender-shop-v1";
    public static ItemId SuperRepelId { get; } = new("pret:ITEM_SUPER_REPEL");
    public static ItemId RawstBerryId { get; } = new("pret:ITEM_RAWST_BERRY");
    private sealed record Expected(string Symbol, int Id, string Name, int Price, string Pocket,
        string Type, string FieldUse, int BattleUsage, string BattleUse, string HoldEffect, int Param,
        string Description, int[] Effects, OpeningItemSourceRef Source, OpeningItemSourceRef? EffectSource, EarlyShopUseSupport Use, uint Cure);
    private static readonly Expected[] Additions = [
        new("SUPER_REPEL", 83, "SUPER REPEL", 500, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_Repel", 0, "NULL", "HOLD_EFFECT_NONE", 200, "Prevents weak wild POKéMON from\nappearing for 200 steps.", [], new("src/data/items.json", "ITEM_SUPER_REPEL", 1331, 1346, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), null, EarlyShopUseSupport.DeferredRepel, 0),
        new("RAWST_BERRY", 136, "RAWST BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_BRN", 0, "When held by a POKéMON, it will be\nused in battle to heal a burn.", [0,0,0,8,0,0], new("src/data/items.json", "ITEM_RAWST_BERRY", 2179, 2194, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_RawstBerry", 307, 309, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25"), EarlyShopUseSupport.StatusCure, 16),
        new("LUM_BERRY", 141, "LUM BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_STATUS", 0, "When held by a POKéMON, it will be\nused in battle to heal any problem.", [0,0,0,63,0,0], new("src/data/items.json", "ITEM_LUM_BERRY", 2259, 2274, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_LumBerry", 329, 331, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25"), EarlyShopUseSupport.NoFieldUse, 0),
        new("LEPPA_BERRY", 138, "LEPPA BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Ether", 1, "BattleUseFunc_Ether", "HOLD_EFFECT_RESTORE_PP", 10, "When held by a POKéMON, it will be\nused in battle to restore 10 PP.", [0,0,0,0,24,0,10], new("src/data/items.json", "ITEM_LEPPA_BERRY", 2211, 2226, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_LeppaBerry", 315, 318, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25"), EarlyShopUseSupport.NoFieldUse, 0),
        new("WEPEAR_BERRY", 151, "WEPEAR BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, "Can be ground up into a powder as\nan ingredient for medicine.", [], new("src/data/items.json", "ITEM_WEPEAR_BERRY", 2419, 2434, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), null, EarlyShopUseSupport.NoFieldUse, 0),
        new("PP_UP", 69, "PP UP", 9800, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_PpUp", 0, "NULL", "HOLD_EFFECT_NONE", 0, "Slightly raises the maximum PP of\na selected move for one POKéMON.", [0,0,0,0,32,224,5,3,2], new("src/data/items.json", "ITEM_PP_UP", 1107, 1122, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_PPUp", 208, 212, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25"), EarlyShopUseSupport.NoFieldUse, 0),
        new("ICE_HEAL", 16, "ICE HEAL", 250, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_NONE", 0, "A spray-type medicine.\nIt defrosts a frozen POKéMON.", [0,0,0,4,0,0], new("src/data/items.json", "ITEM_ICE_HEAL", 259, 274, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_IceHeal", 14, 16, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25"), EarlyShopUseSupport.StatusCure, 32),
    ];
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 45 ||
            catalog.Shops.IsDefault || catalog.Shops.Length != 5 || catalog.Items.Any(i => i is null) || catalog.Items.Select(i => i.Id).Distinct().Count() != 45)
            throw new NotSupportedException("Exact Lavender item profile required.");
        RockTunnelItemRules.ValidateShop(catalog with { Profile = RockTunnelItemRules.Profile, Items = catalog.Items.Take(38).ToImmutableArray(), Shops = catalog.Shops.Take(4).ToImmutableArray() });
        var stock = catalog.Shops[4];
        string[] expectedStock = ["GREAT_BALL", "SUPER_POTION", "REVIVE", "ANTIDOTE", "PARALYZE_HEAL", "BURN_HEAL", "ICE_HEAL", "ESCAPE_ROPE", "SUPER_REPEL"];
        if (stock is null || stock.Id != "LavenderTown_Mart_Items" || stock.Items.IsDefault ||
            !stock.Items.Select(i => i.Value).SequenceEqual(expectedStock.Select(s => "pret:ITEM_" + s)) ||
            stock.Source != new OpeningItemSourceRef("data/maps/LavenderTown_Mart/scripts.inc", "LavenderTown_Mart_Items", 28, 38, "323771243429c1b559bfd5ae41a00525bcd5b0579253ce0ee96da7584201e8d9") || stock.Items.Any(id => !catalog.Items.Any(i => i.Id == id)))
            throw new NotSupportedException("Lavender Mart source stock differs.");
        for (int n = 0; n < Additions.Length; n++)
        {
            var e = Additions[n]; var i = catalog.Items[n + 38]; var m = i?.SourceMetadata;
            if (i is null || m is null || i.Id != new ItemId("pret:ITEM_" + e.Symbol) || i.SourceItemSymbol != "ITEM_" + e.Symbol ||
                i.SourceItemId != e.Id || i.Name != e.Name || i.Price != e.Price || i.Importance != 0 ||
                i.Pocket != e.Pocket || i.Type != e.Type || i.FieldUseFunction != e.FieldUse || i.BattleUsage != e.BattleUsage ||
                i.BattleUseFunction != e.BattleUse || i.HoldEffectParam != e.Param || i.HealHp != 0 || i.CureStatus != e.Cure ||
                i.UseSupport != e.Use || i.EffectBytes.IsDefault || !i.EffectBytes.SequenceEqual(e.Effects) ||
                i.Description != e.Description || i.Source != e.Source || i.EffectSource != e.EffectSource ||
                m.Id != i.Id || m.SourceItemSymbol != i.SourceItemSymbol || m.SourceItemId != e.Id || m.Name != e.Name ||
                m.Price != e.Price || m.Importance != 0 || m.Registrability != 0 || m.Pocket != e.Pocket ||
                m.Type != e.Type || m.HoldEffect != e.HoldEffect || m.HoldEffectParam != e.Param || m.FieldUseFunction != e.FieldUse ||
                m.BattleUsage != e.BattleUsage || m.BattleUseFunction != e.BattleUse || m.SecondaryId != 0 ||
                !m.CanToss || !m.CanGive || !m.CanSell || m.Source != i.Source)
                throw new NotSupportedException("Lavender source item differs: " + e.Symbol);
        }
    }
    public static bool IsBerry(ItemId item) => RockTunnelItemRules.IsBerry(item) || item.Value is "pret:ITEM_RAWST_BERRY" or "pret:ITEM_LUM_BERRY" or "pret:ITEM_LEPPA_BERRY" or "pret:ITEM_WEPEAR_BERRY";
    public static string? ValidateBerries(InventoryState state)
    {
        if (state.KeyItems is {} keys && (keys.IsDefault || keys.Any(k => k is null))) return "inventory_stack_missing";
        int pouch = OpeningInventoryRules.Quantity(state.KeyItems ?? [], SsAnneItemRules.BerryPouchId);
        if (pouch > 1) return "inventory_berry_pouch_quantity";
        if (state.Berries is null) return null;
        var berries = state.Berries.Value;
        if (berries.IsDefault || berries.Length > SsAnneItemRules.BerrySlots) return "inventory_berries_capacity";
        if (berries.Length > 0 && pouch != 1) return "inventory_berry_pouch_missing";
        var seen = new HashSet<ItemId>();
        foreach (var stack in berries)
        {
            if (stack is null) return "inventory_stack_missing";
            if (!IsBerry(stack.Item)) return "inventory_item_unsupported";
            if (stack.Quantity is < 1 or > 999) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }
}
