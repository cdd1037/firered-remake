namespace Game.Domain.Growth;

/// <summary>Source growth-rate IDs; the two unused duplicate tables are not additional curves.</summary>
public enum GrowthRate
{
    MediumFast = 0,
    Erratic = 1,
    Fluctuating = 2,
    MediumSlow = 3,
    Fast = 4,
    Slow = 5
}

public sealed record ExperienceProgress(
    GrowthRate Rate,
    uint Experience,
    int Level,
    uint CurrentLevelThreshold,
    uint? NextLevelThreshold,
    uint ExperienceToNextLevel);

/// <summary>
/// Pure threshold rules for the six FireRed curves. Does not award battle experience or change a Pokémon.
/// Fixed source: pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14,
/// src/data/pokemon/experience_tables.h:1-637 and src/pokemon.c:2184-2205.
/// </summary>
public static class GrowthRules
{
    public const string RulesVersion = "firered-growth-thresholds-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int MinimumLevel = 1;
    public const int MaximumLevel = 100;

    /// <summary>Total experience stored in the pinned table, including the literal 1 at level 1.</summary>
    public static uint ExperienceAtLevel(GrowthRate rate, int level)
    {
        ValidateRate(rate);
        if (level is < MinimumLevel or > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level), "Supported levels are 1 through 100.");
        if (level == 1) return 1;

        int squared = level * level;
        int cubed = squared * level;
        // Keep the source macro's integer divisions in their exact positions.
        int experience = rate switch
        {
            GrowthRate.MediumFast => cubed,
            GrowthRate.Erratic when level <= 50 => (100 - level) * cubed / 50,
            GrowthRate.Erratic when level <= 68 => (150 - level) * cubed / 100,
            GrowthRate.Erratic when level <= 98 => ((1911 - 10 * level) / 3) * cubed / 500,
            GrowthRate.Erratic => (160 - level) * cubed / 100,
            GrowthRate.Fluctuating when level <= 15 => ((level + 1) / 3 + 24) * cubed / 50,
            GrowthRate.Fluctuating when level <= 36 => (level + 14) * cubed / 50,
            GrowthRate.Fluctuating => (level / 2 + 32) * cubed / 50,
            GrowthRate.MediumSlow => 6 * cubed / 5 - 15 * squared + 100 * level - 140,
            GrowthRate.Fast => 4 * cubed / 5,
            GrowthRate.Slow => 5 * cubed / 4,
            _ => throw new ArgumentOutOfRangeException(nameof(rate))
        };
        return (uint)experience;
    }

    /// <summary>
    /// Largest table level whose threshold is at most experience, capped at 100.
    /// Experience 0 is rejected: upstream derives level 0 for that malformed/uninitialized value;
    /// it is outside this module's supported level-1-through-100 profile.
    /// </summary>
    public static int LevelFromExperience(GrowthRate rate, uint experience)
    {
        ValidateRate(rate);
        if (experience == 0) throw new ArgumentOutOfRangeException(nameof(experience), "A supported Pokémon has at least 1 experience.");
        int level = MinimumLevel;
        while (level < MaximumLevel && ExperienceAtLevel(rate, level + 1) <= experience) level++;
        return level;
    }

    /// <summary>Null at level 100; never asks the source table for index 101.</summary>
    public static uint? NextLevelThreshold(GrowthRate rate, int level)
    {
        _ = ExperienceAtLevel(rate, level);
        return level == MaximumLevel ? null : ExperienceAtLevel(rate, level + 1);
    }

    public static ExperienceProgress GetProgress(GrowthRate rate, uint experience)
    {
        int level = LevelFromExperience(rate, experience);
        uint? next = NextLevelThreshold(rate, level);
        return new(rate, experience, level, ExperienceAtLevel(rate, level), next, next.HasValue ? next.Value - experience : 0);
    }

    private static void ValidateRate(GrowthRate rate)
    {
        if (rate is < GrowthRate.MediumFast or > GrowthRate.Slow)
            throw new ArgumentOutOfRangeException(nameof(rate), "Only the six named source growth rates are supported.");
    }
}
