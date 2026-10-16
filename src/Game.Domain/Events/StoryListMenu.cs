namespace Game.Domain.Events;

/// <summary>Native source list callbacks share the catalog menu contract, without map-specific dispatch.</summary>
public static class StoryListMenuRules
{
    public static bool IsOpen(StoryInstruction instruction) => instruction.Op == StoryOp.Special &&
        instruction.Opcode == "special" && instruction.Args.SequenceEqual(new[] { "ListMenu" });
    public static bool IsReturn(StoryInstruction instruction) => instruction.Op == StoryOp.Special &&
        instruction.Opcode == "special" && instruction.Args.SequenceEqual(new[] { "ReturnToListMenu" });

    public static StoryMenu? Display(StoryState state, IStoryCatalog catalog)
    {
        if (state.ListMenu is not { } context || catalog.FindMenu(context.MenuId) is not { ListLayout: { } layout } definition)
            return null;
        return new(definition, layout.Left, layout.Top, context.SelectedIndex, false);
    }

    public static bool IsCallback(StoryInstruction instruction, StoryState state, IStoryCatalog catalog)
    {
        if (instruction.Op != StoryOp.WaitState || instruction.Opcode != "waitstate" || !instruction.Args.IsEmpty ||
            state.ListMenu is not { Suspended: false } || Display(state, catalog) is null || state.Stack.IsEmpty)
            return false;
        var frame = state.Stack[^1];
        var program = catalog.FindProgram(frame.ProgramId);
        int previous = frame.InstructionIndex - 2;
        return program is not null && previous >= 0 && previous < program.Instructions.Length &&
            (IsOpen(program.Instructions[previous]) || IsReturn(program.Instructions[previous]));
    }

    public static StoryMenu? Pending(StoryState state, IStoryCatalog catalog)
    {
        if (state.Wait?.Kind != StoryWaitKind.MenuChoice || state.Stack.IsEmpty) return null;
        var frame = state.Stack[^1];
        var program = catalog.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length &&
            IsCallback(program.Instructions[index], state, catalog) ? Display(state, catalog) : null;
    }
}
