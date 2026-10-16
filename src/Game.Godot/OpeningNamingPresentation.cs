using Game.Content;

public enum OpeningNameAction { Insert, ChangePage, Delete, Submit }
public readonly record struct OpeningNameKey(OpeningNameAction Action, char Character = ' ');

/// <summary>Pointer/controller cursor only. The native editor continues owning text, selection and validation.</summary>
public sealed class OpeningNamingPresentation(PretOpeningPresentation source)
{
    public int Page { get; private set; }
    public int Row { get; private set; }
    public int Column { get; private set; }
    public int Columns => source.Keyboard[Page][0].Length;
    public bool OnAction => Column == Columns;
    public void Reset() { Page = Row = Column = 0; }
    public void Select(int row, int column)
    {
        if (column < 0 || column > Columns || row < 0 || row >= (column == Columns ? 3 : 4)) throw new ArgumentOutOfRangeException(nameof(row));
        Row = row; Column = column;
    }
    public void Move(int dx, int dy)
    {
        Column = (Column + Math.Sign(dx) + Columns + 1) % (Columns + 1);
        int rows = OnAction ? 3 : 4;
        Row = (Math.Min(Row, rows - 1) + Math.Sign(dy) + rows) % rows;
    }
    public void SelectSubmit() => Select(2, Columns);
    public OpeningNameKey Activate()
    {
        if (!OnAction) return new(OpeningNameAction.Insert, source.Keyboard[Page][Row][Column]);
        if (Row == 1) return new(OpeningNameAction.Delete);
        if (Row == 2) return new(OpeningNameAction.Submit);
        Page = (Page + 1) % source.Keyboard.Length; Column = Columns;
        return new(OpeningNameAction.ChangePage);
    }
}
