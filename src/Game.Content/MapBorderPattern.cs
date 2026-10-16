namespace Game.Content;

/// <summary>Map-local repetition of the original border metatile grid.</summary>
public static class MapBorderPattern
{
    public static int Coordinate(int mapCoordinate, int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        int remainder = mapCoordinate % period;
        return remainder < 0 ? remainder + period : remainder;
    }
}
