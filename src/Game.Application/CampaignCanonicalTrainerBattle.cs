using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Presentation-only facts. No conversion back into a gameplay state exists.</summary>
public sealed record CampaignTrainerCombatView(WildBattleState Battle, ImmutableArray<WildBattleCombatant> OpponentParty,
    int OpponentIndex, ForestTrainerCombatPhase Phase, int? PendingOpponentIndex);
public static class CampaignTrainerReadProjection
{
    public static CampaignTrainerCombatView From(SourceBattleFrame frame) => new(CampaignWildReadProjection.From(frame),
        frame.Party(BattleSide.Opponent).Members.Select((m, i) => frame.Get(new(1)).Member!.Value.Index == i
            ? SourceBattleReadProjection.Combatant(frame, new(1)) : new WildBattleCombatant(m.Pokemon, HeldItem: m.HeldItem, Status: m.Status)).ToImmutableArray(),
        frame.Get(new(1)).Member!.Value.Index, frame.Outcome != WildBattleOutcome.Ongoing ? ForestTrainerCombatPhase.Ended : frame.FourTurn?.Phase switch {
            SourceFourActionPhase.ShiftPending => ForestTrainerCombatPhase.AwaitingShiftChoice,
            SourceFourActionPhase.RewardsPending => ForestTrainerCombatPhase.AwaitingExperience,
            SourceFourActionPhase.ReplacementsPending when frame.PendingSwitches.Any(p => p.Slot.Value == 0) => ForestTrainerCombatPhase.AwaitingPlayerReplacement,
            _ => ForestTrainerCombatPhase.Ready }, frame.FourTurn?.TrainerReplacement?.Incoming.Index);
}

