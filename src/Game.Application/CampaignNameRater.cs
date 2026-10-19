using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

/// <summary>Only callback ownership lives here; the original field script owns all dialogue and branches.</summary>
public sealed record CampaignNameRater(bool ChoosingParty, NameRaterTarget? Target);

public sealed partial class CampaignSession
{
    private const string NameRaterMap = "map.lavender-town-house-2";
    private const string NameRaterScriptPath = "data/maps/LavenderTown_House2/scripts.inc";
    private const string NameRaterEntry = "LavenderTown_House2_EventScript_NameRater";
    private const string NameRaterChoose = "LavenderTown_House2_EventScript_ChooseMon";
    private const string NameRaterCheck = "LavenderTown_House2_EventScript_CheckCanRateMon";
    private const string NameRaterRename = "LavenderTown_House2_EventScript_ChooseNewNickname";
    private const string NameRaterSharedPath = "data/event_scripts.s";
    private const string NameRaterShared = "EventScript_ChangePokemonNickname";

    private bool InNameRaterScript(CampaignState state) => _content.NameRaterSource is not null && state.World.Player.MapId.Value == NameRaterMap &&
        state.LastTalked is { } actor && state.Actors.TryGetValue(actor, out var npc) &&
        npc.Active && npc.SourceLocalId == 1 && npc.GraphicsId == "OBJ_EVENT_GFX_GENTLEMAN" &&
        npc.ScriptId == NameRaterEntry && npc.TemplateX == 4 && npc.TemplateY == 4 && npc.Elevation == 3 &&
        state.Script.Locked && !state.Script.Completed;

    private bool NameRaterWait(CampaignState state, StoryWaitKind kind, string path, string symbol)
    {
        if (!InNameRaterScript(state) || state.Script.Wait?.Kind != kind || state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1];
        var program = _content.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length &&
            program.Instructions[index] is { Op: StoryOp.WaitState, Opcode: "waitstate" } instruction &&
            instruction.Args.IsEmpty && instruction.Source.Path == path && instruction.Source.Symbol == symbol;
    }

    private PartyPokemon NameRaterSelected(CampaignState state)
    {
        if (state.NameRater is not { ChoosingParty: false, Target: { } target } ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != target.PartyIndex)
            throw new InvalidDataException("name_rater_target_missing");
        string? error = NameRaterRules.ValidateSelection(state.Party, target.PartyIndex, target.InstanceId);
        if (error is not null) throw new InvalidDataException(error);
        return state.Party[target.PartyIndex];
    }

