using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public static partial class SourceFourActionTurns
{
    // KO policy chooses the boundary; both layouts bind actual reserves through this owner.
    internal static SourceBattleFrame PrepareReplacements(SourceBattleFrame frame)
    {
        var pending = ImmutableArray.CreateBuilder<SourcePendingSwitch>();
        byte absent = frame.SourceAbsentMask, deferred = frame.DeferredKoVacancyMask;
        foreach (var side in new[] { BattleSide.Player, BattleSide.Opponent })
        {
            var legal = SourceBattleIdentityRules.LegalReserveIndices(frame, side);
            var vacancies = frame.Slots.Take(frame.BattlerCount).Where(c => c.Slot.Side == side &&
                (frame.KoReplacementTiming == KoReplacementTiming.EndOfTurn ? frame.IsDeferredKoVacant(c.Slot)
                    : !frame.IsSourceAbsent(c.Slot) && c.Body!.Pokemon.CurrentHp == 0)).ToArray();
            for (int i = 0; i < vacancies.Length; i++)
            {
                var cell = vacancies[i];
                if (i < legal.Length) pending.Add(new(cell.Slot, WildBattleCalledSwitchKind.FaintedReplacement, legal, null));
                else { absent |= cell.Slot.Mask; deferred &= (byte)~cell.Slot.Mask; }
            }
        }
        return frame with { PendingSwitches = pending.ToImmutable(), SourceAbsentMask = absent, DeferredKoVacancyMask = deferred };
    }

    public static SourceFourActionResult ChooseReplacement(PokemonCatalog catalog, SourceBattleFrame frame,
        SourceTurnContinuation continuation, BattlerSlot slot, PartyMemberKey incoming) =>
        ChooseReplacementCore(catalog, frame, continuation, slot, incoming, false);

    private static SourceFourActionResult ChooseReplacementCore(PokemonCatalog catalog, SourceBattleFrame frame,
        SourceTurnContinuation continuation, BattlerSlot slot, PartyMemberKey incoming, bool trainerSelected)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.FourTurn is not { Phase: SourceFourActionPhase.ReplacementsPending } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase))
            return Fail(frame, "source_four_replacement_stale_continuation");
        if (!trainerSelected && frame.TrainerEncounter is not null && slot.Side == BattleSide.Opponent)
            return Fail(frame, "source_trainer_policy_replacement_required");
        if (frame.PendingSwitches.IsEmpty || frame.PendingSwitches[0].Slot != slot || incoming.Side != slot.Side ||
            !frame.PendingSwitches[0].LegalPartyIndices.Contains(incoming.Index) || HasPendingRewards(frame))
            return Fail(frame, "source_four_replacement_choice");
        try
        {
            var previous = frame.Get(slot); var member = frame.Party(incoming.Side).Members[incoming.Index];
            var body = SourceSwitchRules.PrepareBody(previous.Body!, member, false);
            var party = frame.Party(incoming.Side);
            // Retire the current defeat token before its slot is rebound. The six completion bits
            // survive for opposing party identities, so entry cannot erase one-time settlement.
            var prepared = frame with { FourTurn = cursor with { Defeats = cursor.Defeats.Where(d => d.Occupant != previous.Occupant).ToImmutableArray() },
                PendingSwitches = frame.PendingSwitches.Where(p => p.Slot == slot || p.Slot.Side != incoming.Side || p.LegalPartyIndices.Length > 1)
                    .Select(p => p.Slot == slot || p.Slot.Side != incoming.Side ? p : p with { LegalPartyIndices = p.LegalPartyIndices.Remove(incoming.Index) }).ToImmutableArray(),
                Parties = frame.Parties.SetItem((int)incoming.Side, party with { Members = party.Members.SetItem(incoming.Index,
                    member with { HeldItem = body.HeldItem, Status = SourcePartyStatusRules.FromBattle(body.Status) }) }) };
            var reservation = new SourceSwitchReservation(previous.Occupant!.Value, incoming, WildBattleCalledSwitchKind.FaintedReplacement);
            var reserved = SourceBattleIdentityRules.Reserve(prepared, prepared.Revision, reservation);
            if (!reserved.Succeeded) return Fail(frame, reserved.Fault!);
            var bound = SourceBattleIdentityRules.BindReservedEntry(reserved.State, reserved.State.Revision, reservation,
                cursor.EndTurn is null ? SourceEntryBoundary.Action : SourceEntryBoundary.EndOfTurnReplacement,
                body, new() { Damage = previous.Registers.Damage });
            if (!bound.Succeeded) return Fail(frame, bound.Fault!);
            var next = PrepareReplacements(bound.State);
            if (next.PendingSwitches.IsEmpty) next = next with { FourTurn = next.FourTurn! with {
                Phase = cursor.EndTurn is null ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.EndTurnResume } };
            var switched = SourceBattleEventHooks.Publish(next, (n, t) => new SourceCalledPartySwitchedEvent(n, t, slot,
                previous.Member!.Value.Index, incoming.Index, WildBattleCalledSwitchKind.FaintedReplacement) { Replacement = new(body, bound.State.Get(slot).Registers) });
            var entry = SourceFourEntryAbilities.OnReplacement(catalog, switched.State, slot);
            if (!entry.Succeeded) return Fail(frame, entry.Fault!);
            var events = ImmutableArray.CreateBuilder<SourceBattleEvent>(); events.Add(switched.Event); events.AddRange(entry.Events);
            next = entry.State;
            if (next.FourTurn!.OutcomeAfterEndTurn is not null)
                next = next with { PendingSwitches = [], FourTurn = next.FourTurn with {
                    Phase = cursor.EndTurn is null ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.EndTurnResume } };
            else if (HasPendingRewards(next) || next.Outcome != WildBattleOutcome.Ongoing)
                next = next with { PendingSwitches = [], FourTurn = next.FourTurn! with { Phase = HasPendingRewards(next)
                    ? SourceFourActionPhase.RewardsPending : SourceFourActionPhase.BattleEnded } };
            else
            {
                next = PrepareReplacements(next);
                next = next with { FourTurn = next.FourTurn! with { Phase = next.PendingSwitches.IsEmpty
                    ? cursor.EndTurn is null ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.EndTurnResume
                    : SourceFourActionPhase.ReplacementsPending } };
                // A newly entered occupant may faint to the hazard. Its next choice belongs to
                // the same saved residual cursor, without replaying weather/status/timer work.
                if (next.IsDeferredKoVacant(slot) || next.Get(slot).Body!.Pokemon.CurrentHp == 0)
                    foreach (var choice in next.PendingSwitches.Where(p => p.Slot == slot))
                    {
                        var required = SourceBattleEventHooks.Publish(next, (n,t) => new SourceCalledPartySwitchRequiredEvent(n,t,choice));
                        next = required.State; events.Add(required.Event);
                    }
            }
            var settled = next.PendingSwitches.IsEmpty && next.Outcome == WildBattleOutcome.Ongoing &&
                next.FourTurn!.OutcomeAfterEndTurn is null && !HasPendingRewards(next)
                ? SourceFourEntryAbilities.Settle(next, catalog) : new SourceFourActionResult(next, [], [], null);
            if (ValidateCapability(catalog, settled.State) is { } nextError) return Fail(frame, nextError);
            return new(settled.State, [.. events, .. settled.Events], [.. entry.Trace, .. settled.Trace], null);
        }
        catch (SourceRecipeUnsupported e) { return Fail(frame, e.Message); }
        catch (OverflowException) { return Fail(frame, "source_four_replacement_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame, "source_four_replacement_invalid_transition"); }
    }
}