public sealed partial class CampaignSession
{
    private static ImmutableArray<WildBattleEvent> ProjectTrainerWildEvents(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame? previous = null)
    {
        var result = ImmutableArray.CreateBuilder<WildBattleEvent>();
        foreach (var e in events)
        {
            if (e is SourceRewardAppliedEvent or SourceRewardLearningEvent or SourceOpponentRewardCompletedEvent or
                SourceTrainerStartedEvent or SourceTrainerOpponentSelectedEvent or SourceTrainerShiftOfferedEvent or
                SourceReplacementRequiredEvent { Slot.Value: 1 }) continue;
            result.Add(SourceBattleEventAdapter.Export(e));
            if (e is SourceCalledPartySwitchedEvent { Side.Value: 0, Replacement: { } replacement } switched &&
                switched.Kind != WildBattleCalledSwitchKind.Forced)
            {
                var outgoing = previous is not null ? SourceBattleReadProjection.Combatant(previous, new(0)) : throw new InvalidDataException("trainer_switch_display_previous");
                result.Add(switched.Kind == WildBattleCalledSwitchKind.FaintedReplacement
                    ? new WildBattlePlayerReplacedEvent(e.Sequence, e.Turn, outgoing, replacement.Body)
                    : new WildBattlePlayerSwitchedEvent(e.Sequence, e.Turn, outgoing, replacement.Body));
            }
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<ForestTrainerCombatEvent> ProjectTrainerEvents(SourceBattleFrame frame, ImmutableArray<SourceBattleEvent> events, SourceBattleFrame? previous = null)
    {
        var result = ImmutableArray.CreateBuilder<ForestTrainerCombatEvent>();
        foreach (var e in events)
        {
            switch (e)
            {
                case SourceTrainerStartedEvent x: result.Add(new ForestTrainerCombatStartedEvent(x.Sequence, x.Turn, x.Trainer, x.OpponentCount)); break;
                case SourceTrainerOpponentSelectedEvent x: result.Add(new ForestTrainerOpponentSelectedEvent(x.Sequence, x.Turn, x.Incoming.Index,
                    frame.Party(BattleSide.Opponent).Members[x.Incoming.Index].Pokemon)); break;
                case SourceTrainerShiftOfferedEvent x: result.Add(new ForestTrainerShiftOfferedEvent(x.Sequence, x.Turn, x.Incoming.Index,
                    frame.Party(BattleSide.Opponent).Members[x.Incoming.Index].Pokemon)); break;
                case SourceCalledPartySwitchedEvent { Side.Value: 1, Replacement: { } replacement } x:
                    result.Add(new ForestTrainerOpponentReplacedEvent(x.Sequence, x.Turn, x.OutgoingIndex, x.IncomingIndex,
                        previous is not null ? SourceBattleReadProjection.Combatant(previous, new(1)) : new(frame.Party(BattleSide.Opponent).Members[x.OutgoingIndex].Pokemon), replacement.Body)); break;
                case SourceCalledPartySwitchedEvent { Side.Value: 0, Kind: WildBattleCalledSwitchKind.Forced, Replacement: { } replacement } x:
                    result.Add(new SsAnneTrainerPlayerForcedOutEvent(x.Sequence, x.Turn, x.OutgoingIndex, x.IncomingIndex,
                        previous is not null ? SourceBattleReadProjection.Combatant(previous, new(0)) : new(frame.Party(BattleSide.Player).Members[x.OutgoingIndex].Pokemon), replacement.Body)); break;
            }
        }
        return result.ToImmutable();
    }

    private Decision<CampaignState> BeginCanonicalTrainerAction(CampaignState state, SourceActionSelection selection,
        SourceFourActionResult? preparation = null)
    {
        var trainer = state.ForestTrainerBattle!; var frame = preparation?.State ?? trainer.Frame;
        var ai = SourceTrainerEncounterPolicy.PrepareActions(_content.PresetDoubleItems!, CanonicalTrainerPokemon, frame, frame.Revision);
        if (!ai.Succeeded) return new(CommandStatus.Rejected, state, ai.Fault);
        var begun = SourceFourActionTurns.BeginActions(CanonicalTrainerPokemon, ai.State, ai.State.Revision, [selection, .. ai.Selections]);
        if (!begun.Succeeded) return new(CommandStatus.Rejected, state, begun.Fault);
        return CommitCanonicalTrainerResult(state, begun with {
            Events = [.. preparation?.Events ?? [], .. ai.Events, .. begun.Events],
            Trace = [.. preparation?.Trace ?? [], .. ai.Trace, .. begun.Trace] });
    }

    private Decision<CampaignState> CommitCanonicalTrainerResult(CampaignState state, SourceFourActionResult result)
    {
        if (!result.Succeeded) return new(CommandStatus.Rejected, state, result.Fault);
        return new(CommandStatus.Accepted, PrepareForestTrainerPresentation(ApplyCanonicalTrainerResult(state, result)));
    }

    private CampaignState ApplyCanonicalTrainerResult(CampaignState state, SourceFourActionResult result)
    {
        if (!result.Succeeded) throw new InvalidDataException(result.Fault);
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        var pages = ImmutableArray.CreateBuilder<CampaignWildPage>();
        foreach (var e in result.Events)
        {
            if (e is SourceRewardAppliedEvent applied)
            {
                string name = PokemonName(state.Party[applied.Recipient.Index]);
                foreach (var reward in applied.Reward.Events)
                {
                    if (reward is WildRewardExperienceEvent xp) pages.Add(new($"{name} gained\n{xp.Gained} EXP. Points!", reward, applied.Recipient.Index));
                    if (reward is WildRewardLevelEvent level) { pages.Add(new($"{name} grew to\nlevel {level.After}!", reward, applied.Recipient.Index)); pages.Add(new("", reward, applied.Recipient.Index)); }
                    if (reward is WildRewardMoveLearnedEvent learned) pages.Add(new($"{name} learned\n{BattleContent.MoveNames[learned.Move.Value]}!", reward, applied.Recipient.Index));
                }
            }
            else if (e is SourceRewardLearningEvent { Outcome: SourceRewardLearningOutcome.Added } learned)
                pages.Add(new($"{PokemonName(state.Party[learned.Recipient.Index])} learned\n{BattleContent.MoveNames[learned.Candidate.Move.Value]}!", null, learned.Recipient.Index));
        }
        var sent = trainer.OpponentsSent; var seen = state.SeenSpecies;
        foreach (var switched in result.Events.OfType<SourceCalledPartySwitchedEvent>().Where(e => e.Side.Value == 1))
        {
            if (!sent.Contains(switched.IncomingIndex)) sent = sent.Add(switched.IncomingIndex);
            seen = seen.Add(result.State.Party(BattleSide.Opponent).Members[switched.IncomingIndex].Pokemon.SourceSpeciesId);
        }
        b = b with { DisplayBatch = BeginDisplayBatch(b), Frame = result.State, SourceEvents = result.Events, Events = ProjectTrainerWildEvents(result.Events, trainer.Frame), EventIndex = 0,
            Phase = CampaignBattlePhase.Events, Pages = pages.ToImmutable(), PageIndex = 0,
            Replacement = null, RandomTrace = b.RandomTrace.AddRange(result.Trace),
            LastSwitch = result.Events.OfType<SourceCalledPartySwitchedEvent>().Any(e => e.Side.Value == 0 && e.Kind != WildBattleCalledSwitchKind.Voluntary) ? null : b.LastSwitch };
        trainer = trainer with { Presentation = b, TrainerEvents = ProjectTrainerEvents(result.State, result.Events, trainer.Frame), TrainerEventIndex = 0,
            Stage = CampaignForestTrainerStage.Combat, OpponentsSent = sent,
            ExperienceGained = checked(trainer.ExperienceGained + result.Events.OfType<SourceRewardAppliedEvent>().Sum(e => e.Reward.ExperienceCredited)) };
        return ProjectSourceTrainerOwnedParty(state with { ForestTrainerBattle = trainer, SeenSpecies = seen, World = state.World with { Rng = result.State.Rng } });
    }

    private CampaignState ContinueCanonicalTrainer(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation; var frame = trainer.Frame;
        if (!b.Pages.IsEmpty || frame.Rewards?.Pending is not null)
            return state with { ForestTrainerBattle = trainer with { Stage = CampaignForestTrainerStage.Rewards, Presentation = b with { Phase = CampaignBattlePhase.Results } } };
        if (frame.FourTurn?.Phase == SourceFourActionPhase.RewardsPending)
            return PrepareForestTrainerPresentation(ApplyCanonicalTrainerResult(state, SourceFourRewards.Settle(CanonicalTrainerPokemon, frame, TrainerContinuation(frame))));
        if (frame.Outcome != WildBattleOutcome.Ongoing) return SettleForestTrainerBattle(state);
        if (frame.FourTurn?.Phase == SourceFourActionPhase.ShiftPending || frame.PendingSwitches.FirstOrDefault()?.Slot.Value == 0)
            return state with { ForestTrainerBattle = trainer with { Presentation = b with { Phase = CampaignBattlePhase.ChooseMove,
                Replacement = frame.FourTurn?.Phase == SourceFourActionPhase.ShiftPending ? b.Replacement : CampaignReplacementStage.Select } } };
        if (frame.FourTurn?.Phase == SourceFourActionPhase.ActionSwitchPending && frame.PendingSwitches.FirstOrDefault() is { Slot.Value: 1 } pending)
        {
            var chosen = SourceTrainerEncounterPolicy.ChooseReplacement(CanonicalTrainerPokemon, frame, new(1));
            if (!chosen.Succeeded) throw new InvalidDataException(chosen.Fault);
            var selected = frame with { Rng = chosen.Rng };
            var result = SourceFourActionTurns.ChooseActionSwitch(CanonicalTrainerPokemon, selected, TrainerContinuation(selected), pending.Slot, new(BattleSide.Opponent, chosen.PartyIndex!.Value));
            return PrepareForestTrainerPresentation(ApplyCanonicalTrainerResult(state, result with { Trace = chosen.Trace.AddRange(result.Trace) }));
        }
        if (frame.FourTurn is not null)
            return PrepareForestTrainerPresentation(ApplyCanonicalTrainerResult(state, SourceFourActionTurns.Advance(CanonicalTrainerPokemon, frame, TrainerContinuation(frame))));
        return state with { ForestTrainerBattle = trainer with { Presentation = b with { Phase = CampaignBattlePhase.ChooseMove, Replacement = null } } };
    }
}
