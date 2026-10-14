using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignRoute5DayCareStage { FieldScript, Party }
public sealed record CampaignRoute5DayCare(CampaignRoute5DayCareStage Stage, int PartyIndex = -1,
    long? InstanceId = null, SourceDayCareQuote? Quote = null, bool Deposited = false,
    bool UsageCounted = false, bool Withdrawn = false, bool PaymentAcknowledged = false);

/// <summary>The original Route 5 script owns text, questions, cry and refusal order.
/// This single-slot service never evolves, breeds, hatches or allocates an owned identity.</summary>
public sealed partial class CampaignSession
{
    private const string DayCareMap = "map.route-5-pokemon-day-care";
    private const string DayCarePath = "data/scripts/day_care.inc";
    private const string DayCarePrefix = "Route5_PokemonDayCare_EventScript_";
    private const string DayCareUsageStat = "GAME_STAT_USED_DAYCARE";

    // Null is the canonical untouched/empty state. Adding this optional field never silently
    // migrates an old content fingerprint: SaveCodec retains its strict version admission.
    private string? ValidateRoute5DayCare(CampaignState state)
    {
        if (!_content.Route5DayCareEnabled) return state.Route5DayCare is not null ? "save_daycare_content_missing" : null;
        if (state.DayCareInteraction is not null) return "save_event_active";
        if (state.Storage is null) return "save_daycare_storage_missing";
        var error = Route5DayCareRules.Validate(_content.PokemonContent!, _binding.Ownership,
            state.Route5DayCare ?? Route5DayCareState.Empty, state.Party, state.Storage,
            state.Mail ?? SourceMailState.Empty, state.NextPokemonId);
        if (error is not null) return error;
        return state.Route5DayCare?.Resident is not null && state.GameStats?.GetValueOrDefault(DayCareUsageStat, 0) is not > 0
            ? "save_daycare_usage_missing" : null;
    }

    private CampaignState ApplyRoute5DayCareWalkingStep(CampaignState state)
    {
        if (!_content.Route5DayCareEnabled || state.Route5DayCare?.Resident is null) return state;
        // Callers have already declined trainer, frame, coordinate, forced movement and poison
        // scripts. Vs Seeker/Union Room are not admitted in the current composition.
        if (state.Script.QuestLogState != 0 || state.FieldPoison is not null || !state.Script.Completed ||
            state.Script.Locked || state.Script.Wait is not null || state.DayCareInteraction is not null)
            throw new InvalidDataException("daycare_step_context");
        return state with { Route5DayCare = Route5DayCareRules.AdvanceEligibleSteps(state.Route5DayCare) };
    }

