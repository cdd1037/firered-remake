using Game.Domain.Pokemon;

/// <summary>Shared settled Easy Chat field/footer cursor, including Mail's partial final row.</summary>
public static class EasyChatEditorNavigation
{
    public static (int Row, int Column) Move(SourceEasyChatTemplate template, int row, int column, int dx, int dy)
    {
        row = (row + dy + template.Rows + 1) % (template.Rows + 1);
        int columns = row == template.Rows ? 3 : Math.Min(template.Columns, template.WordCount - row * template.Columns);
        if (dx != 0) column = (column + dx + columns) % columns;
        return (row, Math.Min(column, columns - 1));
    }
}
