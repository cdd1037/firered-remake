namespace Game.Domain.Events;

/// <summary>The three exact original six-row source menus, including NO THANKS.</summary>
public static class GameCornerPrizeSource
{
    public static string? MenuKind(StoryInstruction instruction)
    {
        if (instruction is not { Op: StoryOp.Multichoice, Opcode: "multichoice" } ||
            instruction.Source.Path != "data/maps/CeladonCity_GameCorner_PrizeRoom/scripts.inc" ||
            instruction.Args.Length != 4 || instruction.Args[1] != "0" || instruction.Args[3] != "FALSE") return null;
        return (instruction.Source.Symbol, instruction.Args[0], instruction.Args[2]) switch
        {
            ("CeladonCity_GameCorner_PrizeRoom_EventScript_ChoosePrizeMon", "11", "MULTICHOICE_GAME_CORNER_POKEMON_PRIZES") => "pokemon",
            ("CeladonCity_GameCorner_PrizeRoom_EventScript_ChoosePrizeTM", "11", "MULTICHOICE_GAME_CORNER_TMPRIZES") => "machines",
            ("CeladonCity_GameCorner_PrizeRoom_EventScript_ChoosePrizeItem", "10", "MULTICHOICE_GAME_CORNER_BATTLE_ITEM_PRIZES") => "items",
            _ => null
        };
    }
}
