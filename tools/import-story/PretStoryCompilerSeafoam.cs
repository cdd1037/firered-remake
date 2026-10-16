using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static (StoryOp,int,int)? SeafoamInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> args)
    {
        if (!file.Path.StartsWith("data/maps/SeafoamIslands_",StringComparison.Ordinal)) return null;
        if (opcode=="setwildbattle" || opcode=="special"&&args.SequenceEqual(new[]{"StartLegendaryBattle"}))
        {
            if(!FirstSeviiSource(file,"data/maps/SeafoamIslands_B4F/scripts.inc")||
                label!="SeafoamIslands_B4F_EventScript_Articuno")return (StoryOp.Unsupported,0,int.MaxValue);
            if(opcode=="setwildbattle")return args.SequenceEqual(new[]{"SPECIES_ARTICUNO","50"})?(StoryOp.Special,2,2):(StoryOp.Unsupported,0,int.MaxValue);
            return (StoryOp.TrainerBattle,1,1);
        }
        if(opcode=="getplayerxy"||opcode=="addvar")return (StoryOp.Special,2,2);
        return null;
    }
}
