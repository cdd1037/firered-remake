using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

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
