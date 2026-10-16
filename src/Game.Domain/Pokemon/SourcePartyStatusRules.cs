using Game.Domain.WildBattle;

namespace Game.Domain.Pokemon;

/// <summary>
/// Persistent status1 in the bounded Fuchsia Gym successor. Toxic counter bits belong to
/// the live battle structure: the source poison residual command writes HP, not party status.
/// </summary>
public static class SourcePartyStatusRules
{
    public const uint Toxic = WildBattleSourceFieldEffects.ToxicPoison;

    public static bool IsSupported(uint status, bool allowSleep = false, bool allowFreeze = false,
        bool allowToxic = false) => WildBattleStatus.IsSupported(status, allowSleep, allowFreeze) ||
        allowToxic && status == Toxic;

    public static bool IsPoison(uint status, bool allowToxic = false) =>
        status == WildBattleStatus.Poison || allowToxic && status == Toxic;

    /// <summary>Source infliction persists Toxic itself; this is not a battle-end cure or downgrade.</summary>
    public static uint FromBattle(uint status) => WildBattleSourceFieldEffects.ResetToxicCounter(status);
}
