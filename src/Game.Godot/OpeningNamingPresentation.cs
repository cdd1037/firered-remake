using System.Collections.Immutable;
using Game.Content;

public enum OpeningNameAction { Insert, ChangePage, Delete, Submit, None }
public readonly record struct OpeningNameKey(OpeningNameAction Action, char Character = ' ');
public readonly record struct OpeningNamingLayer(int Page, int OffsetY);

/// <summary>Source keyboard navigation and page animation. The native editor
/// continues owning text, selection, IME, caret and validation.</summary>
public sealed class OpeningNamingPresentation(PretOpeningPresentation source)
{
    public int Page { get; private set; }
    public int Row { get; private set; }
    public int Column { get; private set; }
    public int VisualFrame { get; private set; }
    public int SwapFrame { get; private set; }
    public bool Swapping { get; private set; }
    public int Columns => source.Keyboard[Page][0].Length;
    public int IncomingPage => (Page + 1) % source.Keyboard.Length;
    public bool OnAction => Column == Columns;
    public int CursorX => 38 + source.NamingColumnOffsets[Page][Math.Min(Column, Columns - 1)];
    public int PageButtonPage => (Page + (Swapping && SwapFrame >= 8 ? 2 : 1)) % source.Keyboard.Length;
    public int PageButtonOffsetY => !Swapping || SwapFrame >= 12 ? 0 : SwapFrame < 8 ? SwapFrame : SwapFrame - 12;
    public bool PageButtonTextVisible => !Swapping || SwapFrame != 8;
    public int ArrowOffsetX => new[] { 0, -4, -2, -1 }[((VisualFrame + 7) / 8) & 3];
    public int UnderscoreOffsetY => new[] { 2, 3, 2, 1 }[(VisualFrame / 9) & 3];
    public void Reset() { Page = Row = Column = VisualFrame = SwapFrame = 0; Swapping = false; }
    public void AdvanceFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        VisualFrame += frames;
        if (!Swapping) return;
        SwapFrame = Math.Min(33, SwapFrame + frames);
        if (SwapFrame != 33) return;
        bool onAction = OnAction;
        Page = IncomingPage; Column = onAction ? Columns : Math.Min(Column, Columns - 1);
        Swapping = false;
    }
    public ImmutableArray<OpeningNamingLayer> Layers(ImmutableArray<int> sine)
    {
        if (sine.Length != 320) throw new ArgumentException("Native sine table required.", nameof(sine));
        if (!Swapping) return [new(Page, 0)];
        int angle = Math.Min(128, SwapFrame * 4);
        var incoming = new OpeningNamingLayer(IncomingPage, -(sine[angle] * 40 >> 8));
        var outgoing = new OpeningNamingLayer(Page, -(sine[(angle + 128) & 255] * 40 >> 8));
        // Source swaps BG priorities at angle 64, before completing the second half.
        return SwapFrame < 16 ? [incoming, outgoing] : [outgoing, incoming];
    }
    public void Select(int row, int column)
    {
        if (column < 0 || column > Columns || row < 0 || row >= (column == Columns ? 3 : 4)) throw new ArgumentOutOfRangeException(nameof(row));
        if (Swapping) return;
        Row = row; Column = column;
    }
    public void Move(int dx, int dy)
    {
        if (Swapping) return;
        Column = (Column + Math.Sign(dx) + Columns + 1) % (Columns + 1);
        int rows = OnAction ? 3 : 4;
        Row = (Math.Min(Row, rows - 1) + Math.Sign(dy) + rows) % rows;
    }
    public void SelectSubmit() => Select(2, Columns);
    public OpeningNameKey Activate()
    {
        if (Swapping) return new(OpeningNameAction.None);
        if (!OnAction) return new(OpeningNameAction.Insert, source.Keyboard[Page][Row][Column]);
        if (Row == 1) return new(OpeningNameAction.Delete);
        if (Row == 2) return new(OpeningNameAction.Submit);
        SwapFrame = 0; Swapping = true;
        return new(OpeningNameAction.ChangePage);
    }
}

/// <summary>Oak's separate outgoing/incoming trainer alpha tasks. Both start
/// with a two-frame wait; outgoing decreases by one, incoming increases by two,
/// with the source delay between callbacks. Platforms toggle at coefficient 8.</summary>
public sealed class OpeningPortraitTransition(string? previous, string? next, int holdFrames = 0)
{
    public string? Previous { get; } = previous;
    public string? Next { get; } = next;
    public int Frame { get; private set; }
    private int OutFrames => Previous is null ? 0 : 49;
    public bool Complete => Frame >= OutFrames + holdFrames + (Next is null ? 0 : 25);
    public string? Portrait => Frame < OutFrames ? Previous : Next;
    public float Alpha => Frame < OutFrames ? Math.Max(0, 16 - Frame / 3) / 16f
        : Math.Clamp((Frame - OutFrames - holdFrames) / 3 * 2, 0, 16) / 16f;
    public bool PlatformVisible => Frame < OutFrames ? Alpha > .5f : Alpha >= .5f;
    public void AdvanceFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        Frame = Math.Min(OutFrames + holdFrames + (Next is null ? 0 : 25), Frame + frames);
    }
}
