using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Scalar TurnValuesCleanUp/Ready seam shared by the established singles owner and
/// the bounded slot owner. Residual effects and recharge timers must run before this boundary.</summary>
public static class SourceTurnBoundaryRules
{
    // TurnValuesCleanUp(TRUE), before ordinary residuals and delayed attacks.
    // The consecutive-use byte belongs to DisableStruct and survives this boundary.
    public static WildBattleCombatant PrepareBody(WildBattleCombatant body) =>
        body with { Flinched = false, Protected = false, Enduring = false };

    public static int RechargeAfterReady(int turnsRemaining) => Math.Max(0, turnsRemaining - 1);

    public static WildBattleCombatant ReadyBody(WildBattleCombatant body, bool firstTurn, bool damageFlags, bool flinch)
    {
        if (firstTurn) body = body with { FirstTurnCounter = Math.Max(0, body.FirstTurnCounter - 1) };
        if (damageFlags) body = body with { DamagedByOpponentThisTurn = false, Protected = false, Enduring = false };
        if (flinch) body = body with { Flinched = false };
        return body;
    }
}
