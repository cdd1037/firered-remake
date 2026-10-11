using Game.Domain.Battle;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum WildBattleCalledLockKind { Charge, Recharge, Rampage, Rollout, Uproar, Bide }
public enum WildBattleSemiInvulnerable { None, Air, Underground, Underwater }

/// <summary>gLockedMoves and the real PP position are independent of the learned move identity.</summary>
public sealed record WildBattleCalledTurnLock(WildBattleCalledLockKind Kind, MoveId Move, int PpSlot,
    MoveId PpSlotMove, WildBattleSide Target, int TurnsRemaining)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int StoredDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? StoredDamageSource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool UnableToUseMove { get; init; }
}

/// <summary>Opt-in source status2/status3 extension. Null preserves historical snapshot bytes.</summary>
public sealed record WildBattleCalledTurnState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledTurnLock? Lock { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SureHitTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? SureHitSource { get; init; }
}

public static partial class WildBattleRules
{
    private static bool HasCalledTurnLockRecipe(string effect) => effect is "EFFECT_RAZOR_WIND" or
        "EFFECT_SEMI_INVULNERABLE" or "EFFECT_RECHARGE" or "EFFECT_SOLAR_BEAM" or "EFFECT_RAMPAGE" or
        "EFFECT_EARTHQUAKE" or "EFFECT_BIDE" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK" or
        "EFFECT_LOCK_ON" or "EFFECT_FURY_CUTTER" or "EFFECT_UPROAR" or "EFFECT_ROLLOUT" or "EFFECT_SKY_UPPERCUT" or "EFFECT_GUST" or "EFFECT_TWISTER";

