using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public enum SourceFourActionPhase { Actions, EndTurn, RewardsPending, ReplacementsPending, BattleEnded }
public sealed record SourceDefeatedOccupant(OccupantToken Occupant, long FaintEventSequence, bool RequiresExperience);
public sealed record SourceFourActionCursor(int Turn, SourceFourActionPhase Phase,
    ImmutableArray<SourceDefeatedOccupant> Defeats)
{
    public string Version { get; init; } = SourceFourActionTurns.Version;
    public ImmutableArray<BattlerSlot> EndTurnOrder { get; init; } = [];
    // Terminal party state cannot bypass the outstanding source experience boundary.
    public WildBattleOutcome? OutcomeAfterRewards { get; init; }
}
public readonly record struct SourceTurnContinuation(long Revision, long EventSequence, int NextAction,
    SourceFourActionPhase Phase);
public sealed record SourceFourActionResult(SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events,
    ImmutableArray<RandomTrace> Trace, string? Fault)
{
    public bool Succeeded => Fault is null;
    public bool CompletedCleanTurn => Succeeded && State.FourTurn is null;
    public SourceTurnContinuation? Continuation => State.FourTurn is { } cursor
        ? new(State.Revision, State.EventSequence, State.NextAction, cursor.Phase) : null;
}

/// <summary>A bounded, real four-action owner. Uses the one shared recipe kernel, never paired singles.
/// Clean non-KO turns finish. KO reward/entry integration remains an explicit resumable boundary;
/// this capability is not encounter, AI, full residual or campaign admission.</summary>
public static class SourceFourActionTurns
{
    public const string Version = "firered-four-action-clean-turn-v1";

    public static string? ValidateCapability(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (catalog is null || frame is null) return "source_four_action_missing_input";
        if (SourceBattleIdentityRules.Validate(frame) is { } invalid) return invalid;
        if (frame.Layout != SourceBattleLayout.PresetDoubles || frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version ||
            frame.RulesVersion != WildBattleRules.FuchsiaRulesVersion || SourceMechanicsCatalog.For(catalog) is null)
            return "source_four_action_capability";
        if (frame.BattleKind != WildBattleCalledBattleKind.Trainer) return "source_four_action_battle_kind";
        if (frame.Slots.Any(c => c.Body is null)) return "source_four_action_body_required";
        // Positive admission: later field additions (for example permanent weather) are nonempty
        // until their shared end-turn phase is deliberately enabled here.
        if (frame.Field != new SourceFieldRegisters { Terrain = frame.Field.Terrain, PayDayMoney = frame.Field.PayDayMoney } ||
            frame.Sides.Any(s => s != new SourceSideRegisters(s.Side)))
            return "source_four_action_field_phase_not_migrated";
        foreach (var cell in frame.Slots)
        {
            var body = cell.Body!;
            if (SourcePokemonValidator.ValidateForBattle(catalog, body.Pokemon) is not null) return "source_four_action_pokemon";
            if (body.FirstTurnCounter is < 0 or > 2 || body.ProtectConsecutiveUses != 0 || body.FuryCutterCounter != 0 ||
                body.Status != WildBattleStatus.None || body.HeldItem != "ITEM_NONE" || body.CalledVolatile is not null ||
                body.CalledOverlay is not null || body.Transform is not null || body.CopyMoves is not null || body.Cursed || body.Rooted ||
                body.Nightmare || body.DestinyBond || body.Imprison || body.TruantLoafing || body.YawnTurnsRemaining != 0 ||
                body.ChargeTurnsRemaining != 0 || body.BindTurnsRemaining != 0 || body.ConfusionTurnsRemaining != 0 ||
                body.RolloutTurnsRemaining != 0 || body.RampageTurnsRemaining != 0 || body.DigMoveSlot is not null ||
                body.EncoreMoveSlot is not null || body.DisabledMove is not null || body.Protected || body.Enduring || body.Flinched ||
                body.Rage || body.WaterSport || body.MudSport || cell.Registers.Turn is not null ||
                cell.Registers.LeechSeedSource is not null || cell.Registers.MeanLookSource is not null ||
                cell.Registers.InfatuationSource is not null || cell.Position.Wish is not null || cell.Position.FutureAttack is not null)
                return "source_four_action_residual_or_canceller_not_migrated";
            if (WildBattleEffective.Ability(body) is "ABILITY_SPEED_BOOST" or "ABILITY_TRUANT" or "ABILITY_STATIC")
                return "source_four_action_ability_phase_not_migrated";
        }
        return null;
    }

