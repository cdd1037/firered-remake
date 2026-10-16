/// <summary>Native vertical menu geometry and cursor rules, shared by catalog and facility menus.</summary>
internal class SourceVerticalMenuPresentation
{
    internal readonly record struct Layout(int X, int Y, int Width, int Height);
    internal const int Cancel = 127;
    private readonly int _left;
    private readonly int _top;
    internal IReadOnlyList<string> Options { get; }
    internal int Cursor { get; private set; }
    internal int ScrollOffset { get; private set; }
    internal int VisibleRows { get; }
    internal bool IsScrollingList => _heightTiles is not null;
    internal bool CanScrollUp => ScrollOffset > 0;
    internal bool CanScrollDown => ScrollOffset + VisibleRows < Options.Count;
    private readonly int? _heightTiles;
    internal SourceVerticalMenuPresentation(IEnumerable<string> options, int left, int top, int defaultCursor = 0, int? visibleRows = null, int? heightTiles = null)
    {
        var rows = options.ToArray();
        if (rows.Length is < 1 or > 127 || rows.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Menu options are required.", nameof(options));
        if (left is < 0 or > 255 || top is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(left));
        if (defaultCursor < 0 || defaultCursor >= rows.Length) throw new ArgumentOutOfRangeException(nameof(defaultCursor));
        if (visibleRows is < 1 || visibleRows > rows.Length || visibleRows.HasValue != heightTiles.HasValue ||
            heightTiles is <= 0 || visibleRows * 14 > heightTiles * 8) throw new ArgumentOutOfRangeException(nameof(visibleRows));
        Options = Array.AsReadOnly(rows); _left = left; _top = top;
        VisibleRows = visibleRows ?? rows.Length; _heightTiles = heightTiles;
        Select(defaultCursor);
    }
    internal Layout Frame(int textWidth)
    {
        if (textWidth is <= 0 or > 200) throw new ArgumentOutOfRangeException(nameof(textWidth));
        int width = (textWidth + 9) / 8 + 1;
        if (_heightTiles is { } listHeight)
            return new(Math.Max(0, Math.Min(_left, 29 - width) - 1) * 8, Math.Max(0, _top - 1) * 8,
                (width + 2) * 8, (listHeight + 2) * 8);
        return new(Math.Min(_left, 28 - width) * 8, _top * 8, (width + 2) * 8, ((Options.Count * 14 + 7) / 8 + 2) * 8);
    }
    internal bool Move(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (IsScrollingList)
        {
            int nextIndex = Math.Clamp(Cursor + direction, 0, Options.Count - 1);
            if (nextIndex == Cursor) return false;
            int row = Cursor - ScrollOffset;
            int threshold = VisibleRows == 1 ? 0 : direction < 0 ? VisibleRows - (VisibleRows + 1) / 2 - 1 : (VisibleRows + 1) / 2;
            if (direction < 0 && ScrollOffset > 0 && row <= threshold) ScrollOffset--;
            else if (direction > 0 && CanScrollDown && row >= threshold) ScrollOffset++;
            Cursor = nextIndex; return true;
        }
        int next = Options.Count > 3 ? (Cursor + direction + Options.Count) % Options.Count : Math.Clamp(Cursor + direction, 0, Options.Count - 1);
        return Select(next);
    }
    internal bool Select(int index)
    {
        if (index < 0 || index >= Options.Count || index == Cursor) return false;
        if (IsScrollingList && (index < ScrollOffset || index >= ScrollOffset + VisibleRows))
            while (Cursor != index) Move(index < Cursor ? -1 : 1);
        else Cursor = index;
        return true;
    }
    internal int RowAt(double x, double y, int textWidth)
    {
        var f = Frame(textWidth);
        return x >= f.X + 8 && x < f.X + f.Width - 8 && y >= f.Y + 8 && y < f.Y + 8 + 14 * VisibleRows
            ? ScrollOffset + (int)((y - f.Y - 8) / 14) : -1;
    }
}
