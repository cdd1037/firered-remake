using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public static partial class SourceFourActionTurns
{
    internal static string? ValidateVoluntarySwitch(PokemonCatalog catalog, SourceBattleFrame frame, BattlerSlot slot)
    {
        var cell = frame.Get(slot); var body = cell.Body!;
        if (body.Rooted || body.BindTurnsRemaining > 0 || cell.Registers.MeanLookSource is not null ||
            cell.Registers.Turn?.Lock is not null) return "source_four_switch_locked";
        return SourceSwitchRules.TrappingAbility(catalog, slot, frame.Slots.Select(c => c.Slot), s => frame.Get(s).Body!) is { } ability
            ? "source_four_switch_trapped_" + ability : null;
    }

    // Shared Spikes precedes actual entry abilities; unsupported entry item/volatile states
    // still fail capability validation. All voluntary and move-triggered entries happen
    // inside the current action, independently of the approved end-turn KO-entry policy.
    internal static SourceFourActionResult BindActionSwitch(PokemonCatalog catalog, SourceBattleFrame frame,
        BattlerSlot slot, PartyMemberKey incoming, WildBattleCalledSwitchKind kind)
    {
        var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        var next = frame; var previous = frame.Get(slot); var departing = previous.Body!;
        if (WildBattleEffective.Ability(departing) == "ABILITY_NATURAL_CURE" && departing.Status != 0)
        {
            var party = next.Party(slot.Side); int index = previous.Member!.Value.Index;
            var cured = departing with { Status = 0, SourcePartyStatusOverride = null };
            next = next with { Slots = next.Slots.SetItem(slot.Value, previous with { Body = cured }),
                Parties = next.Parties.SetItem((int)slot.Side, party with { Members = party.Members.SetItem(index,
                    party.Members[index] with { Status = 0 }) }) };
            var changed = SourceBattleEventHooks.Publish(next, (n,t) => new SourceStatusChangedEvent(n,t,slot,departing.Status,0,"natural-cure"));
            next = changed.State; events.Add(changed.Event);
        }
        var member = next.Party(incoming.Side).Members[incoming.Index];
        bool baton = kind == WildBattleCalledSwitchKind.BatonPass;
        var body = SourceSwitchRules.PrepareBody(departing, member, baton);
        var registers = new SourceSlotRegisters { Damage = previous.Registers.Damage,
            LeechSeedSource = baton ? previous.Registers.LeechSeedSource : null,
            MeanLookSource = baton ? previous.Registers.MeanLookSource : null,
            Turn = baton && previous.Registers.Turn is { SureHitTurnsRemaining: > 0 } turn ? turn with { Lock = null } : null };
        var partyIn = next.Party(incoming.Side);
        next = next with { Parties = next.Parties.SetItem((int)incoming.Side, partyIn with { Members = partyIn.Members.SetItem(incoming.Index,
            member with { HeldItem = body.HeldItem, Status = SourcePartyStatusRules.FromBattle(body.Status) }) }) };
        var reservation = new SourceSwitchReservation(previous.Occupant!.Value, incoming, kind);
        if (!next.Reservations.Contains(reservation))
        {
            var reserved = SourceBattleIdentityRules.Reserve(next, next.Revision, reservation);
            if (!reserved.Succeeded) return Fail(frame, reserved.Fault!);
            next = reserved.State;
        }
        if (next.FourTurn is { Phase: SourceFourActionPhase.ActionSwitchPending } pendingCursor)
            next = next with { PendingSwitches = [], FourTurn = pendingCursor with { Phase = next.NextAction == next.Queue.Length
                ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions } };
        var bound = SourceBattleIdentityRules.BindReservedEntry(next, next.Revision, reservation, SourceEntryBoundary.Action, body, registers);
        if (!bound.Succeeded) return Fail(frame, bound.Fault!);
        var switched = SourceBattleEventHooks.Publish(bound.State, (n,t) => new SourceCalledPartySwitchedEvent(n,t,slot,
            previous.Member!.Value.Index,incoming.Index,kind) { Replacement = new(body, registers) });
        events.Add(switched.Event);
        var entry = SourceFourEntryAbilities.OnReplacement(catalog, switched.State, slot);
        if (!entry.Succeeded) return Fail(frame, entry.Fault!);
        if (ValidateCapability(catalog, entry.State) is { } invalid) return Fail(frame, invalid);
        return new(entry.State, [.. events, .. entry.Events], entry.Trace, null);
    }

    /// <summary>Resume the original Baton Pass action at its actual choice boundary. The incoming
    /// Pokemon never acquires the departing occupant's queued action.</summary>
    public static SourceFourActionResult ChooseActionSwitch(PokemonCatalog catalog, SourceBattleFrame frame,
        SourceTurnContinuation continuation, BattlerSlot slot, PartyMemberKey incoming)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.FourTurn is not { Phase: SourceFourActionPhase.ActionSwitchPending } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase))
            return Fail(frame,"source_four_action_switch_stale_continuation");
        if (frame.PendingSwitches is not [{ Kind: WildBattleCalledSwitchKind.BatonPass, Invocation: { } invocation } pending] ||
            pending.Slot != slot || incoming.Side != slot.Side || !pending.LegalPartyIndices.Contains(incoming.Index))
            return Fail(frame,"source_four_action_switch_choice");
        try
        {
            var switched = BindActionSwitch(catalog, frame, slot, incoming, pending.Kind);
            if (!switched.Succeeded) return Fail(frame, switched.Fault!);
            var immunity = SourceFrameImmunityRules.Apply(switched.State);
            var berries = SourceFrameBerryItems.Apply(immunity.State, SourceBerryPhase.MoveEnd);
            var actionEvents = switched.Events.AddRange(immunity.Events).AddRange(berries.Events);
            var next = SourceMoveHistoryReducer.Apply(catalog, berries.State,
                invocation with { CurrentMove = new("pret:MOVE_NONE") }, actionEvents, false);
            next = next with { FourTurn = next.FourTurn! with { Phase = HasPendingRewards(next) ? SourceFourActionPhase.RewardsPending
                : next.Outcome != WildBattleOutcome.Ongoing ? SourceFourActionPhase.BattleEnded
                : next.NextAction == next.Queue.Length ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions } };
            var settled = next.Outcome == WildBattleOutcome.Ongoing && !HasPendingRewards(next)
                ? SourceFourEntryAbilities.Settle(next) : new SourceFourActionResult(next, [], [], null);
            if (ValidateCapability(catalog, settled.State) is { } error) return Fail(frame,error);
            return new(settled.State, [.. actionEvents, .. settled.Events], [.. switched.Trace, .. settled.Trace], null);
        }
        catch (SourceRecipeUnsupported e) { return Fail(frame,e.Message); }
        catch (OverflowException) { return Fail(frame,"source_four_action_switch_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame,"source_four_action_switch_invalid_transition"); }
    }
}
