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
        string Description, int[] Effects, EarlyShopUseSupport Use, uint Cure);
    private static readonly Expected[] Additions = [
        new("SUPER_REPEL", 83, "SUPER REPEL", 500, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_Repel", 0, "NULL", "HOLD_EFFECT_NONE", 200, "Prevents weak wild POKéMON from\nappearing for 200 steps.", [], EarlyShopUseSupport.DeferredRepel, 0),
        new("RAWST_BERRY", 136, "RAWST BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_BRN", 0, "When held by a POKéMON, it will be\nused in battle to heal a burn.", [0,0,0,8,0,0], EarlyShopUseSupport.StatusCure, 16),
        new("LUM_BERRY", 141, "LUM BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_STATUS", 0, "When held by a POKéMON, it will be\nused in battle to heal any problem.", [0,0,0,63,0,0], EarlyShopUseSupport.NoFieldUse, 0),
        new("LEPPA_BERRY", 138, "LEPPA BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Ether", 1, "BattleUseFunc_Ether", "HOLD_EFFECT_RESTORE_PP", 10, "When held by a POKéMON, it will be\nused in battle to restore 10 PP.", [0,0,0,0,24,0,10], EarlyShopUseSupport.NoFieldUse, 0),
        new("WEPEAR_BERRY", 151, "WEPEAR BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, "Can be ground up into a powder as\nan ingredient for medicine.", [], EarlyShopUseSupport.NoFieldUse, 0),
        new("PP_UP", 69, "PP UP", 9800, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_PpUp", 0, "NULL", "HOLD_EFFECT_NONE", 0, "Slightly raises the maximum PP of\na selected move for one POKéMON.", [0,0,0,0,32,224,5,3,2], EarlyShopUseSupport.NoFieldUse, 0),
        new("ICE_HEAL", 16, "ICE HEAL", 250, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_NONE", 0, "A spray-type medicine.\nIt defrosts a frozen POKéMON.", [0,0,0,4,0,0], EarlyShopUseSupport.StatusCure, 32),
    ];

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
