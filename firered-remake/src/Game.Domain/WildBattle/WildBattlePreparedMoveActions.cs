using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public sealed record WildBattleFocusPunchPreparedEvent(long Sequence, int Turn, WildBattleSide Actor,
    int ChosenSlot, int PartyIndex) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleFocusPunchLostEvent(long Sequence, int Turn, WildBattleSide Actor,
    int PhysicalDamage, int SpecialDamage) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSnatchReadyEvent(long Sequence, int Turn, WildBattleSide Actor,
    int ActionOrderIndex) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMoveSnatchedEvent(long Sequence, int Turn, WildBattleSide OriginalActor,
    WildBattleSide Snatcher, MoveId Move, WildBattleSide EffectTarget) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSnatchPressurePpEvent(long Sequence, int Turn, WildBattleSide PressuringActor,
    WildBattleSide Snatcher, int Slot, int Before, int After) : WildBattleEvent(Sequence, Turn);

/// <summary>Source command predicates independent of the singles owner. Battler count is the retained
/// source queue size, including canceled/absent actions, never the number of living combatants.</summary>
public static class WildBattleSourcePreparedMoves
{
    public static bool FocusPunchSetup(MoveId chosen, uint status, bool truantLoafing, bool noValidMoves,
        bool runMarker) => !runMarker && chosen.Value == "pret:MOVE_FOCUS_PUNCH" &&
        !WildBattleStatus.IsSleeping(status) && !truantLoafing && !noValidMoves;
    /// <summary>PressurePPLose finds the first actual learned Snatch slot, including an exhausted one.</summary>
    public static int? SnatchPressureSlot(string interceptedActorAbility, IReadOnlyList<PokemonMoveSlot> snatcherMoves)
    {
        if (interceptedActorAbility != "ABILITY_PRESSURE") return null;
        for (int slot = 0; slot < snatcherMoves.Count; slot++)
            if (snatcherMoves[slot].Move.Value == "pret:MOVE_SNATCH") return slot;
        return null;
    }
    public static int SnatchPressurePpAfter(int before) => before is >= 0 and <= 255
        ? Math.Max(0, before - 1) : throw new ArgumentOutOfRangeException(nameof(before));

    public static bool SnatchCanArm(int actionIndex, int battlerCount)
    {
        if (battlerCount is not (2 or 4) || actionIndex < 0 || actionIndex >= battlerCount)
            throw new ArgumentOutOfRangeException(nameof(actionIndex));
        return actionIndex != battlerCount - 1;
    }
}

