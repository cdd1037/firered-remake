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
