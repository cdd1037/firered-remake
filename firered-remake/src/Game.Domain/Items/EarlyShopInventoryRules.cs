using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Opt-in future inventory profile. Does not relax OpeningInventoryRules or alter serialized defaults.</summary>
public static class EarlyShopInventoryRules
{
    public const string MtMoonProfile = "firered-mtmoon-shop-v1";
    public const string Profile = "firered-viridian-pewter-shop-v1";
    public const string ViridianMart = "ViridianCity_Mart_Items", PewterMart = "PewterCity_Mart_Items";
    private sealed record Expected(string Symbol, int Id, string Name, int Price, EarlyShopUseSupport Use,
        int EffectFlag = 0, uint Status = 0);
    private static readonly Expected[] ExpectedItems = [
        new("ITEM_POKE_BALL", 4, "POKé BALL", 200, EarlyShopUseSupport.SeparateCapture),
        new("ITEM_POTION", 13, "POTION", 300, EarlyShopUseSupport.Potion, 4),
        new("ITEM_ANTIDOTE", 14, "ANTIDOTE", 100, EarlyShopUseSupport.StatusCure, 16, 8),
        new("ITEM_PARALYZE_HEAL", 18, "PARLYZ HEAL", 200, EarlyShopUseSupport.StatusCure, 2, 64),
        new("ITEM_AWAKENING", 17, "AWAKENING", 250, EarlyShopUseSupport.DeferredSleep, 32),
        new("ITEM_BURN_HEAL", 15, "BURN HEAL", 250, EarlyShopUseSupport.StatusCure, 8, 16),
        new("ITEM_ESCAPE_ROPE", 85, "ESCAPE ROPE", 550, EarlyShopUseSupport.DeferredEscape),
        new("ITEM_REPEL", 86, "REPEL", 350, EarlyShopUseSupport.DeferredRepel)
    ];