public static partial class WildBattleRules
{
    private static void AddPreparedMoveSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && move.Effect is "EFFECT_FOCUS_PUNCH" or "EFFECT_SNATCH")
            supported = true;
    }

    internal sealed partial class Execution
    {
        private bool choiceBatonPassFailed;
        private bool preparedSwapAttackerTarget;
        private MoveId? preparedPpOwnerPreviousMove;

        private void BeginPreparedMoveInvocation(PokemonCatalog catalog, MoveId? previousMove)
        {
            SourceHitItemsBegin(catalog);
            choiceBatonPassFailed = false;
            preparedSwapAttackerTarget = false;
            preparedPpOwnerPreviousMove = previousMove;
        }

        /// <summary>One pre-action pass in source numeric battler order. The existing actual party
        /// supplies identity; no setup is emitted by called execution or by a replacement's entry.</summary>
        private void PrepareSourceFocusPunch(PokemonCatalog catalog, int playerSlot, int opponentSlot, bool runMarker)
        {
            if (State.RulesVersion != FuchsiaRulesVersion || State.CalledParty is not { } party) return;
            foreach (var (side, slot) in new[] { (WildBattleSide.Player, playerSlot), (WildBattleSide.Opponent, opponentSlot) })
            {
                var mon = Get(side);
                if (slot < 0 || slot >= mon.Pokemon.Moves.Length || mon.Pokemon.CurrentHp == 0) continue;
                var chosen = catalog.Moves[mon.Pokemon.Moves[slot].Move];
                // Source tests the raw register even after an ability change removed Truant.
                if (!WildBattleSourcePreparedMoves.FocusPunchSetup(chosen.Id, mon.Status, mon.TruantLoafing, false, runMarker)) continue;
                Add(n => new WildBattleFocusPunchPreparedEvent(n, State.Turn, side, slot,
                    WildBattleSourcePartyEffects.ActiveIndex(party, side)));
            }
        }

        private void ExecutePreparedMoveEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            if (move.Effect is not ("EFFECT_FOCUS_PUNCH" or "EFFECT_SNATCH")) return;
            if (activeInvocation is null || !MetronomeSourceMoveDefinition.Matches(move))
                throw new UnsupportedAction("prepared_move_source_definition");
            handled = true;
            if (move.Effect == "EFFECT_SNATCH")
            {
                activeInvocation = activeInvocation with { Flags = activeInvocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
                bool arm = WildBattleSourcePreparedMoves.SnatchCanArm(calledActionContext.ActionOrderIndex, 2);
                if (arm) SetCalledVolatile(side, (Get(side).CalledVolatile ?? new()) with { SnatchOrderIndex = calledActionContext.ActionOrderIndex });
                SpendInvocationPp(side, activeInvocation.Target, move);
                if (arm) Add(n => new WildBattleSnatchReadyEvent(n, State.Turn, side, calledActionContext.ActionOrderIndex));
                else Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, activeInvocation.Target, move.Id, "snatch-last-action"));
                return;
            }
            var damage = Get(side).CalledDamage;
            int physical = damage?.Turn == State.Turn ? damage.PhysicalDamage : 0;
            int special = damage?.Turn == State.Turn ? damage.SpecialDamage : 0;
            if (physical != 0 || special != 0)
            {
                // attackcanceler records Protect before jumpifnodamage, but never bypasses that
                // command. Lost focus has no attack string and does not itself mark move failure.
                if (Get(activeInvocation.Target).Protected) invocationCommandNoEffect = true;
                SpendInvocationPp(side, activeInvocation.Target, move, printAttackString: false);
                Add(n => new WildBattleFocusPunchLostEvent(n, State.Turn, side, physical, special));
                return;
            }
            if (RockTunnelProtectionBlocks(side, slot, move)) return;
            SourceGenericHit(catalog, side, activeInvocation.Target, slot, move, purpose);
        }

        private bool SourceSnatchMove(ref WildBattleSide side, int slot, PokemonMoveDefinition move)
        {
            if (activeInvocation is not { } invocation || !move.Flags.Contains("FLAG_SNATCH_AFFECTED")) return false;
            // Stored queue indices preserve source order even when a faster action was canceled.
            var candidates = new[] { WildBattleSide.Player, WildBattleSide.Opponent }
                .Where(s => Get(s).CalledVolatile?.SnatchOrderIndex is not null)
                .OrderBy(s => Get(s).CalledVolatile!.SnatchOrderIndex).ToArray();
            if (candidates.Length == 0) return false;
            var original = side; var snatcher = candidates[0];
            var armed = Get(snatcher).CalledVolatile!;
            SourceSnatchPressure(original, snatcher);
            SetCalledVolatile(snatcher, armed with { SnatchOrderIndex = null });
            SpendInvocationPp(original, invocation.Target, move);
            // snatchsetbattlers followed by swapattackerwithtarget. USER initially has
            // equal actor/target; PSYCH_UP (SELECTED) retains the intercepted actor as target.
            var target = invocation.Actor == invocation.Target ? snatcher : original;
            Add(n => new WildBattleMoveSnatchedEvent(n, State.Turn, original, snatcher, move.Id, target));
            activeInvocation = activeInvocation! with { Actor = snatcher, Target = target, PpOwner = invocation.PpOwner,
                Flags = activeInvocation!.Flags | WildBattleInvocationFlags.AttackStringPrinted |
                    WildBattleInvocationFlags.NoPpDeduct | WildBattleInvocationFlags.AllowNoPp };
            preparedSwapAttackerTarget = !preparedSwapAttackerTarget;
            side = snatcher;
            return true;
        }

        private void SourceSnatchPressure(WildBattleSide original, WildBattleSide snatcher)
        {
            var mon = Get(snatcher);
            if (WildBattleSourcePreparedMoves.SnatchPressureSlot(WildBattleEffective.Ability(Get(original)), mon.Pokemon.Moves) is not { } slot) return;
            int before = mon.Pokemon.Moves[slot].CurrentPp, after = WildBattleSourcePreparedMoves.SnatchPressurePpAfter(before);
            Set(snatcher, mon with { Pokemon = mon.Pokemon with { Moves = mon.Pokemon.Moves.SetItem(slot,
                mon.Pokemon.Moves[slot] with { CurrentPp = after }) } });
            Add(n => new WildBattleSnatchPressurePpEvent(n, State.Turn, original, snatcher, slot, before, after));
        }

        private void FinishPreparedMoveRegisters()
        {
            if (!preparedSwapAttackerTarget || activeInvocation is not { } invocation) return;
            activeInvocation = invocation with { Actor = invocation.Target, Target = invocation.Actor, PpOwner = invocation.PpOwner };
            preparedSwapAttackerTarget = false;
            if (activeInvocation.Actor != invocation.PpOwner)
                Set(invocation.PpOwner, Get(invocation.PpOwner) with { LastUsedMove = preparedPpOwnerPreviousMove });
        }

        internal void SourceClearSnatch(WildBattleSide side)
        {
            if (Get(side).CalledVolatile is { SnatchOrderIndex: not null } v)
                SetCalledVolatile(side, v with { SnatchOrderIndex = null });
        }
    }
}
