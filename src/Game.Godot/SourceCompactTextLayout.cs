/// <summary>Measured, presentation-only treatment of bounded labels. No stored text,
/// source-page boundary, input, or command is changed.</summary>
public static class SourceCompactTextLayout
{
    public const int ScrollEndHoldTicks = 90;
    public const int ScrollTicksPerPixel = 3;

    public static float FitScale(float measuredWidth, float availableWidth)
    {
        ValidateWidths(measuredWidth, availableWidth);
        return measuredWidth <= availableWidth ? 1 : availableWidth / measuredWidth;
    }

    /// <summary>Unscaled integer-pixel motion with a pause at each fully visible end.</summary>
    public static int ScrollOffset(float measuredWidth, float availableWidth, long tick)
    {
        ValidateWidths(measuredWidth, availableWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        int overflow = (int)Math.Ceiling(Math.Max(0, measuredWidth - availableWidth));
        if (overflow == 0) return 0;
        long travel = (long)overflow * ScrollTicksPerPixel;
        long phase = tick % (2 * (ScrollEndHoldTicks + travel));
        if (phase < ScrollEndHoldTicks) return 0;
        phase -= ScrollEndHoldTicks;
        if (phase < travel) return (int)(phase / ScrollTicksPerPixel);
        phase -= travel;
        if (phase < ScrollEndHoldTicks) return overflow;
        return overflow - (int)((phase - ScrollEndHoldTicks) / ScrollTicksPerPixel);
    }

    /// <summary>Use the naming panel's spare space before mild compression. Longer
    /// invalid/preedit text keeps the native editor's own scrolling at >=85% width.</summary>
    public static SourceNameEntryLayout NameEntry(float measuredWidth, int originalWidth, int entryX, int originalGenderX, float genderWidth)
    {
        float available = 200 - entryX - (genderWidth > 0 ? genderWidth + 2 : 0);
        ValidateWidths(measuredWidth, available);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalWidth);
        if (originalWidth > available) throw new ArgumentException("Original name row exceeds its panel.", nameof(originalWidth));
        float sourceWidth = Math.Max(originalWidth, Math.Min(MathF.Ceiling(measuredWidth), available / .85f));
        float scale = FitScale(sourceWidth, available);
        float displayWidth = sourceWidth * scale;
        return new(sourceWidth, displayWidth, scale,
            genderWidth > 0 ? Math.Max(originalGenderX, entryX + displayWidth + 2) : originalGenderX);
    }

    private static void ValidateWidths(float measuredWidth, float availableWidth)
    {
        if (!float.IsFinite(measuredWidth) || measuredWidth < 0) throw new ArgumentOutOfRangeException(nameof(measuredWidth));
        if (!float.IsFinite(availableWidth) || availableWidth <= 0) throw new ArgumentOutOfRangeException(nameof(availableWidth));
    }
}

public readonly record struct SourceNameEntryLayout(float SourceWidth, float DisplayWidth, float Scale, float GenderX);
