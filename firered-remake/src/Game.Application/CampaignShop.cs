using System.Collections.Immutable;
using System.Globalization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public enum CampaignShopStage { Menu, Buy, Sell, Quantity, Confirmation, Message }
public sealed record CampaignShop(string ShopId, CampaignShopStage Stage, string Mode = "buy", ItemId? Item = null,
    int Quantity = 1, string? MessageSymbol = null, CampaignShopStage ReturnStage = CampaignShopStage.Menu);

public sealed partial class CampaignSession
{
    private EarlyShopCatalog ShopCatalog => _content.EarlyShop ?? throw new InvalidDataException("early_shop_not_enabled");
    private CampaignState OpenShop(CampaignState state, StoryEffect effect)
    {
        var catalog = ShopCatalog;
        string map = state.World.Player.MapId.Value;
        string sourceMap = map switch { "map.lavender-town-mart" when _content.CeladonApproachEnabled => "LavenderTown_Mart", "map.vermilion-city-mart" when _content.VermilionEnabled => "VermilionCity_Mart", "map.viridian-mart" => "ViridianCity_Mart", "map.pewter-mart" => "PewterCity_Mart", "map.cerulean-city-mart" when _content.CeruleanEnabled => "CeruleanCity_Mart", _ => throw new InvalidDataException("shop_map_unsupported") };
        if (effect.Args.Length != 1 || effect.Args[0] != sourceMap + "_Items" ||
            effect.Source.Path != "data/maps/" + sourceMap + "/scripts.inc" ||
            effect.Source.Symbol != sourceMap + "_EventScript_Clerk" || !state.Script.Locked ||
            state.Script.Wait is not { Kind: StoryWaitKind.NativeEffect } wait ||
            state.LastTalked is null || !state.Actors.TryGetValue(state.LastTalked, out var clerk) ||
            clerk.ScriptId != sourceMap + "_EventScript_Clerk" || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))
            throw new InvalidDataException("shop_script_context_invalid");
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) throw new InvalidDataException(error);
        return state with { Shop = new(effect.Args[0], CampaignShopStage.Menu), Dialogue = null,
            Script = state.Script with { Wait = wait with { Kind = StoryWaitKind.Inventory } } };
    }

    public ImmutableArray<ItemId> VisibleShopItems
    {
        get
        {
            if (State.Shop is not { } shop) return [];
            if (shop.Mode == "buy") return ShopCatalog.Shops.Single(s => s.Id == shop.ShopId).Items;
            return State.Inventory!.BagItems.Concat(State.Inventory.Balls ?? []).Select(s => s.Item).ToImmutableArray();
        }
    }
    public EarlyShopQuote? CurrentShopQuote => State.Shop is { Item: { } item } shop ? QuoteShop(State, shop, item) : null;
    private EarlyShopQuote QuoteShop(CampaignState state, CampaignShop shop, ItemId item) => shop.Mode == "buy"
        ? EarlyShopRules.QuoteBuy(ShopCatalog, ShopInventoryView(state.Inventory!), state.Money, shop.ShopId, item)
        : EarlyShopRules.QuoteSell(ShopCatalog, ShopInventoryView(state.Inventory!), state.Money, item);

    private Decision<CampaignState> DecideShop(CampaignState state, CampaignCommand command)
    {
        var shop = state.Shop!;
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        Decision<CampaignState> Set(CampaignShop next) => new(CommandStatus.Accepted, state with { Shop = next });
        CampaignShop List() => shop with { Stage = shop.Mode == "buy" ? CampaignShopStage.Buy : CampaignShopStage.Sell, Item = null, Quantity = 1, MessageSymbol = null };
        Decision<CampaignState> Message(string symbol, CampaignShopStage next) => Set(shop with { Stage = CampaignShopStage.Message, MessageSymbol = symbol, ReturnStage = next });
        if (command.Kind == "shop_back")
        {
            if (shop.Stage == CampaignShopStage.Menu) return CloseShop(state);
            if (shop.Stage == CampaignShopStage.Message) return Set(shop with { Stage = shop.ReturnStage, MessageSymbol = null });
            if (shop.Stage is CampaignShopStage.Quantity or CampaignShopStage.Confirmation) return Set(List());
            return Message("gText_AnythingElseICanHelp", CampaignShopStage.Menu);
        }
        if (command.Kind == "shop_ack") return shop.Stage == CampaignShopStage.Message
            ? Set(shop with { Stage = shop.ReturnStage, MessageSymbol = null }) : No("shop_message_not_pending");
        if (command.Kind == "shop_menu")
        {
            if (shop.Stage != CampaignShopStage.Menu) return No("shop_menu_not_pending");
            return command.Text switch
            {
                "buy" => Set(shop with { Stage = CampaignShopStage.Buy, Mode = "buy", Item = null }),
                "sell" => Set(shop with { Stage = CampaignShopStage.Sell, Mode = "sell", Item = null }),
                "cancel" => CloseShop(state), _ => No("shop_menu_selection_invalid")
            };
        }
        if (command.Kind == "shop_select")
        {
            if (shop.Stage is not (CampaignShopStage.Buy or CampaignShopStage.Sell) || string.IsNullOrEmpty(command.Text)) return No("shop_item_selection_invalid");
            var item = new ItemId(command.Text);
            var quote = QuoteShop(state, shop, item);
            if (!quote.Succeeded) return No(quote.Error!);
            if (quote.MaximumQuantity == 0) return shop.Mode == "buy"
                ? Message("gText_YouDontHaveMoney", CampaignShopStage.Buy) : No("shop_item_not_owned");
            return Set(shop with { Stage = CampaignShopStage.Quantity, Item = item, Quantity = 1 });
        }
        if (command.Kind == "shop_quantity")
        {
            if (shop.Stage != CampaignShopStage.Quantity || shop.Item is not { } item) return No("shop_quantity_not_pending");
            var quote = QuoteShop(state, shop, item);
            if (!quote.Succeeded) return No(quote.Error!);
            if (command.Quantity < 1 || command.Quantity > quote.MaximumQuantity) return No("shop_quantity_invalid");
            return Set(shop with { Stage = CampaignShopStage.Confirmation, Quantity = command.Quantity });
        }
        if (command.Kind == "shop_confirm")
        {
            if (shop.Stage != CampaignShopStage.Confirmation || shop.Item is not { } item || command.Choice is not (0 or 1)) return No("shop_confirmation_invalid");
            if (command.Choice == 1) return Set(List());
            var result = shop.Mode == "buy"
                ? EarlyShopRules.Buy(ShopCatalog, ShopInventoryView(state.Inventory!), state.Money, shop.ShopId, item, shop.Quantity)
                : EarlyShopRules.Sell(ShopCatalog, ShopInventoryView(state.Inventory!), state.Money, item, shop.Quantity);
            var next = shop.Mode == "buy" ? CampaignShopStage.Buy : CampaignShopStage.Sell;
            if (!result.Succeeded) return result.Error switch
            {
                "shop_insufficient_money" => Message("gText_YouDontHaveMoney", next),
                "inventory_stack_full" or "inventory_destination_full" => Message("gText_NoMoreRoomForThis", next),
                _ => No(result.Error!)
            };
            return new(CommandStatus.Accepted, state with { Inventory = MergeShopInventory(state.Inventory!,result.Inventory), Money = result.Money,
                Shop = shop with { Stage = CampaignShopStage.Message, MessageSymbol = shop.Mode == "buy" ? "gText_HereYouGoThankYou" : "gText_TurnedOverItemsWorthYen", ReturnStage = next } });
        }
        return No("shop_active");
    }

    private Decision<CampaignState> CloseShop(CampaignState state)
    {
        if (state.Script.Wait is not { Kind: StoryWaitKind.Inventory } wait) return new(CommandStatus.Fault, state, "shop_callback_context_invalid");
        return new(CommandStatus.Accepted, Pump(Advance(state with { Shop = null }, StoryRules.Continue(state.Script, wait.CorrelationId, _content))));
    }
    public string ShopText(string symbol)
    {
        var shop = State.Shop;
        var item = shop?.Item is { } id ? ShopCatalog.Items.Single(i => i.Id == id) : null;
        int quantity = shop?.Quantity ?? 1;
        int price = item is null ? 0 : (shop!.Mode == "buy" ? item.Price : item.Price / 2) * quantity;
        return string.Join("\n", _content.TextPages(symbol)).Replace("{FONT_SMALL}", "", StringComparison.Ordinal)
            .Replace("{STR_VAR_1}", item?.Name ?? "", StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", quantity.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{STR_VAR_3}", price.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("シSス", "(S)", StringComparison.Ordinal);
    }
}