    private bool TryDecideNameRater(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        decision = new(CommandStatus.Rejected, state, "name_rater_not_pending");
        bool chooserCommand = command.Kind is "name_rater_choose" or "name_rater_cancel";
        if (state.NameRater is null) return chooserCommand;
        if (!InNameRaterScript(state)) return true;
        if (state.NameRater.ChoosingParty || chooserCommand)
        {
            if (!state.NameRater.ChoosingParty || !NameRaterWait(state, StoryWaitKind.PartySelection, NameRaterScriptPath, NameRaterChoose))
            { decision = new(CommandStatus.Rejected, state, "name_rater_chooser_not_pending"); return true; }
            if (!chooserCommand)
            { decision = new(CommandStatus.Rejected, state, "name_rater_selection_required"); return true; }
            NameRaterTarget? target = null;
            if (command.Kind == "name_rater_choose")
            {
                string? error = NameRaterRules.ValidateSelection(state.Party, command.PartyIndex, command.TargetInstanceId);
                if (error is not null) { decision = new(CommandStatus.Rejected, state, error); return true; }
                var mon = state.Party[command.PartyIndex];
                target = new(command.PartyIndex, mon.InstanceId, mon.CustomNickname);
            }
            int result = target?.PartyIndex ?? NameRaterRules.CancelSlot;
            var next = state with { NameRater = new(false, target), Dialogue = null, Motions = [],
                PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false,
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", result) } };
            decision = new(CommandStatus.Accepted, Pump(Advance(next,
                StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
            return true;
        }
        if (command.Kind == "nickname")
        {
            decision = SubmitNameRaterNickname(state, command);
            return true;
        }
        if (command.Kind == "ack") return false;
        decision = new(CommandStatus.Rejected, state, "event_active");
        return true;
    }

    private Decision<CampaignState> SubmitNameRaterNickname(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        if (state.NameRater is not { ChoosingParty: false, Target: { } target } ||
            state.Nickname is not { Submitted: false, BoxAddress: null } naming ||
            !NameRaterWait(state, StoryWaitKind.Nickname, NameRaterSharedPath, NameRaterShared)) return Reject("nickname_not_pending");
        if (command.TargetInstanceId != target.InstanceId) return Reject("name_rater_target_mismatch");
        string originalName = PokemonName(state.Party[target.PartyIndex] with { CustomNickname = target.OriginalCustomNickname });
        if (naming.PartyIndex != target.PartyIndex || naming.OriginalCustomNickname != target.OriginalCustomNickname ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != target.PartyIndex ||
            state.TextBuffers.GetValueOrDefault("STR_VAR_2") != originalName ||
            state.TextBuffers.GetValueOrDefault("STR_VAR_3") != originalName) return Reject("name_rater_nickname_context");
        var content = _content.PokemonContent ?? throw new InvalidDataException("pokemon_content_missing");
        var rename = NameRaterRules.Rename(state.Party, target, state.TrainerId, state.PlayerName, command.Text, content.NicknameCharacters);
        if (!rename.Accepted) return Reject(rename.Error!);
        return new(CommandStatus.Accepted, state with { Party = rename.Party, Nickname = naming with { Submitted = true },
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", PokemonName(rename.Party[target.PartyIndex])), ScreenIsBlack = false, PresentationTicks = 16,
            Script = state.Script with { Wait = state.Script.Wait! with { Kind = StoryWaitKind.Presentation } } });
    }

    private bool TryApplyNameRaterEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var a = effect.Args;
        if (effect.Opcode == "waitstate" && state.NameRater is { ChoosingParty: true })
        {
            if (!InNameRaterScript(state) || !a.IsEmpty || effect.Source.Path != NameRaterScriptPath ||
                effect.Source.Symbol != NameRaterChoose || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("name_rater_chooser_wait_context");
            next = state with { Dialogue = null, Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
            return true;
        }
        if (effect.Opcode is not ("special" or "specialvar")) return false;
        int functionIndex = effect.Opcode == "special" ? 0 : 1;
        if (a.Length <= functionIndex) return false;
        string function = a[functionIndex];
        if (function is not ("ChoosePartyMon" or "GetPartyMonSpecies" or "BufferMonNickname" or "IsMonOTIDNotPlayers" or
            "IsMonOTNameNotPlayers" or "ChangePokemonNickname" or "NameRaterWasNicknameChanged")) return false;
        if (function == "ChangePokemonNickname" && state.NameRater is null && !InNameRaterScript(state)) return false;
        bool shared = function == "ChangePokemonNickname";
        bool returns = function is "GetPartyMonSpecies" or "IsMonOTNameNotPlayers" or "NameRaterWasNicknameChanged";
        string expectedSymbol = function switch
        {
            "ChoosePartyMon" => NameRaterChoose,
            "ChangePokemonNickname" => NameRaterShared,
            "NameRaterWasNicknameChanged" => NameRaterRename,
            _ => NameRaterCheck
        };
        bool bufferAtRename = function == "BufferMonNickname" && effect.Source.Symbol == NameRaterRename;
        if (!InNameRaterScript(state) || effect.Opcode != (returns ? "specialvar" : "special") ||
            a.Length != (returns ? 2 : 1) || returns && a[0] != "VAR_RESULT" ||
            effect.Source.Path != (shared ? NameRaterSharedPath : NameRaterScriptPath) ||
            effect.Source.Symbol != expectedSymbol && !bufferAtRename)
            throw new InvalidDataException("name_rater_native_context");
        CampaignState Result(int result) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result) } };
        switch (function)
        {
            case "ChoosePartyMon":
                if (state.NameRater is not null || state.Nickname is not null || state.Pc is not null || state.GiftDelivery is not null)
                    throw new InvalidDataException("name_rater_chooser_already_active");
                string? shape = NameRaterRules.ValidateSelection(state.Party, 0, state.Party.IsDefaultOrEmpty ? null : state.Party[0].InstanceId);
                if (shape is not null) throw new InvalidDataException(shape);
                next = state with { NameRater = new(true, null) };
                break;
            case "GetPartyMonSpecies": next = Result(NameRaterRules.SpeciesOrEgg(NameRaterSelected(state))); break;
            case "BufferMonNickname":
                var buffered = PokemonName(NameRaterSelected(state));
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", buffered) };
                break;
            case "IsMonOTIDNotPlayers": next = Result(NameRaterRules.TrainerIdDiffers(NameRaterSelected(state), state.TrainerId) ? 1 : 0); break;
            case "IsMonOTNameNotPlayers":
                var named = NameRaterSelected(state);
                next = Result(NameRaterRules.TrainerNameDiffers(named, state.PlayerName) ? 1 : 0) with
                { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", named.OriginalTrainerName) };
                break;
            case "ChangePokemonNickname":
                var mon = NameRaterSelected(state); var target = state.NameRater!.Target!;
                string? error = NameRaterRules.Begin(state.Party, target.PartyIndex, target.InstanceId, state.TrainerId, state.PlayerName, out var current);
                if (error is not null) throw new InvalidDataException(error);
                if (current != target || state.Nickname is not null) throw new InvalidDataException("name_rater_original_name_changed");
                next = state with { Nickname = new(target.PartyIndex, mon.CustomNickname, false),
                    TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", PokemonName(mon)).SetItem("STR_VAR_3", PokemonName(mon)) };
                break;
            case "NameRaterWasNicknameChanged":
                var renamed = NameRaterSelected(state);
                if (state.TextBuffers.GetValueOrDefault("STR_VAR_3") != PokemonName(renamed with { CustomNickname = state.NameRater!.Target!.OriginalCustomNickname }))
                    throw new InvalidDataException("name_rater_backup_changed");
                next = Result(string.Equals(PokemonName(renamed), state.TextBuffers["STR_VAR_3"], StringComparison.Ordinal) ? 0 : 1) with
                { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", PokemonName(renamed)) };
                break;
        }
        return true;
    }
}
