using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>An ordinary learned selection in the source action contract, including genuine callers.
/// The move recipe is shared with supplied called results; this is not a whole-turn scheduler.</summary>
public sealed record WildBattleSourceMoveAction(WildBattleSide Actor, int ChosenSlot,
    string RulesVersion = WildBattleRules.CalledMoveKernelRulesVersion)
{
    public WildBattleCalledActionContext Context { get; init; } = new();
}

/// <summary>gProtectStructs damage registers belong to the turn, not last landed move history.</summary>
public sealed record WildBattleCalledDamageState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    public int Turn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PhysicalDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? PhysicalSource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SpecialDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? SpecialSource { get; init; }
}

public sealed record WildBattleCallerSelectionEvent(long Sequence, int Turn, WildBattleSide Actor,
    MoveId Caller, MoveId? Result, ImmutableArray<int> CandidateSourceMoveIds, ulong RngCursor) : WildBattleEvent(Sequence, Turn);
