using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Scalar reset shared with the historical cleanup chain. The canonical faint boundary
/// additionally uses the source FaintClearSetData stage/history reset and M01 slot-link cleanup.</summary>
public static class SourceFaintCommands
{
    public static WildBattleCombatant ClearPewterFlags(WildBattleCombatant mon) => mon with {
        SourceChoiceMove = null, FirstTurnCounter = 0, TruantLoafing = false, Identified = false, DamagedByOpponentThisTurn = false,
        Protected = false, Enduring = false, ProtectConsecutiveUses = 0, Imprison = false, FlashFireBoost = false,
        DefenseCurled = false, BindTurnsRemaining = 0, BindingMove = null };

    internal static WildBattleCombatant ClearCanonicalBody(WildBattleCombatant mon) => ClearPewterFlags(mon) with {
        AttackStage = 6, DefenseStage = 6, SpeedStage = 6, AccuracyStage = 6, EvasionStage = 6,
        SpecialAttackStageDelta = 0, SpecialDefenseStageDelta = 0, FirstTurnCounter = 2,
        CalledVolatile = null, FocusEnergy = false, Flinched = false, ConfusionTurnsRemaining = 0,
        LastUsedMove = null, LastCalledMove = null, LastLandedMove = null, FuryCutterCounter = 0,
        RampageUnableToMove = false, RampageTurnsRemaining = 0, RampageMoveSlot = null,
        RolloutTurnsRemaining = 0, RolloutMoveSlot = null, ChargeTurnsRemaining = 0,
        EncoreTurnsRemaining = 0, EncoreMoveSlot = null, EncoredMove = null,
        DisableTurnsRemaining = 0, DisabledMove = null, DigMoveSlot = null,
        YawnTurnsRemaining = 0, Cursed = false, Nightmare = false, DestinyBond = false,
        Rooted = false, WaterSport = false, MudSport = false, Minimized = false };
}
