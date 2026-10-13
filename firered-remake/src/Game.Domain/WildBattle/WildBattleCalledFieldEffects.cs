using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Opt-in source field registers; never admitted by the historical Fuchsia profile.</summary>
public sealed record WildBattleCalledFieldState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SunTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int HailTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerLightScreenTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentLightScreenTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerMistTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentMistTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerSafeguardTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentSafeguardTurnsRemaining { get; init; }
}

/// <summary>Bounded source commands, shared with the executor and its later full-turn caller.</summary>
public static class WildBattleSourceFieldEffects
{
    public const uint ToxicPoison = 0x80, ToxicCounterMask = 0xF00;
    public static bool IsToxic(uint status) => (status & ToxicPoison) != 0;
    public static bool IsValidToxic(uint status) => IsToxic(status) && (status & ~(ToxicPoison | ToxicCounterMask)) == 0;
    public static (uint Status, int Damage) ToxicTick(uint status, int maximumHp)
    {
        if (!IsValidToxic(status) || maximumHp <= 0) throw new ArgumentOutOfRangeException(nameof(status));
        uint after = (status & ToxicCounterMask) == ToxicCounterMask ? status : status + 0x100;
        // FireRed divides first, clamps the quotient, then multiplies by the counter.
        return (after, checked(Math.Max(1, maximumHp / 16) * (int)((after & ToxicCounterMask) >> 8)));
    }
    public static uint ResetToxicCounter(uint status) => IsValidToxic(status) ? ToxicPoison : status;
    public static int TickTimer(int remaining) => remaining is < 0 or > 5
        ? throw new ArgumentOutOfRangeException(nameof(remaining)) : Math.Max(0, remaining - 1);
    public static (ElementType Type, int Multiplier) WeatherBall(bool rain, bool sand, bool sun, bool hail, bool suppressed)
    {
        if ((rain ? 1 : 0) + (sand ? 1 : 0) + (sun ? 1 : 0) + (hail ? 1 : 0) > 1)
            throw new ArgumentException("Mutually exclusive source weather is required.");
        if (suppressed) return (ElementType.Normal, 1);
        return rain ? (ElementType.Water, 2) : sand ? (ElementType.Rock, 2) : sun ? (ElementType.Fire, 2) :
            hail ? (ElementType.Ice, 2) : (ElementType.Normal, 1);
    }
}
