namespace Game.Domain.WildBattle;

/// <summary>Gen III primary status bits and the bounded sleep counter. Toxic has its own counter contract.</summary>
public static class WildBattleStatus
{
    public const uint None = 0, Poison = 0x08, Burn = 0x10, Freeze = 0x20, Paralysis = 0x40;
    public static bool IsSleeping(uint status) => status is >= 1 and <= 5;
    public static bool IsSupported(uint status, bool earlyStarterRules = false, bool route11Rules = false) =>
        status is None or Poison or Burn or Paralysis || earlyStarterRules && IsSleeping(status) || route11Rules && status == Freeze;
}
