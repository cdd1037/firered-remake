using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

public enum WildBattleSourceEndTurnStage { FutureAttacks, PerishSong, Complete }
public enum WildBattleSourceEndTurnBoundary { FaintedActions, Complete }

/// <summary>
/// A source callback continuation, held by the turn owner while EXP and actual party replacement run.
/// The end-turn order is calculated once. NextBattler is a battler ID for Future Sight, an order index
/// for Perish Song. A completed cursor cannot be resumed. This is not a save-game/whole-turn contract.
/// </summary>
public sealed record WildBattleSourceEndTurnCursor(int Turn, WildBattleSide First,
    WildBattleSourceEndTurnStage Stage, int NextBattler, long MinimumRevision, long MinimumEventSequence)
{
    public string RulesVersion { get; init; } = WildBattleRules.SourceEndTurnRulesVersion;
    public WildBattleSourceEndTurnBoundary Boundary => Stage == WildBattleSourceEndTurnStage.Complete
        ? WildBattleSourceEndTurnBoundary.Complete : WildBattleSourceEndTurnBoundary.FaintedActions;
}

public sealed record WildBattleSourceEndTurnResult(WildBattleResult Battle, WildBattleSourceEndTurnCursor? Cursor)
{
    public bool Succeeded => Battle.Succeeded;
    public WildBattleState? State => Battle.State;
}

public static partial class WildBattleRules
{
    public const string SourceEndTurnRulesVersion = "firered-source-end-turn-phases-v1";

    /// <summary>
    /// Runs normal residuals to HandleFaintedMonActions, or resumes through one delayed script callback.
    /// The caller must award EXP and resolve every pending party replacement before resuming. No action
    /// selection, next-turn increment, Ready call, or next-turn random draw is performed here.
    /// </summary>
    public static WildBattleSourceEndTurnResult ResolveSourceEndTurnPhase(PokemonCatalog catalog,
        WildBattleState state, WildBattleSourceEndTurnCursor? cursor = null)
    {
        string? error = ValidateCalledMoveActionState(catalog, state);
        if (error is null && state.CalledParty is null) error = "source_end_turn_real_parties_required";
        if (error is null && (state.AwaitingReplacement || state.CalledParty?.PendingSwitch is not null)) error = "source_end_turn_replacement_pending";
        if (error is null && cursor is null && state.Outcome != WildBattleOutcome.Ongoing) error = "source_end_turn_start_phase";
        if (error is null && cursor is not null) error = ValidateSourceEndTurnCursor(state, cursor);
        if (error is not null) return new(Fail(state, error), cursor);
        var execution = new Execution(state, false, deferForestOutcome: true);
        try
        {
            var next = execution.AdvanceSourceEndTurn(catalog, cursor);
            execution.SourceCalledSynchronizeParties();
            execution.State = execution.State with { Revision = checked(state.Revision + 1) };
            next = next with { MinimumRevision = execution.State.Revision, MinimumEventSequence = execution.State.EventSequence };
            return new(new(TransitionStatus.Accepted, execution.State, execution.Events.ToImmutable(), execution.Trace.ToImmutable(), null), next);
        }
        catch (UnsupportedAction ex) { return new(Fail(state, ex.Message), cursor); }
        catch (OverflowException) { return new(Fail(state, "wild_battle_counter_exhausted"), cursor); }
        catch (ArgumentOutOfRangeException) { return new(Fail(state, "wild_battle_calculation_range_unsupported"), cursor); }
    }

    private static string? ValidateSourceEndTurnCursor(WildBattleState state, WildBattleSourceEndTurnCursor cursor)
    {
        if (cursor.RulesVersion != SourceEndTurnRulesVersion) return "source_end_turn_cursor_version";
        if (cursor.Turn != state.Turn || state.Outcome is WildBattleOutcome.Escaped or WildBattleOutcome.Caught || !Enum.IsDefined(cursor.First) || !Enum.IsDefined(cursor.Stage) ||
            cursor.Stage == WildBattleSourceEndTurnStage.Complete || cursor.NextBattler is < 0 or > 2 ||
            cursor.MinimumRevision < 0 || cursor.MinimumEventSequence < 0 ||
            state.Revision < cursor.MinimumRevision || state.EventSequence < cursor.MinimumEventSequence)
            return "source_end_turn_cursor";
        return null;
    }

