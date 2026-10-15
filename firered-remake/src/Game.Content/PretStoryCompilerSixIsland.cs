using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableHashSet<string>SixIslandMapSources = new[] {"data/maps/SixIsland/scripts.inc","data/maps/SixIsland_Harbor/scripts.inc","data/maps/SixIsland_WaterPath/scripts.inc","data/maps/SixIsland_RuinValley/scripts.inc","data/maps/SixIsland_DottedHole_1F/scripts.inc","data/maps/SixIsland_DottedHole_B1F/scripts.inc","data/maps/SixIsland_DottedHole_B2F/scripts.inc","data/maps/SixIsland_DottedHole_B3F/scripts.inc","data/maps/SixIsland_DottedHole_B4F/scripts.inc","data/maps/SixIsland_DottedHole_SapphireRoom/scripts.inc","data/maps/SixIsland_PokemonCenter_1F/scripts.inc","data/maps/SixIsland_Mart/scripts.inc"}.ToImmutableHashSet(StringComparer.Ordinal);

    private static bool IsSixIslandMapSource(PretSourceScriptFile file) =>
        SixIslandMapSources.Contains(file.Path) && FirstSeviiSource(file,file.Path);

    private static bool IsSixIslandAssemblyData(PretSourceScriptFile file,string label,string[] body)
    {
        if (!IsSixIslandMapSource(file)) return false;
        if (label == file.Path.Split('/')[2] + "_MapScripts" && body.SequenceEqual(new[] { ".byte 0" })) return true;
        return label == "SixIsland_Mart_Items" && file.Path == "data/maps/SixIsland_Mart/scripts.inc" &&
            body.SequenceEqual(new[] { ".2byte ITEM_ULTRA_BALL",".2byte ITEM_FULL_RESTORE",".2byte ITEM_MAX_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_DREAM_MAIL",".2byte ITEM_NONE","release","end" });
    }

    private static bool IsSixIslandBrailleWait(PretSourceScriptFile file,string label) =>
        IsSixIslandMapSource(file) && file.Path == "data/maps/SixIsland_DottedHole_SapphireRoom/scripts.inc" &&
        label == "SixIsland_DottedHole_SapphireRoom_EventScript_BrailleMessage";

    private static (StoryOp, int, int)? SixIslandInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if (IsSixIslandMapSource(file) && opcode is "braillemessage" or "getbraillestringwidth")
        {
            if (operands.Length != 1 || !operands[0].StartsWith("Braille_Text_", StringComparison.Ordinal))
                throw new InvalidDataException("six_island_braille_operands");
            return (StoryOp.Special,1,1);
        }
        if (FirstSeviiSource(file,"data/scripts/trainers.inc") &&
            (label,opcode) is
                ("SixIsland_WaterPath_EventScript_Miu","trainerbattle_double") or
                ("SixIsland_WaterPath_EventScript_Mia","trainerbattle_double") or
                ("SixIsland_WaterPath_EventScript_MiuRematch","trainerbattle_rematch_double") or
                ("SixIsland_WaterPath_EventScript_MiaRematch","trainerbattle_rematch_double"))
        {
            if (operands.Length != 4 || operands[0] != "TRAINER_TWINS_MIU_MIA")
                throw new InvalidDataException("six_island_double_operands");
            return (StoryOp.TrainerBattle,4,4);
        }
        // Unsupported adjacent maps never become runnable merely because another source
        // file references their entry. Preserve explicit fault nodes for deferred services.
        if (file.Path.StartsWith("data/maps/SixIsland_", StringComparison.Ordinal) &&
            !SixIslandMapSources.Contains(file.Path)) return (StoryOp.Unsupported,0,int.MaxValue);
        return null;
    }
}
