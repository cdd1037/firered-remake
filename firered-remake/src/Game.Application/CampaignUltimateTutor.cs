using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignUltimateTutor(int TutorIndex, int PartyIndex, long InstanceId, bool Learned = false);

/// <summary>The original Cape Brink specials select the lead and set the per-move flag.
/// Teaching uses the ordinary tutor callback; the original script owns both final dialogue flags.</summary>
public sealed partial class CampaignSession
{
    private const string UltimateTutorMap = "map.two-island-cape-brink-house";
    private const string UltimateTutorEntry = "TwoIsland_CapeBrink_House_EventScript_StarterTutor";
    private const string UltimateTutorChoose = "CapeBrinkTutor_EventScript_ChooseMon";
    private const string UltimateTutorTaught = "CapeBrinkTutor_EventScript_TaughtMove";
    private static readonly FieldActorDefinition UltimateTutorActor = new("LOCALID_CAPE_BRINK_TUTOR", 1,
        "OBJ_EVENT_GFX_CHANNELER", 4, 4, 3, "MOVEMENT_TYPE_FACE_DOWN", UltimateTutorEntry, "0", false, "TRAINER_TYPE_NONE", 0);

    private bool InUltimateTutorScript(CampaignState state, bool requireLocked = true)
    {
        if (_content.MoveTutors?.CapeBrink is not { } cape || !ReferenceEquals(cape.Pokemon, _content.PokemonContent?.Catalog) ||
            state.World.Player.MapId.Value != UltimateTutorMap || state.LastTalked != UltimateTutorActor.LocalId ||
            !state.Actors.TryGetValue(UltimateTutorActor.LocalId, out var npc) || !npc.Active || npc.Invisible ||
            state.Script.Completed || requireLocked && !state.Script.Locked || state.Script.Stack.IsEmpty) return false;
        var map = _content.MapDefinition(state.World.Player.MapId);
        var expected = UltimateTutorActor;
        return map.SourceMapSymbol == "MAP_TWO_ISLAND_CAPE_BRINK_HOUSE" && map.Actors.SingleOrDefault(a => a.LocalId == expected.LocalId) == expected &&
            npc.LocalId == expected.LocalId && npc.SourceLocalId == expected.SourceLocalId && npc.GraphicsId == expected.GraphicsId &&
            npc.ScriptId == expected.ScriptId && npc.TemplateX == expected.X && npc.TemplateY == expected.Y &&
            npc.Elevation == expected.Elevation && npc.MovementType == expected.MovementType && npc.HideFlag == expected.HideFlag &&
            state.Script.Stack.Any(f => f.ProgramId is UltimateTutorEntry or UltimateTutorChoose or UltimateTutorTaught);
    }

