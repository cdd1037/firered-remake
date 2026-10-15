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
        string Description, int[] Effects);
    private static readonly Expected[] Additions = [
        new("MAX_ETHER", 35, "MAX ETHER", 2000, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Ether", 1, "BattleUseFunc_Ether", "HOLD_EFFECT_NONE", 255, "Fully restores a selected move's PP\nfor one POKéMON.", [0,0,0,0,24,0,127]),
        new("PERSIM_BERRY", 140, "PERSIM BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_CONFUSION", 0, "When held by a POKéMON, it will be\nused in battle to lift confusion.", [0,0,0,1,0,0]),
        new("NANAB_BERRY", 150, "NANAB BERRY", 20, "POCKET_BERRY_POUCH", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, "Can be ground up into a powder as\nan ingredient for medicine.", []),
        new("EVERSTONE", 195, "EVERSTONE", 200, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_PREVENT_EVOLVE", 0, "An item to be held by a POKéMON.\nThe holding POKéMON is prevented\nfrom evolving.", []),
        new("PEARL", 106, "PEARL", 1400, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, "A relatively small pearl that\nsparkles in a pretty silver color.\nIt can be sold cheaply.", []),
        new("HARD_STONE", 204, "HARD STONE", 100, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_ROCK_POWER", 10, "An item to be held by a POKéMON.\nAn unbreakable stone that boosts\nthe power of ROCK-type moves.", []),
    ];

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
