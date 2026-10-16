using System.Collections.Immutable;

namespace Game.Domain.Events;

/// <summary>The eight original Roof multichoices. Their suspended source instruction owns the callback range.</summary>
public sealed record DepartmentRooftopMenu(string Id, int DrinkMask, bool Vending, ImmutableArray<string> Rows);
public static class DepartmentRooftopSource
{
    public const string Map = "map.celadon-city-department-store-roof";
    public const string Path = "data/maps/CeladonCity_DepartmentStore_Roof/scripts.inc";
    public const string Prefix = "CeladonCity_DepartmentStore_Roof_EventScript_";
    public const string Girl = Prefix + "ThirstyGirl";
    public const string Vending = Prefix + "VendingMachine";
    private static readonly string[] Drinks = ["FRESH WATER", "SODA POP", "LEMONADE"];
    private static readonly string[] Suffixes = ["", "FreshWater", "SodaPop", "FreshWaterSodaPop", "Lemonade", "FreshWaterLemonade", "SodaPopLemonade", "AllDrinks"];
    private static readonly string[] MenuIds = ["", "FRESH_WATER", "SODA_POP", "FRESH_WATER_SODA_POP", "LEMONADE", "FRESH_WATER_LEMONADE", "SODA_POP_LEMONADE", "FRESH_WATER_SODA_POP_LEMONADE"];
    public static DepartmentRooftopMenu? Menu(StoryInstruction? instruction)
    {
        if (instruction is not { Op: StoryOp.Multichoice, Opcode: "multichoice" } || instruction.Source.Path != Path ||
            instruction.Args.Length != 4 || instruction.Args[1] != "0" || instruction.Args[3] != "FALSE") return null;
        var a = instruction.Args;
        if (instruction.Source.Symbol == Prefix + "ChooseDrink" && a[0] == "12" && a[2] == "MULTICHOICE_CELADON_VENDING_MACHINE")
            return new(a[2], 7, true, [.. Drinks, "EXIT"]);
        for (int mask = 1; mask <= 7; mask++)
            if (instruction.Source.Symbol == Prefix + "AskGive" + Suffixes[mask] && a[0] == "0" && a[2] == "MULTICHOICE_THIRSTY_GIRL_" + MenuIds[mask])
                return new(a[2], mask, false, [.. Drinks.Where((_, i) => (mask & (1 << i)) != 0), "EXIT"]);
        return null;
    }
    public static StoryInstruction? Instruction(StoryState state, IStoryCatalog catalog)
    {
        if (state.Stack.IsEmpty) return null;
        var frame = state.Stack[^1]; var program = catalog.FindProgram(frame.ProgramId); int i = frame.InstructionIndex - 1;
        return program is not null && i >= 0 && i < program.Instructions.Length ? program.Instructions[i] : null;
    }
    public static DepartmentRooftopMenu? Pending(StoryState state, IStoryCatalog catalog) =>
        state.Wait?.Kind == StoryWaitKind.MenuChoice ? Menu(Instruction(state, catalog)) : null;
}
