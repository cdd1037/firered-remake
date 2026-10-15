using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignPrizePokemon(int SourceSpeciesId, long InstanceId, uint Personality, SixStats Ivs);
public sealed record CampaignPrizeExchange(string Counter, int Index, int CoinsBefore, bool Paid);

public sealed partial class CampaignSession
{
    private const string PrizePrefix = "CeladonCity_GameCorner_PrizeRoom_EventScript_";
    private GameCornerPrizeCounter? PrizeCounter(CampaignState state)
    {
        if (_content.GameCornerPrizes is not {} catalog || state.World.Player.MapId.Value != GameCornerPrizeCatalog.Map ||
            state.LastTalked is not {} id || !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.Invisible)
            return null;
        var counter = catalog.CounterFor(state.World.Player.MapId.Value, GameCornerPrizeCatalog.SourcePath, actor.ScriptId, actor.SourceLocalId);
        if (counter is null || actor.TemplateX != counter.X || actor.TemplateY != counter.Y || actor.Elevation != counter.Elevation) return null;
        var original = _content.MapDefinition(state.World.Player.MapId).Actors.SingleOrDefault(a => a.LocalId == id);
        return original is { IsClone: false } && original.SourceLocalId == counter.ActorLocalId && original.X == counter.X &&
            original.Y == counter.Y && original.Elevation == counter.Elevation && original.ScriptId == counter.ClerkScript &&
            original.GraphicsId == actor.GraphicsId ? counter : null;
    }
    private bool InGameCornerPrizeScript(CampaignState state) => !state.Script.Completed && PrizeCounter(state) is not null;
    private GameCornerPrizeCounter? PrizeMenu(CampaignState state)
    {
        if (state.Script.Wait?.Kind != StoryWaitKind.MenuChoice || PrizeCounter(state) is not {} counter ||
            GameCornerCoinSource.Instruction(state.Script, _content) is not { Op: StoryOp.Multichoice, Opcode: "multichoice" } instruction ||
            instruction.Source.Path != GameCornerPrizeCatalog.SourcePath || GameCornerPrizeSource.MenuKind(instruction) != counter.Kind ||
            !instruction.Args.SequenceEqual(new[] { counter.Kind == "items" ? "10" : "11", "0", counter.MenuSymbol, "FALSE" })) return null;
        return counter;
    }
    public GameCornerPrizeCounter? PendingGameCornerPrizeMenu => PrizeMenu(State);
    private GameCornerPrizeDefinition SelectedPrize(CampaignState state, GameCornerPrizeCounter counter) =>
        counter.Prizes.SingleOrDefault(p => p.SourceRewardId == state.Script.Variables.GetValueOrDefault("VAR_TEMP_1", -1) &&
            p.Coins == state.Script.Variables.GetValueOrDefault("VAR_TEMP_2", -1)) ?? throw new InvalidDataException("prize_selected_row");
    private long? PendingPrizeGiftInstanceId(CampaignState state) => state.GiftDelivery is not null && InGameCornerPrizeScript(state) &&
        state.PrizePokemon is { IsEmpty: false } gifts && gifts[^1].InstanceId == state.NextPokemonId - 1 ? gifts[^1].InstanceId : null;
    private string? ValidateGameCornerPrizePokemon(CampaignState state)
    {
        var receipts = state.PrizePokemon ?? [];
        if (receipts.IsDefault || receipts.Any(r => r is null)) return "save_prize_receipts_invalid";
        if (_content.GameCornerPrizes is not {} catalog) return receipts.IsEmpty ? null : "save_prize_catalog_missing";
        long previous = 0;
        var history = HistoricalPokemon(state);
        foreach (var receipt in receipts)
        {
            var source = catalog.PokemonPrizes.SingleOrDefault(p => p.SourceRewardId == receipt.SourceSpeciesId);
            var matching = history.Where(p => p.InstanceId == receipt.InstanceId).ToArray();
            if (source is null || receipt.Ivs is null || receipt.InstanceId <= previous || receipt.InstanceId < 2 ||
                receipt.InstanceId >= state.NextPokemonId || matching.Length != 1) return "save_prize_identity";
            var mon = matching[0];
            if (!EvolutionSpeciesMatchesOrigin(state, mon, receipt.SourceSpeciesId) || mon.Pokemon.Personality != receipt.Personality ||
                mon.Pokemon.Ivs != receipt.Ivs || mon.MetLocation != "MAPSEC_CELADON_CITY" || mon.MetLevel != source.Level ||
                mon.Ball != "ITEM_POKE_BALL" || !state.CaughtSpecies.Contains(receipt.SourceSpeciesId) || !state.SeenSpecies.Contains(receipt.SourceSpeciesId))
                return "save_prize_identity";
            previous = receipt.InstanceId;
        }
        return null;
    }
    private bool TryApplyGameCornerPrizeEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool own = effect.Source.Path == GameCornerPrizeCatalog.SourcePath;
        bool item = effect.Opcode is "checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype";
        bool standard = effect.Source.Path == "data/scripts/obtain_item.inc" && effect.Source.Symbol is "Std_ObtainItem" or "EventScript_ObtainItemMessage";
        if (!(own && (item || effect.Opcode is "showcoinsbox" or "hidecoinsbox" or "updatecoinsbox" or "checkcoins" or "removecoins" or "givemon" or "multichoice" or "buffermovename") ||
            standard && item && InGameCornerPrizeScript(state))) return false;
        var counter = PrizeCounter(state) ?? throw new InvalidDataException("prize_actor_source");
        if (!state.Script.Locked || state.Script.Completed || !state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag) ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], GameCornerCoinSource.CoinCase) != 1 ||
            state.Coins is < 0 or > GameCornerCoinSource.Maximum) throw new InvalidDataException("prize_context");
        RequireCoinEffect(state, effect);
        string symbol = effect.Source.Symbol;
        if (effect.Opcode == "multichoice")
        {
            if (PrizeMenu(state) != counter || state.CoinsBox is not { X: 0, Y: 0 }) throw new InvalidDataException("prize_menu_source");
            return true;
        }
        if (effect.Opcode is "showcoinsbox" or "hidecoinsbox" or "updatecoinsbox")
        {
            if (effect.Opcode == "showcoinsbox")
            {
                if (symbol != counter.ClerkScript || state.CoinsBox is not null || !effect.Args.SequenceEqual(new[] { "0", "0" }))
                    throw new InvalidDataException("prize_box_show");
                next = state with { CoinsBox = new(state.Coins, 0, 0) }; return true;
            }
            if (state.CoinsBox is not { X: 0, Y: 0 } box) throw new InvalidDataException("prize_box_context");
            if (effect.Opcode == "hidecoinsbox")
            {
                if (!effect.Args.SequenceEqual(new[] { "0", "0" }) || symbol != PrizePrefix + "EndPrizeExchange" &&
                    symbol != PrizePrefix + "PartyFull" && symbol != PrizePrefix + "BagFull") throw new InvalidDataException("prize_box_hide");
                next = state with { CoinsBox = null }; return true;
            }
            if (!effect.Args.SequenceEqual(new[] { "0", "5" }) || state.PrizeExchange is not { Paid: true } ||
                symbol != PrizePrefix + "ReceivedMonParty" && symbol != PrizePrefix + "ReceivedMonPC" && symbol != PrizePrefix + "TryGivePrize")
                throw new InvalidDataException("prize_box_update");
            // Original updatecoinsbox consumes its coordinates but updates the existing window.
            next = state with { CoinsBox = box with { Amount = state.Coins } }; return true;
        }
        var row = SelectedPrize(state, counter);
        bool pokemon = counter.Kind == "pokemon";
        if (effect.Opcode == "buffermovename")
        {
            if (counter.Kind != "machines" || row.Move is null || effect.Args.Length != 2 || effect.Args[0] != "STR_VAR_2" ||
                "pret:" + effect.Args[1] != row.Move || symbol != row.Source.Symbol ||
                !_content.BattleContent!.MoveNames.TryGetValue(new(row.Move), out string? name))
                throw new InvalidDataException("prize_move_name_source");
            next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", name) }; return true;
        }
        if (effect.Opcode == "checkcoins")
        {
            if (!effect.Args.SequenceEqual(new[] { "VAR_RESULT" }) || state.PrizeExchange is not null ||
                symbol != PrizePrefix + (pokemon ? "ConfirmPrizeMon" : "TryGivePrize")) throw new InvalidDataException("prize_check_coins");
            next = state with { PrizeExchange = new(counter.Kind, row.Index, state.Coins, false),
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.Coins) } }; return true;
        }
        var exchange = state.PrizeExchange;
        bool authenticated = exchange is not null && exchange.Counter == counter.Kind && exchange.Index == row.Index && exchange.CoinsBefore >= row.Coins;
        if (effect.Opcode == "givemon")
        {
            if (!pokemon || !authenticated || exchange!.Paid || state.Coins != exchange.CoinsBefore || row.GiveSource is null ||
                symbol != row.GiveSource.Symbol || effect.Args.Length != 2 || DepartmentValue(state, effect.Args[0]) != row.SourceRewardId ||
                DepartmentValue(state, effect.Args[1]) != row.Level) throw new InvalidDataException("prize_gift_source");
            var received = GiveScriptGift(state, row.SourceRewardId, row.Level, "MAPSEC_CELADON_CITY");
            next = received.State;
            if (received.Gift is {} mon) next = next with { PrizePokemon = (state.PrizePokemon ?? []).Add(new(row.SourceRewardId,
                mon.InstanceId, mon.Pokemon.Personality, mon.Pokemon.Ivs)) };
            return true;
        }
        if (effect.Opcode == "removecoins")
        {
            if (!authenticated || exchange!.Paid || state.Coins != exchange.CoinsBefore || effect.Args.Length != 1 ||
                DepartmentValue(state, effect.Args[0]) != row.Coins || state.CoinsBox?.Amount != exchange.CoinsBefore)
                throw new InvalidDataException("prize_debit_context");
            if (pokemon)
            {
                if (symbol != PrizePrefix + (state.GiftDelivery?.BoxAddress is null ? "ReceivedMonParty" : "ReceivedMonPC") ||
                    PendingPrizeGiftInstanceId(state) is null) throw new InvalidDataException("prize_delivery_before_debit");
            }
            else if (symbol != PrizePrefix + "TryGivePrize" || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1 || !PrizeItemFits(state, counter, row))
                throw new InvalidDataException("prize_capacity_before_debit");
            next = state with { Coins = state.Coins - row.Coins, PrizeExchange = exchange with { Paid = true },
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 0) } }; return true;
        }
        if (pokemon || !item) throw new InvalidDataException("prize_item_owner");
        return ApplyGameCornerPrizeItem(state, effect, counter, row, authenticated, out next);
    }
    private bool PrizeItemFits(CampaignState state, GameCornerPrizeCounter counter, GameCornerPrizeDefinition row) => counter.Kind == "machines"
        ? MachineRules.CanAddMachine(_content.PewterMachines!, state.Inventory!, new(row.Reward))
        : EarlyInventoryAddRules.CheckSpace(ShopCatalog, state.Inventory!, new(row.Reward), 1).Succeeded;
    private bool ApplyGameCornerPrizeItem(CampaignState state, StoryEffect effect, GameCornerPrizeCounter counter,
        GameCornerPrizeDefinition row, bool authenticated, out CampaignState next)
    {
        next = state; var a = effect.Args;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        int itemIndex = buffer ? 1 : 0;
        if (a.Length <= itemIndex || DepartmentValue(state, a[itemIndex]) != row.SourceRewardId ||
            ValidateCampaignInventory(state.Inventory) is {} invalid) throw new InvalidDataException("prize_item_source");
        bool machine = counter.Kind == "machines"; var id = new ItemId(row.Reward);
        if (buffer)
        {
            if (a[0] is not ("STR_VAR_1" or "STR_VAR_2") || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) ||
                a.Length == 3 && DepartmentValue(state, a[2]) != 1) throw new InvalidDataException("prize_item_buffer");
            string name = machine ? MachineRules.Definition(_content.PewterMachines!, id)!.Machine.Item.Name : ShopCatalog.Items.Single(i => i.Id == id).Name;
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], name) }; return true;
        }
        int result;
        if (effect.Opcode == "checkitemtype") result = DepartmentValue(state, machine ? "POCKET_TM_CASE" : "POCKET_ITEMS");
        else
        {
            if (!authenticated || a.Length > 2 || a.Length == 2 && DepartmentValue(state, a[1]) != 1)
                throw new InvalidDataException("prize_item_quantity");
            if (effect.Opcode == "checkitemspace")
            {
                if (state.PrizeExchange!.Paid || effect.Source.Symbol != PrizePrefix + "TryGivePrize" || state.Coins != state.PrizeExchange.CoinsBefore)
                    throw new InvalidDataException("prize_item_space_context");
                result = PrizeItemFits(state, counter, row) ? 1 : 0;
            }
            else
            {
                if (effect.Opcode != "additem" || state.PrizeExchange is not { Paid: true } exchange ||
                    state.Coins != exchange.CoinsBefore - row.Coins || effect.Source.Symbol != "Std_ObtainItem")
                    throw new InvalidDataException("prize_item_add_context");
                if (machine)
                {
                    var added = MachineRules.AddMachine(_content.PewterMachines!, state.Inventory!, id);
                    if (!added.Succeeded) throw new InvalidDataException("prize_machine_add:" + added.Error);
                    next = state with { Inventory = added.State };
                }
                else
                {
                    var added = EarlyInventoryAddRules.Add(ShopCatalog, state.Inventory!, id, 1);
                    if (!added.Succeeded) throw new InvalidDataException("prize_item_add:" + added.Error);
                    next = state with { Inventory = added.State };
                }
                result = 1;
            }
        }
        next = next with { Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", result) } }; return true;
    }
}
