using System.Collections.Immutable;
using System.Globalization;
namespace Game.Domain.Events;

/// <summary>Static source labels and source FONT_NORMAL_COPY_1 width, mounted by authenticated Content.</summary>
public sealed record StoryMenuOption(string TextSymbol, string Text);
public sealed record StoryMenuDefinition(string Id, int SourceId, ImmutableArray<StoryMenuOption> Options, int ColumnWidthTiles)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public StoryListMenuLayout? ListLayout { get; init; }
}
/// <summary>Source-native scrolling list geometry and whether topic selection suspends the window.</summary>
public sealed record StoryListMenuLayout(int Left, int Top, int VisibleRows, int HeightTiles, bool KeepOpen);
public sealed record StoryListMenuState(string MenuId, int SelectedIndex, bool Suspended);
public sealed record StoryGridMenu(StoryMenuDefinition Definition, int Left, int Top, int Columns, bool IgnoreB)
{
    public int Rows => Definition.Options.Length / Columns;
    public const int CancelResult = 127;
    public bool Accepts(int? choice) => choice is { } value &&
        (value >= 0 && value < Definition.Options.Length || value == CancelResult && !IgnoreB);

    // Menu_ProcessInputGridLayout uses MultichoiceGrid_MoveCursorIfValid, not the unused wrapping function.
    public int MoveCursor(int cursor, int dx, int dy)
    {
        if (cursor < 0 || cursor >= Definition.Options.Length || Math.Abs(dx) + Math.Abs(dy) != 1)
            throw new ArgumentOutOfRangeException(nameof(cursor));
        int x = cursor % Columns + dx, y = cursor / Columns + dy;
        return x >= 0 && x < Columns && y >= 0 && y < Rows ? y * Columns + x : cursor;
    }
}

public static class StoryGridMenuRules
{
    public static StoryGridMenu? Pending(StoryState state, IStoryCatalog catalog)
    {
        if (state.Wait?.Kind != StoryWaitKind.MenuChoice || state.Stack.IsEmpty) return null;
        var frame = state.Stack[^1];
        var program = catalog.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length
            ? Resolve(program.Instructions[index], catalog) : null;
    }

    public static StoryGridMenu? Resolve(StoryInstruction instruction, IStoryCatalog catalog)
    {
        if (instruction.Op != StoryOp.Multichoice || instruction.Opcode != "multichoicegrid" || instruction.Args.Length != 5)
            return null;
        var args = instruction.Args;
        // Source operands are bytes, not variables. Keep the supported static labels and visible rectangle bounded.
        bool Number(string token, out int number) => int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out number) ||
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number);
        if (!Number(args[0], out int left) || !Number(args[1], out int top) || !Number(args[3], out int columns) ||
            left is < 0 or > 28 || top is < 0 or > 18 || columns is < 1 or > 12 || args[4] is not ("FALSE" or "TRUE" or "0" or "1") ||
            catalog.FindMenu(args[2]) is not { } definition || definition.Id != args[2] ||
            !catalog.TryConstant(args[2], out int sourceId) || sourceId != definition.SourceId || sourceId is < 0 or > 255 ||
            definition.Options.IsDefault || definition.Options.Length is < 1 or > 12 || definition.Options.Length % columns != 0 ||
            definition.ColumnWidthTiles is < 2 or > 28 || left + definition.ColumnWidthTiles * columns + 2 > 30 ||
            top + definition.Options.Length / columns * 2 + 2 > 20 ||
            definition.Options.Any(option => option is null || string.IsNullOrWhiteSpace(option.TextSymbol) ||
                string.IsNullOrWhiteSpace(option.Text) || option.Text.Length > 64 || option.Text.Any(c => c < ' ' || c > '~')))
            return null;
        return new(definition, left, top, columns, args[4] is "TRUE" or "1");
    }
}