    private bool AtUltimateTutorNative(CampaignState state, StoryEffect effect, string symbol)
    {
        if (effect.Source.Path != TutorPath || effect.Source.Symbol != symbol || state.Script.Wait?.Kind != StoryWaitKind.NativeEffect ||
            state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        return frame.ProgramId == symbol && program is not null && index >= 0 && index < program.Instructions.Length &&
            program.Instructions[index] is { } instruction && instruction.Opcode == effect.Opcode &&
            instruction.Source == effect.Source && instruction.Args.SequenceEqual(effect.Args);
    }

    private string? UltimateTutorTargetError(CampaignState state)
    {
        if (state.UltimateTutor is not { } selected || _content.MoveTutors?.CapeBrink is not { } cape) return "ultimate_tutor_not_pending";
        string? error = MoveTutorRules.ValidateSelection(state.Party, selected.PartyIndex, selected.InstanceId);
        if (error is not null) return error;
        var mon = state.Party[selected.PartyIndex];
        var tutor = cape.Definitions.SingleOrDefault(t => t.Index == selected.TutorIndex);
        if (CapeBrinkTutorRules.LeadIndex(state.Party) != selected.PartyIndex) return "ultimate_tutor_lead_changed";
        if (tutor is null || mon.IsEgg || mon.Pokemon.Species != tutor.Species || mon.Pokemon.Friendship != 255)
            return "ultimate_tutor_ineligible_lead";
        if (state.Script.Variables.GetValueOrDefault("VAR_0x8007", -1) != selected.PartyIndex ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8005", -1) != selected.TutorIndex)
            return "ultimate_tutor_selection_changed";
        if (!selected.Learned && state.Script.Flags.GetValueOrDefault(tutor.Flag)) return "ultimate_tutor_already_taught";
        return null;
    }

    private bool TryUltimateTutorContext(CampaignState state, out MoveTutorDefinition tutor)
    {
        tutor = null!;
        return InUltimateTutorScript(state) && UltimateTutorTargetError(state) is null &&
            state.Script.Stack.Length == 2 && state.Script.Stack[0].ProgramId == UltimateTutorChoose &&
            state.Script.Stack[1].ProgramId == TutorCallback &&
            state.Script.Variables.GetValueOrDefault("VAR_0x8009", -1) == state.UltimateTutor!.TutorIndex &&
            (state.MoveTutor is null || state.MoveTutor.PartyIndex == state.UltimateTutor.PartyIndex &&
                state.MoveTutor.InstanceId == state.UltimateTutor.InstanceId) &&
            _content.MoveTutors!.Definitions.TryGetValue(state.UltimateTutor.TutorIndex, out tutor!);
    }

    private bool TryDecideUltimateTutor(CampaignState state, out Decision<CampaignState> decision)
    {
        decision = new(CommandStatus.Rejected, state, "ultimate_tutor_callback_context");
        if (state.UltimateTutor is null) return false;
        // Once the callback finishes, the source may jump to its final message label.
        if (state.Script.Completed || !state.Script.Locked || state.World.Player.MapId.Value != UltimateTutorMap) return true;
        string? error = UltimateTutorTargetError(state);
        if (error is null) return false;
        decision = new(CommandStatus.Rejected, state, error); return true;
    }

    private bool TryApplyUltimateTutorEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode == "applymovement" && effect.Source.Path == TutorPath &&
            effect.Source.Symbol.StartsWith("CapeBrinkTutor_EventScript_JumpInPlace", StringComparison.Ordinal))
        {
            var jump = effect.Source.Symbol switch
            {
                "CapeBrinkTutor_EventScript_JumpInPlaceDown" => ("Movement_JumpInPlaceDown", "jump_in_place_down", Direction.South),
                "CapeBrinkTutor_EventScript_JumpInPlaceUp" => ("Movement_JumpInPlaceUp", "jump_in_place_up", Direction.North),
                "CapeBrinkTutor_EventScript_JumpInPlaceLeft" => ("Movement_JumpInPlaceLeft", "jump_in_place_left", Direction.West),
                "CapeBrinkTutor_EventScript_JumpInPlaceRight" => ("Movement_JumpInPlaceRight", "jump_in_place_right", Direction.East),
                _ => throw new InvalidDataException("ultimate_tutor_jump_symbol")
            };
            if (!InUltimateTutorScript(state) || !AtUltimateTutorNative(state, effect, effect.Source.Symbol) ||
                state.Script.Stack.Length != 2 || state.Script.Stack[0].ProgramId != UltimateTutorEntry ||
                UltimateTutorTargetError(state) is not null ||
                !effect.Args.SequenceEqual(new[] { UltimateTutorActor.LocalId, jump.Item1 }) ||
                !_content.Movement(jump.Item1).SequenceEqual(new[] { jump.Item2, "step_end" }))
                throw new InvalidDataException("ultimate_tutor_jump_context");
            var actor = state.Actors[UltimateTutorActor.LocalId];
            var position = new TilePosition(state.World.Player.MapId, actor.X, actor.Y, actor.Elevation, actor.Facing);
            next = state with { Actors = state.Actors.SetItem(actor.LocalId, actor with { Facing = jump.Item3 }),
                Motions = state.Motions.Add(new(actor.LocalId, [position, position with { Facing = jump.Item3 }], [jump.Item2, "step_end"])) };
            return true;
        }
        bool cut = effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" }) &&
            effect.Source.Path == TutorPath && effect.Source.Symbol == UltimateTutorEntry;
        bool buffer = effect.Opcode == "bufferleadmonspeciesname";
        bool offer = effect.Opcode == "specialvar" && effect.Args.SequenceEqual(new[] { "VAR_RESULT", "CapeBrinkGetMoveToTeachLeadPokemon" });
        bool complete = effect.Opcode == "specialvar" && effect.Args.SequenceEqual(new[] { "VAR_RESULT", "HasLearnedAllMovesFromCapeBrinkTutor" });
        if (!cut && !buffer && !offer && !complete) return false;
        if (!InUltimateTutorScript(state, !cut) || state.Script.Stack.Length != 1 ||
            !AtUltimateTutorNative(state, effect, complete ? UltimateTutorTaught : UltimateTutorEntry))
            throw new InvalidDataException("ultimate_tutor_native_context");
        if (cut)
        {
            if (state.Script.QuestLogState != 0 || state.UltimateTutor is not null) throw new InvalidDataException("ultimate_tutor_quest_log_context");
            return true;
        }
        var cape = _content.MoveTutors!.CapeBrink!;
        if (buffer)
        {
            if (!effect.Args.SequenceEqual(new[] { "STR_VAR_1" })) throw new InvalidDataException("ultimate_tutor_species_buffer");
            var mon = state.Party[CapeBrinkTutorRules.LeadIndex(state.Party)];
            next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", _content.PokemonContent!.SpeciesNames[mon.Pokemon.SourceSpeciesId]) };
            return true;
        }
        var flags = state.Script.Flags.Where(f => f.Value).Select(f => f.Key).ToImmutableHashSet(StringComparer.Ordinal);
        if (offer)
        {
            if (state.UltimateTutor is not null || state.MoveTutor is not null) throw new InvalidDataException("ultimate_tutor_offer_pending");
            var result = CapeBrinkTutorRules.Offer(cape, state.Party, flags);
            var variables = state.Script.Variables.SetItem("VAR_0x8007", result.LeadIndex).SetItem("VAR_RESULT", result.Allowed ? 1 : 0);
            var buffers = state.TextBuffers;
            if (result.Tutor is { } tutor)
            {
                variables = variables.SetItem("VAR_0x8005", tutor.Index);
                buffers = buffers.SetItem("STR_VAR_2", BattleContent.MoveNames[tutor.Move.Value]);
            }
            if (result.Allowed) variables = variables.SetItem("VAR_0x8006", result.MovesKnown);
            next = state with { UltimateTutor = result.Allowed ? new(result.Tutor!.Index, result.LeadIndex, result.InstanceId) : null,
                TextBuffers = buffers, Script = state.Script with { Variables = variables } };
            return true;
        }
        if (state.UltimateTutor is not { Learned: true } learned || state.MoveTutor is not null ||
            UltimateTutorTargetError(state) is not null || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1 ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8009", -1) != learned.TutorIndex ||
            !state.Party[learned.PartyIndex].Pokemon.Moves.Any(m => m.Move == _content.MoveTutors.Definitions[learned.TutorIndex].Move))
            throw new InvalidDataException("ultimate_tutor_success_context");
        var taught = CapeBrinkTutorRules.FlagsAfterSuccess(cape, flags, learned.TutorIndex);
        var sourceFlag = cape.Definitions.Single(t => t.Index == learned.TutorIndex).Flag;
        next = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(sourceFlag, true),
            Variables = state.Script.Variables.SetItem("VAR_RESULT", CapeBrinkTutorRules.AllMovesTaught(cape, taught) ? 1 : 0) } };
        return true;
    }
}
