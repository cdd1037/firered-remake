using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Bounded ship item additions; ordinary historical shops and serialized defaults stay unchanged.</summary>
public static class SsAnneItemRules
{
    public const string Profile = "firered-ssanne-shop-v1";
    public const int BerrySlots = 43;
    public static ItemId BerryPouchId { get; } = new("pret:ITEM_BERRY_POUCH");
    public static ItemId GreatBallId { get; } = new("pret:ITEM_GREAT_BALL");
    public static ItemId HyperPotionId { get; } = new("pret:ITEM_HYPER_POTION");
    private sealed record Expected(string Symbol, int Id, string Name, int Price, int Importance, int Registrability,
        string Pocket, string Type, string FieldUse, int BattleUsage, string BattleUse, string HoldEffect,
        int Param, int SecondaryId, EarlyShopUseSupport Use, int Heal, uint Cure, int[] Effects);
    private static readonly Expected[] Additions = [
        new("STARDUST", 108, "STARDUST", 2000, 0, 0, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 0, "NULL", "HOLD_EFFECT_NONE", 0, 0, EarlyShopUseSupport.NoFieldUse, 0, 0, []),
        new("X_ATTACK", 75, "X ATTACK", 500, 0, 0, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_OakStopsYou", 2, "BattleUseFunc_StatBooster", "HOLD_EFFECT_NONE", 0, 0, EarlyShopUseSupport.NoFieldUse, 0, 0, [1,0,0,0,0,96,1,1]),
        new("GREAT_BALL", 3, "GREAT BALL", 600, 0, 0, "POCKET_POKE_BALLS", "2", "NULL", 2, "BattleUseFunc_PokeBallEtc", "HOLD_EFFECT_NONE", 0, 2, EarlyShopUseSupport.SeparateCapture, 0, 0, []),
        new("HYPER_POTION", 21, "HYPER POTION", 1200, 0, 0, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_NONE", 200, 0, EarlyShopUseSupport.Potion, 200, 0, [0,0,0,0,4,0,200]),
        new("LAVA_COOKIE", 38, "LAVA COOKIE", 200, 0, 0, "POCKET_ITEMS", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_NONE", 0, 0, EarlyShopUseSupport.NoFieldUse, 0, 0, [0,0,0,63,0,0]),
        new("CHERI_BERRY", 133, "CHERI BERRY", 20, 0, 0, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_PAR", 0, 0, EarlyShopUseSupport.StatusCure, 0, 64, [0,0,0,2,0,0]),
        new("CHESTO_BERRY", 134, "CHESTO BERRY", 20, 0, 0, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_SLP", 0, 0, EarlyShopUseSupport.DeferredSleep, 0, 0, [0,0,0,32,0,0]),
        new("PECHA_BERRY", 135, "PECHA BERRY", 20, 0, 0, "POCKET_BERRY_POUCH", "ITEM_TYPE_PARTY_MENU", "FieldUseFunc_Medicine", 1, "BattleUseFunc_Medicine", "HOLD_EFFECT_CURE_PSN", 0, 0, EarlyShopUseSupport.StatusCure, 0, 8, [0,0,0,16,0,0]),
        new("BERRY_POUCH", 365, "BERRY POUCH", 0, 1, 1, "POCKET_KEY_ITEMS", "ITEM_TYPE_BAG_MENU", "FieldUseFunc_BerryPouch", 3, "BattleUseFunc_BerryPouch", "HOLD_EFFECT_NONE", 0, 0, EarlyShopUseSupport.NoFieldUse, 0, 0, []),
    ];
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 30)
            throw new NotSupportedException("Exact S.S. Anne item profile required.");
        VermilionItemRules.ValidateShop(catalog with { Profile = VermilionItemRules.Profile, Items = catalog.Items.Take(21).ToImmutableArray() });
        for (int n = 0; n < Additions.Length; n++)
        {
            var e = Additions[n]; var i = catalog.Items[n + 21]; var m = i?.SourceMetadata;
            bool ordinary = e.Importance == 0;
            if (i is null || m is null || i.Id != new ItemId("pret:ITEM_" + e.Symbol) || i.SourceItemSymbol != "ITEM_" + e.Symbol ||
                i.SourceItemId != e.Id || i.Name != e.Name || i.Price != e.Price || i.Importance != e.Importance ||
                i.Pocket != e.Pocket || i.Type != e.Type || i.FieldUseFunction != e.FieldUse || i.BattleUsage != e.BattleUsage ||
                i.BattleUseFunction != e.BattleUse || i.HoldEffectParam != e.Param || i.HealHp != e.Heal || i.CureStatus != e.Cure ||
                i.UseSupport != e.Use || i.EffectBytes.IsDefault || !i.EffectBytes.SequenceEqual(e.Effects) ||
                string.IsNullOrWhiteSpace(i.Description) || i.Source is null || (e.Effects.Length > 0) != (i.EffectSource is not null) ||
                m.Id != i.Id || m.SourceItemSymbol != i.SourceItemSymbol || m.SourceItemId != e.Id || m.Name != e.Name ||
                m.Price != e.Price || m.Importance != e.Importance || m.Registrability != e.Registrability || m.Pocket != e.Pocket ||
                m.Type != e.Type || m.HoldEffect != e.HoldEffect || m.HoldEffectParam != e.Param || m.FieldUseFunction != e.FieldUse ||
                m.BattleUsage != e.BattleUsage || m.BattleUseFunction != e.BattleUse || m.SecondaryId != e.SecondaryId ||
                m.CanToss != ordinary || m.CanGive != ordinary || m.CanSell != (e.Price > 0) || m.Source != i.Source)
                throw new NotSupportedException("S.S. Anne source item differs: " + e.Symbol);
        }
    }

    public static bool IsBerry(ItemId item) => item.Value is "pret:ITEM_CHERI_BERRY" or "pret:ITEM_CHESTO_BERRY" or "pret:ITEM_PECHA_BERRY";
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