    // Each omitted source effect can introduce an unported canceller/residual in this same turn.
    // This restricts selected capabilities, not the authentic catalog or a Pokemon's unselected moves.
    private static bool CleanRecipe(PokemonMoveDefinition move) => SourceBattleKernel.SupportsRecipe(move) && move.Effect is not
        ("EFFECT_BURN_HIT" or "EFFECT_PARALYZE_HIT" or "EFFECT_POISON_HIT" or "EFFECT_CONFUSE_HIT" or
         "EFFECT_POISON" or "EFFECT_SLEEP" or "EFFECT_SWAGGER" or "EFFECT_FLATTER");

    public static SourceFourActionResult BeginActions(PokemonCatalog catalog, SourceBattleFrame frame,
        long expectedRevision, ImmutableArray<SourceActionSelection> selections)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.FourTurn is not null || frame.DeferredKoVacancyMask != 0) return Fail(frame, "source_four_action_start_phase");
        if (frame.EventSequence == long.MaxValue) return Fail(frame, "source_four_action_counter_exhausted");
        var committed = SourceBattleIdentityRules.CommitSelections(frame, expectedRevision, selections);
        if (!committed.Succeeded) return Fail(frame, committed.Fault!);
        var next = committed.State;
        foreach (var action in next.Queue)
        {
            if (action.Kind == SourceActionKind.Cancelled) continue;
            if (action.Kind != SourceActionKind.Move || action.MoveSlot is null or < 0 || action.Target is null)
                return Fail(frame, "source_four_action_selection_not_migrated");
            var learned = next.Get(action.Slot).Body!.Pokemon.Moves[action.MoveSlot.Value];
            if (learned.CurrentPp == 0) return Fail(frame, "source_four_action_selection_no_pp");
            var move = catalog.Moves[learned.Move];
            if (!CleanRecipe(move)) return Fail(frame, "source_four_action_effect_phase_not_migrated");
            var target = ResolveSelectedTarget(next, action.Slot, action.Target.Value, move);
            if (target is null) return Fail(frame, "source_four_action_selection_no_target");
            if (SourceBattleKernel.ValidateAction(catalog, next,
                new(action.Token!.Value.Owner, next.Revision, action.MoveSlot.Value, target.Value)) is { } error)
                return Fail(frame, error);
        }
        next = next with { Slots = next.Slots.Select(c => {
            var choice = next.Queue.Single(a => a.Slot == c.Slot);
            return c with { Registers = c.Registers with { ChosenMoveSlot = choice.MoveSlot,
                LastMoveSlot = choice.MoveSlot is >= 0 ? choice.MoveSlot.Value : c.Registers.LastMoveSlot } };
        }).ToImmutableArray() };
        var sorted = SourceBattleTurnOrder.Resolve(catalog, next, WildBattleOrderPhase.Actions);
        if (sorted.Fault is { } fault) return Fail(frame, fault);
        next = next with { Queue = sorted.Order.Select(s => next.Queue.Single(a => a.Slot == s)).ToImmutableArray(),
            Rng = sorted.Rng, NextAction = 0, TurnStartAbsentMask = next.SourceAbsentMask,
            FourTurn = new(next.Turn, SourceFourActionPhase.Actions, []) };
        var published = SourceBattleEventHooks.Publish(next, (n, t) => new SourceOrderEvent(n, t, WildBattleOrderPhase.Actions, sorted.Order));
        return new(published.State, [published.Event], sorted.Trace, null);
    }

    /// <summary>Source absent-position fallback, with deferred KO vacancies applied only at this
    /// action boundary. This policy consequence is separate from original FR's immediate replacement.</summary>
    public static BattlerSlot? ResolveSelectedTarget(SourceBattleFrame frame, BattlerSlot actor,
        BattlerSlot selected, PokemonMoveDefinition move)
    {
        if (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED") return frame.CanAct(actor) ? actor : null;
        if (move.Target != "MOVE_TARGET_SELECTED" || selected == actor) return null;
        if (frame.CanAct(selected)) return selected;
        var fallback = selected.Side != actor.Side ? selected.Partner : actor.Opposite;
        if (frame.CanAct(fallback)) return fallback;
        if (selected.Side == actor.Side && frame.CanAct(fallback.Partner)) return fallback.Partner;
        return null;
    }

    public static SourceFourActionResult Advance(PokemonCatalog catalog, SourceBattleFrame frame, SourceTurnContinuation continuation)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.FourTurn is not { } cursor || continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase))
            return Fail(frame, "source_four_action_stale_continuation");
        if (cursor.Phase == SourceFourActionPhase.RewardsPending) return Fail(frame, "source_four_action_experience_continuation_not_migrated");
        if (cursor.Phase == SourceFourActionPhase.ReplacementsPending) return Fail(frame, "source_four_action_entry_continuation_not_migrated");
        if (cursor.Phase == SourceFourActionPhase.BattleEnded) return Fail(frame, "source_four_action_battle_ended");
        try
        {
            if (cursor.Phase == SourceFourActionPhase.EndTurn) return FinishCleanTurn(catalog, frame);
            var action = frame.Queue[frame.NextAction];
            var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            ImmutableArray<RandomTrace> trace = [];
            var next = frame;
            if (action.Kind == SourceActionKind.Cancelled || action.Token is not { } token || !frame.IsCurrent(token.Owner) || !frame.CanAct(action.Slot))
                next = next with { Revision = checked(next.Revision + 1), Queue = next.Queue.SetItem(next.NextAction,
                    action with { Kind = SourceActionKind.Cancelled, CancellationReason = action.CancellationReason ?? "original-occupant-unavailable" }) };
            else
            {
                var move = catalog.Moves[frame.Get(action.Slot).Body!.Pokemon.Moves[action.MoveSlot!.Value].Move];
                var target = ResolveSelectedTarget(frame, action.Slot, action.Target!.Value, move);
                if (target is null)
                {
                    // No eligible target: do not invent a target, replacement, random reroll or damage.
                    // PP semantics for this new vacancy branch remain unsupported; reject atomically.
                    return Fail(frame, "source_four_action_no_target_continuation_not_migrated");
                }
                var result = SourceBattleKernel.ResolveAction(catalog, frame,
                    new(token.Owner, frame.Revision, action.MoveSlot.Value, target.Value, frame.NextAction));
                if (!result.Succeeded) return Fail(frame, result.Fault!);
                next = result.State; trace = result.Trace; events.AddRange(result.Events);
                foreach (var faint in result.Events.OfType<SourceFaintedEvent>())
                {
                    var defeated = frame.Get(faint.Side).Occupant!.Value;
                    if (next.Get(faint.Side).Body!.Pokemon.CurrentHp != 0) return Fail(frame, "source_four_action_faint_contract");
                    next = next with { DeferredKoVacancyMask = (byte)(next.DeferredKoVacancyMask | faint.Side.Mask),
                        Queue = next.Queue.Select(a => a.Token?.Owner == defeated ? a with {
                            Kind = SourceActionKind.Cancelled, CancellationReason = "original-occupant-fainted" } : a).ToImmutableArray(),
                        FourTurn = next.FourTurn! with { Defeats = next.FourTurn.Defeats.Add(new(defeated, faint.Sequence, faint.Side.Side == BattleSide.Opponent)) } };
                }
            }
            next = next with { NextAction = frame.NextAction + 1, FourTurn = next.FourTurn! with {
                Phase = frame.NextAction + 1 == frame.Queue.Length ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions } };
            bool playerAlive = next.Party(BattleSide.Player).Members.Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
            bool opponentAlive = next.Party(BattleSide.Opponent).Members.Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
            if (!playerAlive || !opponentAlive)
            {
                var outcome = !playerAlive && !opponentAlive ? WildBattleOutcome.Draw : playerAlive ? WildBattleOutcome.Won : WildBattleOutcome.Lost;
                if (next.FourTurn!.Defeats.Any(d => d.RequiresExperience))
                    next = next with { FourTurn = next.FourTurn with { OutcomeAfterRewards = outcome } };
                else
                {
                    next = next with { Outcome = outcome };
                    var ended = SourceBattleEventHooks.Publish(next, (n, t) => new SourceEndedEvent(n, t, outcome));
                    next = ended.State; events.Add(ended.Event);
                }
            }
            var phase = next.FourTurn!.Defeats.Any(d => d.RequiresExperience) ? SourceFourActionPhase.RewardsPending :
                next.Outcome != WildBattleOutcome.Ongoing ? SourceFourActionPhase.BattleEnded :
                next.NextAction == next.Queue.Length ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions;
            next = next with { FourTurn = next.FourTurn with { Phase = phase } };
            return new(next, events.ToImmutable(), trace, null);
        }
        catch (OverflowException) { return Fail(frame, "source_four_action_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame, "source_four_action_invalid_transition"); }
    }

    private static SourceFourActionResult FinishCleanTurn(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (frame.NextAction != frame.Queue.Length || frame.Outcome != WildBattleOutcome.Ongoing)
            return Fail(frame, "source_four_action_end_turn_phase");
        var order = SourceBattleTurnOrder.Resolve(catalog, frame, WildBattleOrderPhase.EndTurn);
        if (order.Fault is { } fault) return Fail(frame, fault);
        var next = frame with { Rng = order.Rng, FourTurn = frame.FourTurn! with { EndTurnOrder = order.Order } };
        var published = SourceBattleEventHooks.Publish(next, (n, t) => new SourceOrderEvent(n, t, WildBattleOrderPhase.EndTurn, order.Order));
        next = published.State;
        var events = ImmutableArray.CreateBuilder<SourceBattleEvent>(); events.Add(published.Event);
        // Every field/battler/delayed callback is proven empty by capability preflight. The damage
        // registers expire after the delayed phase, exactly at the existing shared owner's boundary.
        next = next with { Slots = next.Slots.Select(c => c with { Registers = c.Registers with { Damage = null } }).ToImmutableArray() };
        if (next.DeferredKoVacancyMask != 0)
        {
            var pending = ImmutableArray.CreateBuilder<SourcePendingSwitch>();
            foreach (var side in new[] { BattleSide.Player, BattleSide.Opponent })
            {
                var legal = SourceBattleIdentityRules.LegalReserveIndices(next, side);
                // Present at most as many choices as real reserves, in original numeric slot order.
                foreach (var cell in next.Slots.Where(c => c.Slot.Side == side && next.IsDeferredKoVacant(c.Slot)).Take(legal.Length))
                    pending.Add(new(cell.Slot, WildBattleCalledSwitchKind.FaintedReplacement, legal, null));
            }
            next = next with { PendingSwitches = pending.ToImmutable(), Revision = checked(frame.Revision + 1),
                FourTurn = next.FourTurn! with { Phase = SourceFourActionPhase.ReplacementsPending } };
            foreach (var choice in next.PendingSwitches)
            {
                var item = SourceBattleEventHooks.Publish(next, (n, t) => new SourceCalledPartySwitchRequiredEvent(n, t, choice));
                next = item.State; events.Add(item.Event);
            }
            return new(next, events.ToImmutable(), order.Trace, null);
        }
        next = next with { Slots = next.Slots.Select(c => c with {
            Body = SourceTurnBoundaryRules.ReadyBody(c.Body!, true, true, true),
            Registers = c.Registers with { ChosenMoveSlot = null } }).ToImmutableArray() };
        var draw = Gen3Rng.Next(next.Rng, "wild-battle.turn-end.random-turn-number");
        next = next with { Rng = draw.State, Turn = Math.Min(255, next.Turn + 1), RandomTurnNumber = draw.Value,
            Queue = [], NextAction = 0, FourTurn = null, Revision = checked(frame.Revision + 1), TurnStartAbsentMask = next.SourceAbsentMask };
        var ready = SourceBattleEventHooks.Publish(next, (n, t) => new SourceTurnReadyEvent(n, t, draw.Value));
        events.Add(ready.Event);
        return new(ready.State, events.ToImmutable(), order.Trace.Add(draw.Trace), null);
    }

    private static SourceFourActionResult Fail(SourceBattleFrame state, string error) => new(state, [], [], error);
}
