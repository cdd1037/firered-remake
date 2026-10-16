using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string OldAmberMuseum = "map.pewter-museum-1f";
    private const string OldAmberSource = "data/maps/PewterCity_Museum_1F/scripts.inc";
    private const string OldAmberScientist = "PewterCity_Museum_1F_EventScript_OldAmberScientist";
    private const string OldAmberAcquired = "FLAG_GOT_OLD_AMBER";
    private const string OldAmberHidden = "FLAG_HIDE_OLD_AMBER";
    private const string OldAmberObject = "LOCALID_OLD_AMBER";

    private bool InOldAmberScript(CampaignState state) => _content.FossilLabEnabled &&
        state.World.Player.MapId.Value == OldAmberMuseum && state.LastTalked is { } id &&
        state.Actors.TryGetValue(id, out var scientist) && scientist.Active && scientist.SourceLocalId == 3 &&
        scientist.ScriptId == OldAmberScientist && state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") == 3;

    private static void ValidateOldAmberCatalog(ICampaignCatalog content)
    {
        if (!content.FossilLabEnabled) throw new InvalidDataException("old_amber_current_binding");
        var map = content.MapDefinition(new(OldAmberMuseum));
        if (map.SourceMapSymbol != "MAP_PEWTER_CITY_MUSEUM_1F" ||
            !map.Actors.Any(a => a.SourceLocalId == 3 && a.X == 21 && a.Y == 3 && a.ScriptId == OldAmberScientist) ||
            !map.Actors.Any(a => a.LocalId == OldAmberObject && a.SourceLocalId == 4 && a.X == 22 && a.Y == 3 &&
                a.HideFlag == OldAmberHidden && a.ScriptId == "PewterCity_Museum_1F_EventScript_OldAmber"))
            throw new InvalidDataException("old_amber_map_source");
        var program = content.FindProgram(OldAmberScientist);
        if (program is null || program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
            throw new InvalidDataException("old_amber_reward_source");
        bool Has(string opcode, params string[] args) => program.Instructions.Any(i =>
            i.Source.Path == OldAmberSource && i.Opcode == opcode && i.Args.SequenceEqual(args));
        if (!Has("goto_if_set", OldAmberAcquired, "PewterCity_Museum_1F_EventScript_AlreadyGotOldAmber") ||
            !Has("checkitemspace", "ITEM_OLD_AMBER") || !Has("setflag", OldAmberAcquired) ||
            !Has("removeobject", OldAmberObject) || !Has("additem", "ITEM_OLD_AMBER", "1") ||
            !Has("setorcopyvar", "VAR_0x8002", "MUS_OBTAIN_KEY_ITEM") || !Has("call", "Std_ReceivedItem"))
            throw new InvalidDataException("old_amber_reward_source");
    }

    // The museum's removeobject persists FLAG_HIDE_OLD_AMBER. The fossil can
    // subsequently exist only in the bag, the lab's pending slot, or a receipt.
    private string? ValidateOldAmberProgress(CampaignState state)
    {
        bool acquired = state.Script.Flags.GetValueOrDefault(OldAmberAcquired);
        bool hidden = state.Script.Flags.GetValueOrDefault(OldAmberHidden);
        int quantity = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], ItemIds.OldAmberId);
        if (!_content.FossilLabEnabled) return acquired || hidden || quantity != 0 ? "save_old_amber_profile_missing" : null;
        if (acquired != hidden || quantity + DepositedOrRevivedFossils(state, 3) != (acquired ? 1 : 0))
            return "save_old_amber_conservation";
        if (state.World.Player.MapId.Value == OldAmberMuseum &&
            (!state.Actors.TryGetValue(OldAmberObject, out var amber) || amber.SourceLocalId != 4 ||
                amber.HideFlag != OldAmberHidden || amber.Active == hidden)) return "save_old_amber_actor";
        return null;
    }

    private bool TryApplyOldAmberEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.FossilLabEnabled) return false;
        bool rewardSource = effect.Source.Path == OldAmberSource && effect.Source.Symbol == OldAmberScientist;
        if (rewardSource && effect.Opcode == "lock")
        {
            if (!InOldAmberScript(state)) throw new InvalidDataException("old_amber_caller");
            if (ValidateOldAmberProgress(state) is { } error) throw new InvalidDataException(error);
            return true;
        }
        if (rewardSource && effect.Opcode == "removeobject")
        {
            if (!InOldAmberScript(state) || !effect.Args.SequenceEqual(new[] { OldAmberObject }) ||
                !state.Script.Flags.GetValueOrDefault(OldAmberAcquired) || state.Script.Flags.GetValueOrDefault(OldAmberHidden) ||
                OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], ItemIds.OldAmberId) != 0 ||
                !state.Actors.TryGetValue(OldAmberObject, out var amber) || !amber.Active || amber.SourceLocalId != 4 || amber.HideFlag != OldAmberHidden)
                throw new InvalidDataException("old_amber_remove_context");
            // Preserve the shared removeobject implementation and its hide flag.
            return false;
        }
        if (effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        if (effect.Source.Path == FossilLabSource) return false;
        var a = effect.Args;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        int Value(string token) => int.TryParse(token, out int n) ? n :
            state.Script.Variables.TryGetValue(token, out int v) ? v :
            _content.TryConstant(token, out int c) ? c : throw new InvalidDataException("old_amber_operand_unknown:" + token);
        if (a.Length < (buffer ? 2 : 1) || Value(a[buffer ? 1 : 0]) != 354) return false;
        if (!InOldAmberScript(state) || !rewardSource && effect.Source.Path is not ("data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("old_amber_item_context");
        var item = ShopCatalog.Items.Single(i => i.Id == ItemIds.OldAmberId);
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        var keys = inventory.KeyItems ?? [];
        int quantity = OpeningInventoryRules.Quantity(keys, item.Id);
        bool acquired = state.Script.Flags.GetValueOrDefault(OldAmberAcquired);
        bool hidden = state.Script.Flags.GetValueOrDefault(OldAmberHidden);
        if (buffer || effect.Opcode == "checkitemtype")
        {
            if (!acquired || !hidden || quantity != 1) throw new InvalidDataException("old_amber_message_context");
            if (buffer)
            {
                if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) ||
                    a.Length == 3 && Value(a[2]) != 1) throw new InvalidDataException("old_amber_buffer_source");
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", item.Name) };
            }
            else
            {
                if (a.Length != 1) throw new InvalidDataException("old_amber_item_arity");
                next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", Value("POCKET_KEY_ITEMS")) } };
            }
            return true;
        }
        bool adding = effect.Opcode == "additem";
        if (!rewardSource || a.Length != (adding ? 2 : 1) || a[0] != "ITEM_OLD_AMBER" || adding && a[1] != "1" ||
            quantity != 0 || acquired != adding || hidden != adding || DepositedOrRevivedFossils(state, 3) != 0)
            throw new InvalidDataException("old_amber_reward_context");
        if (ValidateCampaignInventory(inventory) is { } invalid) throw new InvalidDataException(invalid);
        var view = inventory;
        // The narrowed view omits unrelated quest keys; capacity counts all keys.
        var result = keys.Length >= ShopCatalog.QuestLimits.KeyItemSlots
            ? new InventoryTransactionResult(view, false, "inventory_destination_full")
            : adding ? EarlyInventoryAddRules.Add(ShopCatalog, view, item.Id, 1)
            : EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, item.Id, 1);
        if (!result.Succeeded && (adding || result.Error is not ("inventory_stack_full" or "inventory_destination_full")))
            throw new InvalidDataException(result.Error ?? "old_amber_reward_failed");
        next = state with
        {
            Inventory = adding && result.Succeeded ? inventory with { KeyItems = keys.Add(new(item.Id, 1)) } : inventory,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Succeeded ? 1 : 0) }
        };
        return true;
    }
}