    public static void ValidateCatalog(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile == EventIslandShopCatalog.Profile) { EventIslandShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SevenIslandShopCatalog.Profile) { SevenIslandShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == FiveIslandShopCatalog.Profile) { FiveIslandShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SixIslandShopCatalog.Profile) { SixIslandShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == FourIslandShopCatalog.Profile) { FourIslandShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SeviiRubyShopCatalog.Profile) { SeviiRubyShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == VictoryRoadShopCatalog.Profile) { VictoryRoadShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == FirstSeviiShopCatalog.Profile) { FirstSeviiShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == CinnabarShopCatalog.Profile) { CinnabarShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SaffronGymShopCatalog.Profile) { SaffronGymShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SilphShopCatalog.Profile) { SilphShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SafariShopCatalog.Profile) { SafariShopCatalog.ValidateCatalog(catalog); return; }
        if (catalog.Profile == FuchsiaGymItemRules.Profile) { FuchsiaGymItemRules.ValidateShop(catalog); return; }
        if (catalog.Profile == PokemonTowerItemRules.Profile) { PokemonTowerItemRules.ValidateShop(catalog); return; }
        if (catalog.Profile == RocketHideoutItemRules.Profile) { RocketHideoutItemRules.ValidateShop(catalog); return; }
        if (catalog.Profile == CeladonGymItemRules.Profile) { CeladonGymItemRules.ValidateShop(catalog); return; }
        if (catalog.Profile == LavenderItemRules.Profile) { LavenderItemRules.ValidateShop(catalog); return; }
        if (catalog.Profile == RockTunnelItemRules.Profile)
        {
            RockTunnelItemRules.ValidateShop(catalog);
            return;
        }
        if (catalog.Profile == Route11ItemRules.Profile) { Route11ItemRules.ValidateShop(catalog); return; }
        if (catalog.Profile == SsAnneItemRules.Profile)
        {
            SsAnneItemRules.ValidateShop(catalog);
            return;
        }
        if (catalog.Profile == VermilionItemRules.Profile)
        {
            VermilionItemRules.ValidateShop(catalog);
            return;
        }
        if (catalog.Profile == BillItemRules.Profile)
        {
            BillItemRules.ValidateShop(catalog);
            return;
        }
        if (catalog.Profile == CeruleanItemRules.Profile)
        {
            CeruleanItemRules.ValidateShop(catalog);
            return;
        }
        if (catalog.Profile == MtMoonProfile)
        {
            MtMoonItemRules.ValidateShop(catalog);
            return;
        }
        if (catalog.SourceCommit != OpeningInventoryRules.SourceCommit || catalog.Profile != Profile ||
            catalog.CompleteItemCatalog || catalog.Limits != OpeningInventoryRules.SourceLimits ||
            catalog.QuestLimits != QuestItemRules.SourceLimits || catalog.MaximumTransactionQuantity != 99 ||
            catalog.MaximumMoney != 999999 || catalog.Items.IsDefault || catalog.Items.Length != 8 ||
            catalog.Shops.IsDefault || catalog.Shops.Length != 2)
            throw new NotSupportedException("Only the pinned early-shop profile is supported.");
        for (int n = 0; n < ExpectedItems.Length; n++)
        {
            var e = ExpectedItems[n]; var i = catalog.Items[n];
            bool medicine = e.Use is EarlyShopUseSupport.Potion or EarlyShopUseSupport.StatusCure or EarlyShopUseSupport.DeferredSleep;
            bool ball = e.Use == EarlyShopUseSupport.SeparateCapture;
            int[] effects = e.Use == EarlyShopUseSupport.Potion ? [0, 0, 0, 0, 4, 0, 20] :
                medicine ? [0, 0, 0, e.EffectFlag, 0, 0] : [];
            string type = medicine ? "ITEM_TYPE_PARTY_MENU" : ball ? "3" :
                e.Use == EarlyShopUseSupport.DeferredEscape ? "ITEM_TYPE_FIELD" : "ITEM_TYPE_BAG_MENU";
            string field = medicine ? "FieldUseFunc_Medicine" : ball ? "NULL" :
                e.Use == EarlyShopUseSupport.DeferredEscape ? "ItemUseOutOfBattle_EscapeRope" : "FieldUseFunc_Repel";
            if (i is null || i.Id != new ItemId("pret:" + e.Symbol) || i.SourceItemSymbol != e.Symbol ||
                i.SourceItemId != e.Id || i.Name != e.Name || i.Price != e.Price || i.Importance != 0 ||
                string.IsNullOrWhiteSpace(i.Description) || i.Pocket != (ball ? "POCKET_POKE_BALLS" : "POCKET_ITEMS") ||
                i.Type != type || i.FieldUseFunction != field || i.BattleUsage != (medicine ? 1 : ball ? 2 : 0) ||
                i.BattleUseFunction != (medicine ? "BattleUseFunc_Medicine" : ball ? "BattleUseFunc_PokeBallEtc" : "NULL") ||
                i.HoldEffectParam != (e.Use == EarlyShopUseSupport.Potion ? 20 : e.Use == EarlyShopUseSupport.DeferredRepel ? 100 : 0) ||
                i.HealHp != (e.Use == EarlyShopUseSupport.Potion ? 20 : 0) || i.CureStatus != e.Status ||
                i.UseSupport != e.Use || i.EffectBytes.IsDefault || !i.EffectBytes.SequenceEqual(effects) ||
                i.SourceMetadata is not null || i.Source is null || (medicine ? i.EffectSource is null : i.EffectSource is not null))
                throw new NotSupportedException("Unsupported early-shop item metadata.");
        }
        for (int n = 0; n < 2; n++)
        {
            var shop = catalog.Shops[n];
            if (shop is null || shop.Id != (n == 0 ? ViridianMart : PewterMart) || shop.Source is null ||
                shop.Items.IsDefault || !shop.Items.SequenceEqual(catalog.Items.Take(n == 0 ? 4 : 8).Select(i => i.Id)))
                throw new NotSupportedException("Unsupported early-shop stock or ITEM_NONE terminator.");
        }
    }

    public static string? Validate(EarlyShopCatalog catalog, InventoryState? state)
    {
        ValidateCatalog(catalog);
        if (EventIslandShopCatalog.For(catalog) is { } events) return events.ValidateInventory(state);
        if (SevenIslandShopCatalog.For(catalog) is { } sevenIsland) return sevenIsland.ValidateInventory(state);
        if (FiveIslandShopCatalog.For(catalog) is { } fiveIsland) return fiveIsland.ValidateInventory(state);
        if (SixIslandShopCatalog.For(catalog) is { } sixIsland) return sixIsland.ValidateInventory(state);
        if (FourIslandShopCatalog.For(catalog) is { } fourIsland) return fourIsland.ValidateInventory(state);
        if (SeviiRubyShopCatalog.For(catalog) is { } ruby) return ruby.ValidateInventory(state);
        if (VictoryRoadShopCatalog.For(catalog) is { } victoryRoad) return victoryRoad.ValidateInventory(state);
        if (FirstSeviiShopCatalog.For(catalog) is { } sevii) return sevii.ValidateInventory(state);
        if (CinnabarShopCatalog.For(catalog) is { } cinnabar) return cinnabar.ValidateInventory(state);
        if (state is null) return "inventory_missing";
        if (state.Limits != catalog.Limits) return "inventory_limits";
        if (catalog.Profile is not (SsAnneItemRules.Profile or Route11ItemRules.Profile or RockTunnelItemRules.Profile or LavenderItemRules.Profile or CeladonGymItemRules.Profile or RocketHideoutItemRules.Profile or PokemonTowerItemRules.Profile or FuchsiaGymItemRules.Profile or SafariShopCatalog.Profile or SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile) && state.Berries is not null) return "inventory_berry_profile_missing";
        string? berryError = catalog.Profile is RocketHideoutItemRules.Profile or PokemonTowerItemRules.Profile or FuchsiaGymItemRules.Profile or SafariShopCatalog.Profile or SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile ? RocketHideoutItemRules.ValidateBerries(state) : catalog.Profile == CeladonGymItemRules.Profile ? CeladonGymItemRules.ValidateBerries(state) : catalog.Profile == LavenderItemRules.Profile ? LavenderItemRules.ValidateBerries(state) : catalog.Profile == RockTunnelItemRules.Profile ? RockTunnelItemRules.ValidateBerries(state) : catalog.Profile == Route11ItemRules.Profile ? Route11ItemRules.ValidateBerries(state) : catalog.Profile == SsAnneItemRules.Profile ? SsAnneItemRules.ValidateBerries(state) : null;
        if (berryError is not null) return berryError;
        var ordinary = catalog.Items.Where(i => i.Pocket == "POCKET_ITEMS").Select(i => i.Id).ToHashSet();
        return ValidatePocket(state.BagItems, state.Limits.BagItemSlots, ordinary, "bag") ??
            ValidatePocket(state.PcItems, state.Limits.PcItemSlots, catalog.Items.Where(i => i.Pocket != "POCKET_KEY_ITEMS").Select(i => i.Id).ToHashSet(), "pc") ??
            ValidateOptional(state.KeyItems, catalog.QuestLimits.KeyItemSlots, catalog.Profile is SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile ? [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId, MtMoonItemRules.HelixFossilId, MtMoonItemRules.DomeFossilId, BillItemRules.FameCheckerId, BillItemRules.SsTicketId, SsAnneItemRules.BerryPouchId, RocketHideoutItemRules.LiftKeyId, RocketHideoutItemRules.SilphScopeId, PokemonTowerItemRules.PokeFluteId, SilphShopCatalog.CardKeyId] : catalog.Profile is PokemonTowerItemRules.Profile or FuchsiaGymItemRules.Profile or SafariShopCatalog.Profile or SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile ? [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId, MtMoonItemRules.HelixFossilId, MtMoonItemRules.DomeFossilId, BillItemRules.FameCheckerId, BillItemRules.SsTicketId, SsAnneItemRules.BerryPouchId, RocketHideoutItemRules.LiftKeyId, RocketHideoutItemRules.SilphScopeId, PokemonTowerItemRules.PokeFluteId] : catalog.Profile is RocketHideoutItemRules.Profile or PokemonTowerItemRules.Profile ? [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId, MtMoonItemRules.HelixFossilId, MtMoonItemRules.DomeFossilId, BillItemRules.FameCheckerId, BillItemRules.SsTicketId, SsAnneItemRules.BerryPouchId, RocketHideoutItemRules.LiftKeyId, RocketHideoutItemRules.SilphScopeId] : catalog.Profile is SsAnneItemRules.Profile or Route11ItemRules.Profile or RockTunnelItemRules.Profile or LavenderItemRules.Profile or CeladonGymItemRules.Profile or RocketHideoutItemRules.Profile or PokemonTowerItemRules.Profile ? [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId, MtMoonItemRules.HelixFossilId, MtMoonItemRules.DomeFossilId, BillItemRules.FameCheckerId, BillItemRules.SsTicketId, SsAnneItemRules.BerryPouchId] : catalog.Profile is BillItemRules.Profile or VermilionItemRules.Profile ? [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId, MtMoonItemRules.HelixFossilId, MtMoonItemRules.DomeFossilId, BillItemRules.FameCheckerId, BillItemRules.SsTicketId] : catalog.Profile is MtMoonProfile or CeruleanItemRules.Profile ? [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId, MtMoonItemRules.HelixFossilId, MtMoonItemRules.DomeFossilId] : [QuestItemRules.ParcelId, QuestItemRules.TeachyTvId], "key_items") ??
            ValidateOptional(state.Balls, catalog.QuestLimits.BallSlots, catalog.Profile is SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile ? [QuestItemRules.PokeBallId, SsAnneItemRules.GreatBallId, RocketHideoutItemRules.NetBallId, RocketHideoutItemRules.NestBallId, SilphShopCatalog.MasterBallId, SilphShopCatalog.UltraBallId] : catalog.Profile is RocketHideoutItemRules.Profile or PokemonTowerItemRules.Profile or FuchsiaGymItemRules.Profile or SafariShopCatalog.Profile or SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile ? [QuestItemRules.PokeBallId, SsAnneItemRules.GreatBallId, RocketHideoutItemRules.NetBallId, RocketHideoutItemRules.NestBallId] : catalog.Profile is SsAnneItemRules.Profile or Route11ItemRules.Profile or RockTunnelItemRules.Profile or LavenderItemRules.Profile or CeladonGymItemRules.Profile or RocketHideoutItemRules.Profile or PokemonTowerItemRules.Profile ? [QuestItemRules.PokeBallId, SsAnneItemRules.GreatBallId] : [QuestItemRules.PokeBallId], "balls");
    }

    private static string? ValidateOptional(ImmutableArray<InventoryStack>? stacks, int capacity, HashSet<ItemId> allowed, string name)
        => stacks is null ? null : ValidatePocket(stacks.Value, capacity, allowed, name);

    private static string? ValidatePocket(ImmutableArray<InventoryStack> stacks, int capacity, HashSet<ItemId> allowed, string name)
    {
        if (stacks.IsDefault || stacks.Length > capacity) return "inventory_" + name + "_capacity";
        var seen = new HashSet<ItemId>();
        foreach (var stack in stacks)
        {
            if (stack is null) return "inventory_stack_missing";
            if (!allowed.Contains(stack.Item)) return "inventory_item_unsupported";
            if (stack.Quantity is < 1 or > 999) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }

    internal static EarlyShopItemDefinition? Find(EarlyShopCatalog catalog, ItemId item) => catalog.Items.FirstOrDefault(i => i.Id == item);
    internal static ImmutableArray<InventoryStack> Pocket(InventoryState inventory, EarlyShopItemDefinition item)
        => item.Pocket switch { "POCKET_POKE_BALLS" => inventory.Balls ?? [], "POCKET_KEY_ITEMS" => inventory.KeyItems ?? [], "POCKET_BERRY_POUCH" => inventory.Berries ?? [], _ => inventory.BagItems };
    internal static InventoryState SetQuantity(InventoryState inventory, EarlyShopItemDefinition item, int quantity)
    {
        var changed = OpeningInventoryRules.SetQuantity(Pocket(inventory, item), item.Id, quantity);
        return item.Pocket switch { "POCKET_POKE_BALLS" => inventory with { Balls = changed },
            "POCKET_KEY_ITEMS" => inventory with { KeyItems = changed }, "POCKET_BERRY_POUCH" => inventory with { Berries = changed }, _ => inventory with { BagItems = changed } };
    }
}