    private bool InRoute5DayCareScript(CampaignState state)
    {
        if (!_content.Route5DayCareEnabled || state.World.Player.MapId.Value != DayCareMap ||
            !state.Script.Locked || state.Script.Completed || state.Script.QuestLogState != 0 ||
            state.LastTalked is not {} id || !state.Actors.TryGetValue(id, out var actor) ||
            !actor.Active || actor.Invisible || actor.SourceLocalId != 1 || actor.GraphicsId != "OBJ_EVENT_GFX_GENTLEMAN" ||
            actor.ScriptId != DayCarePrefix + "DaycareMan" || actor.TemplateX != 4 || actor.TemplateY != 4 || actor.Elevation != 3)
            return false;
        return _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == id) is
            { SourceLocalId: 1, IsClone: false, GraphicsId: "OBJ_EVENT_GFX_GENTLEMAN", ScriptId: DayCarePrefix + "DaycareMan", X: 4, Y: 4, Elevation: 3 };
    }

    private StoryInstruction? DayCareInstruction(CampaignState state)
    {
        if (state.Script.Stack.IsEmpty) return null;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length ? program.Instructions[index] : null;
    }

    private static string? DayCareSelection(CampaignState state, int index, long? identity) =>
        index < 0 || index >= state.Party.Length ? "daycare_party_index" :
        identity != state.Party[index].InstanceId ? "daycare_target_mismatch" :
        state.Party[index].IsEgg ? "daycare_egg" : null;

    private bool TryDecideRoute5DayCare(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        decision = No("daycare_not_pending");
        if (state.DayCareInteraction is not {} scene) return command.Kind.StartsWith("daycare_", StringComparison.Ordinal);
        if (!InRoute5DayCareScript(state)) { decision = No("daycare_callback_context"); return true; }
        if (scene.Stage == CampaignRoute5DayCareStage.FieldScript)
        {
            if (command.Kind == "ack") return false;
            bool offer = state.Script.Wait?.Kind == StoryWaitKind.Choice && state.Dialogue is
                { TextSymbol: "Route5_PokemonDayCare_Text_OweMeXForMonsReturn" } text && text.Page + 1 == text.Pages.Length;
            if (offer && command.Kind == "daycare_quote")
            { decision = new(CommandStatus.Accepted, state); return true; }
            if (offer && command.Kind is "daycare_withdraw" or "daycare_cancel")
            {
                if (command.Kind == "daycare_withdraw" && command.TargetInstanceId != scene.Quote?.InstanceId)
                    decision = No("daycare_target_mismatch");
                else decision = Decide(state, new("ack", Choice: command.Kind == "daycare_withdraw" ? 1 : 0));
                return true;
            }
            decision = No("daycare_event_active"); return true;
        }
        var instruction = DayCareInstruction(state);
        if (state.Script.Wait?.Kind != StoryWaitKind.PartySelection || instruction is not
            { Opcode: "waitstate", Source.Path: DayCarePath, Source.Symbol: DayCarePrefix + "TryGiveMon" })
        { decision = No("daycare_callback_context"); return true; }
        int selection = 6;
        if (command.Kind != "daycare_cancel")
        {
            if (command.Kind != "daycare_deposit") { decision = No("daycare_selection_required"); return true; }
            if (DayCareSelection(state, command.PartyIndex, command.TargetInstanceId) is {} error)
            { decision = No(error); return true; }
            selection = command.PartyIndex;
        }
        var next = state with { DayCareInteraction = new(CampaignRoute5DayCareStage.FieldScript, selection,
                selection < 6 ? state.Party[selection].InstanceId : null), Dialogue = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", selection) } };
        decision = new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }

    private bool TryApplyRoute5DayCareEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != DayCarePath || !effect.Source.Symbol.StartsWith(DayCarePrefix, StringComparison.Ordinal) ||
            effect.Opcode is not ("special" or "specialvar" or "waitstate" or "showmoneybox" or "hidemoneybox" or "updatemoneybox" or "incrementgamestat" or "getpartysize" or "subvar" or "bufferpartymonnick")) return false;
        if (!InRoute5DayCareScript(state)) throw new InvalidDataException("daycare_source_context");
        var instruction = DayCareInstruction(state);
        if (instruction is null || instruction.Opcode != effect.Opcode || instruction.Source != effect.Source ||
            instruction.Args.Length != effect.Args.Length || instruction.Args.Where((arg, index) =>
                arg.StartsWith("VAR_", StringComparison.Ordinal) && !(effect.Opcode is "specialvar" or "subvar" && index == 0)
                    ? state.Script.Variables.GetValueOrDefault(arg).ToString(CultureInfo.InvariantCulture) != effect.Args[index]
                    : arg != effect.Args[index]).Any()) throw new InvalidDataException("daycare_source_callback");
        string symbol = effect.Source.Symbol[DayCarePrefix.Length..];
        void Require(string expected) { if (symbol != expected) throw new InvalidDataException("daycare_source_label"); }
        CampaignState Set(string variable, int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(variable, value) } };
        SourceDayCareQuote Quote() => Route5DayCareRules.Quote(_content.PokemonContent!, _binding.Ownership,
            state.Route5DayCare ?? Route5DayCareState.Empty);
        PartyPokemon Selected()
        {
            var scene = state.DayCareInteraction;
            if (scene is not { Stage: CampaignRoute5DayCareStage.FieldScript, Deposited: false } ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != scene.PartyIndex ||
                DayCareSelection(state, scene.PartyIndex, scene.InstanceId) is not null)
                throw new InvalidDataException("daycare_selected_context");
            return state.Party[scene.PartyIndex];
        }
        if (effect.Opcode is "getpartysize" or "subvar" or "bufferpartymonnick")
        {
            Require("RetrieveMon");
            if (state.DayCareInteraction is not { Withdrawn: true, PaymentAcknowledged: true, Quote: {} returned } ||
                state.Party.IsEmpty || state.Party[^1].InstanceId != returned.InstanceId)
                throw new InvalidDataException("daycare_returned_party_context");
            if (effect.Opcode == "getpartysize" && effect.Args.IsEmpty) next = Set("VAR_RESULT", state.Party.Length);
            else if (effect.Opcode == "subvar" && effect.Args.SequenceEqual(new[] { "VAR_RESULT", "1" }) &&
                state.Script.Variables.GetValueOrDefault("VAR_RESULT") == state.Party.Length)
                next = Set("VAR_RESULT", state.Party.Length - 1);
            else if (effect.Opcode == "bufferpartymonnick" && effect.Args.SequenceEqual(new[] { "STR_VAR_1", (state.Party.Length - 1).ToString(CultureInfo.InvariantCulture) }))
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", state.Party[^1].Nickname) };
            else throw new InvalidDataException("daycare_returned_party_operands");
            return true;
        }
        if (effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox")
        {
            bool allowed = effect.Opcode == "showmoneybox" ? symbol is "DaycareMan" or "TryGiveMon" :
                effect.Opcode == "hidemoneybox" ? symbol is "TryGiveMon" or "CloseMoneyBox" : symbol == "RetrieveMon";
            if (!allowed) throw new InvalidDataException("daycare_moneybox_context");
            next = ApplySourceMoneyBox(state, effect); return true;
        }
        if (effect.Opcode == "waitstate")
        {
            Require("TryGiveMon");
            if (!effect.Args.IsEmpty || state.DayCareInteraction?.Stage != CampaignRoute5DayCareStage.Party ||
                state.Script.Wait?.Kind != StoryWaitKind.Presentation) throw new InvalidDataException("daycare_wait_context");
            next = state with { Dialogue = null, PresentationTicks = 0,
                Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } }; return true;
        }
        if (effect.Opcode == "incrementgamestat")
        {
            Require("TryGiveMon");
            if (!effect.Args.SequenceEqual(new[] { DayCareUsageStat }) || state.DayCareInteraction is not { Deposited: true, UsageCounted: false } used)
                throw new InvalidDataException("daycare_stat_context");
            var stats = state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
            next = state with { GameStats = stats.SetItem(DayCareUsageStat, Math.Min(0xffffff, stats.GetValueOrDefault(DayCareUsageStat) + 1)),
                DayCareInteraction = used with { UsageCounted = true } }; return true;
        }
        bool returns = effect.Opcode == "specialvar";
        if (effect.Args.Length != (returns ? 2 : 1)) throw new InvalidDataException("daycare_special_operands");
        string function = effect.Args[returns ? 1 : 0];
        string destination = returns ? effect.Args[0] : "";
        if (returns && destination != (function == "GetSelectedMonNicknameAndSpecies" ? "VAR_0x8005" : "VAR_RESULT"))
            throw new InvalidDataException("daycare_special_destination");
        switch (function)
        {
            case "IsThereMonInRoute5Daycare" when returns:
                Require("DaycareMan");
                next = Set(destination, state.Route5DayCare?.Resident is null ? 0 : 1) with { DayCareInteraction = new(CampaignRoute5DayCareStage.FieldScript) }; break;
            case "CountPartyNonEggMons" when returns:
                Require("TryGiveMon"); next = Set(destination, state.Party.Count(p => !p.IsEgg)); break;
            case "ChooseSendDaycareMon" when !returns:
                Require("TryGiveMon");
                next = state with { DayCareInteraction = new(CampaignRoute5DayCareStage.Party) }; break;
            case "CountPartyAliveNonEggMons_IgnoreVar0x8004Slot" when returns:
                Require("TryGiveMon"); _ = Selected();
                next = Set(destination, state.Party.Where((p, i) => i != state.DayCareInteraction!.PartyIndex && !p.IsEgg && p.Pokemon.CurrentHp > 0).Count()); break;
            case "GetSelectedMonNicknameAndSpecies" when returns:
                Require("TryGiveMon"); var selected = Selected();
                next = Set(destination, selected.Pokemon.SourceSpeciesId) with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", selected.Nickname) }; break;
            case "PutMonInRoute5Daycare" when !returns:
                Require("TryGiveMon"); _ = Selected();
                var deposit = Route5DayCareRules.Deposit(_content.PokemonContent!, _binding.Ownership,
                    state.Route5DayCare ?? Route5DayCareState.Empty, state.Party, state.Storage!, state.Mail ?? SourceMailState.Empty, state.DayCareInteraction!.PartyIndex);
                if (!deposit.Succeeded) throw new InvalidDataException(deposit.Error);
                next = state with { Route5DayCare = deposit.State, Party = deposit.Party,
                    Mail = state.Mail is null && deposit.State.Resident?.Mail is null ? null : deposit.Mail,
                    DayCareInteraction = state.DayCareInteraction with { Deposited = true } }; break;
            case "GetNumLevelsGainedForRoute5DaycareMon" when returns:
            case "GetCostToWithdrawRoute5DaycareMon" when !returns:
                Require("CheckOnMon"); var quote = Quote();
                next = Set(returns ? destination : "VAR_0x8005", returns ? quote.LevelsGained : quote.Cost) with
                { DayCareInteraction = state.DayCareInteraction! with { Quote = quote }, TextBuffers = state.TextBuffers
                    .SetItem("STR_VAR_1", state.Route5DayCare!.Resident!.Pokemon.Nickname)
                    .SetItem("STR_VAR_2", (returns ? quote.LevelsGained : quote.Cost).ToString(CultureInfo.InvariantCulture)) }; break;
            case "CalculatePlayerPartyCount" when returns:
                Require("TryRetrieveMon"); next = Set(destination, state.Party.Length); break;
            case "IsEnoughForCostInVar0x8005" when returns:
                Require("TryRetrieveMon"); var cost = Quote().Cost;
                if (state.Script.Variables.GetValueOrDefault("VAR_0x8005") != cost) throw new InvalidDataException("daycare_quote_changed");
                next = SourceMoneyResult(state, cost); break;
            case "TakePokemonFromRoute5Daycare" when returns:
                Require("RetrieveMon");
                var withdrawal = Route5DayCareRules.Withdraw(_content.PokemonContent!, _binding.Ownership,
                    state.Route5DayCare ?? Route5DayCareState.Empty, state.Party, state.Storage!, state.Mail ?? SourceMailState.Empty, state.Money);
                if (!withdrawal.Succeeded) throw new InvalidDataException(withdrawal.Error);
                if (withdrawal.Quote != state.DayCareInteraction?.Quote || state.Script.Variables.GetValueOrDefault("VAR_0x8005") != withdrawal.MoneyPaid)
                    throw new InvalidDataException("daycare_quote_changed");
                // Returning the resident, reallocating Mail and paying form one command transaction.
                // The following native SubtractMoney callback acknowledges this already-committed debit.
                next = DebitSourceMoney(Set(destination, withdrawal.Party[^1].Pokemon.SourceSpeciesId), withdrawal.MoneyPaid) with
                { Route5DayCare = null, Party = withdrawal.Party, Mail = state.Mail is null && state.Route5DayCare?.Resident?.Mail is null ? null : withdrawal.Mail,
                    DayCareInteraction = state.DayCareInteraction! with { Withdrawn = true } }; break;
            case "SubtractMoneyFromVar0x8005" when !returns:
                Require("RetrieveMon");
                if (state.DayCareInteraction is not { Withdrawn: true, PaymentAcknowledged: false, Quote: {} paid } transaction ||
                    state.Script.Variables.GetValueOrDefault("VAR_0x8005") != paid.Cost || state.Route5DayCare is not null ||
                    state.Party[^1].InstanceId != paid.InstanceId) throw new InvalidDataException("daycare_payment_context");
                next = state with { DayCareInteraction = transaction with { PaymentAcknowledged = true } }; break;
            default: throw new InvalidDataException("daycare_special_unsupported: " + function);
        }
        return true;
    }
}
