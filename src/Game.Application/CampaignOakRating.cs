using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string OakRatingSource = "data/scripts/pokedex_rating.inc";
    private const string OakLabSource = "data/maps/PalletTown_ProfessorOaksLab/scripts.inc";
    private const string OakLabEntry = "PalletTown_ProfessorOaksLab_EventScript_ProfOak";
    private const string OakCountEntry = "PalletTown_ProfessorOaksLab_EventScript_RatePokedexOrTryGiveBalls";
    private const string OakBallsEntry = "PalletTown_ProfessorOaksLab_EventScript_CheckIfPlayerNeedsBalls";
    private const string OakRatingEntry = "PokedexRating_EventScript_Rate";
    private const string OakRatingMessage = "PokedexRating_EventScript_ShowRatingMsg";
    private const string OakPcFlag = "FLAG_OAKS_RATING_IS_VIA_PC";

    private string? ValidateOakRatingState(CampaignState state)
    {
        if (state.OakRatingHelpContextBackup is not null) return "save_oak_rating_context_active";
        if (!_content.OakRatingEnabled) return null;
        if (state.Script.Flags.GetValueOrDefault(OakPcFlag)) return "save_oak_rating_pc_flag_active";
        if (OakLaterProgress(state)&&!PokemonLeagueCompletedContinue(state)) return "save_oak_rating_later_progress_unsupported";
        // Frozen Oak-only saves cannot acquire the Route22 rival outcome. The explicit
        // Route22 profile validates that outcome against its recorded trainer transaction.
        if ((!_content.Route22Enabled &&
             (state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE22") != (state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET") ? 1 : 0) ||
              state.Script.Flags.GetValueOrDefault("FLAG_GOT_POKEBALLS_FROM_OAK_AFTER_22_RIVAL") ||
              state.Script.Flags.GetValueOrDefault("FLAG_OAK_SKIP_22_RIVAL_CHECK"))) ||
            (!_content.BillEnabled&&state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CERULEAN_CITY_RIVAL") != 0) ||
            state.World.Player.MapId.Value == "map.professor-oaks-lab" && state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2"))
            return "save_oak_rating_later_progress_unsupported";

        try { _ = _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies); }
        catch (InvalidDataException) { return "save_pokedex_invalid"; }
        return null;
    }

    // This profile cannot acquire National mode or run the post-game celebration.
    // A flag alone is never treated as the original three-part National gate.
    private static bool OakLaterProgress(CampaignState state) =>
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX") ||
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR") ||
        state.Script.Flags.GetValueOrDefault("FLAG_OAK_SAW_DEX_COMPLETION") ||
        state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX") != 0;

    private void PreflightOakInteraction(CampaignState state, CampaignActor actor)
    {
        if (!_content.OakRatingEnabled || actor.ScriptId != OakLabEntry) return;
        if (!_content.NationalDexEnabled && OakLaterProgress(state) || state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2") ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB") > (_content.NationalDexEnabled ? 9 : 6) ||
            (!_content.BillEnabled&&state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CERULEAN_CITY_RIVAL") != 0))
            throw new InvalidDataException("oak_rating_later_progress_unsupported");
    }

    private CampaignOakRatingProjection OakProjection(CampaignState state)
    {
        if (!_content.OakRatingEnabled || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))
            throw new InvalidDataException("oak_rating_pokedex_required");
        if (!_content.NationalDexEnabled && OakLaterProgress(state)) throw new InvalidDataException("oak_rating_later_progress_unsupported");
        if (state.Script.QuestLogState != 0) throw new InvalidDataException("oak_rating_quest_log_unsupported");
        bool pc = state.Script.Flags.GetValueOrDefault(OakPcFlag);
        if (pc ? !state.PcPoweredOn || !IsSupportedHealingCenter(state.World.Player.MapId) :
            state.World.Player.MapId.Value != "map.professor-oaks-lab" || state.LastTalked != "LOCALID_OAKS_LAB_PROF_OAK" ||
            (state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB") != 6 &&
             !(_content.NationalDexEnabled && state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB") is 7 or 8 or 9)))
            throw new InvalidDataException("oak_rating_context_invalid");
        return _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies);
    }

    private bool TryApplyOakRatingEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool ratingSource = effect.Source.Path == OakRatingSource;
        bool countSource = effect.Source.Path == OakLabSource && effect.Source.Symbol == OakCountEntry;
        bool ballsSource = effect.Source.Path == OakLabSource && effect.Source.Symbol == OakBallsEntry;
        bool ownedEffect = effect.Opcode is "buffernumberstring" or "waitbuttonpress" or "checkitem" ||
            effect.Opcode == "specialvar" && effect.Args[1] is "GetPokedexCount" or "HasAllMons" ||
            effect.Opcode == "special" && effect.Args[0] is "GetProfOaksRatingMessage" or "BackupHelpContext" or "RestoreHelpContext" ||
            effect.Opcode == "special" && (ratingSource || ballsSource) && effect.Args[0] is "QuestLog_CutRecording" or "Script_SetHelpContext";
        if (!ownedEffect) return false;
        if (!_content.OakRatingEnabled) throw new InvalidDataException("oak_rating_not_enabled");
        if (!ratingSource && !countSource && !ballsSource) throw new InvalidDataException("oak_rating_source_unsupported");
        var projection = OakProjection(state);
        int Value(string token) => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number :
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(token[2..], 16) :
            _content.TryConstant(token, out number) ? number : throw new InvalidDataException("oak_rating_operand_invalid");
        CampaignState Result(int result) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result) } };
        switch (effect.Opcode)
        {
            case "buffernumberstring":
                if ((!countSource && effect.Source.Symbol != OakRatingEntry) || effect.Args[0] is not ("STR_VAR_1" or "STR_VAR_2"))
                    throw new InvalidDataException("oak_rating_buffer_context_invalid");
                int number = Value(effect.Args[1]);
                if (number is < 0 or > ushort.MaxValue) throw new InvalidDataException("oak_rating_number_out_of_range");
                next = state with { TextBuffers = state.TextBuffers.SetItem(effect.Args[0], number.ToString(CultureInfo.InvariantCulture)) };
                return true;
            case "checkitem":
                if (!ballsSource || effect.Args[0] != QuestItemRules.PokeBallSymbol || effect.Args.Length != 1)
                    throw new InvalidDataException("oak_ball_check_source_unsupported");
                var error = ValidateCampaignInventory(state.Inventory);
                if (error is not null) throw new InvalidDataException(error);
                next = Result(QuestQuantity(_content.QuestItems!, state.Inventory!, QuestItemRules.PokeBallSymbol) >= 1 ? 1 : 0);
                return true;
            case "waitbuttonpress":
                if (effect.Source.Symbol != OakRatingMessage || state.Dialogue?.TextSymbol != projection.RatingTextSymbol ||
                    state.Script.Wait?.Kind != StoryWaitKind.ButtonPress || state.OakRatingHelpContextBackup is null)
                    throw new InvalidDataException("oak_rating_button_context_invalid");
                return true;
            case "specialvar":
                if (effect.Args[0] != "VAR_RESULT" || (!countSource && effect.Source.Symbol != OakRatingEntry))
                    throw new InvalidDataException("oak_rating_national_or_special_unsupported");
                if (effect.Args[1] == "HasAllMons" && _content.NationalDexEnabled && NationalDexEnabledIn(state))
                {
                    next = Result(_content.EvaluateNationalDex(state.SeenSpecies, state.CaughtSpecies).Complete ? 1 : 0);
                    return true;
                }
                int selector = state.Script.Variables.GetValueOrDefault("VAR_0x8004");
                if (effect.Args[1] != "GetPokedexCount" || selector is < 0 or > 1 ||
                    selector == 1 && (!_content.NationalDexEnabled || !NationalDexEnabledIn(state)))
                    throw new InvalidDataException("oak_rating_national_or_special_unsupported");
                var national = selector == 1 ? _content.EvaluateNationalDex(state.SeenSpecies, state.CaughtSpecies) : null;
                next = Result(NationalDexEnabledIn(state) ? 1 : 0);
                next = next with { Script = next.Script with { Variables = next.Script.Variables
                    .SetItem("VAR_0x8005", national?.Seen ?? projection.Seen).SetItem("VAR_0x8006", national?.Caught ?? projection.Caught) } };
                return true;
            case "special":
                switch (effect.Args[0])
                {
                    case "QuestLog_CutRecording":
                        if (!ballsSource && effect.Source.Symbol != "PokedexRating_EventScript_RateInPerson")
                            throw new InvalidDataException("oak_rating_quest_log_source_unsupported");
                        // Validated QL_STATE_INACTIVE has no live recorder or playback data.
                        // No transition from recording/playback is simulated in this profile.
                        return true;
                    case "BackupHelpContext":
                        if (effect.Source.Symbol != OakRatingEntry || state.OakRatingHelpContextBackup is not null ||
                            state.HelpContext != (state.Script.Flags.GetValueOrDefault(OakPcFlag) ? "HELPCONTEXT_PC" : "map"))
                            throw new InvalidDataException("oak_rating_help_backup_invalid");
                        next = state with { OakRatingHelpContextBackup = state.HelpContext }; return true;
                    case "Script_SetHelpContext":
                        if (effect.Source.Symbol != OakRatingEntry || state.OakRatingHelpContextBackup is null ||
                            !_content.TryConstant("HELPCONTEXT_PC_MISC", out int context) || state.Script.Variables.GetValueOrDefault("VAR_0x8004") != context)
                            throw new InvalidDataException("oak_rating_help_context_invalid");
                        next = state with { HelpContext = "HELPCONTEXT_PC_MISC" }; return true;
                    case "RestoreHelpContext":
                        if (effect.Source.Symbol != "PokedexRating_EventScript_EndRating" || state.OakRatingHelpContextBackup is not { } previous ||
                            previous != (state.Script.Flags.GetValueOrDefault(OakPcFlag) ? "HELPCONTEXT_PC" : "map") || state.HelpContext != "HELPCONTEXT_PC_MISC")
                            throw new InvalidDataException("oak_rating_help_restore_invalid");
                        next = state with { HelpContext = previous, OakRatingHelpContextBackup = null }; return true;
                    case "GetProfOaksRatingMessage":
                        if (effect.Source.Symbol != OakRatingMessage || state.OakRatingHelpContextBackup is null ||
                            state.Script.Variables.GetValueOrDefault("VAR_0x8004") != projection.Caught ||
                            state.Script.Variables.GetValueOrDefault("VAR_0x8009") != projection.Caught)
                            throw new InvalidDataException("oak_rating_message_context_invalid");
                        next = Result(projection.KantoComplete ? 1 : 0);
                        next = next with { Dialogue = new(projection.RatingTextSymbol, _content.TextPages(projection.RatingTextSymbol), 0, false, false) };
                        return true;
                }
                break;
        }
        throw new InvalidDataException("oak_rating_effect_unsupported");
    }

    private Decision<CampaignState> DecideOakRatingButton(CampaignState state, CampaignCommand command)
    {
        if (!_content.OakRatingEnabled || state.Script.Wait is not { Kind: StoryWaitKind.ButtonPress } wait)
            return new(CommandStatus.Rejected, state, "button_press_not_pending");
        if (command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "button_press_requires_a_or_b");
        _ = OakProjection(state);
        if (state.OakRatingHelpContextBackup is null || state.Script.Stack.IsEmpty || state.Script.Stack[^1].ProgramId != OakRatingMessage)
            return new(CommandStatus.Fault, state, "oak_rating_button_context_invalid");
        var next = state with { PresentationEffects = [], PresentationTicks = 0, Motions = [] };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content))));
    }
}
