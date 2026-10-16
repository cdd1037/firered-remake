using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignDaisyGrooming(bool ChoosingParty, int PartyIndex = -1, long? InstanceId = null, bool Groomed = false);
public enum CampaignDeferredNativeScreen { Diploma }

public sealed partial class CampaignSession
{
    private const string DaisyPath = "data/maps/PalletTown_RivalsHouse/scripts.inc";
    private const string DaisyPrefix = "PalletTown_RivalsHouse_EventScript_";
    private const string MassageCounter = "VAR_MASSAGE_COOLDOWN_STEP_COUNTER";
    private const string DesignerPath = "data/maps/CeladonCity_Condominiums_3F/scripts.inc";
    private const string DesignerPrefix = "CeladonCity_Condominiums_3F_EventScript_";

    // Callback ownership is checked against the loaded source instruction and map actor.
    // No rule or dialogue is selected by a region feature chain.
    private bool InOrdinaryNativeActor(CampaignState state, string path, string entry)
    {
        if (!state.Script.Locked || state.Script.Completed || state.Script.QuestLogState != 0 ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor) || !actor.Visible ||
            actor.ScriptId != entry || state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            return false;
        var definition = _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == id);
        return definition is { IsClone: false } && definition.ScriptId == entry && definition.SourceLocalId == actor.SourceLocalId &&
            definition.GraphicsId == actor.GraphicsId && _content.FindProgram(entry)?.Instructions.Any(i => i.Source.Path == path) == true;
    }

    private bool InDaisyActor(CampaignState state) => InOrdinaryNativeActor(state, DaisyPath, DaisyPrefix + "Daisy") &&
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR");

    private bool DaisyWait(CampaignState state) => InDaisyActor(state) &&
        state.Script.Wait?.Kind == StoryWaitKind.PartySelection &&
        SizeRecordInstruction(state) is { Opcode: "waitstate" } instruction && instruction.Args.IsEmpty &&
        instruction.Source.Path == DaisyPath && instruction.Source.Symbol == DaisyPrefix + "GroomMon";

    private PartyPokemon DaisySelected(CampaignState state)
    {
        if (state.DaisyGrooming is not { ChoosingParty: false, InstanceId: { } id } selected ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != selected.PartyIndex ||
            MoveTutorRules.ValidateSelection(state.Party, selected.PartyIndex, id) is not null)
            throw new InvalidDataException("daisy_selection_context");
        return state.Party[selected.PartyIndex];
    }

    private bool TryDecideDaisyGrooming(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        bool choice = command.Kind is "daisy_choose" or "daisy_cancel";
        decision = new(CommandStatus.Rejected, state, "daisy_not_pending");
        if (state.DaisyGrooming is not { } scene) return choice;
        if (!InDaisyActor(state)) return true;
        if (!scene.ChoosingParty)
        {
            if (command.Kind == "ack") return false;
            decision = new(CommandStatus.Rejected, state, "daisy_event_active"); return true;
        }
        if (!DaisyWait(state)) return true;
        if (!choice) { decision = new(CommandStatus.Rejected, state, "daisy_selection_required"); return true; }
        int index = 6; long? instance = null;
        if (command.Kind == "daisy_choose")
        {
            if (MoveTutorRules.ValidateSelection(state.Party, command.PartyIndex, command.TargetInstanceId) is { } error)
            { decision = new(CommandStatus.Rejected, state, error); return true; }
            index = command.PartyIndex; instance = state.Party[index].InstanceId;
        }
        var next = state with { DaisyGrooming = new(false, index, instance), Dialogue = null, SignMessage = null,
            Motions = [], PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", index) } };
        decision = new(CommandStatus.Accepted, Pump(Advance(next,
            StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }

    private bool TryApplyDaisyAndDesignerEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path is not (DaisyPath or DesignerPath) || effect.Opcode is not ("special" or "specialvar" or "waitstate")) return false;
        var instruction = SizeRecordInstruction(state);
        if (instruction is null || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode ||
            !instruction.Args.SequenceEqual(effect.Args)) throw new InvalidDataException("ordinary_native_instruction_context");
        bool daisy = effect.Source.Path == DaisyPath;
        if (!(daisy ? InDaisyActor(state) : InOrdinaryNativeActor(state, DesignerPath, DesignerPrefix + "Designer")))
            throw new InvalidDataException("ordinary_native_actor_context");
        if (effect.Opcode == "waitstate")
        {
            if (!effect.Args.IsEmpty || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("ordinary_native_wait_context");
            if (daisy && effect.Source.Symbol == DaisyPrefix + "GroomMon" && state.DaisyGrooming is { ChoosingParty: true })
                next = state with { Dialogue = null, Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
            else if (!daisy && effect.Source.Symbol == DesignerPrefix + "ShowDiploma" && state.DeferredNativeScreen == CampaignDeferredNativeScreen.Diploma)
                next = state with { Dialogue = new("service.diploma.deferred", ["The diploma screen is\nnot available yet."], 0, false, true),
                    Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.Dialog } } };
            else throw new InvalidDataException("ordinary_native_wait_owner");
            return true;
        }
        bool returns = effect.Opcode == "specialvar";
        if (effect.Args.Length != (returns ? 2 : 1) || returns && effect.Args[0] != "VAR_RESULT" || state.Script.Wait?.Kind != StoryWaitKind.NativeEffect)
            throw new InvalidDataException("ordinary_native_operands");
        string function = effect.Args[returns ? 1 : 0];
        string expected = function switch
        {
            "GetLeadMonFriendship" => DaisyPrefix + "RateMonFriendship",
            "HasAllKantoMons" => DesignerPrefix + "Designer",
            "ShowDiploma" => DesignerPrefix + "ShowDiploma",
            _ => DaisyPrefix + "GroomMon"
        };
        if (effect.Source.Symbol != expected || returns != (function is "GetLeadMonFriendship" or "GetPartyMonSpecies" or "HasAllKantoMons"))
            throw new InvalidDataException("ordinary_native_function_context");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        switch (function)
        {
            case "QuestLog_CutRecording": return true;
            case "GetLeadMonFriendship":
                next = Result(SourceGiftEggRules.GetLeadMonFriendship(_content.PokemonContent!, state.Party)); return true;
            case "ChoosePartyMon":
                if (state.DaisyGrooming is not null || state.Party.IsDefaultOrEmpty || state.Script.Variables.GetValueOrDefault(MassageCounter) != 500)
                    throw new InvalidDataException("daisy_chooser_context");
                next = state with { DaisyGrooming = new(true) }; return true;
            case "GetPartyMonSpecies": next = Result(NameRaterRules.SpeciesOrEgg(DaisySelected(state))); return true;
            case "DaisyMassageServices":
                var mon = DaisySelected(state);
                if (mon.IsEgg || state.DaisyGrooming!.Groomed || state.Script.Variables.GetValueOrDefault(MassageCounter) != 500)
                    throw new InvalidDataException("daisy_groom_context");
                next = state with { Party = state.Party.SetItem(state.DaisyGrooming.PartyIndex,
                    DaisyGroomingRules.Apply(mon, _content.MapDefinition(state.World.Player.MapId).RegionMapSection)),
                    DaisyGrooming = state.DaisyGrooming with { Groomed = true },
                    Script = state.Script with { Variables = state.Script.Variables.SetItem(MassageCounter, 0) } }; return true;
            case "BufferMonNickname":
                var name = DaisySelected(state).Nickname;
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", name[..Math.Min(10, name.Length)]) }; return true;
            case "HasAllKantoMons": next = Result(KantoPokedexRules.HasAllKantoMons(state.CaughtSpecies) ? 1 : 0); return true;
            case "ShowDiploma":
                if (!KantoPokedexRules.HasAllKantoMons(state.CaughtSpecies) || state.DeferredNativeScreen is not null)
                    throw new InvalidDataException("diploma_completion_context");
                next = state with { DeferredNativeScreen = CampaignDeferredNativeScreen.Diploma }; return true;
            default: throw new InvalidDataException("ordinary_native_unsupported: " + function);
        }
    }
}
