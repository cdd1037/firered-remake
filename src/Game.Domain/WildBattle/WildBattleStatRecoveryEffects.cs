using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Cmd_recoverbasedonsunlight arithmetic, independent of weather storage.</summary>
public static class WildBattleSourceRecovery
{
    public static int SunlightAmount(int maximumHp, bool weatherPresent, bool sunny, bool weatherSuppressed)
    {
        if (maximumHp <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHp));
        return Math.Max(1, !weatherPresent || weatherSuppressed ? maximumHp / 2 : sunny ? 20 * maximumHp / 30 : maximumHp / 4);
    }

    /// <summary>Cmd_maxattackhalvehp returns no HP debit on either failed condition.</summary>
    public static int BellyDrumCost(int maximumHp, int currentHp, int attackStage)
    {
        if (maximumHp <= 0 || currentHp < 0 || currentHp > maximumHp || attackStage is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(maximumHp));
        int half = Math.Max(1, maximumHp / 2);
        return attackStage < 12 && currentHp > half ? half : 0;
    }
}
