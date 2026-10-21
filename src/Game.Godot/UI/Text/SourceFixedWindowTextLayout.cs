using System.Collections.Immutable;

/// <summary>Measured layout for static text whose existing owner has no continuation input.
/// Ordinary LF boundaries are retained. Overflow fails without returning partial text;
/// callers must never turn a fixed label, choice or inspector into hidden ACK pages.</summary>
public static class SourceFixedWindowTextLayout
{
    public static ImmutableArray<string> Create(string text, int width, int rows, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        if (text.Any(c => char.IsControl(c) && c != '\n'))
            throw new ArgumentException("Expand source controls before fixed-window layout.", nameof(text));
        float Measured(string value)
        {
            float result = measure(value);
            if (!float.IsFinite(result) || result < 0)
                throw new ArgumentException("Font measurement must be finite and nonnegative.", nameof(measure));
            return result;
        }
        var lines = SourceHelpTextLayout.WrapPreservingText(text, width, Measured).ToImmutableArray();
        if (lines.Length > rows || lines.Any(line => Measured(line) > width))
            throw new InvalidDataException($"Text does not fit a {width}px by {rows}-row fixed window.");
        return lines;
    }
}
