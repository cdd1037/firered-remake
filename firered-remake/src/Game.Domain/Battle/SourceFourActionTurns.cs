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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceEndTurnCursor? EndTurn { get; init; }
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
/// Admitted residual turns finish. KO reward/entry integration remains an explicit resumable boundary;
/// this capability is not encounter, AI, all residual effects or campaign admission.</summary>
public static class SourceFourActionTurns
{
    public const string Version = "firered-four-action-shared-end-turn-v2";

    public static string? ValidateCapability(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (catalog is null || frame is null) return "source_four_action_missing_input";
        if (SourceBattleIdentityRules.Validate(frame) is { } invalid) return invalid;
        if (frame.Layout != SourceBattleLayout.PresetDoubles || frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version ||
            frame.RulesVersion != WildBattleRules.FuchsiaRulesVersion || SourceMechanicsCatalog.For(catalog) is null)
            return "source_four_action_capability";
        if (frame.BattleKind != WildBattleCalledBattleKind.Trainer) return "source_four_action_battle_kind";
        if (new[] { frame.Field.RainTurnsRemaining, frame.Field.SandstormTurnsRemaining, frame.Field.SunTurnsRemaining, frame.Field.HailTurnsRemaining }.Count(t => t > 0) > 1)
            return "source_four_action_weather_conflict";
        if (frame.Slots.Any(c => c.Body is null)) return "source_four_action_body_required";
        // Positive admission: later field additions (for example permanent weather) are nonempty
        // until their shared end-turn phase is deliberately enabled here.
        if (frame.Field != new SourceFieldRegisters { Terrain = frame.Field.Terrain, PayDayMoney = frame.Field.PayDayMoney,
                RainTurnsRemaining = frame.Field.RainTurnsRemaining, SandstormTurnsRemaining = frame.Field.SandstormTurnsRemaining,
                SunTurnsRemaining = frame.Field.SunTurnsRemaining, HailTurnsRemaining = frame.Field.HailTurnsRemaining } ||
            frame.Sides.Any(s => s != new SourceSideRegisters(s.Side)))
            return "source_four_action_field_phase_not_migrated";
        foreach (var cell in frame.Slots)
        {
            var body = cell.Body!;
            if (SourcePokemonValidator.ValidateForBattle(catalog, body.Pokemon) is not null) return "source_four_action_pokemon";
            if (body.FirstTurnCounter is < 0 or > 2 || body.ProtectConsecutiveUses != 0 || body.FuryCutterCounter != 0 ||
                (body.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn) && !WildBattleSourceFieldEffects.IsValidToxic(body.Status)) || body.HeldItem != "ITEM_NONE" || body.CalledVolatile is not null ||
                body.CalledOverlay is not null || body.Transform is not null || body.CopyMoves is not null || body.Cursed || body.Rooted ||
                body.Nightmare || body.DestinyBond || body.Imprison || body.TruantLoafing || body.YawnTurnsRemaining != 0 ||
                body.ChargeTurnsRemaining != 0 || body.BindTurnsRemaining != 0 || body.ConfusionTurnsRemaining != 0 ||
                body.RolloutTurnsRemaining != 0 || body.RampageTurnsRemaining != 0 || body.DigMoveSlot is not null ||
                body.EncoreMoveSlot is not null || body.DisabledMove is not null || body.Protected || body.Enduring || body.Flinched ||
                body.Rage || body.WaterSport || body.MudSport || cell.Registers.Turn is not null ||
                cell.Registers.LeechSeedSource is not null || cell.Registers.MeanLookSource is not null ||
                cell.Registers.InfatuationSource is not null || cell.Position.Wish is not null || cell.Position.FutureAttack is not null)
                return "source_four_action_residual_or_canceller_not_migrated";
            if (WildBattleEffective.Ability(body) is "ABILITY_TRUANT" or "ABILITY_STATIC" or "ABILITY_TRACE" or "ABILITY_FORECAST")
                return "source_four_action_ability_phase_not_migrated";
        }
        return null;
    }

    // Each omitted source effect can introduce an unported canceller/residual in this same turn.
    // This restricts selected capabilities, not the authentic catalog or a Pokemon's unselected moves.
    private static bool CleanRecipe(PokemonMoveDefinition move) => SourceBattleKernel.SupportsRecipe(move) && move.Effect is not
        ("EFFECT_PARALYZE_HIT" or "EFFECT_CONFUSE_HIT" or
         "EFFECT_SLEEP" or "EFFECT_SWAGGER" or "EFFECT_FLATTER");

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
            // Preflight never publishes target-selection RNG; execution resolves against retained order.
            var selection = SourceMoveTargeting.ResolveLearned(next, action.Slot, action.Target.Value, move);
            if (selection.Fault is { } targetError) return Fail(frame, targetError);
            var target = selection.Target;
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
        if (move.Target is not ("MOVE_TARGET_SELECTED" or "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED")) return null;
        var resolved = SourceMoveTargeting.ResolveLearned(frame, actor, selected, move);
        return resolved.Succeeded ? resolved.Target : null;
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
            if (cursor.Phase == SourceFourActionPhase.EndTurn) return FinishEndTurn(catalog, frame);
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
                var selection = SourceMoveTargeting.ResolveLearned(frame, action.Slot, action.Target!.Value, move);
                if (selection.Fault is { } targetError) return Fail(frame, targetError);
                var target = selection.Target;
                if (target is null)
                    return Fail(frame, "source_four_action_no_target_continuation_not_migrated");
                var result = SourceBattleKernel.ResolveAction(catalog, frame with { Rng = selection.Rng },
                    new(token.Owner, frame.Revision, action.MoveSlot.Value, target.Value, frame.NextAction) {
                        LightningRodRedirected = selection.LightningRodRedirected });
                if (!result.Succeeded) return Fail(frame, result.Fault!);
                next = result.State; trace = selection.Trace.AddRange(result.Trace); events.AddRange(result.Events);
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
                if (HasPendingRewards(next))
                    next = next with { FourTurn = next.FourTurn! with { OutcomeAfterRewards = outcome } };
                else
                {
                    next = next with { Outcome = outcome };
                    var ended = SourceBattleEventHooks.Publish(next, (n, t) => new SourceEndedEvent(n, t, outcome));
                    next = ended.State; events.Add(ended.Event);
                }
            }
            var phase = HasPendingRewards(next) ? SourceFourActionPhase.RewardsPending :
                next.Outcome != WildBattleOutcome.Ongoing ? SourceFourActionPhase.BattleEnded :
                next.NextAction == next.Queue.Length ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions;
            next = next with { FourTurn = next.FourTurn! with { Phase = phase } };
            return new(next, events.ToImmutable(), trace, null);
        }
        catch (SourceRecipeUnsupported ex) { return Fail(frame, ex.Message); }
        catch (OverflowException) { return Fail(frame, "source_four_action_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame, "source_four_action_invalid_transition"); }
    }

    internal static bool HasPendingRewards(SourceBattleFrame frame) => frame.FourTurn!.Defeats.Any(d => d.RequiresExperience);

    private static SourceFourActionResult FinishEndTurn(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (frame.NextAction != frame.Queue.Length || frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn!.EndTurn is not null)
            return Fail(frame, "source_four_action_end_turn_phase");
        var next = frame;
        var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var cursor = frame.FourTurn!.EndTurn;
        if (cursor is null)
        {
            var preparation = new FrameEndTurnContext(next); preparation.Prepare(); next = preparation.Frame;
            var order = SourceBattleTurnOrder.Resolve(catalog, next, WildBattleOrderPhase.EndTurn);
            if (order.Fault is { } fault) return Fail(frame, fault);
            next = next with { Rng = order.Rng };
            var published = SourceBattleEventHooks.Publish(next, (n, t) => new SourceOrderEvent(n, t, WildBattleOrderPhase.EndTurn, order.Order));
            next = published.State; events.Add(published.Event); trace.AddRange(order.Trace);
            var normal = new FrameEndTurnContext(next);
            cursor = normal.Advance(catalog, order.Order);
            next = normal.Frame; events.AddRange(normal.Events); trace.AddRange(normal.Trace);
        }
        // HandleFaintedMonActions is after every ordinary field/battler residual and before
        // delayed scripts. The cursor prevents replay while the actual owner supplies EXP/entry.
        if (HasPendingRewards(next))
            return Pause(SourceFourActionPhase.RewardsPending);
        if (next.DeferredKoVacancyMask != 0 && next.Outcome == WildBattleOutcome.Ongoing)
        {
            var pending = ImmutableArray.CreateBuilder<SourcePendingSwitch>();
            foreach (var side in new[] { BattleSide.Player, BattleSide.Opponent })
            {
                var legal = SourceBattleIdentityRules.LegalReserveIndices(next, side);
                foreach (var cell in next.Slots.Where(c => c.Slot.Side == side && next.IsDeferredKoVacant(c.Slot)).Take(legal.Length))
                    pending.Add(new(cell.Slot, WildBattleCalledSwitchKind.FaintedReplacement, legal, null));
            }
            next = next with { PendingSwitches = pending.ToImmutable(), FourTurn = next.FourTurn! with {
                Phase = SourceFourActionPhase.ReplacementsPending, EndTurnOrder = cursor.Order, EndTurn = cursor } };
            foreach (var choice in next.PendingSwitches)
            {
                var item = SourceBattleEventHooks.Publish(next, (n, t) => new SourceCalledPartySwitchRequiredEvent(n, t, choice));
                next = item.State; events.Add(item.Event);
            }
            return Pause(SourceFourActionPhase.ReplacementsPending);
        }
        var delayed = new FrameEndTurnContext(next);
        cursor = delayed.Advance(catalog, cursor.Order, cursor);
        next = delayed.Frame; events.AddRange(delayed.Events); trace.AddRange(delayed.Trace);
        if (cursor.Stage != WildBattleSourceEndTurnStage.Complete)
            throw new SourceRecipeUnsupported("source_four_action_delayed_callback_not_migrated");
        if (next.Outcome != WildBattleOutcome.Ongoing) return Pause(SourceFourActionPhase.BattleEnded);
        next = next with { Slots = next.Slots.Select(c => c with {
            Body = SourceTurnBoundaryRules.ReadyBody(c.Body!, true, true, true),
            Registers = c.Registers with { ChosenMoveSlot = null } }).ToImmutableArray() };
        var draw = Gen3Rng.Next(next.Rng, "wild-battle.turn-end.random-turn-number");
        next = next with { Rng = draw.State, Turn = Math.Min(255, next.Turn + 1), RandomTurnNumber = draw.Value,
            Queue = [], NextAction = 0, FourTurn = null, Revision = checked(frame.Revision + 1), TurnStartAbsentMask = next.SourceAbsentMask };
        var ready = SourceBattleEventHooks.Publish(next, (n, t) => new SourceTurnReadyEvent(n, t, draw.Value));
        events.Add(ready.Event); trace.Add(draw.Trace);
        return new(ready.State, events.ToImmutable(), trace.ToImmutable(), null);

        SourceFourActionResult Pause(SourceFourActionPhase phase)
        {
            next = next with { Revision = checked(frame.Revision + 1), FourTurn = next.FourTurn! with { Phase = phase, EndTurnOrder = cursor.Order,
                EndTurn = cursor with { MinimumRevision = checked(frame.Revision + 1), MinimumEventSequence = next.EventSequence } } };
            return new(next, events.ToImmutable(), trace.ToImmutable(), null);
        }
    }

    private static SourceFourActionResult Fail(SourceBattleFrame state, string error) => new(state, [], [], error);
}