    private static void AddCalledTurnLockSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledTurnLockRecipe(move.Effect)) supported = true;
    }

    public static WildBattleSemiInvulnerable CalledSemiInvulnerable(WildBattleCombatant mon) =>
        mon.CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge ? SourceChargeRules.SemiInvulnerable(charge.Move)
            : mon.DigMoveSlot is not null ? WildBattleSemiInvulnerable.Underground : WildBattleSemiInvulnerable.None;

    private static string? ValidateCalledTurnLockState(PokemonCatalog catalog, WildBattleState state)
    {
        foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
        {
            var mon = side == WildBattleSide.Player ? state.Player : state.Opponent;
            if (mon.CalledTurnState is not { } turn) continue;
            if (turn.RulesVersion != CalledMoveKernelRulesVersion) return "called_turn_state_version";
            if (turn.SureHitTurnsRemaining is < 0 or > 2 || (turn.SureHitTurnsRemaining == 0) != (turn.SureHitSource is null) ||
                turn.SureHitSource is { } source && (!Enum.IsDefined(source) || source == side)) return "called_turn_sure_hit";
            if (mon.Pokemon.CurrentHp == 0 && (turn.Lock is not null || turn.SureHitTurnsRemaining != 0)) return "called_turn_fainted";
            if (turn.Lock is not { } locked) continue;
            if (!Enum.IsDefined(locked.Kind) || !Enum.IsDefined(locked.Target) || locked.PpSlot < 0 || locked.PpSlot >= mon.Pokemon.Moves.Length ||
                mon.Pokemon.Moves[locked.PpSlot].Move != locked.PpSlotMove || !catalog.Moves.TryGetValue(locked.Move, out var move) ||
                !MetronomeSourceMoveDefinition.Matches(move)) return "called_turn_lock_identity";
            if (mon.DigMoveSlot is not null || mon.RolloutTurnsRemaining != 0 || mon.RampageTurnsRemaining != 0) return "called_turn_competing_lock";
            bool valid = locked.Kind switch {
                WildBattleCalledLockKind.Charge => move.Effect is "EFFECT_RAZOR_WIND" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_SOLAR_BEAM" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK" && locked.TurnsRemaining == 1,
                WildBattleCalledLockKind.Recharge => move.Effect == "EFFECT_RECHARGE" &&
                    (locked.TurnsRemaining == 1 || state.SourceMechanicsRulesVersion == Game.Domain.Battle.BattleRulesProfile.CanonicalSourceSinglesVersion && locked.TurnsRemaining == 2),
                WildBattleCalledLockKind.Rampage => move.Effect == "EFFECT_RAMPAGE" && locked.TurnsRemaining is >= 1 and <= 3,
                WildBattleCalledLockKind.Rollout => move.Effect == "EFFECT_ROLLOUT" && locked.TurnsRemaining is >= 1 and <= 4,
                WildBattleCalledLockKind.Uproar => move.Effect == "EFFECT_UPROAR" && locked.TurnsRemaining is >= 1 and <= 5,
                WildBattleCalledLockKind.Bide => move.Effect == "EFFECT_BIDE" && locked.TurnsRemaining is >= 1 and <= 2,
                _ => false
            };
            if (!valid || locked.StoredDamage < 0 || locked.StoredDamageSource is { } damageSource && !Enum.IsDefined(damageSource) ||
                locked.Kind != WildBattleCalledLockKind.Bide && (locked.StoredDamage != 0 || locked.StoredDamageSource is not null) ||
                locked.Kind == WildBattleCalledLockKind.Bide && (locked.StoredDamage == 0) != (locked.StoredDamageSource is null) ||
                locked.UnableToUseMove && locked.Kind is not (WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Uproar))
                return "called_turn_lock_state";
        }
        return null;
    }

    /// <summary>Executes the forced source current move through ForestMove. It never looks up a fabricated learned slot.
    /// The caller composes other actions and end-turn commands separately under the called-action contract.</summary>
    public static WildBattleCalledMoveResult ResolveCalledMoveContinuation(PokemonCatalog catalog, WildBattleState state,
        WildBattleSide side, WildBattleCalledActionContext? actionContext = null)
    {
        string? error = ValidateCalledMoveActionState(catalog, state);
        if (error is null && actionContext is { } supplied && (supplied.ActionOrderIndex is < 0 or > 1 || supplied.ActorPartyIndex is < 0 or > 5)) error = "called_action_context";
        if (error is null && (!Enum.IsDefined(side) || state.Outcome != WildBattleOutcome.Ongoing || state.AwaitingReplacement || state.CalledParty?.PendingSwitch is not null)) error = "called_turn_action_phase";
        var actor = side == WildBattleSide.Player ? state.Player : state.Opponent;
        if (error is null && actor.CalledTurnState?.Lock is null) error = "called_turn_no_continuation";
        if (error is not null) return new(Fail(state, error), null);
        WildBattleMoveInvocation? invocation = null;
        var result = Execute(state, state, context => {
            context.ExecuteCalledMoveContinuation(catalog, side, actionContext);
            context.SourceHeldStandaloneActionFinished();
            context.State = context.State with { Revision = checked(state.Revision + 1) };
            invocation = context.activeInvocation;
        });
        return new(result, result.Succeeded ? invocation : null);
    }

    /// <summary>Only the source Uproar/Thrash/sure-hit residual commands, in supplied battler order.
    /// Does not advance the turn, process poison/weather, or enable ordinary Fuchsia turn scheduling.</summary>
    public static WildBattleResult ResolveCalledTurnLockEndTurn(PokemonCatalog catalog, WildBattleState state,
        WildBattleSide first = WildBattleSide.Player)
    {
        string? error = ValidateCalledMoveActionState(catalog, state);
        if (error is null && !Enum.IsDefined(first)) error = "called_turn_residual_order";
        if (error is null && state.CalledParty?.PendingSwitch is not null) error = "called_party_switch_pending";
        if (error is not null) return Fail(state, error);
        return Execute(state, state, context => {
            context.CalledTurnLockResidual(first);
            context.CalledTurnLockResidual(first == WildBattleSide.Player ? WildBattleSide.Opponent : WildBattleSide.Player);
            context.CalledCancelSleepingLocks();
            context.State = context.State with { Revision = checked(state.Revision + 1) };
        });
    }

    internal sealed partial class Execution
    {
        private void SetCalledTurnState(WildBattleSide side, WildBattleCalledTurnState turn) => Set(side, Get(side) with {
            CalledTurnState = turn.Lock is null && turn.SureHitTurnsRemaining == 0 ? null : turn });
        private void ChangeCalledLock(WildBattleSide side, WildBattleCalledTurnLock? next, string reason)
        {
            var turn = Get(side).CalledTurnState ?? new(); var before = turn.Lock;
            if (before == next) return;
            SetCalledTurnState(side, turn with { Lock = next });
            Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side,
                "called-" + (next?.Kind ?? before!.Kind).ToString().ToLowerInvariant(), before?.TurnsRemaining ?? 0,
                next?.TurnsRemaining ?? 0, reason));
        }
        private void StartCalledLock(WildBattleSide side, PokemonMoveDefinition move, WildBattleCalledLockKind kind, int turns)
        {
            var invocation = activeInvocation!;
            ChangeCalledLock(side, new(kind, move.Id, invocation.PpSlot, Get(side).Pokemon.Moves[invocation.PpSlot].Move,
                invocation.Target, turns), "move");
        }
        private bool CalledTurnActive => activeInvocation is not null || State.Player.CalledTurnState is not null || State.Opponent.CalledTurnState is not null;
        private bool CalledSureHit(WildBattleSide side, WildBattleSide target) =>
            Get(target).CalledTurnState is { SureHitTurnsRemaining: > 0 } turn && turn.SureHitSource == side;
        private bool CalledSemiInvulnerableMiss(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move)
        {
            if (!CalledTurnActive || side == target || CalledSureHit(side, target)) return false;
            return SourceChargeRules.Misses(CalledSemiInvulnerable(Get(target)),move);
        }
        private int CalledSemiDamageMultiplier(WildBattleSide target, PokemonMoveDefinition move) =>
            SourceChargeRules.DamageMultiplier(CalledSemiInvulnerable(Get(target)),move);
        private bool CalledUproarPreventsSleep(WildBattleSide side) => Recipes.SharedUproarPreventsSleep(S(side));
        private void CalledUproarWake(WildBattleSide side) => Recipes.SharedUproarWake(S(side));
        private void CancelCalledTurnLock(WildBattleSide side, string reason)
        {
            if (Get(side).CalledTurnState?.Lock is { Kind: not WildBattleCalledLockKind.Recharge }) ChangeCalledLock(side, null, reason);
        }
        private void ClearCalledTurnState(WildBattleSide side, string reason)
        {
            if (Get(side).CalledTurnState is not null) Set(side, Get(side) with { CalledTurnState = null });
            var other = Other(side); var turn = Get(other).CalledTurnState;
            if (turn?.SureHitSource == side) SetCalledTurnState(other, turn with { SureHitTurnsRemaining = 0, SureHitSource = null });
        }
        private void CalledTrackDamage(WildBattleSide source, WildBattleSide target, int damage) =>
            Recipes.StoreBideDamage(S(source), S(target), damage);
        private bool CalledRechargePreventsMove(WildBattleSide side, MoveId move) =>
            new SinglesRecipeBridge(this).RechargePreventsMove(SinglesBattleAdapter.Slot(side), move);

        private void RecordCalledTurnInability(int eventStart, WildBattleSide side)
        {
            if (Get(side).CalledTurnState?.Lock is not { Kind: WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Uproar } locked) return;
            bool unable = Events.Skip(eventStart).Any(e => e is WildBattleActionBlockedEvent { Status: WildBattleStatus.Paralysis } ||
                e is WildBattleConfusionSelfHitEvent || e is WildBattleInfatuationBlockedEvent ||
                e is WildBattleEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } ||
                e is WildBattleAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" } ||
                e is WildBattleAbilityPreventedEvent { Ability: "ABILITY_WONDER_GUARD" } guard && sourceHitItemCatalog is { } sourceCatalog &&
                    SourceTypeResultRules.Calculate(sourceCatalog, Get(guard.Target), sourceCatalog.Moves[guard.Move], invocationEffectiveType).TypeImmune ||
                e is WildBattleMoveFailedEvent { Reason: "disabled" or "imprison" or "taunted" });
            if (unable) ChangeCalledLock(side, locked with { UnableToUseMove = true }, "unable-to-use-move");
        }
        internal void ExecuteCalledMoveContinuation(PokemonCatalog catalog, WildBattleSide side, WildBattleCalledActionContext? actionContext = null)
        {
            calledActionContext = actionContext ?? new();
            SourceCalledPreparePartyExecution(side);
            var locked = Get(side).CalledTurnState!.Lock!; var current = catalog.Moves[locked.Move];
            // HandleAction_UseMove sets BOTH chosen/current to gLockedMoves; the PP index survives.
            activeInvocation = new(side, locked.PpSlot, locked.PpSlot, current.Id, current.Id, current.Id, null,
                locked.Target, WildBattleAttackCancellerPhase.Start, WildBattleInvocationFlags.AllowNoPp);
            invocationEffectiveType = null;
            invocationCommandNoEffect = false;
            invocationSkipsMoveEnd = false;
            var originalActorLastUsedMove = Get(side).LastUsedMove;
            BeginPreparedMoveInvocation(catalog, originalActorLastUsedMove);
            int eventStart = Events.Count;
            earlyPartyActed.Add(side);
            ForestMove(catalog, side, locked.PpSlot, current);
            RecordCalledTurnInability(eventStart, side);
            if (invocationSkipsMoveEnd)
            {
                Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove });
                return;
            }
            CompleteCalledInvocationMoveEnd(catalog, eventStart, side);
            SourceCalledSynchronizeParties();
        }
        internal void CalledTurnLockResidual(WildBattleSide side)
        {
            CalledUproarResidual(side); CalledRampageResidual(side); CalledSureHitResidual(side);
        }
        internal void CalledUproarResidual(WildBattleSide side) => new SinglesEndTurnBridge(this).UproarResidual(S(side));
        internal void CalledRampageResidual(WildBattleSide side) => new SinglesEndTurnBridge(this).RampageResidual(S(side));
        internal void CalledSureHitResidual(WildBattleSide side) => new SinglesEndTurnBridge(this).LockOnResidual(S(side));
        internal void CalledCancelSleepingLocks()
        {
            // Complete source turns call HandleEndTurn_ContinueBattle before field/battler residuals.
            // This command-only helper retains whichever phase order its caller supplies.
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                if (WildBattleStatus.IsSleeping(Get(side).Status)) CancelCalledTurnLock(side, "sleep");
        }

        private void ExecuteCalledTurnLockEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            if (!HasCalledTurnLockRecipe(move.Effect)) return;
            handled = true;
            var target = activeInvocation!.Target;
            if (SourceRecipeContext.HasChargeRecipe(move.Effect))
            {
                // Protect was recorded by attackcanceler; the script still executes its release.
                if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") && !Recipes.ChargeFirstTurn(move))
                    CancelRoute3Rollout(side,"protected");
                Recipes.ExecuteCharge(catalog,S(side),move,purpose,ref handled);return;
            }
            if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED"))
            { CancelRoute3Rollout(side,"protected"); CancelCalledTurnLock(side,"protected"); }
            if (SourceRecipeContext.HasRechargeRampageRecipe(move.Effect))
            { Recipes.ExecuteRechargeRampage(catalog, S(side), move, purpose); return; }
            if (move.Effect == "EFFECT_BIDE")
            {
                SpendInvocationPp(side, target, move);
                Recipes.StartBide(S(side), move); return;
            }
            if (move.Effect == "EFFECT_LOCK_ON")
            { Recipes.ExecuteTargetMark(S(side), S(target), slot, move, purpose); return; }
            if (move.Effect == "EFFECT_FURY_CUTTER") { CalledFuryCutter(catalog, side, target, move, purpose); return; }
            if (move.Effect == "EFFECT_ROLLOUT") { CalledRollout(catalog, side, target, move, purpose); return; }
            bool beforeAccuracy = move.Effect == "EFFECT_EARTHQUAKE";
            bool repeated = Get(side).CalledTurnState?.Lock?.Kind is WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Uproar;
            CalledTurnHit(catalog, side, target, move, purpose, beforeAccuracy, !repeated);
        }

        private void CalledTurnHit(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target,
            PokemonMoveDefinition move, string purpose, bool spendBeforeAccuracy, bool spendPp, int? power = null)
        {
            if (spendBeforeAccuracy && spendPp) SpendInvocationPp(side, target, move);
            bool hit = ScalarAccuracy(side, target, move, purpose, ScalarType(move.Type));
            // Rampage/Uproar hit continuations skip ppreduce; a miss jumps to PrintMoveMissed and DOES spend PP.
            if (!spendBeforeAccuracy && (spendPp || !hit)) SpendInvocationPp(side, target, move);
            else PrintInvocationAttackString();
            if (!hit) { SourceGenericMiss(side, target, move); return; }
            if (Route11WaterAbsorb(side, target, move) || SsAnneFlashFire(side, target, move)) return;
            bool critical = SourceGenericCritical(Get(side), Get(target), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = ScalarAdjust(target, move, ScalarDamage(catalog, side, target, move, critical, percent, power,
                multiplier: CalledSemiDamageMultiplier(target, move)), purpose);
            int lost = SourceGenericApplyDamage(side, target, move, damage, critical, percent);
            ScalarReportType(catalog, side, target, move, ScalarType(move.Type));
            bool effective = !ScalarTypeResult(catalog, target, ScalarType(move.Type)).Immune;
            var secondary = Recipes.SecondaryEffect(S(side), move, purpose, !effective);
            if (secondary.Apply)
            {
                if (move.Effect == "EFFECT_UPROAR" && Get(side).CalledTurnState?.Lock is null)
                    StartCalledLock(side, move, WildBattleCalledLockKind.Uproar, (Draw($"{purpose}.uproar-duration", 4) & 3) + 2);
                else if (move.Effect is "EFFECT_SKY_ATTACK" or "EFFECT_TWISTER") Recipes.ApplySecondaryFlinch(S(target), secondary.Certain);
                else if (move.Id.Value == "pret:MOVE_BOUNCE") Recipes.InflictStatus(catalog, S(target), WildBattleStatus.Paralysis, false, certain: secondary.Certain);
            }
            ScalarFinish(catalog, side, target, move, purpose, lost, ScalarType(move.Type));
        }

        private void CalledFuryCutter(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target,
            PokemonMoveDefinition move, string purpose)
        {
            SpendInvocationPp(side, target, move);
            if (!ScalarAccuracy(side, target, move, purpose, ScalarType(move.Type)))
            { ChangeFuryCutterCounter(side, 0, "missed"); SourceGenericMiss(side, target, move); return; }
            int power = NextFuryCutterPower(Get(side).FuryCutterCounter);
            ChangeFuryCutterCounter(side, Math.Min(5, Get(side).FuryCutterCounter + 1), "hit");
            bool critical = SourceGenericCritical(Get(side), Get(target), move, purpose);
            if (ScalarTypeResult(catalog, target, ScalarType(move.Type)).Immune)
            { ChangeFuryCutterCounter(side, 0, "no-effect"); ScalarReportType(catalog, side, target, move, ScalarType(move.Type)); return; }
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = ScalarAdjust(target, move, ScalarDamage(catalog, side, target, move, critical, percent, power), purpose);
            int lost = SourceGenericApplyDamage(side, target, move, damage, critical, percent);
            ScalarReportType(catalog, side, target, move, ScalarType(move.Type));
            Draw($"{purpose}.secondary-effect", 100);
            ScalarFinish(catalog, side, target, move, purpose, lost, ScalarType(move.Type));
        }
        private void CalledRollout(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target,
            PokemonMoveDefinition move, string purpose)
            => Recipes.SourceRollout(catalog, S(side), S(target), move, purpose);
        // Source CANCELLER_BIDE is after infatuation and before ability/no-PP/Protect checks.
        private bool CalledBideContinuation(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move, string purpose) =>
            Recipes.ContinueBide(catalog, S(side), move, purpose);

    }
}
