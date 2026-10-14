using System.Globalization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed record CampaignCoinsBox(int Amount, int X, int Y);

public sealed partial class CampaignSession
{
    private string? ValidateGameCornerCoins(CampaignState state)
    {
        if (state.Coins is < 0 or > GameCornerCoinSource.Maximum) return "save_coins_range";
        bool received = state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag);
        int quantity = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], GameCornerCoinSource.CoinCase);
        if ((state.Inventory?.KeyItems ?? []).Count(s => s.Item == GameCornerCoinSource.CoinCase) > 1) return "save_coin_case_duplicate";
        bool gifts = GameCornerCoinSource.GiftFlags.Any(f => state.Script.Flags.GetValueOrDefault(f)) ||
            _content.FieldPickups.Any(p => IsHiddenCoinPickup(p) && state.Script.Flags.GetValueOrDefault(p.Flag));
        if (!_content.GameCornerCoinsEnabled) return received || quantity != 0 || gifts || state.Coins != 0 ? "save_game_corner_coins_missing" : null;
        return quantity != (received ? 1 : 0) || !received && (gifts || state.Coins != 0) ? "save_coin_case_receipt" : null;
    }

    private bool AtCoinServiceActor(CampaignState state, string map, string script, int localId, string graphics, int x, int y, int elevation)
    {
        if (!_content.GameCornerCoinsEnabled || state.World.Player.MapId.Value != map || state.Script.Completed ||
            state.LastTalked is not {} id || !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.Invisible ||
            actor.SourceLocalId != localId || actor.ScriptId != script || actor.GraphicsId != graphics ||
            actor.TemplateX != x || actor.TemplateY != y || actor.Elevation != elevation) return false;
        var original = _content.MapDefinition(state.World.Player.MapId).Actors.SingleOrDefault(a => a.LocalId == id);
        return original is { IsClone: false } && original.SourceLocalId == localId && original.ScriptId == script &&
            original.GraphicsId == graphics && original.X == x && original.Y == y && original.Elevation == elevation;
    }
    private bool AtCoinCaseMan(CampaignState state) => AtCoinServiceActor(state, GameCornerCoinSource.RestaurantMap,
        GameCornerCoinSource.CoinCaseMan, 4, "OBJ_EVENT_GFX_BALDING_MAN", 1, 2, 0);
    private bool AtCoinsClerk(CampaignState state) => AtCoinServiceActor(state, GameCornerCoinSource.Map,
        GameCornerCoinSource.Clerk, 2, "OBJ_EVENT_GFX_WORKER_M", 6, 2, 3);
    private int CoinGiftIndex(CampaignState state) =>
        AtCoinServiceActor(state, GameCornerCoinSource.Map, GameCornerCoinSource.Prefix + "Fisher", 5, "OBJ_EVENT_GFX_FISHER", 4, 7, 3) ? 0 :
        AtCoinServiceActor(state, GameCornerCoinSource.Map, GameCornerCoinSource.Prefix + "Scientist", 9, "OBJ_EVENT_GFX_SCIENTIST", 13, 7, 3) ? 1 :
        AtCoinServiceActor(state, GameCornerCoinSource.Map, GameCornerCoinSource.Prefix + "Gentleman", 10, "OBJ_EVENT_GFX_GENTLEMAN", 16, 10, 3) ? 2 : -1;

    private void RequireCoinEffect(CampaignState state, StoryEffect effect)
    {
        var instruction = GameCornerCoinSource.Instruction(state.Script, _content);
        if (instruction is null || instruction.Opcode != effect.Opcode || instruction.Source != effect.Source ||
            instruction.Args.Length != effect.Args.Length || instruction.Args.Where((a, i) =>
                a.StartsWith("VAR_", StringComparison.Ordinal) && effect.Opcode != "checkcoins" ?
                    DepartmentValue(state, a) != DepartmentValue(state, effect.Args[i]) : a != effect.Args[i]).Any())
            throw new InvalidDataException("game_corner_coin_source_callback");
    }
    private bool TryApplyGameCornerCoinEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool own = effect.Source.Path == GameCornerCoinSource.Path;
        bool item = effect.Opcode is "checkitemspace" or "additem" or "removeitem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype";
        bool caseItem = item && effect.Args.Length > (effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal) ? 1 : 0) &&
            DepartmentValue(state, effect.Args[effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal) ? 1 : 0]) == 260;
        bool coins = effect.Opcode is "showcoinsbox" or "hidecoinsbox" or "updatecoinsbox" or "checkcoins" or "addcoins";
        bool money = effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox" or "checkmoney" or "removemoney";
        if (!(caseItem || own && (coins || money || effect.Opcode == "multichoice"))) return false;
        if (!_content.GameCornerCoinsEnabled || !state.Script.Locked || state.Coins is < 0 or > GameCornerCoinSource.Maximum)
            throw new InvalidDataException("game_corner_coins_context");
        RequireCoinEffect(state, effect);
        if (caseItem) return ApplyCoinCaseItem(state, effect, out next);
        bool clerk = AtCoinsClerk(state); int gift = CoinGiftIndex(state);
        string symbol = effect.Source.Symbol;
        if (!clerk && gift < 0) throw new InvalidDataException("game_corner_coin_actor");
        if (effect.Opcode == "multichoice")
        {
            if (!clerk || !GameCornerCoinSource.IsMenu(GameCornerCoinSource.Instruction(state.Script, _content)) ||
                state.MoneyBox is null || state.CoinsBox is null) throw new InvalidDataException("game_corner_coin_menu");
            return true;
        }
        if (effect.Opcode is "showcoinsbox" or "hidecoinsbox" or "updatecoinsbox" or "showmoneybox" or "hidemoneybox" or "updatemoneybox")
        {
            string expected = effect.Opcode.StartsWith("show", StringComparison.Ordinal) ? "CoinsClerk" :
                effect.Opcode.StartsWith("hide", StringComparison.Ordinal) ? "ClerkEnd" : "BoughtCoins";
            if (!clerk || symbol != GameCornerCoinSource.Prefix + expected) throw new InvalidDataException("game_corner_coin_box_source");
            if (money)
            {
                if (effect.Opcode == "showmoneybox" ? state.MoneyBox is not null : state.MoneyBox is null)
                    throw new InvalidDataException("game_corner_money_box_context");
                next = ApplySourceMoneyBox(state, effect); return true;
            }
            if (!effect.Args.SequenceEqual(new[] { "0", "5" }) || effect.Opcode == "showcoinsbox" && state.CoinsBox is not null ||
                effect.Opcode != "showcoinsbox" && state.CoinsBox is null) throw new InvalidDataException("game_corner_coin_box_context");
            next = state with { CoinsBox = effect.Opcode == "hidecoinsbox" ? null : new(state.Coins, 0, 5) }; return true;
        }
        int selection = state.Script.Variables.GetValueOrDefault("VAR_0x8009", -1);
        int amount = clerk ? selection switch { 0 => 50, 1 => 500, _ => 0 } : gift == 0 ? 10 : 20;
        int price = amount == 50 ? 1000 : 10000;
        string owner = clerk ? "Buy" + amount + "Coins" : new[] { "Fisher", "Scientist", "Gentleman" }[gift];
        if (amount == 0 || symbol != GameCornerCoinSource.Prefix + owner ||
            !state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag) ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], GameCornerCoinSource.CoinCase) != 1 ||
            gift >= 0 && state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.GiftFlags[gift]) ||
            clerk && (state.MoneyBox is null || state.CoinsBox is null)) throw new InvalidDataException("game_corner_coin_transaction_context");
        if (effect.Opcode == "checkcoins")
        {
            if (!effect.Args.SequenceEqual(new[] { "VAR_TEMP_1" })) throw new InvalidDataException("game_corner_coin_destination");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_TEMP_1", state.Coins) } }; return true;
        }
        if (effect.Args.Length != 1 || DepartmentValue(state, effect.Args[0]) != (money ? price : amount))
            throw new InvalidDataException("game_corner_coin_transaction_amount");
        int before = state.Script.Variables.GetValueOrDefault("VAR_TEMP_1", -1);
        if (before < 0 || before > GameCornerCoinSource.Maximum - amount) throw new InvalidDataException("game_corner_coin_capacity_context");
        if (effect.Opcode == "checkmoney")
        {
            if (!clerk || state.Coins != before) throw new InvalidDataException("game_corner_coin_money_context");
            next = SourceMoneyResult(state, price); return true;
        }
        if (effect.Opcode == "addcoins")
        {
            if (state.Coins != before || clerk && (state.Money < price || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1))
                throw new InvalidDataException("game_corner_coin_add_context");
            var result = GameCornerCoinSource.Add(state.Coins, amount);
            next = state with { Coins = result.Coins, Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.ScriptResult) } }; return true;
        }
        if (effect.Opcode != "removemoney" || !clerk || state.Coins != before + amount || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 0)
            throw new InvalidDataException("game_corner_coin_payment_context");
        next = DebitSourceMoney(state, price); return true;
    }

    private bool ApplyCoinCaseItem(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool own = effect.Source.Path == GameCornerCoinSource.RestaurantPath && effect.Source.Symbol == GameCornerCoinSource.CoinCaseMan;
        bool putAway = effect.Source.Path == "data/event_scripts.s" && effect.Source.Symbol == "Std_PutItemAway";
        if (!AtCoinCaseMan(state) || state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag) ||
            !(own || putAway) || effect.Opcode == "removeitem") throw new InvalidDataException("coin_case_gift_source");
        var args = effect.Args; bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (buffer)
        {
            if (args[0] != "STR_VAR_2" || args.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) ||
                args.Length == 3 && DepartmentValue(state, args[2]) != 1) throw new InvalidDataException("coin_case_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], "COIN CASE") }; return true;
        }
        int result;
        if (effect.Opcode == "checkitemtype") result = DepartmentValue(state, "POCKET_KEY_ITEMS");
        else
        {
            if (!own || effect.Opcode is not ("checkitemspace" or "additem") || args.Length > 2 ||
                args.Length == 2 && DepartmentValue(state, args[1]) != 1) throw new InvalidDataException("coin_case_quantity");
            var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
            if (ValidateCampaignInventory(inventory) is {} invalid) throw new InvalidDataException(invalid);
            var keys = inventory.KeyItems ?? [];
            bool fits = keys.Length < ShopCatalog.QuestLimits.KeyItemSlots && !keys.Any(k => k.Item == GameCornerCoinSource.CoinCase);
            result = fits ? 1 : 0;
            if (effect.Opcode == "additem")
            {
                if (!fits) throw new InvalidDataException("coin_case_add_capacity");
                next = state with { Inventory = inventory with { KeyItems = keys.Add(new(GameCornerCoinSource.CoinCase, 1)) } };
            }
        }
        next = next with { Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", result) } }; return true;
    }

    private Decision<CampaignState> UseCoinCase(CampaignState state, CampaignCommand command)
    {
        if (!_content.GameCornerCoinsEnabled || command.Text != GameCornerCoinSource.CoinCase.Value ||
            state.ItemMenu is not { Origin: "bag", View: "bag" } menu || ValidateCampaignInventory(state.Inventory) is not null ||
            ValidateGameCornerCoins(state) is not null || !state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag))
            return new(CommandStatus.Rejected, state, "coin_case_use_context");
        return new(CommandStatus.Accepted, state with {
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", state.Coins.ToString(CultureInfo.InvariantCulture)),
            ItemMenu = menu with { View = "message", MessageSymbol = "gText_CoinCase", ReturnView = "bag" }
        });
    }
}
