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
