using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public enum SourceFourActionPhase { Actions, EndTurn, RewardsPending, ReplacementsPending, BattleEnded, EndTurnResume, ActionSwitchPending }
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
/// Admitted turns include exact reward/learning and end-turn KO entry continuations.
/// This capability is not encounter, AI, all residual effects or campaign admission.</summary>
public static partial class SourceFourActionTurns
{
    public const string Version = "firered-four-action-lifetimes-v7";

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
        // Shared weather, side protections and ally support own their admitted lifetimes.
        // Existing hazards require the actual entry lifecycle; initial battles have no hazards.
        if (frame.Sides.Any(s => s.Spikes != 0) && frame.EntryAbilities is null)
            return "source_four_spikes_entry_owner_required";
        if (frame.Field != new SourceFieldRegisters { Terrain = frame.Field.Terrain, PayDayMoney = frame.Field.PayDayMoney,
                RainTurnsRemaining = frame.Field.RainTurnsRemaining, SandstormTurnsRemaining = frame.Field.SandstormTurnsRemaining,
                SunTurnsRemaining = frame.Field.SunTurnsRemaining, HailTurnsRemaining = frame.Field.HailTurnsRemaining,
                PermanentWeather = frame.Field.PermanentWeather } ||
            frame.Sides.Any(s => s != new SourceSideRegisters(s.Side) { ReflectTurnsRemaining = s.ReflectTurnsRemaining,
                LightScreenTurnsRemaining = s.LightScreenTurnsRemaining, MistTurnsRemaining = s.MistTurnsRemaining,
                SafeguardTurnsRemaining = s.SafeguardTurnsRemaining, Spikes = s.Spikes, FollowMeTimer = s.FollowMeTimer, FollowMeTarget = s.FollowMeTarget }))
            return "source_four_action_field_phase_not_migrated";
        foreach (var cell in frame.Slots)
        {
            var body = cell.Body!;
            if (frame.EntryAbilities is { } entry && entry.AppliedEntries[cell.Slot.Value] != cell.Occupant ||
                frame.EntryAbilities is null && SourceFourEntryAbilities.HasEntryAbility(body.Pokemon.AbilityId))
                return "source_four_action_initial_entry_required";
            if (SourcePokemonValidator.ValidateForBattle(catalog, body.Pokemon) is not null) return "source_four_action_pokemon";
            if (body.FirstTurnCounter is < 0 or > 2 || body.FuryCutterCounter != 0 ||
                (body.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) && !WildBattleStatus.IsSleeping(body.Status) && !WildBattleSourceFieldEffects.IsValidToxic(body.Status)) || !SourceFrameBerryItems.SupportsHeldItem(body.HeldItem) || body.CalledVolatile is not null ||
                body.SourceChoiceMove is not null || !SourceFourEntryAbilities.OverlaySupported(body) || body.Transform is not null || body.CopyMoves is not null || body.Cursed || body.Rooted ||
                body.Nightmare || body.DestinyBond || body.Imprison || body.YawnTurnsRemaining != 0 ||
                body.ChargeTurnsRemaining != 0 || body.ConfusionTurnsRemaining is < 0 or > 5 ||
                body.RolloutTurnsRemaining != 0 || body.RampageTurnsRemaining != 0 || body.DigMoveSlot is not null ||
                body.EncoreMoveSlot is not null ||
                body.Rage || body.WaterSport || body.MudSport ||
                cell.Registers.Turn is { } turn && (turn.SureHitTurnsRemaining != 0 || turn.Lock is not { } locked || !SourceBattleKernel.SupportsTurnLock(catalog, locked) || body.Pokemon.CurrentHp == 0) ||
                cell.Registers.LeechSeedSource is not null ||
                cell.Position.FocusBanded || cell.Position.Wish is not null)
                return "source_four_action_residual_or_canceller_not_migrated";
            if (SourceVolatileTargetRules.Validate(catalog, frame, cell) is { } volatileError) return volatileError;
            if (SourceProtectionRules.Validate(body) is { } protectionError) return protectionError;
            if (SourceTurnMoveRules.Validate(body, catalog) is { } turnMoveError) return turnMoveError;
            if (cell.Position.FutureAttack is { } future && (future.Move.Value is not ("pret:MOVE_FUTURE_SIGHT" or "pret:MOVE_DOOM_DESIRE") ||
                future.Damage > 65535 || !catalog.Moves.ContainsKey(future.Move))) return "source_four_action_future_contract";
        }
        return null;
    }

    /// <summary>For an admitted frame: recipe support is deliberately not a move limitation.
    /// Unsupported positive-PP moves must never earn a fallback Struggle.</summary>
    public static bool RequiresStruggle(PokemonCatalog catalog, SourceBattleFrame frame, BattlerSlot actor) =>
        actor.Value < frame.BattlerCount && frame.CanAct(actor) && frame.Get(actor).Registers.Turn?.Lock is null &&
        WildBattleRules.IsCalledMoveSelectable(catalog, frame.Get(actor).Body!, WildBattleRules.StruggleSlot);

    private static bool CleanRecipe(PokemonMoveDefinition move) => SourceBattleKernel.SupportsRecipe(move);

    public static SourceFourActionResult BeginActions(PokemonCatalog catalog, SourceBattleFrame frame,
        long expectedRevision, ImmutableArray<SourceActionSelection> selections)
    {
        if (ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.FourTurn is not null || frame.DeferredKoVacancyMask != 0) return Fail(frame, "source_four_action_start_phase");
        if (frame.EventSequence == long.MaxValue) return Fail(frame, "source_four_action_counter_exhausted");
        var committed = SourceBattleIdentityRules.CommitSelections(frame, expectedRevision, selections);
        if (!committed.Succeeded) return Fail(frame, committed.Fault!);
        var next = SourceFourRewards.ObserveParticipation(committed.State with { Rewards = committed.State.Rewards ?? new() });
        next = next with { Queue = next.Queue.Select(a => a with { OriginalChosenMove = a.Kind != SourceActionKind.Move ? null
            : a.MoveSlot == WildBattleRules.StruggleSlot ? WildBattleRules.StruggleMove
            : next.Get(a.Slot).Body!.Pokemon.Moves[a.MoveSlot!.Value].Move }).ToImmutableArray() };
        foreach (var action in next.Queue)
        {
            if (action.Kind == SourceActionKind.Cancelled) continue;
            if (action.Kind == SourceActionKind.Switch)
            {
                if (ValidateVoluntarySwitch(catalog, next, action.Slot) is { } switchError) return Fail(frame, switchError);
                continue;
            }
            if (action.Kind == SourceActionKind.Item)
            {
                if (!SourceFourMedicineRules.IsPrepared(next, action)) return Fail(frame, "source_medicine_not_prepared");
                continue;
            }
            if (action.Kind != SourceActionKind.Move || action.MoveSlot is null or < -1 || action.Target is null)
                return Fail(frame, "source_four_action_selection_not_migrated");
            bool struggle = action.MoveSlot == WildBattleRules.StruggleSlot;
            var body = next.Get(action.Slot).Body!;
            var recharge = next.Get(action.Slot).Registers.Turn?.Lock;
            if (struggle)
            {
                if (!RequiresStruggle(catalog, next, action.Slot)) return Fail(frame, "source_four_action_struggle_not_required");
                if (action.Target.Value.Side == action.Slot.Side) return Fail(frame, "source_four_action_struggle_target");
            }
            else if (recharge is null)
            {
                var learned = body.Pokemon.Moves[action.MoveSlot.Value];
                if (body.DisableTurnsRemaining > 0 && learned.Move == body.DisabledMove) return Fail(frame, "source_four_action_selection_disabled");
                if (learned.CurrentPp == 0) return Fail(frame, "source_four_action_selection_no_pp");
            }
            if (recharge is not null && (action.MoveSlot != recharge.PpSlot || action.Target != recharge.Target))
                return Fail(frame, "source_four_action_recharge_selection");
            var move = SourceBattleKernel.ActionMove(catalog, next.Get(action.Slot), action.MoveSlot.Value);
            if (recharge is null && !CleanRecipe(move)) return Fail(frame, "source_four_action_effect_phase_not_migrated");
            // Preflight never publishes target-selection RNG; execution resolves against retained order.
            var selection = struggle ? SourceMoveTargeting.ResolveCalled(next, action.Slot, move)
                : SourceMoveTargeting.ResolveLearned(next, action.Slot, action.Target.Value, move);
            if (selection.Fault is { } targetError) return Fail(frame, targetError);
            var target = selection.Target ?? (move.Effect == "EFFECT_SPIKES" ? action.Slot.Opposite : (BattlerSlot?)null);
            if (target is null) return Fail(frame, "source_four_action_selection_no_target");
            if (SourceBattleKernel.ValidateAction(catalog, next,
                new(action.Token!.Value.Owner, next.Revision, action.MoveSlot.Value, target.Value)) is { } error)
                return Fail(frame, error);
        }
        var selectionTrace = ImmutableArray.CreateBuilder<RandomTrace>();
        try
        {
            foreach (var chosen in selections.OrderBy(s => s.Actor.Slot.Value))
                if (chosen.IsStruggle && !chosen.StruggleSelectionPrepared)
                {
                    var draw = Gen3Rng.Next(next.Rng, $"source-battle.slot-{chosen.Actor.Slot.Value}.struggle-selection-target");
                    selectionTrace.Add(draw.Trace); next = next with { Rng = draw.State };
                    var target = new BattlerSlot((chosen.Actor.Slot.Value ^ 1) | (draw.Value & 2));
                    next = next with { Queue = next.Queue.Select(a => a.Slot == chosen.Actor.Slot ? a with { Target = target } : a).ToImmutableArray() };
                }
        }
        catch (OverflowException) { return Fail(frame, "source_four_action_counter_exhausted"); }
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
        return new(published.State, [published.Event], selectionTrace.ToImmutable().AddRange(sorted.Trace), null);
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
        if (cursor.Phase == SourceFourActionPhase.RewardsPending) return SourceFourRewards.Settle(catalog, frame, continuation);
        if (cursor.Phase == SourceFourActionPhase.ActionSwitchPending) return Fail(frame, "source_four_action_switch_choice_required");
        if (cursor.Phase == SourceFourActionPhase.ReplacementsPending) return Fail(frame, "source_four_action_replacement_choice_required");
        if (cursor.Phase == SourceFourActionPhase.BattleEnded) return Fail(frame, "source_four_action_battle_ended");
        try
        {
            if (cursor.Phase is SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume) return FinishEndTurn(catalog, frame);
            var action = frame.Queue[frame.NextAction];
            var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            ImmutableArray<RandomTrace> trace = [];
            var next = frame;
            if (action.Kind == SourceActionKind.Cancelled || action.Token is not { } token || !frame.IsCurrent(token.Owner) || !frame.CanAct(action.Slot))
                next = next with { Revision = checked(next.Revision + 1), Queue = next.Queue.SetItem(next.NextAction,
                    action with { Kind = SourceActionKind.Cancelled, CancellationReason = action.CancellationReason ?? "original-occupant-unavailable" }) };
            else if (action.Kind == SourceActionKind.Switch)
            {
                var switched = BindActionSwitch(catalog, frame, action.Slot, action.Incoming!.Value, WildBattleCalledSwitchKind.Voluntary);
                if (!switched.Succeeded) return Fail(frame, switched.Fault!);
                var immunity = SourceFrameImmunityRules.Apply(switched.State);
                next = immunity.State; events.AddRange(switched.Events); events.AddRange(immunity.Events); trace = switched.Trace;
            }
            else if (action.Kind == SourceActionKind.Item)
                next = SourceFourMedicineRules.Execute(frame, action);
            else
            {
                var move = SourceBattleKernel.ActionMove(catalog, frame.Get(action.Slot), action.MoveSlot!.Value);
                // Source forced/recharge selection precedes the overwritten-selected-move branch.
                bool struggle = action.MoveSlot == WildBattleRules.StruggleSlot;
                var replaced = !struggle && frame.Get(action.Slot).Registers.Turn?.Lock is null && action.OriginalChosenMove != move.Id;
                var selection = struggle ? SourceMoveTargeting.ResolveCalled(frame, action.Slot, move)
                    : replaced ? SourceMoveTargeting.ResolveReplaced(frame, action.Slot, move)
                    : SourceMoveTargeting.ResolveLearned(frame, action.Slot, action.Target!.Value, move);
                if (selection.Fault is { } targetError) return Fail(frame, targetError);
                var target = selection.Target ?? (move.Effect == "EFFECT_SPIKES" ? action.Slot.Opposite : (BattlerSlot?)null);
                SourceKernelResult result;
                if (target is null)
                {
                    if (!CleanRecipe(move)) return Fail(frame, "source_four_action_effect_phase_not_migrated");
                    var noTarget = new FrameRecipeContext(catalog, frame with { Rng = selection.Rng },
                        new(token.Owner, frame.Revision, action.MoveSlot.Value, replaced ? action.Slot.Opposite : action.Target!.Value, frame.NextAction));
                    noTarget.RunNoTarget();
                    result = new(noTarget.Frame, noTarget.Events.ToImmutable(), noTarget.Trace.ToImmutable(), noTarget.Invocation, null);
                }
                else result = SourceBattleKernel.ResolveAction(catalog, frame with { Rng = selection.Rng },
                    new(token.Owner, frame.Revision, action.MoveSlot.Value, target.Value, frame.NextAction) {
                        LightningRodRedirected = selection.LightningRodRedirected });
                if (!result.Succeeded) return Fail(frame, result.Fault!);
                next = result.State; trace = selection.Trace.AddRange(result.Trace); events.AddRange(result.Events);
                // noValidMoves is cleared before cancellation. End-turn sorting returns to the
                // retained real chosen position, including when sleep prevents Struggle.
                if (struggle) next = next with { Queue = next.Queue.SetItem(frame.NextAction,
                    next.Queue[frame.NextAction] with { MoveSlot = frame.Get(action.Slot).Registers.LastMoveSlot }) };
                foreach (var faint in result.Events.OfType<SourceFaintedEvent>())
                {
                    var defeated = next.Get(faint.Side).Occupant!.Value;
                    // Entry hazards already register the newly bound occupant, never the departing one.
                    if (next.FourTurn!.Defeats.Any(d => d.Occupant == defeated)) continue;
                    if (next.Get(faint.Side).Body!.Pokemon.CurrentHp != 0) return Fail(frame, "source_four_action_faint_contract");
                    next = next with { DeferredKoVacancyMask = (byte)(next.DeferredKoVacancyMask | faint.Side.Mask),
                        Queue = next.Queue.Select(a => a.Token?.Owner == defeated ? a with {
                            Kind = SourceActionKind.Cancelled, MoveSlot = a.MoveSlot == WildBattleRules.StruggleSlot
                                ? frame.Get(faint.Side).Registers.LastMoveSlot : a.MoveSlot,
                            CancellationReason = "original-occupant-fainted" } : a).ToImmutableArray(),
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
            var phase = !next.PendingSwitches.IsEmpty ? SourceFourActionPhase.ActionSwitchPending : HasPendingRewards(next) ? SourceFourActionPhase.RewardsPending :
                next.Outcome != WildBattleOutcome.Ongoing ? SourceFourActionPhase.BattleEnded :
                next.NextAction == next.Queue.Length ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions;
            next = next with { FourTurn = next.FourTurn! with { Phase = phase } };
            // The source fainted-action callbacks run after each complete action. Pending
            // actual KO entries defer their settlement to the approved end-turn boundary.
            if (next.Outcome == WildBattleOutcome.Ongoing && next.PendingSwitches.IsEmpty && !HasPendingRewards(next) &&
                (next.Slots.Any(c => c.Occupant != frame.Get(c.Slot).Occupant) ||
                 !next.Slots.Any(c => next.IsDeferredKoVacant(c.Slot) &&
                    !SourceBattleIdentityRules.LegalReserveIndices(next, c.Slot.Side).IsEmpty)))
            {
                var settled = SourceFourEntryAbilities.Settle(next);
                next = settled.State; events.AddRange(settled.Events); trace = trace.AddRange(settled.Trace);
            }
            return new(next, events.ToImmutable(), trace, null);
        }
        catch (SourceRecipeUnsupported ex) { return Fail(frame, ex.Message); }
        catch (OverflowException) { return Fail(frame, "source_four_action_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame, "source_four_action_invalid_transition"); }
    }

    internal static bool HasPendingRewards(SourceBattleFrame frame) => frame.FourTurn!.Defeats.Any(d => d.RequiresExperience);

    private static SourceFourActionResult FinishEndTurn(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (frame.NextAction != frame.Queue.Length || frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn!.EndTurn?.Stage == WildBattleSourceEndTurnStage.Complete)
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
            next = PrepareReplacements(next);
            if (!next.PendingSwitches.IsEmpty)
            {
                foreach (var choice in next.PendingSwitches)
                {
                    var item = SourceBattleEventHooks.Publish(next, (n, t) => new SourceCalledPartySwitchRequiredEvent(n, t, choice));
                    next = item.State; events.Add(item.Event);
                }
                return Pause(SourceFourActionPhase.ReplacementsPending);
            }
        }
        var delayed = new FrameEndTurnContext(next);
        cursor = delayed.Advance(catalog, cursor.Order, cursor);
        next = delayed.Frame; events.AddRange(delayed.Events); trace.AddRange(delayed.Trace);
        if (HasPendingRewards(next)) return Pause(SourceFourActionPhase.RewardsPending);
        if (next.Outcome != WildBattleOutcome.Ongoing) return Pause(SourceFourActionPhase.BattleEnded);
        if (cursor.Stage != WildBattleSourceEndTurnStage.Complete) return Pause(SourceFourActionPhase.EndTurnResume);
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
