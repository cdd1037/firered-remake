using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public static partial class SourceFourActionTurns
{
    // The approved policy moves only KO entry to this boundary. This never implements a
    // voluntary/Baton Pass/Roar action or grants their timings to a KO request.
    internal static SourceBattleFrame PrepareReplacements(SourceBattleFrame frame)
    {
        var pending = ImmutableArray.CreateBuilder<SourcePendingSwitch>();
        byte absent = frame.SourceAbsentMask, deferred = frame.DeferredKoVacancyMask;
        foreach (var side in new[] { BattleSide.Player, BattleSide.Opponent })
        {
            var legal = SourceBattleIdentityRules.LegalReserveIndices(frame, side);
            var vacancies = frame.Slots.Where(c => c.Slot.Side == side && frame.IsDeferredKoVacant(c.Slot)).ToArray();
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
        SourceTurnContinuation continuation, BattlerSlot slot, PartyMemberKey incoming)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.FourTurn is not { Phase: SourceFourActionPhase.ReplacementsPending } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase))
            return Fail(frame, "source_four_replacement_stale_continuation");
        if (frame.PendingSwitches.IsEmpty || frame.PendingSwitches[0].Slot != slot || incoming.Side != slot.Side ||
            !frame.PendingSwitches[0].LegalPartyIndices.Contains(incoming.Index) || HasPendingRewards(frame))
            return Fail(frame, "source_four_replacement_choice");
        try
        {
            var previous = frame.Get(slot); var member = frame.Party(incoming.Side).Members[incoming.Index];
            // No entry-effect capability is inferred from the action/residual capability.
            if (member.Pokemon.AbilityId is "ABILITY_INTIMIDATE" or "ABILITY_TRACE" or "ABILITY_FORECAST" or
                "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or "ABILITY_SAND_STREAM")
                return Fail(frame, "source_four_replacement_entry_ability_not_migrated");
            var body = new WildBattleCombatant(member.Pokemon,
                HeldItem: member.KnockedOffItem is not null ? "ITEM_NONE" : WildBattleSourcePartyEffects.PersistentHeldItem(member),
                Status: WildBattleSourceFieldEffects.ResetToxicCounter(member.Status), AttackBadgeBoost: previous.Body!.AttackBadgeBoost)
            { SpeedBadgeBoost = previous.Body.SpeedBadgeBoost, DefenseBadgeBoost = previous.Body.DefenseBadgeBoost,
                FirstTurnCounter = 2, KnockedOffItem = member.KnockedOffItem };
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
                SourceEntryBoundary.EndOfTurnReplacement, body, new() { Damage = previous.Registers.Damage });
            if (!bound.Succeeded) return Fail(frame, bound.Fault!);
            var next = PrepareReplacements(bound.State);
            if (next.PendingSwitches.IsEmpty) next = next with { FourTurn = next.FourTurn! with { Phase = SourceFourActionPhase.EndTurnResume } };
            if (ValidateCapability(catalog, next) is { } nextError) return Fail(frame, nextError);
            var switched = SourceBattleEventHooks.Publish(next, (n, t) => new SourceCalledPartySwitchedEvent(n, t, slot,
                previous.Member!.Value.Index, incoming.Index, WildBattleCalledSwitchKind.FaintedReplacement));
            return new(switched.State, [switched.Event], [], null);
        }
        catch (OverflowException) { return Fail(frame, "source_four_replacement_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame, "source_four_replacement_invalid_transition"); }
    }
}
