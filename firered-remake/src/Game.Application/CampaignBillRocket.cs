using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static readonly ItemId BillRocketMachine = new("pret:ITEM_TM28");
    private const string BillRocketSource = "data/maps/CeruleanCity/scripts.inc";
    private const string BillRocketEntry = "CeruleanCity_EventScript_Grunt";
    private const string BillRocketTrigger = "CeruleanCity_EventScript_GruntTrigger";
    private const string BillRocketReward = "CeruleanCity_EventScript_GruntDefeated";

    private bool IsBillRocketButton(CampaignState state) => _content.BillEnabled &&
        state.World.Player.MapId.Value == "map.cerulean-city" &&
        state.Script.Wait?.Kind == StoryWaitKind.ButtonPress && !state.Script.Stack.IsEmpty &&
        state.Script.Stack[^1].ProgramId == BillRocketEntry;

    private Decision<CampaignState> DecideBillRocketButton(CampaignState state, CampaignCommand command)
    {
        if (!IsBillRocketButton(state) || command.Text is not ("A" or "B"))
            return new(CommandStatus.Rejected, state, "bill_rocket_button_invalid");
        var next = state with { PresentationEffects = [], PresentationTicks = 0, Motions = [] };
        return new(CommandStatus.Accepted, Pump(Advance(next,
            StoryRules.Continue(next.Script, next.Script.Wait!.CorrelationId, _content))));
    }

    // Both the direct conversation and the two coordinate triggers use the same
    // original trainer. Authenticate the second call site instead of relaxing
    // source-line checks for all no-intro battles.
    private bool IsBillRocketTriggeredBattle(StoryEffect effect, TrainerId trainer)
    {
        if (!_content.BillEnabled || trainer != CeruleanTrainerPartyFactory.Rocket ||
            effect.Source.Path != BillRocketSource || effect.Source.Symbol != BillRocketTrigger ||
            effect.Opcode != "trainerbattle_no_intro") return false;
        var instruction = _content.FindProgram(BillRocketTrigger)?.Instructions
            .SingleOrDefault(i => i.Op == StoryOp.TrainerBattle);
        return instruction is not null && instruction.Source == effect.Source &&
            instruction.Opcode == effect.Opcode && instruction.Args.SequenceEqual(effect.Args);
    }

    private bool TryApplyBillRocketEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.BillEnabled) return false;
        var a = effect.Args;
        if (effect.Opcode == "goto_if_defeated" && effect.Source.Path == BillRocketSource)
        {
            var instruction = _content.FindProgram(BillRocketEntry)?.Instructions
                .SingleOrDefault(i => i.Opcode == "goto_if_defeated");
            if (instruction?.Source != effect.Source || !a.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_GRUNT_5", BillRocketReward }) ||
                state.World.Player.MapId.Value != "map.cerulean-city" || state.Script.Stack.IsEmpty ||
                state.Script.Stack[^1].ProgramId != BillRocketEntry)
                throw new InvalidDataException("bill_rocket_defeated_branch_source_context");
            // Like the original comparison-result register, this branch must not
            // overwrite VAR_RESULT, which belongs to the later capacity check.
            if (state.BattledTrainers.Contains(CeruleanTrainerPartyFactory.Rocket.Value))
                next = state with { Script = state.Script with { Stack = state.Script.Stack.SetItem(
                    state.Script.Stack.Length - 1, new(BillRocketReward, 0)) } };
            return true;
        }
        if (effect.Opcode == "waitbuttonpress" && effect.Source.Path == BillRocketSource)
        {
            var instruction = _content.FindProgram(BillRocketEntry)?.Instructions
                .SingleOrDefault(i => i.Opcode == "waitbuttonpress");
            if (a.Length != 0 || !IsBillRocketButton(state) || instruction?.Source != effect.Source ||
                state.Dialogue?.TextSymbol != "CeruleanCity_Text_GruntIntro" ||
                !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET"))
                throw new InvalidDataException("bill_rocket_button_source_context");
            return true;
        }
        if (effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or
            "bufferitemname" or "bufferitemnameplural")) return false;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal)
            ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n
            : _content.TryConstant(token, out n) ? n
            : throw new InvalidDataException("bill_rocket_item_operand_missing: " + token);
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1)) throw new InvalidDataException("bill_rocket_item_arity");
        if (Value(a[buffer ? 1 : 0]) != Value("ITEM_TM28")) return false;
        if (state.World.Player.MapId.Value != "map.cerulean-city" ||
            effect.Source.Path is not (BillRocketSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("bill_rocket_tm_source_context");
        if (!state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET") ||
            !state.BattledTrainers.Contains(CeruleanTrainerPartyFactory.Rocket.Value) ||
            !(state.ForestTrainerHistory ?? []).Any(h => h.TrainerId == CeruleanTrainerPartyFactory.Rocket.Value && h.Outcome == WildBattleOutcome.Won) ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CERULEAN_CITY_ROCKET") != 1 ||
            state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM28_FROM_ROCKET") ||
            state.Script.Flags.GetValueOrDefault("FLAG_HIDE_CERULEAN_ROCKET"))
            throw new InvalidDataException("bill_rocket_tm_reward_context");
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) ||
                a.Length == 3 && Value(a[2]) != 1) throw new InvalidDataException("bill_rocket_tm_buffer_context");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "TM28") };
            return true;
        }
        int result;
        if (effect.Opcode == "checkitemtype")
        {
            if (a.Length != 1) throw new InvalidDataException("bill_rocket_item_arity");
            result = Value("POCKET_TM_CASE");
        }
        else
        {
            if (a.Length > 2 || (a.Length == 2 ? Value(a[1]) : 1) != 1 ||
                effect.Source.Path != BillRocketSource || effect.Source.Symbol != BillRocketReward)
                throw new InvalidDataException("bill_rocket_tm_quantity_or_source");
            var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
            var invalid = ValidateCampaignInventory(inventory);
            if (invalid is not null) throw new InvalidDataException(invalid);
            var machines = _content.PewterMachines ?? throw new InvalidDataException("machines_missing");
            if (effect.Opcode == "checkitemspace")
                result = MachineRules.CanAddMachine(machines, inventory, BillRocketMachine) ? 1 : 0;
            else
            {
                if (OpeningInventoryRules.Quantity(inventory.Machines ?? [], BillRocketMachine) != 0)
                    throw new InvalidDataException("bill_rocket_tm_duplicate");
                var added = MachineRules.AddMachine(machines, inventory, BillRocketMachine);
                if (!added.Succeeded) throw new InvalidDataException("bill_rocket_tm_add_after_space_check: " + added.Error);
                next = state with { Inventory = added.State };
                result = 1;
            }
        }
        next = next with { Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", result) } };
        return true;
    }

    private string? ValidateBillRocketProgress(CampaignState state)
    {
        bool got = state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM28_FROM_ROCKET");
        bool hidden = state.Script.Flags.GetValueOrDefault("FLAG_HIDE_CERULEAN_ROCKET");
        int scene = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CERULEAN_CITY_ROCKET");
        int quantity = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], BillRocketMachine);
        var history = (state.ForestTrainerHistory ?? []).Where(h => h.TrainerId == CeruleanTrainerPartyFactory.Rocket.Value).ToArray();
        if (!_content.BillEnabled) return got || hidden || scene != 0 || quantity != 0 || history.Length != 0
            ? "save_bill_rocket_profile_missing" : null;
        bool won = history.Any(h => h.Outcome == WildBattleOutcome.Won);
        if (scene is < 0 or > 1 || won != (scene == 1) ||
            state.BattledTrainers.Contains(CeruleanTrainerPartyFactory.Rocket.Value) != won ||
            history.Length != 0 && !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET"))
            return "save_bill_rocket_progress";
        // A successful battle with a full pocket leaves scene 1 and a visible
        // Grunt. The original goto_if_defeated then retries only the gift.
        // Zero after receipt is valid because TM28 may already have been taught.
        if (got && !won || hidden != got || quantity > 1 || !got && quantity != 0)
            return "save_bill_rocket_tm_reward";
        if (state.World.Player.MapId.Value == "map.cerulean-city" &&
            (!state.Actors.TryGetValue("LOCALID_CERULEAN_GRUNT", out var grunt) || hidden && grunt.Active))
            return "save_bill_rocket_actor";
        return null;
    }
}
