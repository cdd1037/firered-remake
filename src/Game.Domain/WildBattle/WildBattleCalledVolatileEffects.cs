using System.Text.Json.Serialization;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Battler-local registers cleared by ordinary departure/faint, independently of delayed slot effects.</summary>
public sealed record WildBattleCalledVolatileState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SubstituteHp { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int StockpileCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PerishSongTimer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Tormented { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int TauntTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool MagicCoat { get; init; }
    /// <summary>Retained action-order position of this turn's armed Snatch; cleared on consumption/departure.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SnatchOrderIndex { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Grudge { get; init; }
}

public sealed record WildBattleCalledWish(int TurnsRemaining, int SourcePartyIndex, SpeciesId SourceSpecies,
    uint SourceOriginalTrainerId, uint SourcePersonality);
public sealed record WildBattleCalledFutureAttack(int TurnsRemaining, MoveId Move, WildBattleSide Attacker, int Damage);
/// <summary>Wish and Future Sight/Doom Desire belong to field positions and survive either combatant switching.</summary>
public sealed record WildBattleCalledDelayedState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledWish? PlayerWish { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledWish? OpponentWish { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledFutureAttack? PlayerFutureAttack { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledFutureAttack? OpponentFutureAttack { get; init; }
}
