using System.Text.Json.Serialization;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Source singles action position and actual party slot. A full-turn caller must supply both.</summary>
public sealed record WildBattleCalledActionContext(int ActionOrderIndex = 0, int ActorPartyIndex = 0);

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
public sealed record WildBattleSubstituteDamagedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical, int RandomPercent)
    : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledDelayedEvent(long Sequence, int Turn, WildBattleSide Target, MoveId Move,
    int Before, int After, string Reason, int? SourcePartyIndex = null) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMagicCoatPressurePpEvent(long Sequence, int Turn, WildBattleSide PressuringActor, WildBattleSide Reflector, int Slot, int Before, int After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMagicCoatEvent(long Sequence, int Turn, WildBattleSide OriginalActor,
    WildBattleSide Reflector, MoveId Move) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleGrudgePpEvent(long Sequence, int Turn, WildBattleSide Fainted,
    WildBattleSide Attacker, MoveId Move, int ChosenSlot, int Before) : WildBattleEvent(Sequence, Turn);
