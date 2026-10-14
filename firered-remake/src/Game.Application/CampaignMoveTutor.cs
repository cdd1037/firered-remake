using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignMoveTutorStage { Party, Notice, ReplaceQuestion, ChooseMove, StopQuestion, Forgotten, Learned }
public sealed record CampaignMoveTutor(int TutorIndex, CampaignMoveTutorStage Stage, int PartyIndex = -1,
    long? InstanceId = null, int MoveSlot = -1, string? TextSymbol = null,
    int Page = 0, CampaignMoveTutorStage? NoticeReturn = null)
{
    public ImmutableArray<string> Pages { get; init; } = [];
}

/// <summary>The native callback owns selection and learning; the source field script alone
/// owns its dialogue, VAR_RESULT branch and once-only flag.</summary>
public sealed partial class CampaignSession
{
    private const string TutorPath = "data/scripts/move_tutors.inc";
    private const string TutorCallback = "EventScript_ChooseMoveTutorMon";

    private bool TryMoveTutorContext(CampaignState state, out MoveTutorDefinition tutor)
    {
        if (state.UltimateTutor is not null) return TryUltimateTutorContext(state, out tutor);
        tutor = null!;
        if (_content.MoveTutors is not { } catalog || !ReferenceEquals(catalog.Pokemon, _content.PokemonContent?.Catalog) ||
            state.LastTalked is not { } actor || !state.Actors.TryGetValue(actor, out var npc) ||
            !npc.Active || npc.Invisible || !state.Script.Locked || state.Script.Completed) return false;
        var map = _content.MapDefinition(state.World.Player.MapId);
        var definition = map.Actors.FirstOrDefault(a => a.LocalId == actor);
        foreach (var binding in TutorBindings)
        {
            var expected = binding.Actor;
            if (binding.Map != map.Id.Value || binding.SourceMap != map.SourceMapSymbol || definition != expected ||
                npc.LocalId != expected.LocalId || npc.SourceLocalId != expected.SourceLocalId || npc.GraphicsId != expected.GraphicsId ||
                npc.ScriptId != expected.ScriptId || npc.TemplateX != expected.X || npc.TemplateY != expected.Y ||
                npc.Elevation != expected.Elevation || npc.MovementType != expected.MovementType || npc.HideFlag != expected.HideFlag ||
                state.Script.Flags.GetValueOrDefault(binding.Flag) || !state.Script.Stack.Any(f => f.ProgramId == binding.Entry)) continue;
            return catalog.Definitions.TryGetValue(binding.Index, out tutor!);
        }
        return false;
    }

