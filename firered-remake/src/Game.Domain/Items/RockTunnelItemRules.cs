using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Six source additions to Route11; inherited active item effects stay unchanged.</summary>
public static class RockTunnelItemRules
{
    public const string Profile = "firered-rocktunnel-shop-v1";
    public static ItemId EverstoneId { get; } = new("pret:ITEM_EVERSTONE");
    public static ItemId MaxEtherId { get; } = new("pret:ITEM_MAX_ETHER");
    public static ItemId ReviveId { get; } = new("pret:ITEM_REVIVE");
    public static ItemId HardStoneId { get; } = new("pret:ITEM_HARD_STONE");
    private sealed record Expected(string Symbol, int Id, string Name, int Price, string Pocket,
        string Type, string FieldUse, int BattleUsage, string BattleUse, string HoldEffect, int Param,
        string Description, int[] Effects, OpeningItemSourceRef Source, OpeningItemSourceRef? EffectSource);
    private static readonly Expected[] Additions = [
        new("MAX_ETHER", 35, "MAX ETHER", 2000, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Ether", 1, "BattleUseFunc_Ether", "HOLD_EFFECT_NONE", 255, "Fully restores a selected move's PP\nfor one POKéMON.", [0,0,0,0,24,0,127], new("src/data/items.json", "ITEM_MAX_ETHER", 563, 578, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_MaxEther", 121, 124, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25")),
        new("PERSIM_BERRY", 140, "PERSIM BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_CONFUSION", 0, "When held by a POKéMON, it will be\nused in battle to lift confusion.", [0,0,0,1,0,0], new("src/data/items.json", "ITEM_PERSIM_BERRY", 2243, 2258, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), new("src/data/pokemon/item_effects.h", "sItemEffect_PersimBerry", 325, 327, "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25")),
        new("NANAB_BERRY", 150, "NANAB BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, "Can be ground up into a powder as\nan ingredient for medicine.", [], new("src/data/items.json", "ITEM_NANAB_BERRY", 2403, 2418, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), null),
        new("EVERSTONE", 195, "EVERSTONE", 200, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_PREVENT_EVOLVE", 0, "An item to be held by a POKéMON.\nThe holding POKéMON is prevented\nfrom evolving.", [], new("src/data/items.json", "ITEM_EVERSTONE", 3123, 3138, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), null),
        new("PEARL", 106, "PEARL", 1400, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, "A relatively small pearl that\nsparkles in a pretty silver color.\nIt can be sold cheaply.", [], new("src/data/items.json", "ITEM_PEARL", 1699, 1714, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), null),
        new("HARD_STONE", 204, "HARD STONE", 100, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_ROCK_POWER", 10, "An item to be held by a POKéMON.\nAn unbreakable stone that boosts\nthe power of ROCK-type moves.", [], new("src/data/items.json", "ITEM_HARD_STONE", 3267, 3282, "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489"), null),
    ];
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 38 ||
            catalog.Items.Any(i => i is null) || catalog.Items.Select(i => i.Id).Distinct().Count() != 38)
            throw new NotSupportedException("Exact RockTunnel item profile required.");
        Route11ItemRules.ValidateShop(catalog with { Profile = Route11ItemRules.Profile, Items = catalog.Items.Take(32).ToImmutableArray() });
        for (int n = 0; n < Additions.Length; n++)
        {
            var e = Additions[n]; var i = catalog.Items[n + 32]; var m = i?.SourceMetadata;
            if (i is null || m is null || i.Id != new ItemId("pret:ITEM_" + e.Symbol) || i.SourceItemSymbol != "ITEM_" + e.Symbol ||
                i.SourceItemId != e.Id || i.Name != e.Name || i.Price != e.Price || i.Importance != 0 ||
                i.Pocket != e.Pocket || i.Type != e.Type || i.FieldUseFunction != e.FieldUse || i.BattleUsage != e.BattleUsage ||
                i.BattleUseFunction != e.BattleUse || i.HoldEffectParam != e.Param || i.HealHp != 0 || i.CureStatus != 0 ||
                i.UseSupport != EarlyShopUseSupport.NoFieldUse || i.EffectBytes.IsDefault || !i.EffectBytes.SequenceEqual(e.Effects) ||
                i.Description != e.Description || i.Source != e.Source || i.EffectSource != e.EffectSource ||
                m.Id != i.Id || m.SourceItemSymbol != i.SourceItemSymbol || m.SourceItemId != e.Id || m.Name != e.Name ||
                m.Price != e.Price || m.Importance != 0 || m.Registrability != 0 || m.Pocket != e.Pocket ||
                m.Type != e.Type || m.HoldEffect != e.HoldEffect || m.HoldEffectParam != e.Param || m.FieldUseFunction != e.FieldUse ||
                m.BattleUsage != e.BattleUsage || m.BattleUseFunction != e.BattleUse || m.SecondaryId != 0 ||
                !m.CanToss || !m.CanGive || !m.CanSell || m.Source != i.Source)
                throw new NotSupportedException("RockTunnel source item differs: " + e.Symbol);
        }
    }
    public static bool IsBerry(ItemId item) => Route11ItemRules.IsBerry(item) || item.Value is "pret:ITEM_PERSIM_BERRY" or "pret:ITEM_NANAB_BERRY";
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
