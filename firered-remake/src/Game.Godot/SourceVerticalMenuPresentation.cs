/// <summary>Native vertical menu geometry and cursor rules, shared by catalog and facility menus.</summary>
internal class SourceVerticalMenuPresentation
{
    internal readonly record struct Layout(int X, int Y, int Width, int Height);
    internal const int Cancel = 127;
    private readonly int _left;
    private readonly int _top;
    internal IReadOnlyList<string> Options { get; }
    internal int Cursor { get; private set; }
    internal SourceVerticalMenuPresentation(IEnumerable<string> options, int left, int top, int defaultCursor = 0)
    {
        var rows = options.ToArray();
        if (rows.Length is < 1 or > 127 || rows.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Menu options are required.", nameof(options));
        if (left is < 0 or > 255 || top is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(left));
        if (defaultCursor < 0 || defaultCursor >= rows.Length) throw new ArgumentOutOfRangeException(nameof(defaultCursor));
        Options = Array.AsReadOnly(rows); _left = left; _top = top; Cursor = defaultCursor;
    }
    internal Layout Frame(int textWidth)
    {
        if (textWidth is <= 0 or > 200) throw new ArgumentOutOfRangeException(nameof(textWidth));
        int width = (textWidth + 9) / 8 + 1;
        return new(Math.Min(_left, 28 - width) * 8, _top * 8, (width + 2) * 8, ((Options.Count * 14 + 7) / 8 + 2) * 8);
    }
    internal bool Move(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        int next = Options.Count > 3 ? (Cursor + direction + Options.Count) % Options.Count : Math.Clamp(Cursor + direction, 0, Options.Count - 1);
        return Select(next);
    }
    internal bool Select(int index)
    {
        if (index < 0 || index >= Options.Count || index == Cursor) return false;
        Cursor = index; return true;
    }
    internal int RowAt(double x, double y, int textWidth)
    {
        var f = Frame(textWidth);
        return x >= f.X + 8 && x < f.X + f.Width - 8 && y >= f.Y + 8 && y < f.Y + 8 + 14 * Options.Count
            ? (int)((y - f.Y - 8) / 14) : -1;
    }
}
