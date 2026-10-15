using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Bounded Route11 item additions over the historical ship catalog.</summary>
public static class Route11ItemRules
{
    public const string Profile = "firered-route11-shop-v1";
    public const int BerrySlots = 43;
    public static ItemId BerryPouchId { get; } = new("pret:ITEM_BERRY_POUCH");
    public static ItemId GreatBallId { get; } = new("pret:ITEM_GREAT_BALL");
    public static ItemId HyperPotionId { get; } = new("pret:ITEM_HYPER_POTION");
    private sealed record Expected(string Symbol, int Id, string Name, int Price, int Importance, int Registrability,
        string Pocket, string Type, string FieldUse, int BattleUsage, string BattleUse, string HoldEffect,
        int Param, int SecondaryId, EarlyShopUseSupport Use, int Heal, uint Cure, int[] Effects);
    private static readonly Expected[] Additions = [
        new("X_DEFEND", 76, "X DEFEND", 550, 0, 0, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 2, "BattleUseFunc_StatBooster", "HOLD_EFFECT_NONE", 0, 0, EarlyShopUseSupport.NoFieldUse, 0, 0, [0,16,0,0,0,96,1,1]),
        new("ASPEAR_BERRY", 137, "ASPEAR BERRY", 20, 0, 0, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_FRZ", 0, 0, EarlyShopUseSupport.StatusCure, 0, 32, [0,0,0,4,0,0]),
    ];


    public static bool IsBerry(ItemId item) => item.Value is "pret:ITEM_CHERI_BERRY" or "pret:ITEM_CHESTO_BERRY" or "pret:ITEM_PECHA_BERRY" or "pret:ITEM_ASPEAR_BERRY";
    public static EarlyShopItemDefinition? Definition(EarlyShopCatalog catalog, ItemId item)
        => catalog.Items.FirstOrDefault(i => i.Id == item);

    public static string? ValidateBerries(InventoryState state)
    {
        if (state.KeyItems is {} keys && (keys.IsDefault || keys.Any(k => k is null))) return "inventory_stack_missing";
        int pouch = OpeningInventoryRules.Quantity(state.KeyItems ?? [], BerryPouchId);
        if (pouch > 1) return "inventory_berry_pouch_quantity";
        if (state.Berries is null) return null;
        var berries = state.Berries.Value;
        if (berries.IsDefault || berries.Length > BerrySlots) return "inventory_berries_capacity";
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
