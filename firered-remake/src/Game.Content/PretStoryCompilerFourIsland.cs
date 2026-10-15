using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableHashSet<string>FourIslandMapSources = new[] {"data/maps/FourIsland/scripts.inc","data/maps/FourIsland_Harbor/scripts.inc","data/maps/FourIsland_IcefallCave_1F/scripts.inc","data/maps/FourIsland_IcefallCave_B1F/scripts.inc","data/maps/FourIsland_IcefallCave_Back/scripts.inc","data/maps/FourIsland_IcefallCave_Entrance/scripts.inc","data/maps/FourIsland_Mart/scripts.inc","data/maps/FourIsland_PokemonCenter_1F/scripts.inc"}.ToImmutableHashSet(StringComparer.Ordinal);

    private static bool IsFourIslandMapSource(PretSourceScriptFile file) =>
        FourIslandMapSources.Contains(file.Path) && PokemonLeagueSource(file,file.Path);

    private static bool IsFourIslandAssemblyData(PretSourceScriptFile file,string label,string[] body)
    {
        if (!IsFourIslandMapSource(file)) return false;
        if (label == file.Path.Split('/')[2] + "_MapScripts" && body.SequenceEqual(new[] { ".byte 0" })) return true;
        return label == "FourIsland_Mart_Items" && file.Path == "data/maps/FourIsland_Mart/scripts.inc" &&
            body.SequenceEqual(new[] { ".2byte ITEM_ULTRA_BALL",".2byte ITEM_FULL_RESTORE",".2byte ITEM_MAX_POTION",".2byte ITEM_REVIVE",".2byte ITEM_ICE_HEAL",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end" });
    }

    private static (StoryOp, int, int)? FourIslandInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if (!IsFourIslandMapSource(file)) return null;
        if (opcode == "trainerbattle_no_intro" && label == "FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene")
        {
            if (!operands.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_GRUNT_45","FourIsland_IcefallCave_Back_Text_GruntDefeat" }))
                throw new InvalidDataException("four_island_trainer_operands");
            return (StoryOp.TrainerBattle,2,2);
        }
        if (opcode == "setstepcallback" && label == "FourIsland_IcefallCave_1F_OnResume")
        {
            if (!operands.SequenceEqual(new[] { "STEP_CB_ICE" })) throw new InvalidDataException("four_island_ice_callback_operands");
            return (StoryOp.Special,1,1);
        }
        if (opcode == "warphole" && label == "FourIsland_IcefallCave_1F_EventScript_FallDownHole")
        {
            if (!operands.SequenceEqual(new[] { "MAP_FOUR_ISLAND_ICEFALL_CAVE_B1F" })) throw new InvalidDataException("four_island_hole_operands");
            return (StoryOp.Special,1,1);
        }
        return null;
    }
}
