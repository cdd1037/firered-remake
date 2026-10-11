using System.Collections.Immutable;
using Game.Domain.Battle;
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
            activeInvocation = null;
            invocationEffectiveType = null;
            if (cursor is not null && ValidateSourceEndTurnCursor(State, cursor) is { } error) throw new UnsupportedAction(error);
            var bridge = new SinglesEndTurnBridge(this);
            if (cursor is null) bridge.Prepare();
            var first = cursor?.First ?? SourceEndTurnOrder(catalog);
            ImmutableArray<BattlerSlot> order = [new((int)first), new((int)Other(first))];
            var shared = cursor is null ? null : new SourceEndTurnCursor(cursor.Turn, order, cursor.Stage,
                cursor.NextBattler, cursor.MinimumRevision, cursor.MinimumEventSequence, cursor.RulesVersion);
            var next = bridge.Advance(catalog, order, shared);
            return new(next.Turn, first, next.Stage, next.NextBattler, next.MinimumRevision, next.MinimumEventSequence)
                { RulesVersion = next.RulesVersion };
        }

        private void SourceEndTurnPrepare()
        {
            // HandleEndTurn_ContinueBattle precedes every field/battler callback, including Uproar.
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                Set(side, SourceTurnBoundaryRules.PrepareBody(Get(side)));
            SnorlaxCancelSleepingLocks();
            CalledCancelSleepingLocks();
        }

        /// <summary>Both selected actions must be moves; item/switch/run precedence belongs to the turn owner.</summary>
        internal WildBattleSide SourceActionsOrder(PokemonCatalog catalog) => SourceOrder(catalog, WildBattleOrderPhase.Actions);
        internal WildBattleSide SourceEndTurnOrder(PokemonCatalog catalog) => SourceOrder(catalog, WildBattleOrderPhase.EndTurn);

        private WildBattleSide SourceOrder(PokemonCatalog catalog, WildBattleOrderPhase phase)
        {
            uint Speed(WildBattleCombatant mon, bool playerSide)
            {
                bool boosted = !SourceCalledWeatherSuppressed &&
                    (WildBattleEffective.Ability(mon) == "ABILITY_SWIFT_SWIM" && SourceCalledRain ||
                     WildBattleEffective.Ability(mon) == "ABILITY_CHLOROPHYLL" && SourceCalledSun);
                if (State.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion)
                    return SourceBattleTurnOrder.EffectiveSpeed(mon, boosted, playerSide, State.RandomTurnNumber);
                int value = WildBattleDamage.Stat(mon.Pokemon.Stats.Speed * (boosted ? 2 : 1), mon.SpeedStage);
                if (mon.SpeedBadgeBoost) value = value * 110 / 100;
                if (mon.Status == WildBattleStatus.Paralysis) value /= 4;
                return (uint)value;
            }
            int Priority(WildBattleCombatant mon, int? slot) => slot is null or < 0 || slot >= mon.Pokemon.Moves.Length ? 0 :
                catalog.Moves[mon.Pokemon.Moves[slot.Value].Move].Priority;
            // Both move-action sorting and DoFieldEndTurnEffects call GetWhoStrikesFirst(..., FALSE): chosen move
            // priority still participates. This is not a fresh randomTurnNumber/Quick Claw draw.
            int playerPriority = Priority(State.Player, State.ForestPlayerMoveSlot);
            int opponentPriority = Priority(State.Opponent, State.ForestOpponentMoveSlot);
            uint p = Speed(State.Player, true), o = Speed(State.Opponent, false);
            bool opponent = playerPriority < opponentPriority || playerPriority == opponentPriority &&
                (p < o || p == o && (Draw($"wild-battle.{phase.ToString().ToLowerInvariant()}.speed-tie", 2) & 1) != 0);
            var first = opponent ? WildBattleSide.Opponent : WildBattleSide.Player;
            Add(n => new WildBattleOrderEvent(n, State.Turn, phase, first, Other(first)));
            return first;
        }

        internal void SourceEndTurnFieldEffects(PokemonCatalog catalog, WildBattleSide first) =>
            new SinglesEndTurnBridge(this).FieldEffects(catalog, [new((int)first), new((int)Other(first))]);

        // Narrow split of the legacy combined field tickers. Legacy entry points remain unchanged.
        private void SourceEndTurnFieldTimer(WildBattleSide side, string effect) =>
            new SinglesEndTurnBridge(this).TickTimer((BattleSide)side, effect);

        internal void SourceEndTurnBattlerEffects(PokemonCatalog catalog, WildBattleSide side) =>
            new SinglesEndTurnBridge(this).BattlerEffects(new((int)side));

        private void SourceEndTurnAbility(WildBattleSide side) => new SinglesEndTurnBridge(this).Ability(new((int)side));
        private void SourceEndTurnStatusDamage(WildBattleSide side, uint status) =>
            new SinglesEndTurnBridge(this).StatusDamage(new((int)side), status);
        private void SourceEndTurnDisable(WildBattleSide side)
            => new SinglesEndTurnBridge(this).Disable(new((int)side));
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
            SourceTraceForecastFaintedActions();
            SourceCalledSynchronizeParties();
        }
    }
}
