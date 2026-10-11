using Game.Domain.Ids;
using Game.Domain.Battle;
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
        => InterceptionPressureSlot(interceptedActorAbility, snatcherMoves, new("pret:MOVE_SNATCH"));

    public static int? InterceptionPressureSlot(string interceptedActorAbility, IReadOnlyList<PokemonMoveSlot> interceptorMoves, MoveId preparedMove)
    {
        if (interceptedActorAbility != "ABILITY_PRESSURE") return null;
        for (int slot = 0; slot < interceptorMoves.Count; slot++)
            if (interceptorMoves[slot].Move == preparedMove) return slot;
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
        private MoveId? preparedPpOwnerPreviousMove;

        private void BeginPreparedMoveInvocation(PokemonCatalog catalog, MoveId? previousMove)
        {
            SourceHitItemsBegin(catalog);
            invocationFireThawStart = State.EventSequence;
            choiceBatonPassFailed = false;
            Recipes.ResetPreparedRegisters();
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
            PokemonMoveDefinition move, string purpose, ref bool handled) =>
            Recipes.ExecutePreparedMove(catalog, S(side), slot, move, purpose, ref handled);

        private bool SourceSnatchMove(ref WildBattleSide side, int slot, PokemonMoveDefinition move)
        {
            if (!Recipes.TryPreparedInterception(move, reflect: false)) return false;
            side = activeInvocation!.Actor; return true;
        }

        private void FinishPreparedMoveRegisters() => Recipes.FinishPreparedRegisters();

        internal void SourceClearSnatch(WildBattleSide side)
        {
            if (Get(side).CalledVolatile is { SnatchOrderIndex: not null } v)
                SetCalledVolatile(side, v with { SnatchOrderIndex = null });
        }
    }
}
