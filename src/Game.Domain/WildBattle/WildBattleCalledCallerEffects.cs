using System.Text.Json.Serialization;

namespace Game.Domain.WildBattle;

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
