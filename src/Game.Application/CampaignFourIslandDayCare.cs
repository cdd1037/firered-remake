using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string FourDayCareHouse = "map.four-island-pokemon-day-care";
    private const string FourDayCareTown = "map.four-island";
    private const string FourDayCareHouseSource = "data/maps/FourIsland_PokemonDayCare/scripts.inc";
    private const string FourDayCareTownSource = "data/maps/FourIsland/scripts.inc";
    private const string FourDayCareWoman = "FourIsland_PokemonDayCare_EventScript_DaycareWoman";
    private const string FourDayCareMan = "FourIsland_EventScript_DaycareMan";
    private const string PendingDayCareEggFlag = "FLAG_PENDING_DAYCARE_EGG";

    private PokemonBreedingData BreedingMetadata => _content.BreedingMetadata is {} metadata &&
        ReferenceEquals(metadata, _content.PokemonContent!.Catalog.BreedingData)
        ? metadata : throw new InvalidDataException("daycare_breeding_metadata_identity");
    private static FourIslandDayCareState FourDayCare(CampaignState state) => state.FourIslandDayCare ?? FourIslandDayCareState.Empty;
    private SourceDayCareResident FourDayCareResident(CampaignState state, int slot) =>
        (slot == 0 ? FourDayCare(state).First : slot == 1 ? FourDayCare(state).Second : null)
        ?? throw new InvalidDataException("daycare_resident_slot");
    private SourceDayCareQuote FourDayCareQuote(CampaignState state, int slot) =>
        FourIslandDayCareRules.Quote(_content.PokemonContent!, _binding.Ownership, FourDayCare(state), slot);
    private static CampaignRoute5DayCare FourDayCareScene(CampaignRoute5DayCareStage stage = CampaignRoute5DayCareStage.FieldScript) =>
        new(stage) { Service = CampaignDayCareService.FourIsland };

    private string? ValidateFourIslandDayCare(CampaignState state)
    {
        if (state.EggHatch is not null || state.DayCareInteraction is not null) return "save_event_active";
        if (state.Storage is null) return "save_daycare_storage_missing";
        var error = FourIslandDayCareRules.Validate(_content.PokemonContent!, _binding.Ownership,
            FourDayCare(state), state.Route5DayCare ?? Route5DayCareState.Empty, state.Party,
            state.Storage, state.Mail ?? SourceMailState.Empty, state.NextPokemonId);
        if (error is not null) return error;
        if (state.Script.Flags.GetValueOrDefault(PendingDayCareEggFlag) != FourDayCare(state).HasPendingEgg) return "save_daycare_pending_flag";
        if (FourDayCare(state).Count > 0 && state.GameStats?.GetValueOrDefault(DayCareUsageStat, 0) is not > 0) return "save_daycare_usage_missing";
        // The source only relocates the man on map entry. Producing/rejecting an egg does not teleport him.
        if (state.World.Player.MapId.Value == FourDayCareTown && state.Actors.Values.SingleOrDefault(a => a.SourceLocalId == 1) is {} man &&
            (man.TemplateX != 16 || man.TemplateY is not (13 or 14))) return "save_daycare_man_position";
        return ValidateDayCareEggReceipts(state);
    }

    private bool InFourDayCareScript(CampaignState state, bool woman, bool requireLock = true)
    {
        if (requireLock && !state.Script.Locked || state.Script.Completed || state.Script.QuestLogState != 0 ||
            state.World.Player.MapId.Value != (woman ? FourDayCareHouse : FourDayCareTown) || state.LastTalked is not {} id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.Invisible || actor.SourceLocalId != 1 ||
            actor.GraphicsId != (woman ? "OBJ_EVENT_GFX_OLD_WOMAN" : "OBJ_EVENT_GFX_OLD_MAN_1") ||
            actor.ScriptId != (woman ? FourDayCareWoman : FourDayCareMan)) return false;
        var definition = _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == id);
        return definition is { SourceLocalId: 1, IsClone: false } && definition.ScriptId == actor.ScriptId &&
            definition.GraphicsId == actor.GraphicsId && definition.X == (woman ? 2 : 16) && definition.Y == (woman ? 2 : 13);
    }

    private bool TryDecideFourIslandDayCare(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        decision = No("daycare_not_pending");
        if (state.DayCareInteraction is not { Service: CampaignDayCareService.FourIsland } scene) return false;
        bool woman = state.World.Player.MapId.Value == FourDayCareHouse;
        if (!InFourDayCareScript(state, woman)) { decision = No("daycare_callback_context"); return true; }
        if (scene.Stage == CampaignRoute5DayCareStage.FieldScript)
        {
            if (command.Kind == "button_press" && !woman && state.Script.Wait is { Kind: StoryWaitKind.ButtonPress } button &&
                DayCareInstruction(state)?.Source.Symbol is "FourIsland_EventScript_DaycareReceivedEgg" or "FourIsland_EventScript_CheckOnTwoMons")
            {
                if (command.Text is not ("A" or "B")) { decision = No("daycare_button_required"); return true; }
                decision = new(CommandStatus.Accepted, Pump(Advance(state with { PresentationEffects = [], Motions = [], PresentationTicks = 0 },
                    StoryRules.Continue(state.Script, button.CorrelationId, _content)))); return true;
            }
            if (command.Kind == "ack") return false;
            bool offer = state.Script.Wait?.Kind == StoryWaitKind.Choice && state.Dialogue is { TextSymbol: "DayCare_Text_ItWillCostX" } text && text.Page + 1 == text.Pages.Length;
            if (offer && command.Kind == "daycare_quote") { decision = new(CommandStatus.Accepted, state); return true; }
            if (offer && command.Kind is "daycare_withdraw" or "daycare_cancel")
            {
                decision = command.Kind == "daycare_withdraw" && command.TargetInstanceId != scene.Quote?.InstanceId
                    ? No("daycare_target_mismatch") : Decide(state, new("ack", Choice: command.Kind == "daycare_withdraw" ? 1 : 0));
                return true;
            }
            decision = No("daycare_event_active"); return true;
        }
        var instruction = DayCareInstruction(state);
        if (!woman || instruction?.Source.Path != FourDayCareHouseSource || instruction.Opcode != "waitstate" || state.Script.Wait?.Kind != StoryWaitKind.PartySelection)
        { decision = No("daycare_callback_context"); return true; }
        int selected;
        string variable;
        CampaignRoute5DayCare updated;
        if (scene.Stage == CampaignRoute5DayCareStage.Party)
        {
            if (instruction.Source.Symbol != "FourIsland_PokemonDayCare_GiveMonToRaise") { decision = No("daycare_callback_context"); return true; }
            selected = 6; variable = "VAR_0x8004";
            if (command.Kind != "daycare_cancel")
            {
                if (command.Kind != "daycare_deposit") { decision = No("daycare_selection_required"); return true; }
                if (DayCareSelection(state, command.PartyIndex, command.TargetInstanceId) is {} error) { decision = No(error); return true; }
                selected = command.PartyIndex;
            }
            updated = FourDayCareScene() with { PartyIndex = selected, InstanceId = selected < 6 ? state.Party[selected].InstanceId : null };
        }
        else
        {
            if (instruction.Source.Symbol != "FourIsland_PokemonDayCare_TryRetrieveMon") { decision = No("daycare_callback_context"); return true; }
            selected = 2; variable = "VAR_RESULT";
            if (command.Kind != "daycare_cancel")
            {
                if (command.Kind != "daycare_resident" || command.Choice is not (0 or 1)) { decision = No("daycare_resident_selection_required"); return true; }
                selected = command.Choice.Value;
                if (FourDayCareResident(state, selected).Pokemon.InstanceId != command.TargetInstanceId) { decision = No("daycare_target_mismatch"); return true; }
            }
            updated = FourDayCareScene() with { ResidentSlot = selected < 2 ? selected : -1 };
        }
        var next = state with { DayCareInteraction = updated, Dialogue = null, Motions = [], PresentationTicks = 0,
            PresentationEffects = [], ScreenIsBlack = false, Script = state.Script with { Variables = state.Script.Variables.SetItem(variable, selected) } };
        decision = new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait.CorrelationId, _content))));
        return true;
    }

    private bool TryApplyFourIslandDayCareEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool woman = effect.Source.Path == FourDayCareHouseSource;
        bool man = effect.Source.Path == FourDayCareTownSource && effect.Source.Symbol is
            "FourIsland_EventScript_DaycareMan" or "FourIsland_EventScript_DaycareEggWaiting" or "FourIsland_EventScript_DaycareAcceptEgg" or
            "FourIsland_EventScript_DaycareReceivedEgg" or "FourIsland_EventScript_CheckOnOneMon" or "FourIsland_EventScript_CheckOnTwoMons";
        if (!woman && !man || effect.Opcode is not ("special" or "specialvar" or "waitstate" or "waitbuttonpress" or "incrementgamestat")) return false;
        bool beforeLock = man && effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" });
        if (!InFourDayCareScript(state, woman, !beforeLock)) throw new InvalidDataException("daycare_source_context");
        var instruction = DayCareInstruction(state);
        if (instruction is null || instruction.Opcode != effect.Opcode || instruction.Source != effect.Source || !instruction.Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("daycare_source_callback");
        string symbol = effect.Source.Symbol;
        void Require(params string[] labels) { if (!labels.Contains(symbol)) throw new InvalidDataException("daycare_source_label"); }
        CampaignState Set(string variable, int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(variable, value) } };
        PartyPokemon Selected()
        {
            var scene = state.DayCareInteraction;
            if (scene is not { Service: CampaignDayCareService.FourIsland, Stage: CampaignRoute5DayCareStage.FieldScript, Deposited: false } ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != scene.PartyIndex || DayCareSelection(state, scene.PartyIndex, scene.InstanceId) is not null)
                throw new InvalidDataException("daycare_selected_context");
            return state.Party[scene.PartyIndex];
        }
        if (effect.Opcode == "waitbuttonpress")
        {
            Require("FourIsland_EventScript_DaycareReceivedEgg", "FourIsland_EventScript_CheckOnTwoMons");
            if (!effect.Args.IsEmpty || state.Script.Wait?.Kind != StoryWaitKind.ButtonPress || state.Dialogue is null)
                throw new InvalidDataException("daycare_button_context");
            return true;
        }
        if (effect.Opcode == "waitstate")
        {
            Require("FourIsland_PokemonDayCare_GiveMonToRaise", "FourIsland_PokemonDayCare_TryRetrieveMon");
            if (!effect.Args.IsEmpty || state.DayCareInteraction?.Stage is not (CampaignRoute5DayCareStage.Party or CampaignRoute5DayCareStage.Residents) || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("daycare_wait_context");
            next = state with { Dialogue = null, PresentationTicks = 0, Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } }; return true;
        }
        if (effect.Opcode == "incrementgamestat")
        {
            Require("FourIsland_PokemonDayCare_GiveMonToRaise");
            if (!effect.Args.SequenceEqual(new[] { DayCareUsageStat }) || state.DayCareInteraction is not { Deposited: true, UsageCounted: false } used) throw new InvalidDataException("daycare_stat_context");
            var stats = state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
            next = state with { GameStats = stats.SetItem(DayCareUsageStat, Math.Min(0xffffff, stats.GetValueOrDefault(DayCareUsageStat) + 1)), DayCareInteraction = used with { UsageCounted = true } }; return true;
        }
        bool returns = effect.Opcode == "specialvar";
        if (effect.Args.Length != (returns ? 2 : 1)) throw new InvalidDataException("daycare_special_operands");
        string function = effect.Args[returns ? 1 : 0], destination = returns ? effect.Args[0] : "";
        if (returns && destination != (function == "GetSelectedMonNicknameAndSpecies" ? "VAR_0x8005" : "VAR_RESULT")) throw new InvalidDataException("daycare_special_destination");
        switch (function)
        {
            case "QuestLog_CutRecording" when !returns:
                Require(FourDayCareMan); break;
            case "GetDaycareState" when returns:
                Require(FourDayCareWoman, FourDayCareMan, "FourIsland_PokemonDayCare_GiveMonToRaise", "FourIsland_PokemonDayCare_TryRetrieveMon", "FourIsland_PokemonDayCare_RetrieveMon");
                next = Set(destination, FourIslandDayCareRules.GetScriptState(FourDayCare(state))) with { DayCareInteraction = state.DayCareInteraction ?? FourDayCareScene() }; break;
            case "CountPartyNonEggMons" when returns:
                Require("FourIsland_PokemonDayCare_GiveMonToRaise"); next = Set(destination, state.Party.Count(p => !p.IsEgg)); break;
            case "ChooseSendDaycareMon" when !returns:
                Require("FourIsland_PokemonDayCare_GiveMonToRaise"); next = state with { DayCareInteraction = FourDayCareScene(CampaignRoute5DayCareStage.Party) }; break;
            case "CountPartyAliveNonEggMons_IgnoreVar0x8004Slot" when returns:
                Require("FourIsland_PokemonDayCare_GiveMonToRaise"); _ = Selected();
                next = Set(destination, state.Party.Where((p, i) => i != state.DayCareInteraction!.PartyIndex && !p.IsEgg && p.Pokemon.CurrentHp > 0).Count()); break;
            case "GetSelectedMonNicknameAndSpecies" when returns:
                Require("FourIsland_PokemonDayCare_GiveMonToRaise"); var selected = Selected();
                next = Set(destination, selected.Pokemon.SourceSpeciesId) with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", selected.Nickname) }; break;
            case "StoreSelectedPokemonInDaycare" when !returns:
                Require("FourIsland_PokemonDayCare_GiveMonToRaise"); _ = Selected();
                var deposit = FourIslandDayCareRules.Deposit(_content.PokemonContent!, _binding.Ownership, FourDayCare(state), state.Route5DayCare ?? Route5DayCareState.Empty,
                    state.Party, state.Storage!, state.Mail ?? SourceMailState.Empty, state.DayCareInteraction!.PartyIndex);
                if (!deposit.Succeeded) throw new InvalidDataException(deposit.Error);
                next = state with { FourIslandDayCare = deposit.State, Party = deposit.Party,
                    Mail = state.Mail is null && deposit.State.First?.Mail is null && deposit.State.Second?.Mail is null ? null : deposit.Mail,
                    DayCareInteraction = state.DayCareInteraction with { Deposited = true } }; break;
            case "GetNumLevelsGainedFromDaycare" when returns:
            case "GetDaycareCost" when !returns:
                Require(returns ? "FourIsland_PokemonDayCare_DisplayLevelsGained" : "FourIsland_PokemonDayCare_CostPrompt");
                int slot = state.Script.Variables.GetValueOrDefault("VAR_0x8004"); var quote = FourDayCareQuote(state, slot);
                next = Set(returns ? destination : "VAR_0x8005", returns ? quote.LevelsGained : quote.Cost) with {
                    DayCareInteraction = FourDayCareScene() with { ResidentSlot = slot, Quote = quote }, TextBuffers = state.TextBuffers
                        .SetItem("STR_VAR_1", FourDayCareResident(state, slot).Pokemon.Nickname).SetItem("STR_VAR_2", (returns ? quote.LevelsGained : quote.Cost).ToString(CultureInfo.InvariantCulture)) }; break;
            case "CalculatePlayerPartyCount" when returns:
                Require("FourIsland_PokemonDayCare_TryRetrieveMon", "FourIsland_EventScript_DaycareAcceptEgg"); next = Set(destination, state.Party.Length); break;
            case "ShowDaycareLevelMenu" when !returns:
                Require("FourIsland_PokemonDayCare_TryRetrieveMon");
                if (FourDayCare(state).Count != 2 || FourDayCare(state).HasPendingEgg) throw new InvalidDataException("daycare_resident_menu_context");
                next = state with { DayCareInteraction = FourDayCareScene(CampaignRoute5DayCareStage.Residents) with { Residents = Enumerable.Range(0, 2).Select(i => {
                    var mon = FourDayCareResident(state, i).Pokemon; var q = FourDayCareQuote(state, i);
                    return new CampaignDayCareResidentRow(i, mon.InstanceId, mon.Nickname, mon.Pokemon.SourceSpeciesId, q.LevelAfter, q.LevelsGained); }).ToImmutableArray() } }; break;
            case "IsEnoughForCostInVar0x8005" when returns:
                Require("FourIsland_PokemonDayCare_CheckEnoughMoney");
                int cost = FourDayCareQuote(state, state.DayCareInteraction!.ResidentSlot).Cost;
                if (state.Script.Variables.GetValueOrDefault("VAR_0x8005") != cost) throw new InvalidDataException("daycare_quote_changed");
                next = SourceMoneyResult(state, cost); break;
            case "TakePokemonFromDaycare" when returns:
                Require("FourIsland_PokemonDayCare_RetrieveMon");
                var scene = state.DayCareInteraction ?? throw new InvalidDataException("daycare_withdraw_context");
                var withdrawal = FourIslandDayCareRules.Withdraw(_content.PokemonContent!, _binding.Ownership, FourDayCare(state), state.Route5DayCare ?? Route5DayCareState.Empty,
                    state.Party, state.Storage!, state.Mail ?? SourceMailState.Empty, scene.ResidentSlot, state.Money);
                if (!withdrawal.Succeeded) throw new InvalidDataException(withdrawal.Error);
                if (withdrawal.Quote != scene.Quote || state.Script.Variables.GetValueOrDefault("VAR_0x8005") != withdrawal.MoneyPaid) throw new InvalidDataException("daycare_quote_changed");
                next = DebitSourceMoney(Set(destination, withdrawal.Party[^1].Pokemon.SourceSpeciesId), withdrawal.MoneyPaid) with { FourIslandDayCare = withdrawal.State,
                    Party = withdrawal.Party, Mail = state.Mail is null && FourDayCareResident(state, scene.ResidentSlot).Mail is null ? null : withdrawal.Mail,
                    TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", withdrawal.Party[^1].Nickname), DayCareInteraction = scene with { Withdrawn = true } }; break;
            case "SubtractMoneyFromVar0x8005" when !returns:
                Require("FourIsland_PokemonDayCare_RetrieveMon");
                if (state.DayCareInteraction is not { Withdrawn: true, PaymentAcknowledged: false, Quote: {} paid } transaction ||
                    state.Script.Variables.GetValueOrDefault("VAR_0x8005") != paid.Cost || state.Party[^1].InstanceId != paid.InstanceId) throw new InvalidDataException("daycare_payment_context");
                next = state with { DayCareInteraction = transaction with { PaymentAcknowledged = true } }; break;
            case "GetDaycareMonNicknames" when !returns:
                Require(FourDayCareMan, "FourIsland_EventScript_CheckOnOneMon", "FourIsland_EventScript_CheckOnTwoMons");
                var buffers = state.TextBuffers;
                if (FourDayCare(state).First is {} first) buffers = buffers.SetItem("STR_VAR_1", first.Pokemon.Nickname).SetItem("STR_VAR_3", first.Pokemon.OriginalTrainerName);
                if (FourDayCare(state).Second is {} second) buffers = buffers.SetItem("STR_VAR_2", second.Pokemon.Nickname);
                next = state with { TextBuffers = buffers }; break;
            case "SetDaycareCompatibilityString" when !returns:
                Require("FourIsland_EventScript_CheckOnTwoMons");
                int score = FourIslandDayCareRules.Compatibility(FourDayCare(state), _binding.BreedingSpecies);
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_4", _content.DayCareCompatibilityTextSymbols[score]) }; break;
            case "ShowFieldMessageStringVar4" when !returns:
                Require("FourIsland_EventScript_CheckOnTwoMons");
                string text = state.TextBuffers["STR_VAR_4"];
                next = state with { TextBuffers = state.TextBuffers.Remove("STR_VAR_4"), Dialogue = new(text, _content.TextPages(text), 0, false, false) }; break;
            case "GiveEggFromDaycare" when !returns:
                Require("FourIsland_EventScript_DaycareReceivedEgg"); next = ReceiveDayCareEgg(state); break;
            case "RejectEggFromDayCare" when !returns:
                Require("FourIsland_EventScript_DaycareEggWaiting");
                if (!FourDayCare(state).HasPendingEgg || state.Script.Flags.GetValueOrDefault(PendingDayCareEggFlag)) throw new InvalidDataException("daycare_reject_context");
                next = state with { FourIslandDayCare = FourIslandDayCareRules.ClearPendingEgg(FourDayCare(state)) }; break;
            default: throw new InvalidDataException("four_island_daycare_special_unsupported: " + function);
        }
        return true;
    }
}
