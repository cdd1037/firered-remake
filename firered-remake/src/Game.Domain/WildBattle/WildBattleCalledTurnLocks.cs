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
        mon.CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge ? charge.Move.Value switch {
            "pret:MOVE_FLY" or "pret:MOVE_BOUNCE" => WildBattleSemiInvulnerable.Air,
            "pret:MOVE_DIG" => WildBattleSemiInvulnerable.Underground,
            "pret:MOVE_DIVE" => WildBattleSemiInvulnerable.Underwater,
            _ => WildBattleSemiInvulnerable.None
        } : mon.DigMoveSlot is not null ? WildBattleSemiInvulnerable.Underground : WildBattleSemiInvulnerable.None;

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
                WildBattleCalledLockKind.Recharge => move.Effect == "EFFECT_RECHARGE" && locked.TurnsRemaining == 1,
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
            return CalledSemiInvulnerable(Get(target)) switch {
                WildBattleSemiInvulnerable.Air => move.Id.Value is not ("pret:MOVE_GUST" or "pret:MOVE_TWISTER" or "pret:MOVE_THUNDER" or "pret:MOVE_SKY_UPPERCUT"),
                WildBattleSemiInvulnerable.Underground => move.Effect is not ("EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE"),
                WildBattleSemiInvulnerable.Underwater => move.Id.Value is not ("pret:MOVE_SURF" or "pret:MOVE_WHIRLPOOL"),
                _ => false
            };
        }
        private int CalledSemiDamageMultiplier(WildBattleSide target, PokemonMoveDefinition move) => CalledSemiInvulnerable(Get(target)) switch {
            WildBattleSemiInvulnerable.Air when move.Id.Value is "pret:MOVE_GUST" or "pret:MOVE_TWISTER" => 2,
            WildBattleSemiInvulnerable.Underground when move.Effect is "EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE" => 2,
            WildBattleSemiInvulnerable.Underwater when move.Id.Value is "pret:MOVE_SURF" or "pret:MOVE_WHIRLPOOL" => 2,
            _ => 1
        };
        private bool CalledUproarPreventsSleep(WildBattleSide side) => WildBattleEffective.Ability(Get(side)) != "ABILITY_SOUNDPROOF" &&
            new[] { State.Player, State.Opponent }.Any(m => m.CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Uproar });
        private void CalledUproarWake(WildBattleSide side)
        {
            if (!WildBattleStatus.IsSleeping(Get(side).Status) || !CalledUproarPreventsSleep(side)) return;
            ChangeStatus(side, WildBattleStatus.None, "uproar-wake");
            if (Get(side).Nightmare) Set(side, Get(side) with { Nightmare = false });
        }
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
        private void CalledTrackDamage(WildBattleSide source, WildBattleSide target, int damage)
        {
            if (damage <= 0 || Get(target).CalledTurnState?.Lock is not { Kind: WildBattleCalledLockKind.Bide } locked) return;
            ChangeCalledLock(target, locked with { StoredDamage = checked(locked.StoredDamage + damage), StoredDamageSource = source }, "stored-damage");
        }
        private bool CalledRechargePreventsMove(WildBattleSide side, MoveId move)
        {
            if (Get(side).CalledTurnState?.Lock?.Kind != WildBattleCalledLockKind.Recharge) return false;
            ChangeCalledLock(side, null, "recharged");
            CancelRoute3Rollout(side, "recharged");
            Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, Other(side), move, "must-recharge"));
            return true;
        }
        private void RecordCalledTurnInability(int eventStart, WildBattleSide side)
        {
            if (Get(side).CalledTurnState?.Lock is not { Kind: WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Uproar } locked) return;
            bool unable = Events.Skip(eventStart).Any(e => e is WildBattleActionBlockedEvent { Status: WildBattleStatus.Paralysis } ||
                e is WildBattleConfusionSelfHitEvent || e is WildBattleInfatuationBlockedEvent ||
                e is WildBattleEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } ||
                e is WildBattleAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" } ||
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
            BeginPreparedMoveInvocation(originalActorLastUsedMove);
            int eventStart = Events.Count;
            earlyPartyActed.Add(side);
            ForestMove(catalog, side, locked.PpSlot, current);
            RecordCalledTurnInability(eventStart, side);
            if (invocationSkipsMoveEnd)
            {
                Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove });
                return;
            }
            if (Route11) Route11DefrostOnFireHit(catalog, side, eventStart);
            if (Vermilion) { VermilionImmunityAbilities(); SourceCalledApplyChangedItems(); VermilionMoveEndBerries(); }
            FinishCalledMoveHistory(catalog, eventStart);
            SourceCalledSynchronizeParties();
        }
        internal void CalledTurnLockResidual(WildBattleSide side)
        {
            CalledUproarResidual(side); CalledRampageResidual(side); CalledSureHitResidual(side);
        }
        internal void CalledUproarResidual(WildBattleSide side)
        {
            if (Get(side).Pokemon.CurrentHp == 0 || Get(side).CalledTurnState?.Lock is not { Kind: WildBattleCalledLockKind.Uproar } locked) return;
            CalledUproarWake(WildBattleSide.Player); CalledUproarWake(WildBattleSide.Opponent);
            if (locked.UnableToUseMove || locked.TurnsRemaining == 1) CancelCalledTurnLock(side, "uproar-ended");
            else ChangeCalledLock(side, locked with { TurnsRemaining = locked.TurnsRemaining - 1 }, "countdown");
        }
        internal void CalledRampageResidual(WildBattleSide side)
        {
            if (Get(side).Pokemon.CurrentHp == 0 || Get(side).CalledTurnState?.Lock is not { Kind: WildBattleCalledLockKind.Rampage } locked) return;
            if (locked.UnableToUseMove) CancelCalledTurnLock(side, "rampage-interrupted");
            else if (locked.TurnsRemaining == 1)
            {
                ChangeCalledLock(side, null, "rampage-ended");
                ApplyConfusion(side, $"wild-battle.{side.ToString().ToLowerInvariant()}", false);
            }
            else ChangeCalledLock(side, locked with { TurnsRemaining = locked.TurnsRemaining - 1 }, "countdown");
        }
        internal void CalledSureHitResidual(WildBattleSide side)
        {
            if (Get(side).Pokemon.CurrentHp > 0 && Get(side).CalledTurnState is { SureHitTurnsRemaining: > 0 } turn)
                SetCalledTurnState(side, turn with { SureHitTurnsRemaining = turn.SureHitTurnsRemaining - 1,
                    SureHitSource = turn.SureHitTurnsRemaining == 1 ? null : turn.SureHitSource });
        }
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
            bool charge = move.Effect is "EFFECT_RAZOR_WIND" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_SOLAR_BEAM" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK";
            bool releasing = Get(side).CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Charge;
            bool solarSun = move.Effect == "EFFECT_SOLAR_BEAM" && SourceCalledSun && !SourceCalledWeatherSuppressed;
            // attackcanceler ignores Protect on a first charging turn. Protection cancels existing locks.
            bool protectedMove = Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") && (!charge || releasing);
            if (protectedMove) { CancelRoute3Rollout(side, "protected"); CancelCalledTurnLock(side, "protected"); }
            if (charge)
            {
                if (!releasing || solarSun)
                {
                    if (solarSun) StartCalledLock(side, move, WildBattleCalledLockKind.Charge, 1);
                    SpendInvocationPp(side, target, move, printAttackString: false);
                    if (!solarSun)
                    {
                        StartCalledLock(side, move, WildBattleCalledLockKind.Charge, 1);
                        if (move.Effect == "EFFECT_SKULL_BASH") ChangeStage(side, side, WildBattleStat.Defense, 1, move.Id);
                        return;
                    }
                }
                ChangeCalledLock(side, null, "released");
                activeInvocation = activeInvocation with { Flags = activeInvocation.Flags | WildBattleInvocationFlags.NoPpDeduct };
                CalledTurnHit(catalog, side, target, move, purpose, spendBeforeAccuracy: false, spendPp: true);
                return;
            }
            if (move.Effect == "EFFECT_BIDE")
            {
                SpendInvocationPp(side, target, move);
                StartCalledLock(side, move, WildBattleCalledLockKind.Bide, 2); return;
            }
            if (move.Effect == "EFFECT_LOCK_ON")
            {
                SpendInvocationPp(side, target, move);
                if (!ScalarAccuracy(side, target, move, purpose, ScalarType(move.Type))) { SourceGenericMiss(side, target, move); return; }
                SetCalledTurnState(target, (Get(target).CalledTurnState ?? new()) with { SureHitTurnsRemaining = 2, SureHitSource = side });
                Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, target, "sure-hit", 0, 2, "move")); return;
            }
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
            if (move.Effect == "EFFECT_RECHARGE" && effective)
                StartCalledLock(side, move, WildBattleCalledLockKind.Recharge, 1); // MOVE_EFFECT_CERTAIN: no chance draw.
            else
            {
                int roll = Draw($"{purpose}.secondary-effect", 100) % 100;
                int chance = move.SecondaryEffectChance * (WildBattleEffective.Ability(Get(side)) == "ABILITY_SERENE_GRACE" ? 2 : 1);
                if (effective && roll <= chance)
                {
                    if (move.Effect == "EFFECT_RAMPAGE" && Get(side).CalledTurnState?.Lock is null)
                        StartCalledLock(side, move, WildBattleCalledLockKind.Rampage, (Draw($"{purpose}.rampage-duration", 2) & 1) + 2);
                    else if (move.Effect == "EFFECT_UPROAR" && Get(side).CalledTurnState?.Lock is null)
                        StartCalledLock(side, move, WildBattleCalledLockKind.Uproar, (Draw($"{purpose}.uproar-duration", 4) & 3) + 2);
                    else if (move.Effect is "EFFECT_SKY_ATTACK" or "EFFECT_TWISTER") ApplyFlinch(target);
                    else if (move.Id.Value == "pret:MOVE_BOUNCE") InflictStatus(catalog, target, WildBattleStatus.Paralysis, false);
                }
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
        {
            var locked = Get(side).CalledTurnState?.Lock;
            if (locked is null) SpendInvocationPp(side, target, move); else PrintInvocationAttackString();
            if (!ScalarAccuracy(side, target, move, purpose, ScalarType(move.Type)))
            { CancelCalledTurnLock(side, "missed"); SourceGenericMiss(side, target, move); return; }
            if (ScalarTypeResult(catalog, target, ScalarType(move.Type)).Immune)
            { CancelCalledTurnLock(side, "no-effect"); ScalarReportType(catalog, side, target, move, ScalarType(move.Type)); return; }
            int remaining = (locked?.TurnsRemaining ?? 5) - 1;
            int power = (move.Power << (4 - remaining)) * (Get(side).DefenseCurled ? 2 : 1);
            if (locked is null) StartCalledLock(side, move, WildBattleCalledLockKind.Rollout, 4);
            else ChangeCalledLock(side, remaining == 0 ? null : locked with { TurnsRemaining = remaining }, remaining == 0 ? "finished" : "countdown");
            bool critical = SourceGenericCritical(Get(side), Get(target), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = ScalarAdjust(target, move, ScalarDamage(catalog, side, target, move, critical, percent, power), purpose);
            int lost = SourceGenericApplyDamage(side, target, move, damage, critical, percent);
            ScalarReportType(catalog, side, target, move, ScalarType(move.Type));
            Draw($"{purpose}.secondary-effect", 100);
            ScalarFinish(catalog, side, target, move, purpose, lost, ScalarType(move.Type));
        }
        // Source CANCELLER_BIDE is after infatuation and before ability/no-PP/Protect checks.
        private bool CalledBideContinuation(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move, string purpose)
        {
            if (Get(side).CalledTurnState?.Lock is not { Kind: WildBattleCalledLockKind.Bide } locked) return false;
            if (locked.TurnsRemaining > 1)
            {
                ChangeCalledLock(side, locked with { TurnsRemaining = locked.TurnsRemaining - 1 }, "storing-energy");
                return true;
            }
            ChangeCalledLock(side, null, "released");
            activeInvocation = activeInvocation! with { CancellerPhase = WildBattleAttackCancellerPhase.ConditionsPassed, Flags = activeInvocation.Flags | WildBattleInvocationFlags.Obeys };
            if (locked.StoredDamage == 0)
            { Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, activeInvocation.Target, move.Id, "bide-no-energy")); return true; }
            var target = locked.StoredDamageSource!.Value;
            if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) CancelRoute3Rollout(side, "protected");
            activeInvocation = activeInvocation with { Target = target, Flags = activeInvocation.Flags | WildBattleInvocationFlags.Obeys };
            if (!ScalarAccuracy(side, target, move, purpose, ScalarType(move.Type))) { SourceGenericMiss(side, target, move); return true; }
            bool immune = ScalarTypeResult(catalog, target, ScalarType(move.Type)).Immune;
            int adjusted = ScalarAdjust(target, move, checked(locked.StoredDamage * 2), purpose);
            int damage = immune ? 0 : adjusted;
            int lost = SourceGenericApplyDamage(side, target, move, damage, false, 100);
            ScalarReportType(catalog, side, target, move, ScalarType(move.Type), fixedDamage: true);
            ScalarFinish(catalog, side, target, move, purpose, lost, ScalarType(move.Type));
            return true;
        }
    }
}
