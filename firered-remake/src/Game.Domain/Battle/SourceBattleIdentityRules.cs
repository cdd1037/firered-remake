using System.Collections.Immutable;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourceIdentityResult(SourceBattleFrame State, string? Fault)
{
    public bool Succeeded => Fault is null;
}
public enum SourceEntryBoundary { Action, EndOfTurnReplacement }
public enum SourceDepartureKind { Switch, BatonPass, Faint }

/// <summary>Identity and storage validation only. Mechanics/catalog/encounter admission remains a separate gate.</summary>
public static class SourceBattleIdentityRules
{
    public static string? Validate(SourceBattleFrame? frame)
    {
        if (frame is null) return "source_frame_null";
        if (frame.Version != SourceBattleFrame.IdentityVersion || frame.SourceCommit != WildBattleRules.SourceCommit ||
            string.IsNullOrWhiteSpace(frame.RulesVersion)) return "source_frame_version";
        if (!Enum.IsDefined(frame.Layout) || !Enum.IsDefined(frame.KoReplacementTiming) || !Enum.IsDefined(frame.BattleKind) ||
            !Enum.IsDefined(frame.Outcome)) return "source_frame_options";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && frame.KoReplacementTiming != KoReplacementTiming.EndOfTurn)
            return "source_frame_ko_policy";
        if (frame.SourceMechanicsRulesVersion is { } mechanicsVersion &&
            (mechanicsVersion != BattleRulesProfile.CanonicalSourceSinglesVersion || frame.Layout != SourceBattleLayout.Singles) &&
            (mechanicsVersion != SourceBattleKernel.Version || frame.Layout != SourceBattleLayout.PresetDoubles))
            return "source_frame_mechanics_profile_layout";
        if (frame.Turn < 0 || frame.Revision < 0 || frame.EventSequence < 0 || frame.RunAttempts < 0 ||
            frame.Rng.AlgorithmId != Gen3Rng.AlgorithmId) return "source_frame_counters";
        if (frame.Parties.IsDefault || frame.Parties.Length != 2 || frame.Sides.IsDefault || frame.Sides.Length != 2 ||
            frame.Slots.IsDefault || frame.Slots.Length != 4 || frame.Field is null) return "source_frame_shape";
        int outside = frame.Layout == SourceBattleLayout.Singles ? 12 : 0;
        if ((frame.SourceAbsentMask & ~15) != 0 || (frame.TurnStartAbsentMask & ~15) != 0 ||
            (frame.DeferredKoVacancyMask & ~15) != 0 || (frame.SourceAbsentMask & outside) != outside ||
            (frame.TurnStartAbsentMask & outside) != outside || (frame.DeferredKoVacancyMask & outside) != 0 ||
            frame.KoReplacementTiming != KoReplacementTiming.EndOfTurn && frame.DeferredKoVacancyMask != 0)
            return "source_frame_presence_masks";
        for (int s = 0; s < 2; s++)
        {
            var party = frame.Parties[s]; var side = frame.Sides[s];
            if (party is null || party.Side != (BattleSide)s || party.Members.IsDefault || party.Members.Length is < 1 or > 6 ||
                party.Members.Any(m => m?.Pokemon is null || m.Pokemon.Moves.IsDefault)) return "source_frame_party";
            if (side is null || side.Side != (BattleSide)s || side.ReflectTurnsRemaining is < 0 or > 5 ||
                side.LightScreenTurnsRemaining is < 0 or > 5 || side.MistTurnsRemaining is < 0 or > 5 ||
                side.SafeguardTurnsRemaining is < 0 or > 5 || side.Spikes is < 0 or > 3 || side.FollowMeTimer is < 0 or > 1 ||
                (side.FollowMeTimer == 0) != (side.FollowMeTarget is null) ||
                side.FollowMeTarget is { } target && (target.Value >= frame.BattlerCount || target.Side != side.Side))
                return "source_frame_side_registers";
        }
        var field = frame.Field;
        if (field.PermanentWeather is not null && frame.RulesVersion != WildBattleRules.FuchsiaRulesVersion) return "source_weather_profile";
        if (SourceWeatherRules.Validate(field.PermanentWeather, frame.SourceMechanicsRulesVersion, field.RainTurnsRemaining,
            field.SandstormTurnsRemaining, field.SunTurnsRemaining, field.HailTurnsRemaining) is { } weatherError) return weatherError;
        if (field.SandstormTurnsRemaining is < 0 or > 5 || field.RainTurnsRemaining is < 0 or > 5 ||
            field.SunTurnsRemaining is < 0 or > 5 || field.HailTurnsRemaining is < 0 or > 5 || field.PayDayMoney < 0 ||
            field.Terrain is { } terrain && !Enum.IsDefined(terrain)) return "source_frame_field";
        var active = new HashSet<PartyMemberKey>();
        for (int i = 0; i < 4; i++)
        {
            var cell = frame.Slots[i];
            if (cell is null || cell.Slot.Value != i || cell.Registers is null || cell.Position is null || cell.EntrySerial < 0)
                return "source_frame_slot_shape";
            if (i >= frame.BattlerCount && (cell.Body is not null || cell.Member is not null || cell.EntrySerial != 0 ||
                cell.Registers != new SourceSlotRegisters() || cell.Position != new SourcePositionRegisters())) return "source_frame_unused_slot";
            if ((cell.Body is null) != (cell.Member is null)) return "source_frame_binding";
            if (cell.Body is null)
            {
                if (!frame.IsSourceAbsent(cell.Slot) || frame.IsDeferredKoVacant(cell.Slot)) return "source_frame_empty_presence";
            }
            else
            {
                if (cell.Body.Pokemon is null || cell.Body.Pokemon.Moves.IsDefault ||
                    cell.Body.Transform is { } transform && (transform.OriginalPokemon is null || transform.OriginalPokemon.Moves.IsDefault))
                    return "source_frame_pokemon_shape";
                if (SourceTraceForecastRules.Validate(cell.Body, frame.SourceMechanicsRulesVersion) is { } formError) return formError;
                var key = cell.Member!.Value;
                if (key.Side != cell.Slot.Side || key.Index >= frame.Party(key.Side).Members.Length || cell.EntrySerial == 0)
                    return "source_frame_party_binding";
                if (!active.Add(key)) return "source_frame_duplicate_active_member";
                var member = frame.Party(key.Side).Members[key.Index];
                if (member.IsEgg || !MatchesPermanent(member, cell.Body)) return "source_frame_active_party_mismatch";
                if (HasLegacyPointers(cell.Body)) return "source_frame_competing_legacy_registers";
                if (cell.Body.CalledVolatile?.SnatchOrderIndex is { } snatch && (snatch < 0 || snatch >= frame.BattlerCount - 1)) return "source_frame_snatch_order";
                if ((cell.Body.BindTurnsRemaining > 0) != (cell.Registers.WrappedBy is not null)) return "source_frame_wrapped_source";
                if (frame.IsDeferredKoVacant(cell.Slot) && cell.Body.Pokemon.CurrentHp != 0) return "source_frame_live_ko_vacancy";
            }
            if (ValidateRegisters(frame, cell) is { } registerError) return registerError;
        }
        if (SourceFourEntryAbilities.ValidateState(frame) is { } entryError) return entryError;
        if (frame.Reservations.IsDefault || frame.Queue.IsDefault || frame.PendingSwitches.IsDefault) return "source_frame_collections";
        var reserves = new HashSet<PartyMemberKey>(); var outgoing = new HashSet<BattlerSlot>();
        foreach (var reservation in frame.Reservations)
        {
            if (reservation is null || !Enum.IsDefined(reservation.Kind) || !frame.IsCurrent(reservation.Outgoing) ||
                reservation.Incoming.Side != reservation.Outgoing.Slot.Side ||
                reservation.Incoming.Index >= frame.Party(reservation.Incoming.Side).Members.Length)
                return "source_frame_reservation_identity";
            if ((reservation.Kind == WildBattleCalledSwitchKind.FaintedReplacement) !=
                (frame.Get(reservation.Outgoing.Slot).Body!.Pokemon.CurrentHp == 0)) return "source_frame_reservation_hp";
            if (!reserves.Add(reservation.Incoming) || !outgoing.Add(reservation.Outgoing.Slot) || active.Contains(reservation.Incoming))
                return "source_frame_duplicate_reservation";
            var incoming = frame.Party(reservation.Incoming.Side).Members[reservation.Incoming.Index];
            if (incoming.IsEgg || incoming.Pokemon.CurrentHp <= 0) return "source_frame_ineligible_reserve";
        }
        if (frame.Queue.Length != 0 && frame.Queue.Length != frame.BattlerCount ||
            frame.NextAction < 0 || frame.NextAction > frame.Queue.Length) return "source_frame_queue_shape";
        var queueSlots = new HashSet<BattlerSlot>();
        foreach (var action in frame.Queue)
        {
            if (action is null || action.Slot.Value >= frame.BattlerCount || !queueSlots.Add(action.Slot) ||
                !Enum.IsDefined(action.Kind) || action.Target is { } target && target.Value >= frame.BattlerCount)
                return "source_frame_queue_slot";
            if (action.Token is { } token && (token.Owner.Slot != action.Slot || token.Ordinal < 0 || token.Ordinal >= frame.BattlerCount ||
                token.Revision < 0 || token.Revision > frame.Revision || token.EventSequence < 0 || token.EventSequence > frame.EventSequence))
                return "source_frame_action_token";
            if (action.Kind != SourceActionKind.Cancelled && (action.Token is not { } owner || !frame.IsCurrent(owner.Owner)))
                return "source_frame_stale_occupant";
            if (action.Kind == SourceActionKind.Move && action.MoveSlot is not (>= -1 and <= 3) ||
                action.Kind == SourceActionKind.Switch && action.Incoming is null ||
                action.Kind == SourceActionKind.Item && action.Item is null) return "source_frame_action_payload";
            if (action.Kind == SourceActionKind.Switch && !frame.Reservations.Any(r => r.Outgoing == action.Token!.Value.Owner &&
                r.Incoming == action.Incoming && r.Kind == WildBattleCalledSwitchKind.Voluntary)) return "source_frame_switch_action_reservation";
        }
        var pendingSlots = new HashSet<BattlerSlot>();
        foreach (var pending in frame.PendingSwitches)
            if (pending is null || pending.Slot.Value >= frame.BattlerCount || !pendingSlots.Add(pending.Slot) ||
                !Enum.IsDefined(pending.Kind) || pending.LegalPartyIndices.IsDefaultOrEmpty ||
                pending.LegalPartyIndices.Distinct().Count() != pending.LegalPartyIndices.Length ||
                pending.LegalPartyIndices.Any(i => i < 0 || i >= frame.Party(pending.Slot.Side).Members.Length) ||
                pending.Invocation is { } invocation && (invocation.Actor.Value >= frame.BattlerCount || invocation.Target.Value >= frame.BattlerCount || invocation.PpOwner.Value >= frame.BattlerCount))
                return "source_frame_pending_switch";
        foreach (var pending in frame.PendingSwitches)
            if (pending.LegalPartyIndices.Any(i => active.Contains(new(pending.Slot.Side, i)) ||
                frame.Party(pending.Slot.Side).Members[i].IsEgg || frame.Party(pending.Slot.Side).Members[i].Pokemon.CurrentHp <= 0))
                return "source_frame_pending_switch_reserve";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && (frame.SinglesEncoding is not null || frame.WildTurn is not null))
            return "source_frame_singles_encoding_in_doubles";
        if (frame.SinglesEncoding is { } encoding && (
            encoding.HasPartyExtension != (encoding.PartyRulesVersion is not null) ||
            new[] { encoding.PartyRulesVersion, encoding.FieldRulesVersion, encoding.DelayedRulesVersion }
                .Any(v => v is not null && v != WildBattleRules.CalledMoveKernelRulesVersion) ||
            encoding.SourceWildRulesVersion is not null && encoding.SourceWildRulesVersion != WildBattleRules.SourceWildRulesVersion ||
            frame.WildTurn is not null && encoding.SourceWildRulesVersion is null)) return "source_frame_boundary_encoding";
        if (frame.WildTurn is { } wild && (!ValidOrder(wild.Order, 2) || wild.PlayerMoveSlot is < -1 or > 3 ||
            wild.OpponentMoveSlot is < -1 or > 3 || wild.NextAction is < 0 or > 2 ||
            wild.EndTurn is { } end && (!ValidOrder(end.Order, 2) || end.Turn != frame.Turn || end.NextBattler is < 0 or > 2 ||
                !Enum.IsDefined(end.Stage) || end.RulesVersion != WildBattleRules.SourceEndTurnRulesVersion ||
                end.MinimumRevision < 0 || end.MinimumRevision > frame.Revision || end.MinimumEventSequence < 0 ||
                end.MinimumEventSequence > frame.EventSequence))) return "source_frame_singles_continuation";
        if (frame.FourTurn is { } four && (frame.Layout != SourceBattleLayout.PresetDoubles ||
            frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version || four.Version != SourceFourActionTurns.Version ||
            four.Turn != frame.Turn || !Enum.IsDefined(four.Phase) || frame.Queue.Length != 4 || four.Defeats.IsDefault ||
            frame.Queue.Any(a => a.Kind == SourceActionKind.Move && (a.OriginalChosenMove is null || string.IsNullOrWhiteSpace(a.OriginalChosenMove.Value.Value))) ||
            four.Defeats.Any(d => d is null || !frame.IsCurrent(d.Occupant) || frame.Get(d.Occupant.Slot).Body!.Pokemon.CurrentHp != 0 ||
                d.FaintEventSequence < 0 || d.FaintEventSequence > frame.EventSequence ||
                d.RequiresExperience != (d.Occupant.Member.Side == BattleSide.Opponent)) ||
            four.Defeats.Select(d => d.Occupant).Distinct().Count() != four.Defeats.Length ||
            four.EndTurnOrder.IsDefault || four.EndTurnOrder.Length != 0 && !ValidOrder(four.EndTurnOrder, 4) ||
            (four.EndTurn is null) != four.EndTurnOrder.IsEmpty ||
            four.Phase == SourceFourActionPhase.EndTurn && four.EndTurn is not null ||
            four.Phase is SourceFourActionPhase.EndTurnResume or SourceFourActionPhase.ReplacementsPending && four.EndTurn is null ||
            four.EndTurn is { } residual && (!ValidOrder(residual.Order, 4) || !residual.Order.SequenceEqual(four.EndTurnOrder) ||
                residual.Turn != frame.Turn || residual.NextBattler is < 0 or > 4 || !Enum.IsDefined(residual.Stage) ||
                residual.RulesVersion != WildBattleRules.SourceEndTurnRulesVersion || residual.MinimumRevision < 0 ||
                residual.MinimumRevision > frame.Revision || residual.MinimumEventSequence < 0 || residual.MinimumEventSequence > frame.EventSequence ||
                frame.NextAction != 4 || four.Phase is not (SourceFourActionPhase.EndTurnResume or SourceFourActionPhase.RewardsPending or SourceFourActionPhase.ReplacementsPending or SourceFourActionPhase.BattleEnded) ||
                residual.Stage == WildBattleSourceEndTurnStage.Complete && (residual.NextBattler != 4 || four.Phase != SourceFourActionPhase.BattleEnded) ||
                residual.Stage is not (WildBattleSourceEndTurnStage.FutureAttacks or WildBattleSourceEndTurnStage.Complete) ||
                four.Phase == SourceFourActionPhase.BattleEnded && frame.Outcome == WildBattleOutcome.Ongoing) ||
            four.OutcomeAfterRewards is { } pendingOutcome && (pendingOutcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw) ||
                !four.Defeats.Any(d => d.RequiresExperience)) ||
            four.Phase == SourceFourActionPhase.Actions && frame.NextAction >= 4 ||
            four.Phase is SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume or SourceFourActionPhase.ReplacementsPending && frame.NextAction != 4 ||
            four.Phase == SourceFourActionPhase.RewardsPending && !four.Defeats.Any(d => d.RequiresExperience) ||
            four.Phase == SourceFourActionPhase.ActionSwitchPending && (frame.NextAction < 1 ||
                frame.PendingSwitches is not [{ Kind: WildBattleCalledSwitchKind.BatonPass, Invocation: { } }])))
            return "source_frame_four_action_continuation";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && frame.FourTurn is { } actionCursor &&
            frame.PendingSwitches.Any(p => p.Kind == WildBattleCalledSwitchKind.BatonPass))
        {
            int index = actionCursor.Phase == SourceFourActionPhase.ActionSwitchPending ? frame.NextAction - 1 : frame.NextAction;
            if (frame.PendingSwitches is not [{ Kind: WildBattleCalledSwitchKind.BatonPass, Invocation: { } invocation } pending] ||
                index < 0 || index >= frame.Queue.Length || frame.Queue[index] is not { Kind: SourceActionKind.Move, Token: { } owner } action ||
                action.Slot != pending.Slot || !frame.IsCurrent(owner.Owner) || invocation.Actor != pending.Slot ||
                invocation.Target != pending.Slot || invocation.PpOwner != pending.Slot || invocation.ChosenSlot != action.MoveSlot ||
                invocation.PpSlot != action.MoveSlot || !invocation.Flags.HasFlag(WildBattleInvocationFlags.Obeys))
                return "source_frame_action_switch_owner";
        }
        if (SourceFourRewards.Validate(frame) is { } rewardError) return rewardError;
        return null;
    }

    private static bool ValidOrder(ImmutableArray<BattlerSlot> order, int count) => !order.IsDefault && order.Length == count &&
        order.All(s => s.Value < count) && order.Distinct().Count() == count;

    private static string? ValidateRegisters(SourceBattleFrame frame, SourceBattleSlot cell)
    {
        var r = cell.Registers;
        if (!SourceHitItemRules.Valid(cell.Position.HitItemReceipt)) return "source_frame_hit_receipt";
        if (new[] { r.LeechSeedSource, r.MeanLookSource, r.LastHitBy, r.InfatuationSource, r.WrappedBy,
                r.Damage?.PhysicalSource, r.Damage?.SpecialSource, r.Turn?.SureHitSource,
                r.Turn?.Lock?.Target, r.Turn?.Lock?.StoredDamageSource, cell.Position.FutureAttack?.Attacker }
            .Any(slot => slot is { } value && value.Value >= frame.BattlerCount)) return "source_frame_register_slot";
        if (r.ChosenMoveSlot is < -1 or > 3 || r.LastMoveSlot is < -1 or > 3) return "source_frame_chosen_slot";
        if (r.History is { } history && (history.RulesVersion != WildBattleRules.CalledMoveKernelRulesVersion ||
            history.LastTakenFrom.IsDefault || history.LastTakenFrom.Length != 4 ||
            history.LastTakenFrom.Skip(frame.BattlerCount).Any(m => m is not null))) return "source_frame_history";
        if (r.Damage is { } damage && (damage.RulesVersion != WildBattleRules.CalledMoveKernelRulesVersion ||
            damage.Turn < 0 || damage.Turn > frame.Turn || damage.PhysicalDamage is < 0 or > 65535 ||
            damage.SpecialDamage is < 0 or > 65535 || damage.PhysicalDamage > 0 && damage.PhysicalSource is null ||
            damage.SpecialDamage > 0 && damage.SpecialSource is null)) return "source_frame_damage";
        if (r.Turn is { } turn && (turn.RulesVersion != WildBattleRules.CalledMoveKernelRulesVersion ||
            turn.SureHitTurnsRemaining is < 0 or > 2 || (turn.SureHitTurnsRemaining == 0) != (turn.SureHitSource is null)))
            return "source_frame_turn_registers";
        if (r.Turn?.Lock is { } locked && (!Enum.IsDefined(locked.Kind) || cell.Body is null ||
            locked.PpSlot < 0 || locked.PpSlot >= cell.Body.Pokemon.Moves.Length ||
            cell.Body.Pokemon.Moves[locked.PpSlot].Move != locked.PpSlotMove || locked.TurnsRemaining <= 0 ||
            locked.StoredDamage < 0 || locked.StoredDamage > 0 && locked.StoredDamageSource is null))
            return "source_frame_lock_identity";
        if (cell.Position.Wish is { } wish && (wish.TurnsRemaining is < 1 or > 2 || wish.SourcePartyIndex < 0 ||
            wish.SourcePartyIndex >= frame.Party(cell.Slot.Side).Members.Length)) return "source_frame_wish";
        if (cell.Position.FutureAttack is { } future && (future.TurnsRemaining is < 1 or > 3 || future.Damage < 0))
            return "source_frame_future_attack";
        return null;
    }

    internal static bool HasLegacyPointers(WildBattleCombatant body) => body.LeechSeedSource is not null ||
        body.MeanLookSource is not null || body.LastHitBy is not null || body.InfatuationSource is not null ||
        body.SourceMoveHistory is not null || body.CalledDamage is not null || body.CalledTurnState is not null;

    internal static bool MatchesPermanent(WildBattleCalledPartyMember member, WildBattleCombatant body) =>
        WildBattleSourcePartyEffects.SamePokemon(member.Pokemon, WildBattleTransformationRules.PersistentPokemon(body)) &&
        member.Status == WildBattleTransformationRules.PersistentStatus(body) && member.HeldItem == body.HeldItem &&
        member.KnockedOffItem == body.KnockedOffItem;

    public static ImmutableArray<int> LegalReserveIndices(SourceBattleFrame frame, BattleSide side)
    {
        if (Validate(frame) is { } error) throw new ArgumentException(error, nameof(frame));
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        var unavailable = frame.Slots.Where(s => s.Member is not null).Select(s => s.Member!.Value)
            .Concat(frame.Reservations.Select(r => r.Incoming)).ToHashSet();
        return frame.Party(side).Members.Select((member, index) => (member, index))
            .Where(x => !x.member.IsEgg && x.member.Pokemon.CurrentHp > 0 && !unavailable.Contains(new(side, x.index)))
            .Select(x => x.index).ToImmutableArray();
    }

    /// <summary>Atomically bind selections to current occupants. This does not authorize or execute a move recipe.</summary>
    public static SourceIdentityResult CommitSelections(SourceBattleFrame frame, long expectedRevision,
        ImmutableArray<SourceActionSelection> selections)
    {
        if (Validate(frame) is { } error) return new(frame, error);
        if (expectedRevision != frame.Revision) return new(frame, "source_selection_stale_revision");
        if (frame.Revision == long.MaxValue) return new(frame, "source_revision_exhausted");
        if (frame.Outcome != WildBattleOutcome.Ongoing || frame.Queue.Length != 0 || frame.PendingSwitches.Length != 0 ||
            frame.Reservations.Length != 0) return new(frame, "source_selection_phase");
        if (selections.IsDefault) return new(frame, "source_selection_missing");
        var bySlot = new Dictionary<BattlerSlot, SourceActionSelection>();
        var reserved = new HashSet<PartyMemberKey>();
        foreach (var selection in selections)
        {
            if (selection is null || !frame.IsCurrent(selection.Actor)) return new(frame, "source_selection_stale_occupant");
            if (!bySlot.TryAdd(selection.Actor.Slot, selection)) return new(frame, "source_selection_duplicate_actor");
            if (!frame.CanAct(selection.Actor.Slot) || selection.Kind is SourceActionKind.Cancelled || !Enum.IsDefined(selection.Kind))
                return new(frame, "source_selection_ineligible_actor");
            if (selection.Target is { } target && target.Value >= frame.BattlerCount ||
                selection.Kind == SourceActionKind.Move && selection.MoveSlot is not (>= -1 and <= 3) ||
                selection.Kind == SourceActionKind.Item && selection.Item is null) return new(frame, "source_selection_payload");
            if (selection.Kind == SourceActionKind.Move && selection.MoveSlot >= frame.Get(selection.Actor.Slot).Body!.Pokemon.Moves.Length)
                return new(frame, "source_selection_missing_move_slot");
            if (selection.Kind != SourceActionKind.Move && (selection.MoveSlot is not null || selection.Target is not null) ||
                selection.Kind != SourceActionKind.Switch && selection.Incoming is not null ||
                selection.Kind != SourceActionKind.Item && selection.Item is not null) return new(frame, "source_selection_mixed_payload");
            if (selection.Kind == SourceActionKind.Switch)
            {
                if (selection.Incoming is not { } incoming || incoming.Side != selection.Actor.Slot.Side ||
                    !LegalReserveIndices(frame, incoming.Side).Contains(incoming.Index) || !reserved.Add(incoming))
                    return new(frame, "source_selection_reserve");
            }
        }
        if (Enumerable.Range(0, frame.BattlerCount).Select(i => new BattlerSlot(i)).Any(s => frame.CanAct(s) != bySlot.ContainsKey(s)))
            return new(frame, "source_selection_missing_actor");
        var queue = ImmutableArray.CreateBuilder<SourceQueuedAction>();
        var reservations = ImmutableArray.CreateBuilder<SourceSwitchReservation>();
        for (int i = 0; i < frame.BattlerCount; i++)
        {
            var slot = new BattlerSlot(i);
            if (!bySlot.TryGetValue(slot, out var selection)) { queue.Add(new(slot, null, SourceActionKind.Cancelled, CancellationReason: "unavailable-at-selection")); continue; }
            var token = new SourceActionToken(frame.Revision, frame.EventSequence, i, selection.Actor);
            queue.Add(new(slot, token, selection.Kind, selection.MoveSlot, selection.Target, selection.Incoming, selection.Item));
            if (selection.Kind == SourceActionKind.Switch) reservations.Add(new(selection.Actor, selection.Incoming!.Value, WildBattleCalledSwitchKind.Voluntary));
        }
        return new(frame with { Queue = queue.ToImmutable(), Reservations = reservations.ToImmutable(), Revision = checked(frame.Revision + 1) }, null);
    }

    public static SourceIdentityResult Reserve(SourceBattleFrame frame, long expectedRevision, SourceSwitchReservation reservation)
    {
        if (Validate(frame) is { } error) return new(frame, error);
        if (expectedRevision != frame.Revision) return new(frame, "source_reserve_stale_revision");
        if (frame.Revision == long.MaxValue) return new(frame, "source_revision_exhausted");
        if (reservation is null || !frame.IsCurrent(reservation.Outgoing) || !Enum.IsDefined(reservation.Kind)) return new(frame, "source_reserve_stale_occupant");
        if ((reservation.Kind == WildBattleCalledSwitchKind.FaintedReplacement) !=
            (frame.Get(reservation.Outgoing.Slot).Body!.Pokemon.CurrentHp == 0)) return new(frame, "source_reserve_hp");
        if (reservation.Incoming.Side != reservation.Outgoing.Slot.Side || !LegalReserveIndices(frame, reservation.Incoming.Side).Contains(reservation.Incoming.Index) ||
            frame.Reservations.Any(r => r.Outgoing.Slot == reservation.Outgoing.Slot)) return new(frame, "source_reserve_unavailable");
        var next = frame with { Reservations = frame.Reservations.Add(reservation), Revision = checked(frame.Revision + 1) };
        return new(next, null);
    }

    /// <summary>Structural entry commit after a shared switch recipe prepared its body/registers. No entry effects, RNG,
    /// Baton Pass scalar transfers, Spikes or outcome work are simulated here.</summary>
    public static SourceIdentityResult BindReservedEntry(SourceBattleFrame frame, long expectedRevision,
        SourceSwitchReservation reservation, SourceEntryBoundary boundary, WildBattleCombatant preparedBody,
        SourceSlotRegisters preparedRegisters)
    {
        if (Validate(frame) is { } error) return new(frame, error);
        if (frame.Revision != expectedRevision) return new(frame, "source_entry_stale_revision");
        if (!frame.Reservations.Contains(reservation) || !frame.IsCurrent(reservation.Outgoing)) return new(frame, "source_entry_reservation");
        if (!Enum.IsDefined(boundary) || preparedBody is null || preparedRegisters is null) return new(frame, "source_entry_payload");
        var slot = reservation.Outgoing.Slot;
        if (frame.Revision == long.MaxValue || frame.Get(slot).EntrySerial == long.MaxValue) return new(frame, "source_entry_counter_exhausted");
        if (reservation.Kind == WildBattleCalledSwitchKind.FaintedReplacement &&
            (frame.Get(slot).Body!.Pokemon.CurrentHp != 0 || frame.KoReplacementTiming == KoReplacementTiming.EndOfTurn && boundary != SourceEntryBoundary.EndOfTurnReplacement))
            return new(frame, "source_entry_ko_boundary");
        if (reservation.Kind != WildBattleCalledSwitchKind.FaintedReplacement && boundary != SourceEntryBoundary.Action)
            return new(frame, "source_entry_immediate_switch_boundary");
        var next = ClearDepartureReferences(frame, slot, reservation.Kind == WildBattleCalledSwitchKind.BatonPass ? SourceDepartureKind.BatonPass : SourceDepartureKind.Switch);
        var old = next.Get(slot);
        var cell = old with { Member = reservation.Incoming, EntrySerial = checked(old.EntrySerial + 1), Body = preparedBody, Registers = preparedRegisters };
        next = next with { Slots = next.Slots.SetItem(slot.Value, cell),
            SourceAbsentMask = (byte)(next.SourceAbsentMask & ~slot.Mask), DeferredKoVacancyMask = (byte)(next.DeferredKoVacancyMask & ~slot.Mask),
            Queue = next.Queue.Select(a => a.Slot == slot ? a with { Kind = SourceActionKind.Cancelled, CancellationReason = "occupant-departed" } : a).ToImmutableArray(),
            Reservations = next.Reservations.Remove(reservation), PendingSwitches = next.PendingSwitches.Where(p => p.Slot != slot).ToImmutableArray(),
            Revision = checked(frame.Revision + 1) };
        next = SourceFourRewards.ObserveParticipation(next);
        return Validate(next) is { } nextError ? new(frame, nextError) : new(next, null);
    }

    /// <summary>Only reference/history lifetime operations from SwitchInClearSetData/FaintClearSetData.
    /// Scalar volatile cleanup and battle scripts belong to the shared execution kernel.</summary>
    public static SourceBattleFrame ClearDepartureReferences(SourceBattleFrame frame, BattlerSlot departed, SourceDepartureKind kind)
    {
        if (Validate(frame) is { } error) throw new ArgumentException(error, nameof(frame));
        if (departed.Value >= frame.BattlerCount || !Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(departed));
        var cells = frame.Slots;
        for (int i = 0; i < frame.BattlerCount; i++)
        {
            var cell = cells[i]; var r = cell.Registers;
            var history = r.History;
            if (history is not null) history = i == departed.Value ? new SourceMoveHistory() : history with {
                LastTakenMove = null, LastTakenFrom = history.LastTakenFrom.SetItem(departed.Value, null) };
            if (i == departed.Value)
            {
                r = r with { LeechSeedSource = kind == SourceDepartureKind.BatonPass ? r.LeechSeedSource : null,
                    MeanLookSource = kind == SourceDepartureKind.BatonPass ? r.MeanLookSource : null,
                    InfatuationSource = null, WrappedBy = null, LastHitBy = null, History = history, HelpingHand = false,
                    Turn = kind == SourceDepartureKind.BatonPass && r.Turn is { } ownTurn ? ownTurn with { Lock = null } : null };
                if (cell.Body is { } ownBody) cell = cell with { Body = ownBody with { BindTurnsRemaining = 0, BindingMove = null } };
            }
            else
            {
                var turn = r.Turn;
                if (turn?.SureHitSource == departed)
                {
                    if (kind == SourceDepartureKind.Switch) turn = turn with { SureHitSource = null, SureHitTurnsRemaining = 0 };
                    else if (kind == SourceDepartureKind.BatonPass && cell.Slot.Side != departed.Side) turn = turn with { SureHitTurnsRemaining = 2 };
                    // FaintClearSetData deliberately does not clear other battlers' sure-hit registers.
                }
                bool unwrap = r.WrappedBy == departed;
                r = r with { MeanLookSource = r.MeanLookSource == departed && kind != SourceDepartureKind.BatonPass ? null : r.MeanLookSource,
                    InfatuationSource = r.InfatuationSource == departed ? null : r.InfatuationSource,
                    WrappedBy = unwrap ? null : r.WrappedBy, History = history, Turn = turn };
                if (unwrap && cell.Body is { } body) cell = cell with { Body = body with { BindTurnsRemaining = 0, BindingMove = null } };
            }
            cells = cells.SetItem(i, cell with { Registers = r });
        }
        return frame with { Slots = cells, EntryAbilities = frame.EntryAbilities is { } entry ? entry with {
            PendingIntimidateMask = (byte)(entry.PendingIntimidateMask & ~departed.Mask) } : null };
    }

    public static string? ValidateContinuation(SourceBattleFrame frame, SourceContinuationToken token)
    {
        if (Validate(frame) is { } error) return error;
        if (token.Revision != frame.Revision || token.EventSequence != frame.EventSequence || token.NextAction != frame.NextAction ||
            token.NextAction >= frame.Queue.Length) return "source_continuation_stale";
        var action = frame.Queue[token.NextAction];
        return action.Token != token.Action || action.Kind == SourceActionKind.Cancelled || !frame.IsCurrent(token.Action.Owner)
            ? "source_continuation_occupant" : null;
    }
}
