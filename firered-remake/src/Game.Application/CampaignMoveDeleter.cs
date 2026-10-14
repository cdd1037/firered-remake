using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignMoveDeleterStage { FieldScript, Party, Moves }
public sealed record CampaignMoveDeleter(CampaignMoveDeleterStage Stage, int PartyIndex = -1,
    long? InstanceId = null, int MoveSlot = -1, MoveId? Move = null, bool Forgotten = false);

/// <summary>The original field script owns all questions, refusals, retry destinations and
/// the successful fanfare. Only its party and summary callbacks suspend for these menus.</summary>
public sealed partial class CampaignSession
{
    private const string DeleterMap = "map.fuchsia-city-house-3";
    private const string DeleterPath = "data/maps/FuchsiaCity_House3/scripts.inc";
    private const string DeleterEntry = "FuchsiaCity_House3_EventScript_MoveDeleter";
    private const string DeleterChoose = "FuchsiaCity_House3_EventScript_ChooseMonForMoveDeleter";
    private const string DeleterForget = "FuchsiaCity_House3_EventScript_ForgetMove";
    private const string DeleterOnlyMove = "FuchsiaCity_House3_EventScript_CantForgetOnlyMove";

    private bool InMoveDeleterScript(CampaignState state)
    {
        if (_content.MoveDeleter is not { } catalog || !ReferenceEquals(catalog.Pokemon, _content.PokemonContent?.Catalog) ||
            state.World.Player.MapId.Value != DeleterMap || state.LastTalked is not { } actor ||
            !state.Actors.TryGetValue(actor, out var npc) || !npc.Active || npc.Invisible ||
            npc.SourceLocalId != 1 || npc.GraphicsId != "OBJ_EVENT_GFX_BALDING_MAN" || npc.ScriptId != DeleterEntry ||
            npc.TemplateX != 4 || npc.TemplateY != 4 || npc.Elevation != 3 || !state.Script.Locked || state.Script.Completed) return false;
        return _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == actor) is
            { SourceLocalId: 1, IsClone: false, GraphicsId: "OBJ_EVENT_GFX_BALDING_MAN", ScriptId: DeleterEntry, X: 4, Y: 4, Elevation: 3 };
    }

    private bool AtMoveDeleterWait(CampaignState state, bool party)
    {
        if (!InMoveDeleterScript(state) || state.Script.Wait?.Kind != StoryWaitKind.PartySelection || state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        if (program is null || index < 0 || index >= program.Instructions.Length) return false;
        var instruction = program.Instructions[index];
        if (instruction.Source.Path != DeleterPath || instruction.Source.Symbol != DeleterChoose) return false;
        // The summary callback has no waitstate in the original script. It resumes at
        // fadescreen FADE_FROM_BLACK; the party callback resumes after its waitstate.
        if (!party) return instruction.Op == StoryOp.Special && instruction.Opcode == "special" &&
            instruction.Args.SequenceEqual(new[] { "SelectMoveDeleterMove" });
        return instruction.Op == StoryOp.WaitState && instruction.Opcode == "waitstate" && instruction.Args.IsEmpty && index > 0 &&
            program.Instructions[index - 1] is { Op: StoryOp.Special, Opcode: "special" } previous &&
            previous.Source.Path == DeleterPath && previous.Source.Symbol == DeleterChoose && previous.Args.SequenceEqual(new[] { "ChoosePartyMon" });
    }

    private static string? DeleterSelection(CampaignState state, int index, long? instanceId) =>
        MoveTutorRules.ValidateSelection(state.Party, index, instanceId)?.Replace("move_tutor_", "move_deleter_", StringComparison.Ordinal);

    private bool TryDecideMoveDeleter(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        decision = Reject("move_deleter_not_pending");
        if (state.MoveDeleter is not { } scene) return command.Kind.StartsWith("move_deleter_", StringComparison.Ordinal);
        if (!InMoveDeleterScript(state)) { decision = Reject("move_deleter_callback_context"); return true; }
        if (scene.Stage == CampaignMoveDeleterStage.FieldScript)
        {
            if (command.Kind == "ack") return false;
            decision = Reject("move_deleter_event_active"); return true;
        }
        bool party = scene.Stage == CampaignMoveDeleterStage.Party;
        if (!AtMoveDeleterWait(state, party)) { decision = Reject("move_deleter_callback_context"); return true; }
        CampaignState next;
        if (party)
        {
            if (command.Kind == "move_deleter_cancel") next = state with
            {
                MoveDeleter = new(CampaignMoveDeleterStage.FieldScript),
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", 6) }
            };
            else
            {
                if (command.Kind != "move_deleter_choose") { decision = Reject("move_deleter_selection_required"); return true; }
                var error = DeleterSelection(state, command.PartyIndex, command.TargetInstanceId);
                if (error is not null) { decision = Reject(error); return true; }
                var mon = state.Party[command.PartyIndex];
                next = state with { MoveDeleter = new(CampaignMoveDeleterStage.FieldScript, command.PartyIndex, mon.InstanceId),
                    Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", command.PartyIndex) } };
            }
        }
        else
        {
            var error = DeleterSelection(state, scene.PartyIndex, scene.InstanceId);
            if (error is not null) { decision = Reject(error); return true; }
            var mon = state.Party[scene.PartyIndex]; int slot;
            if (command.Kind == "move_deleter_cancel") slot = 4;
            else
            {
                if (command.Kind != "move_deleter_move") { decision = Reject("move_deleter_move_required"); return true; }
                if (command.TargetInstanceId != scene.InstanceId) { decision = Reject("move_deleter_target_mismatch"); return true; }
                if (command.MoveSlot < 0 || command.MoveSlot >= mon.Pokemon.Moves.Length) { decision = Reject("move_deleter_invalid_slot"); return true; }
                slot = command.MoveSlot;
            }
            next = state with { MoveDeleter = scene with { Stage = CampaignMoveDeleterStage.FieldScript, MoveSlot = slot,
                    Move = slot == 4 ? null : mon.Pokemon.Moves[slot].Move },
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8005", slot) } };
        }
        next = next with { Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false };
        decision = new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }

    private PartyPokemon DeleterSelected(CampaignState state)
    {
        if (state.MoveDeleter is not { Stage: CampaignMoveDeleterStage.FieldScript, Forgotten: false } selected ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != selected.PartyIndex ||
            DeleterSelection(state, selected.PartyIndex, selected.InstanceId) is not null)
            throw new InvalidDataException("move_deleter_target_context");
        return state.Party[selected.PartyIndex];
    }

    private bool TryApplyMoveDeleterEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (_content.MoveDeleter is null || effect.Source.Path != DeleterPath) return false;
        if (effect.Opcode == "waitstate")
        {
            if (!InMoveDeleterScript(state) || effect.Source.Symbol != DeleterChoose || !effect.Args.IsEmpty ||
                state.MoveDeleter?.Stage != CampaignMoveDeleterStage.Party || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("move_deleter_wait_context");
            next = state with { Dialogue = null, SignMessage = null, PresentationTicks = 0,
                Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
            return true;
        }
        if (effect.Opcode != "special" || effect.Args.Length != 1 || effect.Args[0] is not
            ("ChoosePartyMon" or "IsSelectedMonEgg" or "GetNumMovesSelectedMonHas" or "SelectMoveDeleterMove" or "BufferMoveDeleterNicknameAndMove" or "MoveDeleterForgetMove")) return false;
        if (!InMoveDeleterScript(state)) throw new InvalidDataException("move_deleter_source_context");
        string function = effect.Args[0];
        if (function == "ChoosePartyMon")
        {
            if (effect.Source.Symbol != DeleterChoose || state.Party.IsDefaultOrEmpty || state.MoveDeleter?.Forgotten == true)
                throw new InvalidDataException("move_deleter_party_context");
            next = state with { MoveDeleter = new(CampaignMoveDeleterStage.Party) }; return true;
        }
        var mon = DeleterSelected(state); var selected = state.MoveDeleter!;
        if (function is "IsSelectedMonEgg" or "GetNumMovesSelectedMonHas" or "SelectMoveDeleterMove")
        {
            if (effect.Source.Symbol != DeleterChoose) throw new InvalidDataException("move_deleter_selection_context");
            if (function != "SelectMoveDeleterMove")
                next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT",
                    function == "IsSelectedMonEgg" ? mon.IsEgg ? 1 : 0 : mon.Pokemon.Moves.Length) } };
            else
            {
                var error = MoveDeleterRules.ValidateSelection(_content.MoveDeleter, mon);
                if (error is not null) throw new InvalidDataException(error);
                if (state.Script.Wait?.Kind != StoryWaitKind.NativeEffect) throw new InvalidDataException("move_deleter_summary_context");
                next = state with { MoveDeleter = selected with { Stage = CampaignMoveDeleterStage.Moves },
                    Dialogue = null, ScreenIsBlack = false, PresentationTicks = 0,
                    Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
            }
            return true;
        }
        bool onlyMove = effect.Source.Symbol == DeleterOnlyMove && mon.Pokemon.Moves.Length == 1 && !mon.IsEgg;
        bool confirmed = selected.MoveSlot >= 0 && selected.MoveSlot < mon.Pokemon.Moves.Length &&
            state.Script.Variables.GetValueOrDefault("VAR_0x8005", -1) == selected.MoveSlot && mon.Pokemon.Moves[selected.MoveSlot].Move == selected.Move;
        if (function == "BufferMoveDeleterNicknameAndMove")
        {
            if (!onlyMove && (effect.Source.Symbol != DeleterChoose || !confirmed)) throw new InvalidDataException("move_deleter_buffer_context");
            // The one-move refusal only displays STR_VAR_1; the original uninitialized
            // move variable must not be used to index the compact managed move array.
            next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname)
                .SetItem("STR_VAR_2", confirmed ? BattleContent.MoveNames[selected.Move!.Value.Value] : "") };
            return true;
        }
        if (effect.Source.Symbol != DeleterForget || !confirmed || state.Script.Variables.GetValueOrDefault("VAR_RESULT", -1) != 1)
            throw new InvalidDataException("move_deleter_forget_context");
        var result = MoveDeleterRules.Forget(_content.MoveDeleter, _content.PokemonContent!.Catalog, mon, selected.MoveSlot);
        if (!result.Succeeded) throw new InvalidDataException(result.Error);
        next = state with { Party = state.Party.SetItem(selected.PartyIndex, result.Pokemon), MoveDeleter = selected with { Forgotten = true } };
        return true;
    }
}