    internal sealed partial class Execution
    {
        /// <summary>The trainer/full-turn owner calls this on its existing deferred Execution.</summary>
        internal WildBattleSourceEndTurnCursor AdvanceSourceEndTurn(PokemonCatalog catalog,
            WildBattleSourceEndTurnCursor? cursor = null)
        {
            if (!deferForestOutcome) throw new UnsupportedAction("source_end_turn_requires_deferred_execution");
            if (State.CalledParty is null) throw new UnsupportedAction("source_end_turn_real_parties_required");
            if (State.CalledParty.PendingSwitch is not null || State.AwaitingReplacement)
                throw new UnsupportedAction("source_end_turn_replacement_pending");
            if (State.Outcome == WildBattleOutcome.Ongoing &&
                (State.Player.Pokemon.CurrentHp == 0 || State.Opponent.Pokemon.CurrentHp == 0))
                throw new UnsupportedAction("source_end_turn_fainted_actions_pending");
            // HandleAction_ActionFinished has cleared gCurrentMove before BattleTurnPassed.
            activeInvocation = null;
            invocationEffectiveType = null;
            if (cursor is null)
            {
                SourceEndTurnPrepare();
                var first = SourceEndTurnOrder(catalog);
                SourceEndTurnFieldEffects(catalog, first);
                if (State.Outcome == WildBattleOutcome.Ongoing) SourceEndTurnBattlerEffects(catalog, first);
                if (State.Outcome == WildBattleOutcome.Ongoing) SourceEndTurnBattlerEffects(catalog, Other(first));
                SourceEndTurnExposeFaintedActions();
                return new(State.Turn, first, WildBattleSourceEndTurnStage.FutureAttacks, 0, State.Revision, State.EventSequence);
            }
            if (ValidateSourceEndTurnCursor(State, cursor) is { } error) throw new UnsupportedAction(error);
            // HandleFaintedMonActions case 6 is reached after EXP and every actual replacement.
            // Its ITEMEFFECT_NORMAL call has battlerId=0 and moveTurn=TRUE; Oran does not run here.
            SnorlaxEndTurnItems();
            var stage = cursor.Stage;
            int next = cursor.NextBattler;
            if (stage == WildBattleSourceEndTurnStage.FutureAttacks)
            {
                for (; next < 2; next++)
                {
                    var side = (WildBattleSide)next; // Source numeric battler order, independent of speed.
                    bool callback = SourceCalledFuture(side) is { TurnsRemaining: 1 } && Get(side).Pokemon.CurrentHp > 0;
                    SourceCalledFutureResidual(catalog, side);
                    if (!callback) continue;
                    SourceEndTurnExposeFaintedActions();
                    return cursor with { NextBattler = next + 1, MinimumRevision = State.Revision, MinimumEventSequence = State.EventSequence };
                }
                stage = WildBattleSourceEndTurnStage.PerishSong;
                next = 0;
            }
            for (; next < 2; next++)
            {
                var side = next == 0 ? cursor.First : Other(cursor.First);
                bool callback = Get(side).CalledVolatile?.PerishSongTimer is not null;
                SourceCalledPerishResidual(side);
                if (!callback) continue;
                SourceEndTurnExposeFaintedActions();
                return cursor with { Stage = stage, NextBattler = next + 1, MinimumRevision = State.Revision, MinimumEventSequence = State.EventSequence };
            }
            // The physical/special damage registers live until TurnValuesCleanUp(FALSE), after
            // all delayed scripts. The full-turn owner still owns Ready/turn+1/randomTurnNumber.
            SourceCalledClearMagicCoat(WildBattleSide.Player);
            SourceCalledClearMagicCoat(WildBattleSide.Opponent);
            SourceCalledResetDamageTurn();
            SourceCalledSynchronizeParties();
            return cursor with { Stage = WildBattleSourceEndTurnStage.Complete, NextBattler = 2,
                MinimumRevision = State.Revision, MinimumEventSequence = State.EventSequence };
        }

