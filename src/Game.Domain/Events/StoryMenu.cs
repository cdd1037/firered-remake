using System.Globalization;
namespace Game.Domain.Events;

/// <summary>A catalog-defined vertical menu. Source location does not determine its choices.</summary>
public sealed record StoryMenu(StoryMenuDefinition Definition, int Left, int Top, int DefaultChoice, bool IgnoreB)
{
    public const int CancelResult = 127;
    public bool Accepts(int? choice) => choice is { } value &&
        (value >= 0 && value < Definition.Options.Length || value == CancelResult && !IgnoreB);
}

public static class StoryMenuRules
{
    public static StoryMenu? Pending(StoryState state, IStoryCatalog catalog)
    {
        if (state.Wait?.Kind != StoryWaitKind.MenuChoice || state.Stack.IsEmpty) return null;
        var frame = state.Stack[^1];
        var program = catalog.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length
            ? Resolve(program.Instructions[index], catalog) : null;
    }

    // Existing native services also use multichoice, but do not all have catalog definitions yet.
    // Those services retain their own dispatch until their definitions are migrated.
    public static bool HasDefinition(StoryInstruction instruction, IStoryCatalog catalog) =>
        instruction.Op == StoryOp.Multichoice && instruction.Opcode is "multichoice" or "multichoicedefault" &&
        instruction.Args.Length >= 3 && catalog.FindMenu(instruction.Args[2]) is not null;

    public static StoryMenu? Resolve(StoryInstruction instruction, IStoryCatalog catalog)
    {
        if (!HasDefinition(instruction, catalog)) return null;
        bool withDefault = instruction.Opcode == "multichoicedefault";
        var args = instruction.Args;
        if (args.Length != (withDefault ? 5 : 4)) return null;
        bool Byte(string token, out int value)
        {
            bool parsed = int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) ||
                token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) ||
                catalog.TryConstant(token, out value);
            return parsed && value is >= 0 and <= 255;
        }
        var definition = catalog.FindMenu(args[2])!;
        int defaultChoice = 0;
        if (!Byte(args[0], out int left) || !Byte(args[1], out int top) ||
            !Byte(args[2], out int sourceId) || sourceId != definition.SourceId || definition.Id != args[2] ||
            !Byte(args[^1], out int flags) ||
            definition.Options.IsDefaultOrEmpty || definition.Options.Length > StoryMenu.CancelResult ||
            definition.Options.Any(option => option is null || string.IsNullOrWhiteSpace(option.TextSymbol) ||
                string.IsNullOrWhiteSpace(option.Text)) ||
            withDefault && (!Byte(args[3], out defaultChoice) || defaultChoice >= definition.Options.Length))
            return null;
        // The original vertical menu masks ignoreBpress with 1; bit 2 controls Quest Log display.
        return new(definition, left, top, defaultChoice, (flags & 1) != 0);
    }
}
