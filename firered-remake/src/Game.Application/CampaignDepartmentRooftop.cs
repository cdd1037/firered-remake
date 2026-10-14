using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static readonly string[] RoofDrinks = ["ITEM_FRESH_WATER", "ITEM_SODA_POP", "ITEM_LEMONADE"];
    private static readonly string[] RoofRewards = ["ITEM_TM16", "ITEM_TM20", "ITEM_TM33"];
    private static readonly string[] RoofFlags = ["FLAG_GOT_TM16_FROM_THIRSTY_GIRL", "FLAG_GOT_TM20_FROM_THIRSTY_GIRL", "FLAG_GOT_TM33_FROM_THIRSTY_GIRL"];
    private static readonly string[] RoofNames = ["FreshWater", "SodaPop", "Lemonade"];
    private static readonly int[] RoofPrices = [200, 300, 350];

    private bool AtDepartmentRooftop(CampaignState state) => _content.DepartmentRooftopEnabled &&
        state.World.Player.MapId.Value == DepartmentRooftopSource.Map && !state.Script.Completed;
    private bool AtRooftopGirl(CampaignState state)
    {
        if (!AtDepartmentRooftop(state) || state.LastTalked is not {} id || !state.Actors.TryGetValue(id, out var npc) ||
            !npc.Active || npc.Invisible || npc.SourceLocalId != 2 || npc.ScriptId != DepartmentRooftopSource.Girl ||
            npc.GraphicsId != "OBJ_EVENT_GFX_LITTLE_GIRL" || npc.TemplateX != 5 || npc.TemplateY != 4 || npc.Elevation != 3) return false;
        return _content.MapDefinition(state.World.Player.MapId).Actors.SingleOrDefault(a => a.LocalId == id) is
            { SourceLocalId: 2, IsClone: false, GraphicsId: "OBJ_EVENT_GFX_LITTLE_GIRL", X: 5, Y: 4, Elevation: 3, ScriptId: DepartmentRooftopSource.Girl };
    }
    private bool AtRooftopVending(CampaignState state)
    {
        if (!AtDepartmentRooftop(state) || !state.Script.Locked) return false;
        var p = state.World.Player; var (dx, dy) = Delta(p.Facing); int x = p.X + dx, y = p.Y + dy;
        return x is >= 10 and <= 12 && y == 3 && _content.MapDefinition(p.MapId).BackgroundTriggers.Any(b =>
            b.X == x && b.Y == y && b.Elevation == 0 && b.ScriptId == DepartmentRooftopSource.Vending && b.HiddenItem is null && b.PlayerFacing is null && !b.Underfoot);
    }
    private void RequireRooftopEffect(CampaignState state, StoryEffect effect)
    {
        var instruction = DepartmentRooftopSource.Instruction(state.Script, _content);
        if (instruction is null || instruction.Opcode != effect.Opcode || instruction.Source != effect.Source ||
            instruction.Args.Length != effect.Args.Length || instruction.Args.Where((a, i) =>
                a.StartsWith("VAR_", StringComparison.Ordinal) && !(effect.Opcode == "addvar" && i == 0) ? DepartmentValue(state, a) != DepartmentValue(state, effect.Args[i]) : a != effect.Args[i]).Any())
            throw new InvalidDataException("department_rooftop_source_callback");
    }
    private bool TryApplyDepartmentRooftopEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool own = effect.Source.Path == DepartmentRooftopSource.Path;
        bool putAway = effect.Source.Path == "data/event_scripts.s" && effect.Source.Symbol == "Std_PutItemAway";
        bool cut = effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" });
        bool item = effect.Opcode is "checkitem" or "checkitemspace" or "additem" or "removeitem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype";
        bool money = effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox" or "checkmoney" or "removemoney";
        if (!(own && (cut || item || money || effect.Opcode is "multichoice" or "addvar") || putAway && item && AtRooftopGirl(state))) return false;
        if (!AtDepartmentRooftop(state)) throw new InvalidDataException("department_rooftop_feature_missing");
        RequireRooftopEffect(state, effect);
        if (cut)
        {
            if (effect.Source.Symbol != DepartmentRooftopSource.Girl || !AtRooftopGirl(state) || state.Script.QuestLogState != 0)
                throw new InvalidDataException("department_rooftop_questlog_context");
            return true;
        }
        if (!state.Script.Locked) throw new InvalidDataException("department_rooftop_unlocked");
        if (effect.Opcode == "addvar")
        {
            int index = Array.FindIndex(RoofNames, name => effect.Source.Symbol == DepartmentRooftopSource.Prefix + "SetHas" + name);
            int mask = state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");
            if (!AtRooftopGirl(state) || index < 0 || !effect.Args.SequenceEqual(new[] { "VAR_TEMP_1", (1 << index).ToString(System.Globalization.CultureInfo.InvariantCulture) }) ||
                mask is < 0 or > 7 || (mask & (1 << index)) != 0 || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1)
                throw new InvalidDataException("department_rooftop_drink_mask");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_TEMP_1", mask + (1 << index)) } };
            return true;
        }
        if (effect.Opcode == "multichoice")
        {
            var menu = DepartmentRooftopSource.Menu(DepartmentRooftopSource.Instruction(state.Script, _content))
                ?? throw new InvalidDataException("department_rooftop_menu");
            if (menu.Vending ? !AtRooftopVending(state) || state.MoneyBox is null : !AtRooftopGirl(state) ||
                state.Script.Variables.GetValueOrDefault("VAR_TEMP_1") != menu.DrinkMask)
                throw new InvalidDataException("department_rooftop_menu_context");
            return true;
        }
        if (money)
        {
            if (!AtRooftopVending(state)) throw new InvalidDataException("department_rooftop_money_context");
            string symbol = effect.Source.Symbol; var a = effect.Args;
            if (effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox")
            {
                string expected = effect.Opcode switch { "showmoneybox" => "VendingMachine", "hidemoneybox" => "ExitVendingMachine", _ => "TryBuyDrink" };
                if (symbol != DepartmentRooftopSource.Prefix + expected || effect.Opcode == "showmoneybox" && state.MoneyBox is not null)
                    throw new InvalidDataException("department_rooftop_moneybox_source");
                next = ApplySourceMoneyBox(state, effect); return true;
            }
            int index = state.Script.Variables.GetValueOrDefault("VAR_TEMP_1", -1);
            if (index is < 0 or > 2 || a.Length != 1 || DepartmentValue(state, a[0]) != RoofPrices[index] || state.MoneyBox is null ||
                state.Script.Variables.GetValueOrDefault("VAR_TEMP_0") != DepartmentValue(state, RoofDrinks[index]) ||
                symbol != DepartmentRooftopSource.Prefix + (effect.Opcode == "checkmoney" ? "Buy" : "RemoveMoney") + RoofNames[index])
                throw new InvalidDataException("department_rooftop_payment_source");
            if (effect.Opcode == "checkmoney") next = SourceMoneyResult(state, RoofPrices[index]);
            else
            {
                if (state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1 ||
                    !EarlyInventoryAddRules.CheckSpace(ShopCatalog, ShopInventoryView(state.Inventory!), new("pret:" + RoofDrinks[index]), 1).Succeeded)
                    throw new InvalidDataException("department_rooftop_payment_capacity");
                next = DebitSourceMoney(state, RoofPrices[index]);
            }
            return true;
        }
        var args = effect.Args; bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (args.Length < (buffer ? 2 : 1)) throw new InvalidDataException("department_rooftop_item_arity");
        int sourceId = DepartmentValue(state, args[buffer ? 1 : 0]);
        int drink = Array.FindIndex(RoofDrinks, s => DepartmentValue(state, s) == sourceId);
        int reward = Array.FindIndex(RoofRewards, s => DepartmentValue(state, s) == sourceId);
        bool vending = own && effect.Source.Symbol == DepartmentRooftopSource.Prefix + "TryBuyDrink";
        bool checking = own && effect.Source.Symbol == DepartmentRooftopSource.Prefix + "CheckPlayerHasDrinks";
        bool giving = own && effect.Source.Symbol == DepartmentRooftopSource.Prefix + "GiveDrink";
        if (drink < 0 && reward < 0 || (vending ? !AtRooftopVending(state) || drink < 0 : !AtRooftopGirl(state) || !(checking || giving || putAway)))
            throw new InvalidDataException("department_rooftop_item_context");
        if (vending && (effect.Opcode is not ("bufferitemname" or "checkitemspace" or "additem") ||
            sourceId != state.Script.Variables.GetValueOrDefault("VAR_TEMP_0")) ||
            checking && (effect.Opcode != "checkitem" || drink < 0) ||
            giving && (effect.Opcode == "removeitem" ? drink < 0 : effect.Opcode is "checkitemspace" or "additem" ? reward < 0 : effect.Opcode != "bufferitemname") ||
            putAway && (reward < 0 || effect.Opcode is not ("bufferitemnameplural" or "checkitemtype")))
            throw new InvalidDataException("department_rooftop_item_source");
        if (giving || putAway)
        {
            int selected = Array.FindIndex(RoofDrinks, s => DepartmentValue(state, s) == state.Script.Variables.GetValueOrDefault("VAR_0x8008"));
            if (selected < 0 || state.Script.Variables.GetValueOrDefault("VAR_0x8009") != DepartmentValue(state, RoofRewards[selected]) ||
                state.Script.Flags.GetValueOrDefault(RoofFlags[selected]) || (drink >= 0 ? drink : reward) != selected)
                throw new InvalidDataException("department_rooftop_reward_context");
        }
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is {} invalid) throw new InvalidDataException(invalid);
        var id = new ItemId("pret:" + (drink >= 0 ? RoofDrinks[drink] : RoofRewards[reward]));
        if (buffer)
        {
            if (args[0] is not ("STR_VAR_1" or "STR_VAR_2") || args.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) ||
                args.Length == 3 && DepartmentValue(state, args[2]) != 1) throw new InvalidDataException("department_rooftop_buffer");
            string name = drink >= 0 ? ShopCatalog.Items.Single(i => i.Id == id).Name : PewterMachineRules.Definition(_content.PewterMachines!, id)!.Machine.Item.Name;
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], name) }; return true;
        }
        int result;
        if (effect.Opcode == "checkitemtype") result = DepartmentValue(state, "POCKET_TM_CASE");
        else
        {
            if (args.Length > 2 || args.Length == 2 && DepartmentValue(state, args[1]) != 1) throw new InvalidDataException("department_rooftop_quantity");
            if (effect.Opcode == "checkitem") result = OpeningInventoryRules.Quantity(inventory.BagItems, id) > 0 ? 1 : 0;
            else if (reward >= 0)
            {
                if (effect.Opcode == "checkitemspace") result = PewterMachineRules.CanAddMachine(_content.PewterMachines!, inventory, id) ? 1 : 0;
                else
                {
                    var added = PewterMachineRules.AddMachine(_content.PewterMachines!, inventory, id);
                    if (!added.Succeeded) throw new InvalidDataException("department_rooftop_reward_add:" + added.Error);
                    next = state with { Inventory = added.State }; result = 1;
                }
            }
            else
            {
                var view = ShopInventoryView(inventory);
                var changed = effect.Opcode switch
                {
                    "checkitemspace" => EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, id, 1),
                    "additem" => EarlyInventoryAddRules.Add(ShopCatalog, view, id, 1),
                    "removeitem" => EarlyInventoryAddRules.Remove(ShopCatalog, view, id, 1),
                    _ => throw new InvalidDataException("department_rooftop_item_opcode")
                };
                if (!changed.Succeeded && (effect.Opcode != "checkitemspace" || changed.Error is not ("inventory_stack_full" or "inventory_destination_full")))
                    throw new InvalidDataException("department_rooftop_inventory:" + changed.Error);
                next = state with { Inventory = MergeShopInventory(inventory, changed.State) }; result = changed.Succeeded ? 1 : 0;
            }
        }
        next = next with { Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", result) } };
        return true;
    }
}
