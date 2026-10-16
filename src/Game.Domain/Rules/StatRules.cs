namespace Game.Domain.Rules;

public enum Stat { Attack, Defense, Speed, SpecialAttack, SpecialDefense }
public enum Nature
{
    Hardy, Lonely, Brave, Adamant, Naughty, Bold, Docile, Relaxed, Impish, Lax,
    Timid, Hasty, Serious, Jolly, Naive, Modest, Mild, Quiet, Bashful, Rash,
    Calm, Gentle, Sassy, Careful, Quirky
}
public sealed record BattleStats(int MaxHp, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed);

public static class StatRules
{
    public static int CalculateHp(int baseStat, int iv, int ev, int level, bool isShedinja = false)
    {
        Validate(baseStat, iv, ev, level);
        return isShedinja ? 1 : (2 * baseStat + iv + ev / 4) * level / 100 + level + 10;
    }

    public static int CalculateOther(int baseStat, int iv, int ev, int level, Nature nature, Stat stat)
    {
        Validate(baseStat, iv, ev, level);
        int value = (2 * baseStat + iv + ev / 4) * level / 100 + 5;
        return ApplyNature(value, nature, stat);
    }

    // Preserves the original (non-BUGFIX) u16 multiplication wrap, even outside official base-stat combinations.
    public static int ApplyNature(int value, Nature nature, Stat stat)
    {
        if (value is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
        if (!Enum.IsDefined(nature)) throw new ArgumentOutOfRangeException(nameof(nature));
        if (!Enum.IsDefined(stat)) throw new ArgumentOutOfRangeException(nameof(stat));
        int up = (int)nature / 5, down = (int)nature % 5;
        if (up == down) return value;
        if ((int)stat == up) return unchecked((ushort)(value * 110)) / 100;
        if ((int)stat == down) return unchecked((ushort)(value * 90)) / 100;
        return value;
    }

    private static void Validate(int baseStat, int iv, int ev, int level)
    {
        if (baseStat is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(baseStat));
        if (iv is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(iv));
        if (ev is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(ev));
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
    }
}