        private void SourceEndTurnPrepare()
        {
            // HandleEndTurn_ContinueBattle precedes every field/battler callback, including Uproar.
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                Set(side, Get(side) with { Flinched = false, Protected = false, Enduring = false });
            SnorlaxCancelSleepingLocks();
            CalledCancelSleepingLocks();
        }

        /// <summary>Both selected actions must be moves; item/switch/run precedence belongs to the turn owner.</summary>
        internal WildBattleSide SourceActionsOrder(PokemonCatalog catalog) => SourceOrder(catalog, WildBattleOrderPhase.Actions);
        internal WildBattleSide SourceEndTurnOrder(PokemonCatalog catalog) => SourceOrder(catalog, WildBattleOrderPhase.EndTurn);

        private WildBattleSide SourceOrder(PokemonCatalog catalog, WildBattleOrderPhase phase)
        {
            int Speed(WildBattleCombatant mon)
            {
                bool boosted = !SourceCalledWeatherSuppressed &&
                    (WildBattleEffective.Ability(mon) == "ABILITY_SWIFT_SWIM" && State.RainTurnsRemaining > 0 ||
                     WildBattleEffective.Ability(mon) == "ABILITY_CHLOROPHYLL" && SourceCalledSun);
                int value = WildBattleDamage.Stat(mon.Pokemon.Stats.Speed * (boosted ? 2 : 1), mon.SpeedStage);
                if (mon.SpeedBadgeBoost) value = value * 110 / 100;
                if (mon.Status == WildBattleStatus.Paralysis) value /= 4;
                return value;
            }
            int Priority(WildBattleCombatant mon, int? slot) => slot is null or < 0 || slot >= mon.Pokemon.Moves.Length ? 0 :
                catalog.Moves[mon.Pokemon.Moves[slot.Value].Move].Priority;
            // Both move-action sorting and DoFieldEndTurnEffects call GetWhoStrikesFirst(..., FALSE): chosen move
            // priority still participates. This is not a fresh randomTurnNumber/Quick Claw draw.
            int playerPriority = Priority(State.Player, State.ForestPlayerMoveSlot);
            int opponentPriority = Priority(State.Opponent, State.ForestOpponentMoveSlot);
            int p = Speed(State.Player), o = Speed(State.Opponent);
            bool opponent = playerPriority < opponentPriority || playerPriority == opponentPriority &&
                (p < o || p == o && (Draw($"wild-battle.{phase.ToString().ToLowerInvariant()}.speed-tie", 2) & 1) != 0);
            var first = opponent ? WildBattleSide.Opponent : WildBattleSide.Player;
            Add(n => new WildBattleOrderEvent(n, State.Turn, phase, first, Other(first)));
            return first;
        }