    private bool AtMoveTutorWait(CampaignState state)
    {
        if (!TryMoveTutorContext(state, out var tutor) || state.MoveTutor?.TutorIndex != tutor.Index || state.Script.Wait?.Kind != StoryWaitKind.PartySelection || state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length &&
            program.Instructions[index] is { Op: StoryOp.WaitState, Opcode: "waitstate" } instruction &&
            instruction.Args.IsEmpty && instruction.Source.Path == TutorPath && instruction.Source.Symbol == TutorCallback &&
            state.Script.Variables.GetValueOrDefault("VAR_0x8005", -1) == state.MoveTutor?.TutorIndex;
    }

    private CampaignMoveTutor TutorMessage(CampaignMoveTutor scene, CampaignMoveTutorStage stage, string symbol,
        PartyPokemon? mon = null, string? moveName = null, CampaignMoveTutorStage? noticeReturn = null)
    {
        string raw = _content.MoveTutors!.Texts[symbol]
            .Replace("{STR_VAR_1}", mon?.Nickname ?? "", StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", moveName ?? BattleContent.MoveNames[_content.MoveTutors.Definitions[scene.TutorIndex].Move.Value], StringComparison.Ordinal);
        // Control tokens remain in the authenticated source text. This functional menu uses
        // existing page/ack presentation and emits the source sound/fanfare separately.
        raw = Regex.Replace(raw, @"\{[^}]+\}", "", RegexOptions.CultureInvariant);
        var pages = raw.Split("\\p", StringSplitOptions.None).Select(p => p.Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\l", "\n", StringComparison.Ordinal)).ToImmutableArray();
        return scene with { Stage = stage, TextSymbol = symbol, Pages = pages, Page = 0, NoticeReturn = noticeReturn };
    }

    private bool TryDecideMoveTutor(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        decision = Reject("move_tutor_not_pending");
        if (state.MoveTutor is not { } scene) return command.Kind.StartsWith("move_tutor_", StringComparison.Ordinal);
        if (!AtMoveTutorWait(state)) { decision = Reject("move_tutor_callback_context"); return true; }
        var catalog = _content.MoveTutors!; var tutor = catalog.Definitions[scene.TutorIndex];
        CampaignState Message(CampaignMoveTutorStage stage, string symbol, PartyPokemon? mon = null, CampaignMoveTutorStage? next = null) =>
            state with { MoveTutor = TutorMessage(scene, stage, symbol, mon, noticeReturn: next),
                TextBuffers = mon is null ? state.TextBuffers : state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname)
                    .SetItem("STR_VAR_2", BattleContent.MoveNames[tutor.Move.Value]) };
        CampaignState result;
        if (scene.Stage == CampaignMoveTutorStage.Party)
        {
            if (command.Kind == "move_tutor_cancel") { decision = new(CommandStatus.Accepted, FinishMoveTutor(state, false)); return true; }
            if (command.Kind != "move_tutor_choose") { decision = Reject("move_tutor_selection_required"); return true; }
            string? error = MoveTutorRules.ValidateSelection(state.Party, command.PartyIndex, command.TargetInstanceId);
            if (error is not null) { decision = Reject(error); return true; }
            var mon = state.Party[command.PartyIndex];
            if (mon.IsEgg) { decision = Reject("move_tutor_egg"); return true; }
            result = StartTutorLesson(state, scene with { PartyIndex = command.PartyIndex, InstanceId = mon.InstanceId });
            decision = new(CommandStatus.Accepted, result); return true;
        }
        string? targetError = MoveTutorRules.ValidateSelection(state.Party, scene.PartyIndex, scene.InstanceId);
        if (targetError is not null) { decision = Reject(targetError); return true; }
        var selected = state.Party[scene.PartyIndex];
        if (command.Kind == "move_tutor_ack" && scene.Stage != CampaignMoveTutorStage.ChooseMove)
        {
            if (scene.Page + 1 < scene.Pages.Length) result = state with { MoveTutor = scene with { Page = scene.Page + 1 } };
            else if (scene.Stage == CampaignMoveTutorStage.Notice) result = scene.NoticeReturn is { } after
                ? Message(after, "gText_WhichMoveToForget", selected) : FinishMoveTutor(state, false);
            else if (scene.Stage == CampaignMoveTutorStage.Learned) result = FinishMoveTutor(state, true);
            else if (scene.Stage == CampaignMoveTutorStage.Forgotten) result = LearnTutorMove(state, scene.MoveSlot);
            else { decision = Reject("move_tutor_choice_required"); return true; }
            decision = new(CommandStatus.Accepted, result); return true;
        }
        if (scene.Stage is CampaignMoveTutorStage.ReplaceQuestion or CampaignMoveTutorStage.StopQuestion &&
            scene.Page == scene.Pages.Length - 1 && command.Kind is "move_tutor_choice" or "move_tutor_cancel")
        {
            int choice = command.Kind == "move_tutor_cancel" ? 0 : command.Choice ?? -1; // source YES=1, NO/B=0
            if (choice is not (0 or 1)) { decision = Reject("move_tutor_choice_invalid"); return true; }
            result = scene.Stage == CampaignMoveTutorStage.ReplaceQuestion
                ? choice == 1 ? Message(CampaignMoveTutorStage.ChooseMove, "gText_WhichMoveToForget", selected)
                              : Message(CampaignMoveTutorStage.StopQuestion, "gText_StopLearningMove2", selected)
                : choice == 1 ? Message(CampaignMoveTutorStage.Notice, "gText_MoveNotLearned", selected)
                              : Message(CampaignMoveTutorStage.ReplaceQuestion, "gText_PkmnNeedsToReplaceMove", selected);
            decision = new(CommandStatus.Accepted, result); return true;
        }
        if (scene.Stage == CampaignMoveTutorStage.ChooseMove)
        {
            if (command.Kind == "move_tutor_cancel") result = Message(CampaignMoveTutorStage.StopQuestion, "gText_StopLearningMove2", selected);
            else if (command.Kind == "move_tutor_replace")
            {
                if (command.TargetInstanceId != scene.InstanceId) { decision = Reject("move_tutor_target_mismatch"); return true; }
                if (command.MoveSlot is < 0 or > 3 || selected.Pokemon.Moves.Length != 4) { decision = Reject("move_tutor_invalid_slot"); return true; }
                if (PokemonMoveRules.IsHiddenMachineMove(selected.Pokemon.Moves[command.MoveSlot].Move))
                    result = Message(CampaignMoveTutorStage.Notice, "gText_PokeSum_HmMovesCantBeForgotten", selected, CampaignMoveTutorStage.ChooseMove);
                else
                {
                    scene = scene with { MoveSlot = command.MoveSlot };
                    string forgotten = BattleContent.MoveNames[selected.Pokemon.Moves[command.MoveSlot].Move.Value];
                    result = state with { MoveTutor = TutorMessage(scene, CampaignMoveTutorStage.Forgotten, "gText_12PoofForgotMove", selected, forgotten),
                        TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", selected.Nickname).SetItem("STR_VAR_2", forgotten) };
                    result = TutorSound(result, "playse", "SE_BALL_BOUNCE_1");
                }
            }
            else { decision = Reject("move_tutor_move_required"); return true; }
            decision = new(CommandStatus.Accepted, result); return true;
        }
        decision = Reject("move_tutor_command_not_allowed"); return true;
    }

    private CampaignState StartTutorLesson(CampaignState state, CampaignMoveTutor scene)
    {
        var mon = state.Party[scene.PartyIndex];
        var tutor = _content.MoveTutors!.Definitions[scene.TutorIndex];
        var eligibility = MoveTutorRules.Eligibility(tutor, mon);
        string? symbol = eligibility switch
        {
            MoveTutorEligibility.Incompatible => "gText_PkmnCantLearnMove",
            MoveTutorEligibility.AlreadyKnown => "gText_PkmnAlreadyKnows",
            MoveTutorEligibility.Egg => throw new InvalidDataException("move_tutor_egg"),
            _ when mon.Pokemon.Moves.Length == 4 => "gText_PkmnNeedsToReplaceMove",
            _ => null
        };
        if (symbol is null) return LearnTutorMove(state with { MoveTutor = scene }, null);
        return state with { MoveTutor = TutorMessage(scene,
            eligibility == MoveTutorEligibility.Able ? CampaignMoveTutorStage.ReplaceQuestion : CampaignMoveTutorStage.Notice, symbol, mon),
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname)
                .SetItem("STR_VAR_2", BattleContent.MoveNames[tutor.Move.Value]) };
    }

    private CampaignState LearnTutorMove(CampaignState state, int? slot)
    {
        var scene = state.MoveTutor!; var mon = state.Party[scene.PartyIndex];
        var result = MoveTutorRules.Teach(_content.MoveTutors!, _content.PokemonContent!.Catalog, mon, scene.TutorIndex, slot);
        if (!result.Succeeded) throw new InvalidDataException(result.Error);
        return TutorSound(state with { Party = state.Party.SetItem(scene.PartyIndex, result.Pokemon),
            UltimateTutor = state.UltimateTutor is { } ultimate ? ultimate with { Learned = true } : null,
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname).SetItem("STR_VAR_2", BattleContent.MoveNames[_content.MoveTutors!.Definitions[scene.TutorIndex].Move.Value]),
            MoveTutor = TutorMessage(scene, CampaignMoveTutorStage.Learned, "gText_PkmnLearnedMove3", result.Pokemon) }, "playfanfare", "MUS_LEVEL_UP");
    }

    private static CampaignState TutorSound(CampaignState state, string opcode, string sound)
    {
        long sequence = checked(state.Script.EffectSequence + 1);
        return state with { Script = state.Script with { EffectSequence = sequence },
            PresentationEffects = state.PresentationEffects.Add(new(sequence, opcode, state.World.Player.MapId, [sound])) };
    }

    private CampaignState FinishMoveTutor(CampaignState state, bool learned)
    {
        var wait = state.Script.Wait!;
        var next = state with { MoveTutor = null, Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            ScreenIsBlack = false, Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", learned ? 1 : 0) } };
        return Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content)));
    }

    private bool TryApplyMoveTutorEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "ChooseMonForMoveTutor" }))
        {
            if (!TryMoveTutorContext(state, out var tutor) || state.MoveTutor is not null || effect.Source.Path != TutorPath || effect.Source.Symbol != TutorCallback ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8005", -1) != tutor.Index || state.Party.IsDefaultOrEmpty)
                throw new InvalidDataException("move_tutor_native_context");
            next = state with { MoveTutor = new(tutor.Index, CampaignMoveTutorStage.Party),
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 0) } };
            if (state.UltimateTutor is { } ultimate)
                next = StartTutorLesson(next, new(tutor.Index, CampaignMoveTutorStage.Notice, ultimate.PartyIndex, ultimate.InstanceId));
            return true;
        }
        if (effect.Opcode != "waitstate" || state.MoveTutor is null) return false;
        if (!TryMoveTutorContext(state, out var pendingTutor) || state.MoveTutor.TutorIndex != pendingTutor.Index ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8005", -1) != pendingTutor.Index || !effect.Args.IsEmpty || effect.Source.Path != TutorPath || effect.Source.Symbol != TutorCallback ||
            state.Script.Wait?.Kind != StoryWaitKind.Presentation) throw new InvalidDataException("move_tutor_wait_context");
        next = state with { Dialogue = null, SignMessage = null, PresentationTicks = 0,
            Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
        return true;
    }
}
