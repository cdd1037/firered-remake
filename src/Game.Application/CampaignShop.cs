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

public sealed record CampaignShopItem(ItemId Id, string Name, int Price, string Description, int Quantity, MoveId? Move = null);

public sealed partial class CampaignSession
{
    private EarlyShopCatalog ShopCatalog => _content.EarlyShop ?? throw new InvalidDataException("early_shop_not_enabled");
    private CampaignState OpenShop(CampaignState state, StoryEffect effect)
    {
        var catalog = ShopCatalog;
        string map = state.World.Player.MapId.Value;
        string stockId, sourcePath, clerkScript;
        DepartmentStoreCounterStock? counter = null;
        PokeDollShopStock? fourthFloor = null;
        if(_content.TrainerTowerEnabled && map==TrainerTowerLobby)
        { stockId=ItemIds.TrainerTowerLobbyShopId;sourcePath="data/maps/TrainerTower_Lobby/scripts.inc";clerkScript="TrainerTower_Lobby_EventScript_MartClerk"; }
        else if (_content.PokemonLeagueEnabled && map == PokemonLeagueMartStock.ExpectedMapId)
        {
            var stock=PokemonLeagueStock(PokemonLeagueMartStock.ExpectedStockId)!;
            stockId=stock.Id;sourcePath=PokemonLeagueMartStock.ExpectedSourcePath;clerkScript=PokemonLeagueMartStock.ExpectedClerkScript;
        }
        else if (_content.FirstSeviiEnabled && map is "map.two-island" or "map.three-island-mart")
        {
            bool two=map=="map.two-island";
            stockId=two?(state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_TWO_ISLAND")==2?"TwoIsland_Items_ShopExpanded1":"TwoIsland_Items_ShopInitial"):"ThreeIsland_Mart_Items";
            sourcePath=two?"data/maps/TwoIsland/scripts.inc":"data/maps/ThreeIsland_Mart/scripts.inc";
            clerkScript=two?"TwoIsland_EventScript_Clerk":"ThreeIsland_Mart_EventScript_Clerk";
        }
        else if (map == PokeDollShopStock.ExpectedMapId)
        {
            var stock = fourthFloor = PokeDollStock(PokeDollShopStock.ExpectedStockId)!;
            stockId = stock.Id; sourcePath = PokeDollShopStock.ExpectedSourcePath; clerkScript = PokeDollShopStock.ExpectedClerkScript;
        }
        else if (map == DepartmentStoreOrdinaryStock.ExpectedMapId)
        {
            if (effect.Args.Length == 1 && effect.Args[0] == DepartmentStoreMachineStock.ExpectedStockId)
            {
                var stock = DepartmentMachineStock(effect.Args[0])!;
                if (!stock.IsClerk(map, effect.Source.Path, effect.Source.Symbol, effect.Args[0]))
                    throw new InvalidDataException("shop_script_context_invalid");
                stockId = stock.Id; sourcePath = DepartmentStoreMachineStock.ExpectedSourcePath; clerkScript = DepartmentStoreMachineStock.ExpectedClerkScript;
            }
            else
            {
                var stock = DepartmentStock(DepartmentStoreOrdinaryStock.ExpectedStockId)!;
                stockId = stock.Id; sourcePath = stock.Source.Path; clerkScript = stock.ClerkScript;
            }
        }
        else if (map == DepartmentStoreCounterStock.ExpectedMapId)
        {
            counter = effect.Args.Length == 1 ? DepartmentCounterStock(effect.Args[0]) : null;
            if (counter is null || !counter.IsClerk(map, effect.Source.Path, effect.Source.Symbol, effect.Source.Line))
                throw new InvalidDataException("shop_script_context_invalid");
            stockId = counter.Id; sourcePath = counter.Source.Path; clerkScript = counter.ClerkScript;
        }
        else
        {
            string sourceMap = map switch { "map.seven-island-mart" when _content.SevenIslandEnabled => "SevenIsland_Mart", "map.six-island-mart" when _content.SixIslandEnabled => "SixIsland_Mart", "map.four-island-mart" when _content.FourIslandEnabled => "FourIsland_Mart", "map.cinnabar-island-mart" when _content.CinnabarEnabled => "CinnabarIsland_Mart", "map.saffron-city-mart" when _content.SaffronGymEnabled => "SaffronCity_Mart", "map.lavender-town-mart" when _content.CeladonApproachEnabled => "LavenderTown_Mart", "map.vermilion-city-mart" when _content.VermilionEnabled => "VermilionCity_Mart", "map.viridian-mart" => "ViridianCity_Mart", "map.pewter-mart" => "PewterCity_Mart", "map.cerulean-city-mart" when _content.CeruleanEnabled => "CeruleanCity_Mart", _ => throw new InvalidDataException("shop_map_unsupported") };
            stockId = sourceMap + "_Items"; sourcePath = "data/maps/" + sourceMap + "/scripts.inc"; clerkScript = sourceMap + "_EventScript_Clerk";
        }
        if (effect.Opcode != "pokemart" || effect.Args.Length != 1 || effect.Args[0] != stockId ||
            effect.Source.Path != sourcePath ||
            (effect.Source.Symbol != clerkScript && !(_content.FirstSeviiEnabled && map=="map.two-island" &&
                effect.Source.Symbol==(stockId=="TwoIsland_Items_ShopExpanded1"?"TwoIsland_EventScript_ShopExpanded1":"TwoIsland_EventScript_ShopInitial"))) || !state.Script.Locked ||
            state.Script.Wait is not { Kind: StoryWaitKind.NativeEffect } wait ||
            state.LastTalked is null || !state.Actors.TryGetValue(state.LastTalked, out var clerk) ||
            clerk.ScriptId != clerkScript || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))
            throw new InvalidDataException("shop_script_context_invalid");
        if (fourthFloor is not null && (effect.Source.Line != 22 || !clerk.Active || clerk.Invisible ||
            !_content.MapDefinition(state.World.Player.MapId).Actors.Any(actor => actor.LocalId == state.LastTalked &&
                fourthFloor.IsSourceActor(actor) && clerk.SourceLocalId == actor.SourceLocalId &&
                clerk.LocalId == actor.LocalId && clerk.GraphicsId == actor.GraphicsId &&
                clerk.X == actor.X && clerk.Y == actor.Y && clerk.TemplateX == actor.X && clerk.TemplateY == actor.Y &&
                clerk.Elevation == actor.Elevation && clerk.MovementType == actor.MovementType && clerk.HideFlag == actor.HideFlag)))
            throw new InvalidDataException("shop_script_context_invalid");
        if ((stockId == DepartmentStoreMachineStock.ExpectedStockId || counter is not null) &&
            (!clerk.Active || !_content.MapDefinition(state.World.Player.MapId).Actors.Any(actor =>
                actor.LocalId == state.LastTalked && actor.SourceLocalId == clerk.SourceLocalId &&
                actor.ScriptId == clerkScript && actor.GraphicsId == clerk.GraphicsId && !actor.IsClone &&
                (counter is null || counter.IsSourceActor(actor) && clerk.LocalId == actor.LocalId &&
                    clerk.X == actor.X && clerk.Y == actor.Y && clerk.TemplateX == actor.X && clerk.TemplateY == actor.Y &&
                    clerk.Elevation == actor.Elevation && clerk.MovementType == actor.MovementType && clerk.HideFlag == actor.HideFlag))))
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
            if (shop.Mode == "buy") return DepartmentMachineStock(shop.ShopId)?.Items ?? OrdinaryStock(shop.ShopId)?.Items ?? ShopCatalog.Shops.Single(s => s.Id == shop.ShopId).Items;
            return State.Inventory!.BagItems.Concat(State.Inventory.Balls ?? []).Concat(State.Inventory.Machines ?? []).Select(s => s.Item).ToImmutableArray();
        }
    }
    public EarlyShopQuote? CurrentShopQuote => State.Shop is { Item: { } item } shop ? QuoteShop(State, shop, item) : null;
    public string? ShopAvailabilityNotice => State.Shop?.ShopId == PokeDollShopStock.ExpectedStockId
        ? "Mail writing and blank Mail\npreview are not available yet." : null;
    private PokeDollShopStock? PokeDollStock(string shopId)
    {
        if (shopId != PokeDollShopStock.ExpectedStockId) return null;
        var stock = _content.PokeDollShopStock ?? throw new InvalidDataException("poke_doll_shop_not_admitted");
        stock.Validate(ShopCatalog); return stock;
    }
    private DepartmentStoreMachineStock? DepartmentMachineStock(string shopId)
    {
        if (shopId != DepartmentStoreMachineStock.ExpectedStockId) return null;
        if (!_content.RegionalInteriorServicesEnabled || _content.PewterMachines is not { } machines)
            throw new InvalidDataException("department_machine_shop_not_admitted");
        return new DepartmentStoreMachineStock(machines);
    }
    private bool IsShopMachine(ItemId item) => _content.PewterMachines is { } machines &&
        MachineRules.Definition(machines, item) is not null;
    private DepartmentStoreCounterStock? DepartmentCounterStock(string shopId)
    {
        if (!DepartmentStoreCounterStock.IsStockId(shopId)) return null;
        var stock = _content.DepartmentStoreCounter(shopId) ?? throw new InvalidDataException("department_counter_not_admitted");
        stock.Validate(ShopCatalog); return stock;
    }
    private AuthenticatedShopStock? OrdinaryStock(string shopId) =>
        (AuthenticatedShopStock?)DepartmentCounterStock(shopId) ?? (AuthenticatedShopStock?)PokeDollStock(shopId) ??
        (AuthenticatedShopStock?)PokemonLeagueStock(shopId) ?? DepartmentStock(shopId);

    // A single authenticated metadata path serves ordinary Bag rows and the TM CASE.
    public CampaignShopItem ShopItem(ItemId id)
    {
        var inventory = State.Inventory!;
        // Some inherited shop definitions also describe HMs. The authenticated machine
        // owner supplies their move, description and Machines-pocket quantity.
        if (_content.PewterMachines is { } machines && MachineRules.Definition(machines, id) is { } machine)
            return new(id, machine.Machine.Item.Name, machine.Machine.Item.Price, machine.Machine.Description,
                OpeningInventoryRules.Quantity(inventory.Machines ?? [], id), machine.Move);
        if (ShopCatalog.Items.FirstOrDefault(item => item.Id == id) is { } ordinary)
            return new(id, ordinary.Name, ordinary.Price, ordinary.Description,
                OpeningInventoryRules.Quantity(ordinary.Pocket switch { "POCKET_ITEMS" => inventory.BagItems, "POCKET_POKE_BALLS" => inventory.Balls ?? [],
                    "POCKET_BERRY_POUCH" => inventory.Berries ?? [], "POCKET_KEY_ITEMS" => inventory.KeyItems ?? [],
                    _ => throw new InvalidDataException("shop_item_pocket_unsupported") }, id));
        throw new InvalidDataException("shop_item_metadata_missing");
    }
    private DepartmentStoreOrdinaryStock? DepartmentStock(string shopId)
    {
        if (shopId != DepartmentStoreOrdinaryStock.ExpectedStockId) return null;
        if (!_content.RegionalInteriorServicesEnabled || _content.DepartmentStoreOrdinaryStock is not { } stock)
            throw new InvalidDataException("shop_map_unsupported");
        stock.Validate(ShopCatalog);
        return stock;
    }
    private PokemonLeagueMartStock? PokemonLeagueStock(string shopId)
    {
        if(shopId!=PokemonLeagueMartStock.ExpectedStockId)return null;
        if(!_content.PokemonLeagueEnabled||_content.PokemonLeagueMartStock is not {} stock)throw new InvalidDataException("pokemon_league_shop_not_admitted");
        stock.Validate(ShopCatalog);return stock;
    }
    private EarlyShopQuote QuoteShop(CampaignState state, CampaignShop shop, ItemId item)
    {
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) return new(false, 0, 0, error);
        if (shop.Mode != "buy" && IsShopMachine(item))
            return EarlyShopRules.QuoteSell(ShopCatalog, _content.PewterMachines!, state.Inventory!, state.Money, item);
        if (shop.Mode == "buy" && DepartmentMachineStock(shop.ShopId) is { } machines)
            return EarlyShopRules.QuoteBuy(ShopCatalog, state.Inventory!, state.Money, machines, item);
        var inventory = state.Inventory!;
        if (shop.Mode != "buy") return EarlyShopRules.QuoteSell(ShopCatalog, inventory, state.Money, item);
        return OrdinaryStock(shop.ShopId) is { } stock
            ? EarlyShopRules.QuoteBuy(ShopCatalog, inventory, state.Money, stock, item)
            : EarlyShopRules.QuoteBuy(ShopCatalog, inventory, state.Money, shop.ShopId, item);
    }
    private EarlyShopTransactionResult BuyShop(CampaignState state, CampaignShop shop, ItemId item) =>
        DepartmentMachineStock(shop.ShopId) is { } machines
            ? EarlyShopRules.Buy(ShopCatalog, state.Inventory!, state.Money, machines, item, shop.Quantity)
            : OrdinaryStock(shop.ShopId) is { } stock
            ? EarlyShopRules.Buy(ShopCatalog, state.Inventory!, state.Money, stock, item, shop.Quantity)
            : EarlyShopRules.Buy(ShopCatalog, state.Inventory!, state.Money, shop.ShopId, item, shop.Quantity);

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
            if (!quote.Succeeded) return shop.Mode == "sell" && quote.Error == "shop_item_not_sellable"
                ? Message("gText_OhNoICantBuyThat", CampaignShopStage.Sell) : No(quote.Error!);
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
            var inventoryError = ValidateCampaignInventory(state.Inventory);
            if (inventoryError is not null) return No(inventoryError);
            bool machineTransaction = shop.Mode == "buy" ? DepartmentMachineStock(shop.ShopId) is not null : IsShopMachine(item);
            var result = shop.Mode == "buy"
                ? BuyShop(state, shop, item)
                : machineTransaction
                ? EarlyShopRules.Sell(ShopCatalog, _content.PewterMachines!, state.Inventory!, state.Money, item, shop.Quantity)
                : EarlyShopRules.Sell(ShopCatalog, state.Inventory!, state.Money, item, shop.Quantity);
            var next = shop.Mode == "buy" ? CampaignShopStage.Buy : CampaignShopStage.Sell;
            if (!result.Succeeded) return result.Error switch
            {
                "shop_insufficient_money" => Message("gText_YouDontHaveMoney", next),
                "inventory_stack_full" or "inventory_destination_full" or "inventory_key_items_full" => Message("gText_NoMoreRoomForThis", next),
                _ => No(result.Error!)
            };
            return new(CommandStatus.Accepted, state with { Inventory = machineTransaction ? result.Inventory : result.Inventory, Money = result.Money,
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
        var item = shop?.Item is { } id ? ShopItem(id) : null;
        int quantity = shop?.Quantity ?? 1;
        int price = item is null ? 0 : (shop!.Mode == "buy" ? item.Price : item.Price / 2) * quantity;
        return string.Join("\n", _content.TextPages(symbol)).Replace("{FONT_SMALL}", "", StringComparison.Ordinal)
            .Replace("{STR_VAR_1}", item?.Name ?? "", StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", quantity.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{STR_VAR_3}", price.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("シSス", "(S)", StringComparison.Ordinal);
    }
}
