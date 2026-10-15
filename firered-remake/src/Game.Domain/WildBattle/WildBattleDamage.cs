namespace Game.Domain.WildBattle;

public static class WildBattleDamage
{
    public static int Stat(int basis, int stage)
    {
        if (basis is < 1 or > 999 || stage is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(stage));
        return stage < 6 ? basis * 10 / (10 + (6 - stage) * 5) : basis * (10 + (stage - 6) * 5) / 10;
    }

    public static int Accuracy(int baseAccuracy, int accuracyStage, int evasionStage)
    {
        if (baseAccuracy is < 1 or > 100 || accuracyStage is < 0 or > 12 || evasionStage is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(accuracyStage));
        (int numerator, int denominator) = Math.Clamp(accuracyStage + 6 - evasionStage, 0, 12) switch
        {
            0 => (33, 100), 1 => (36, 100), 2 => (43, 100), 3 => (50, 100), 4 => (60, 100),
            5 => (75, 100), 6 => (1, 1), 7 => (133, 100), 8 => (166, 100), 9 => (2, 1),
            10 => (233, 100), 11 => (133, 50), _ => (3, 1)
        };
        return baseAccuracy * numerator / denominator;
    }
}