        internal void SourceEndTurnFieldEffects(PokemonCatalog catalog, WildBattleSide first)
        {
            foreach (string effect in new[] { "reflect", "light-screen", "mist", "safeguard" })
                foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent }) SourceEndTurnFieldTimer(side, effect);
            SourceCalledWishResidual(catalog, first);
            SourceCalledWishResidual(catalog, Other(first));
            Route11WeatherResidual();
            SourceEndTurnFieldTimer(WildBattleSide.Player, "sandstorm");
            if (!SourceCalledWeatherSuppressed)
                foreach (var side in new[] { first, Other(first) })
                {
                    if (State.Outcome != WildBattleOutcome.Ongoing) break;
                    RockTunnelSandstormDamage(catalog, side);
                    SourceEndTurnCheckTeamsLost();
                }
            if (State.Outcome != WildBattleOutcome.Ongoing) return;
            SourceEndTurnFieldTimer(WildBattleSide.Player, "sun");
            SourceEndTurnFieldTimer(WildBattleSide.Player, "hail");
            foreach (var side in new[] { first, Other(first) })
            {
                if (State.Outcome != WildBattleOutcome.Ongoing) break;
                CalledFieldHailDamage(catalog, side);
                SourceEndTurnCheckTeamsLost();
            }
        }

        // Narrow split of the legacy combined field tickers. Legacy entry points remain unchanged.
        private void SourceEndTurnFieldTimer(WildBattleSide side, string effect)
        {
            var f = State.CalledField ?? new();
            bool player = side == WildBattleSide.Player;
            int before = effect switch {
                "reflect" => player ? State.PlayerReflectTurnsRemaining : State.OpponentReflectTurnsRemaining,
                "light-screen" => player ? f.PlayerLightScreenTurnsRemaining : f.OpponentLightScreenTurnsRemaining,
                "mist" => player ? f.PlayerMistTurnsRemaining : f.OpponentMistTurnsRemaining,
                "safeguard" => player ? f.PlayerSafeguardTurnsRemaining : f.OpponentSafeguardTurnsRemaining,
                "sandstorm" => State.SandstormTurnsRemaining, "sun" => f.SunTurnsRemaining, "hail" => f.HailTurnsRemaining,
                _ => throw new UnsupportedAction("source_end_turn_field") };
            if (before == 0) return;
            int after = WildBattleSourceFieldEffects.TickTimer(before);
            State = effect switch {
                "reflect" => player ? State with { PlayerReflectTurnsRemaining = after } : State with { OpponentReflectTurnsRemaining = after },
                "light-screen" => State with { CalledField = player ? f with { PlayerLightScreenTurnsRemaining = after } : f with { OpponentLightScreenTurnsRemaining = after } },
                "mist" => State with { CalledField = player ? f with { PlayerMistTurnsRemaining = after } : f with { OpponentMistTurnsRemaining = after } },
                "safeguard" => State with { CalledField = player ? f with { PlayerSafeguardTurnsRemaining = after } : f with { OpponentSafeguardTurnsRemaining = after } },
                "sandstorm" => State with { SandstormTurnsRemaining = after },
                "sun" => State with { CalledField = f with { SunTurnsRemaining = after } },
                _ => State with { CalledField = f with { HailTurnsRemaining = after } } };
            Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, effect, before, after, after == 0 ? "expired" : "countdown"));
        }

        internal void SourceEndTurnBattlerEffects(PokemonCatalog catalog, WildBattleSide side)
        {
            // Each delegate is an existing shared effect command or a narrow split around its
            // existing state/event helpers. A faint clears that battler's volatiles immediately;
            // its real replacement occurs only after every normal residual has finished.
            Action[] steps = [
                () => CeladonGymIngrainResidual(side),
                () => SourceEndTurnAbility(side),
                () => Route3HeldItemResidual(side),
                () => LeechSeedResidual(side),
                () => SourceEndTurnStatusDamage(side, WildBattleStatus.Poison),
                () => CalledFieldToxicResidual(side),
                () => SourceEndTurnStatusDamage(side, WildBattleStatus.Burn),
                () => SourceEndTurnNightmare(side),
                () => { if (Get(side).Pokemon.CurrentHp > 0 && Get(side).Cursed) TowerHp(side, "curse", Math.Max(1, Get(side).Pokemon.Stats.MaxHp / 4)); },
                () => BindingResidual(side),
                () => CalledUproarResidual(side),
                () => { CalledRampageResidual(side); if (Get(side).Pokemon.CurrentHp > 0) MtMoonRampageResidual(side); },
                () => SourceEndTurnDisable(side),
                () => SourceEndTurnEncore(side),
                () => CalledSureHitResidual(side),
                () => { if (Get(side).ChargeTurnsRemaining > 0) ChangeCharge(side, Get(side).ChargeTurnsRemaining - 1, "countdown"); },
                () => SourceCalledTauntResidual(side),
                () => SnorlaxYawnResidual(side),
                () => VermilionHeldBerry(side)
            ];
            foreach (var step in steps)
            {
                if (State.Outcome != WildBattleOutcome.Ongoing) break;
                step();
                SourceEndTurnCheckTeamsLost();
            }
        }

        private void SourceEndTurnAbility(WildBattleSide side)
        {
            var mon = Get(side);
            if (mon.Pokemon.CurrentHp == 0 || mon.Status == 0 || WildBattleEffective.Ability(mon) != "ABILITY_SHED_SKIN") return;
            if (Draw($"wild-battle.{side.ToString().ToLowerInvariant()}.shed-skin", 3) % 3 != 0) return;
            RecordAbility?.Invoke(side, "ABILITY_SHED_SKIN");
            ChangeStatus(side, WildBattleStatus.None, "shed-skin");
        }
        private void SourceEndTurnStatusDamage(WildBattleSide side, uint status)
        {
            var mon = Get(side);
            if (mon.Pokemon.CurrentHp == 0 || mon.Status != status) return;
            int before = mon.Pokemon.CurrentHp, damage = Math.Max(1, mon.Pokemon.Stats.MaxHp / 8), after = Math.Max(0, before - damage);
            Set(side, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
            Add(n => new WildBattleStatusDamageEvent(n, State.Turn, side, status, before, after, damage));
            if (after == 0) ForestFaint(side);
        }
        private void SourceEndTurnNightmare(WildBattleSide side)
        {
            var mon = Get(side);
            if (mon.Pokemon.CurrentHp == 0 || !mon.Nightmare) return;
            if (!WildBattleStatus.IsSleeping(mon.Status)) SetTowerFlag(side, "nightmare", false, "awake");
            else TowerHp(side, "nightmare", Math.Max(1, mon.Pokemon.Stats.MaxHp / 4));
        }
        private void SourceEndTurnDisable(WildBattleSide side)
        {
            var mon = Get(side);
            if (mon.DisabledMove is { } move && !mon.Pokemon.Moves.Any(m => m.Move == move))
                ChangeDisable(side, move, 0, "move-changed");
            else Route3DisableResidual(side);
        }
        private void SourceEndTurnEncore(WildBattleSide side)
        {
            var mon = Get(side);
            if (mon.EncoreMoveSlot is not { } slot) return;
            if (mon.EncoredMove is { } move && mon.Pokemon.Moves[slot].Move != move)
            { ChangeEncore(side, slot, 0, "move-changed"); return; }
            int remaining = mon.Pokemon.Moves[slot].CurrentPp == 0 ? 0 : mon.EncoreTurnsRemaining - 1;
            ChangeEncore(side, slot, remaining, remaining == 0 ? "expired" : "countdown");
        }

        private void SourceEndTurnCheckTeamsLost()
        {
            SourceCalledSynchronizeParties();
            var party = State.CalledParty!;
            bool p = WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Player);
            bool o = WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Opponent);
            if (p && o) return;
            var outcome = !p && !o ? WildBattleOutcome.Draw : !o ? WildBattleOutcome.Won : WildBattleOutcome.Lost;
            if (State.Outcome == outcome) return;
            State = State with { Outcome = outcome, AwaitingReplacement = false, CalledParty = party with { PendingSwitch = null },
                ForestPlayerMoveSlot = null, ForestOpponentMoveSlot = null, ForestResidualsFinished = false };
            Add(n => new WildBattleEndedEvent(n, State.Turn, outcome));
        }
        private void SourceEndTurnExposeFaintedActions()
        {
            SourceEndTurnCheckTeamsLost();
            if (State.Outcome == WildBattleOutcome.Ongoing) SourceCalledPartyOutcome();
            SourceCalledSynchronizeParties();
        }
    }
}
