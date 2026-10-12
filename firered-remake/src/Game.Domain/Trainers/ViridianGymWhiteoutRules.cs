namespace Game.Domain.Trainers;

/// <summary>The original eight-badge overworld loss table. Admission to the eighth-badge
/// application profile remains the caller's responsibility; earlier profiles are not widened.</summary>
public static class ViridianGymWhiteoutRules
{
    public static int Loss(int money, int highestPartyLevel, int badgeCount)
    {
        if (money is < 0 or > 999999) throw new ArgumentOutOfRangeException(nameof(money));
        if (highestPartyLevel is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(highestPartyLevel));
        if (badgeCount is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(badgeCount));
        ReadOnlySpan<int> multipliers = [2, 4, 6, 9, 12, 16, 20, 25, 30];
        return Math.Min(money, highestPartyLevel * 4 * multipliers[badgeCount]);
    }
}
