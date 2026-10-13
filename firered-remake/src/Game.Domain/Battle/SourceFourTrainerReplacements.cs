using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public static partial class SourceFourActionTurns
{
    /// <summary>The trainer policy selects once, after EXP and any player KO replacement.
    /// A Shift prompt retains this choice in the existing turn cursor.</summary>
    public static SourceFourActionResult ContinueTrainerReplacement(PokemonCatalog catalog, SourceBattleFrame frame,
        SourceTurnContinuation continuation)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.TrainerEncounter is null || frame.FourTurn is not { Phase: SourceFourActionPhase.ReplacementsPending } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase) ||
            frame.PendingSwitches.FirstOrDefault() is not { Slot.Value: 1, Kind: WildBattleCalledSwitchKind.FaintedReplacement } pending ||
            HasPendingRewards(frame)) return Fail(frame, "source_trainer_replacement_phase");
        try
        {
            var next = frame; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            var trace = ImmutableArray.CreateBuilder<Game.Domain.Randomness.RandomTrace>();
            var receipt = cursor.TrainerReplacement;
            if (receipt is null)
            {
                var choice = SourceTrainerEncounterPolicy.ChooseReplacement(catalog, next, new(1));
                if (!choice.Succeeded || choice.PartyIndex is not { } index || !pending.LegalPartyIndices.Contains(index))
                    return Fail(frame, choice.Fault ?? "source_trainer_replacement_choice");
                bool shift = SourceTrainerEncounterPolicy.CanOfferShift(next);
                receipt = new(new(BattleSide.Opponent, index), shift);
                next = next with { Rng = choice.Rng, Revision = checked(next.Revision + 1),
                    FourTurn = next.FourTurn! with { TrainerReplacement = receipt,
                        Phase = shift ? SourceFourActionPhase.ShiftPending : SourceFourActionPhase.ReplacementsPending } };
                trace.AddRange(choice.Trace);
                var selected = SourceBattleEventHooks.Publish(next, (n,t) => new SourceTrainerOpponentSelectedEvent(n,t,receipt.Incoming));
                next = selected.State; events.Add(selected.Event);
                if (shift)
                {
                    var offered = SourceBattleEventHooks.Publish(next, (n,t) => new SourceTrainerShiftOfferedEvent(n,t,receipt.Incoming));
                    next = offered.State; events.Add(offered.Event);
                    if (ValidateCapability(catalog, next) is { } error) return Fail(frame,error);
                    return new(next, events.ToImmutable(), trace.ToImmutable(), null);
                }
            }
            next = next with { FourTurn = next.FourTurn! with { TrainerReplacement = null } };
            var bound = ChooseReplacementCore(catalog, next,
                new(next.Revision,next.EventSequence,next.NextAction,next.FourTurn!.Phase), new(1), receipt.Incoming, true);
            return bound.Succeeded ? bound with { Events = events.ToImmutable().AddRange(bound.Events),
                Trace = trace.ToImmutable().AddRange(bound.Trace) } : Fail(frame,bound.Fault!);
        }
        catch (OverflowException) { return Fail(frame,"source_trainer_replacement_counter_exhausted"); }
        catch (ArgumentException e) { return Fail(frame,e.Message); }
    }

    /// <summary>Null declines Shift. A chosen player reserve enters before the already selected
    /// opponent, using the shared switch/entry code; no enemy action is reselected.</summary>
    public static SourceFourActionResult ChooseShift(PokemonCatalog catalog, SourceBattleFrame frame,
        SourceTurnContinuation continuation, PartyMemberKey? incoming)
    {
        if (ValidateCapability(catalog,frame) is { } invalid) return Fail(frame,invalid);
        if (frame.FourTurn is not { Phase: SourceFourActionPhase.ShiftPending, TrainerReplacement: { } receipt } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision,frame.EventSequence,frame.NextAction,cursor.Phase) ||
            !SourceTrainerEncounterPolicy.CanOfferShift(frame) || HasPendingRewards(frame)) return Fail(frame,"source_trainer_shift_phase");
        if (incoming is not null && frame.Get(new(0)).Body!.Rooted) return Fail(frame,"source_trainer_shift_rooted");
        if (incoming is { } chosen && (chosen.Side != BattleSide.Player ||
            !SourceBattleIdentityRules.LegalReserveIndices(frame,BattleSide.Player).Contains(chosen.Index)))
            return Fail(frame,"source_trainer_shift_choice");
        try
        {
            var next = frame with { Revision = checked(frame.Revision+1), FourTurn = cursor with { Phase = SourceFourActionPhase.ReplacementsPending } };
            var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            var trace = ImmutableArray.CreateBuilder<Game.Domain.Randomness.RandomTrace>();
            if (incoming is { } member)
            {
                var switched = BindActionSwitch(catalog,next,new(0),member,WildBattleCalledSwitchKind.Voluntary);
                if (!switched.Succeeded) return Fail(frame,switched.Fault!);
                next=switched.State;events.AddRange(switched.Events);trace.AddRange(switched.Trace);
                if (next.Outcome != WildBattleOutcome.Ongoing || next.FourTurn!.OutcomeAfterEndTurn is not null || HasPendingRewards(next))
                {
                    next=next with { PendingSwitches=[], FourTurn=next.FourTurn! with { TrainerReplacement=null,
                        Phase=HasPendingRewards(next)?SourceFourActionPhase.RewardsPending:
                            next.Outcome != WildBattleOutcome.Ongoing?SourceFourActionPhase.BattleEnded:
                            next.FourTurn.EndTurn is null?SourceFourActionPhase.EndTurn:SourceFourActionPhase.EndTurnResume } };
                    return new(next,events.ToImmutable(),trace.ToImmutable(),null);
                }
                // Source ChooseShift always binds the already selected opponent after
                // the optional player entry, even if that entrant faints to Spikes.
                // The common KO handler then requests the player's next reserve.
            }
            var bound=ContinueTrainerReplacement(catalog,next,new(next.Revision,next.EventSequence,next.NextAction,next.FourTurn!.Phase));
            return bound.Succeeded?bound with { Events=events.ToImmutable().AddRange(bound.Events), Trace=trace.ToImmutable().AddRange(bound.Trace) }:Fail(frame,bound.Fault!);
        }
        catch(OverflowException){return Fail(frame,"source_trainer_shift_counter_exhausted");}
        catch(ArgumentException e){return Fail(frame,e.Message);}
    }
}
