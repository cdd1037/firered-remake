using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>A suspended part of the current voluntary switch. It never owns a second queue.</summary>
public sealed record SourcePursuitSwitchCursor(OccupantToken Outgoing, PartyMemberKey Incoming, int NextOpponent);

public static partial class SourceFourActionTurns
{
    internal static bool IsPursuitSwitchReservation(SourceBattleFrame frame, SourceSwitchReservation reservation) =>
        reservation.Kind == WildBattleCalledSwitchKind.Voluntary && frame.FourTurn?.PursuitSwitch is { } pending &&
        pending.Outgoing == reservation.Outgoing && pending.Incoming == reservation.Incoming;

    internal static string? ValidatePursuitSwitch(SourceBattleFrame frame)
    {
        if (frame.FourTurn?.PursuitSwitch is not { } pending) return null;
        if (pending.NextOpponent is < 0 or > 2 || !frame.IsCurrent(pending.Outgoing) ||
            frame.FourTurn.Phase is not (SourceFourActionPhase.Actions or SourceFourActionPhase.RewardsPending) ||
            frame.NextAction >= frame.Queue.Length || frame.Queue[frame.NextAction] is not { Kind: SourceActionKind.Switch } action ||
            action.Token?.Owner != pending.Outgoing || action.Incoming != pending.Incoming ||
            !frame.Reservations.Any(r => IsPursuitSwitchReservation(frame, r)))
            return "source_four_pursuit_switch_cursor";
        return null;
    }

    // Cmd_jumpifnopursuitswitchdmg visits opposing right (counter 2), then left (1).
    // Any EXP/learning pause retains this same switch action, incoming reservation and index.
    private static SourceFourActionResult InterceptVoluntarySwitch(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        var action = frame.Queue[frame.NextAction];
        var pending = frame.FourTurn!.PursuitSwitch ?? new(action.Token!.Value.Owner, action.Incoming!.Value, 0);
        var next = frame with { FourTurn = frame.FourTurn with { PursuitSwitch = pending } };
        var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        var trace = ImmutableArray.CreateBuilder<Game.Domain.Randomness.RandomTrace>();
        while (pending.NextOpponent < 2)
        {
            var pursuer = new BattlerSlot((action.Slot.Value ^ 1) & 1 | (pending.NextOpponent == 0 ? 2 : 0));
            pending = pending with { NextOpponent = pending.NextOpponent + 1 };
            next = next with { FourTurn = next.FourTurn! with { PursuitSwitch = pending } };
            int ordinal = Enumerable.Range(0, next.Queue.Length).Single(i => next.Queue[i].Slot == pursuer);
            var chosen = next.Queue[ordinal];
            if (ordinal < next.NextAction || chosen.Kind != SourceActionKind.Move || chosen.Target != action.Slot ||
                chosen.OriginalChosenMove?.Value != "pret:MOVE_PURSUIT" || chosen.MoveSlot is not >= 0 ||
                chosen.Token is not { } token || !next.IsCurrent(token.Owner) || !next.CanAct(pursuer) ||
                !SourcePursuitRules.Eligible(next.Get(pursuer).Body!, next.Get(action.Slot).Body!)) continue;
            // TRY_FINISH consumes the original queued action before entering the special script.
            next = next with { Queue = next.Queue.SetItem(ordinal, chosen with {
                Kind = SourceActionKind.Cancelled, CancellationReason = "pursuit-intercept-consumed" }) };
            var execution = new FrameRecipeContext(catalog, next,
                new(token.Owner, next.Revision, chosen.MoveSlot.Value, action.Slot, ordinal), catalog.Moves[new("pret:MOVE_PURSUIT")]);
            execution.RunPursuitIntercept();
            next = execution.Frame; events.AddRange(execution.Events); trace.AddRange(execution.Trace);
            foreach (var faint in execution.Events.OfType<SourceFaintedEvent>())
            {
                var defeated = next.Get(faint.Side).Occupant!.Value;
                if (next.FourTurn!.Defeats.Any(d => d.Occupant == defeated)) continue;
                next = next with { DeferredKoVacancyMask = (byte)(next.DeferredKoVacancyMask | faint.Side.Mask),
                    Queue = next.Queue.Select(a => a.Token?.Owner == defeated && a.Kind != SourceActionKind.Switch
                        ? a with { Kind = SourceActionKind.Cancelled, CancellationReason = "original-occupant-fainted" } : a).ToImmutableArray(),
                    FourTurn = next.FourTurn with { Defeats = next.FourTurn.Defeats.Add(new(defeated, faint.Sequence, faint.Side.Side == BattleSide.Opponent)) } };
            }
            if (HasPendingRewards(next))
                return new(next with { FourTurn = next.FourTurn! with { Phase = SourceFourActionPhase.RewardsPending } }, events.ToImmutable(), trace.ToImmutable(), null);
        }
        // Player KOs have no EXP. Opponent KOs return here only after the reward owner has settled.
        next = next with { FourTurn = next.FourTurn! with { Defeats = next.FourTurn!.Defeats.Where(d => d.Occupant != pending.Outgoing).ToImmutableArray() } };
        return new(next, events.ToImmutable(), trace.ToImmutable(), null);
    }
}
