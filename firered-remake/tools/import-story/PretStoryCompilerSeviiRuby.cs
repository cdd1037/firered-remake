using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableHashSet<string>SeviiRubyMapSources = new[] {"data/maps/MtEmber_Exterior/scripts.inc","data/maps/MtEmber_RubyPath_1F/scripts.inc","data/maps/MtEmber_RubyPath_B1F/scripts.inc","data/maps/MtEmber_RubyPath_B1F_Stairs/scripts.inc","data/maps/MtEmber_RubyPath_B2F/scripts.inc","data/maps/MtEmber_RubyPath_B2F_Stairs/scripts.inc","data/maps/MtEmber_RubyPath_B3F/scripts.inc","data/maps/MtEmber_RubyPath_B4F/scripts.inc","data/maps/MtEmber_RubyPath_B5F/scripts.inc","data/maps/OneIsland_KindleRoad/scripts.inc","data/maps/OneIsland_KindleRoad_EmberSpa/scripts.inc"}.ToImmutableHashSet(StringComparer.Ordinal);
    private static bool IsSeviiRubyAssemblyData(PretSourceScriptFile file,string label,string[] body) =>
        SeviiRubyMapSources.Contains(file.Path)&&FirstSeviiSource(file,file.Path)&&
        label==file.Path.Split('/')[2]+"_MapScripts"&&body.SequenceEqual(new[]{".byte 0"});

    private static (StoryOp,int,int)? SeviiRubyInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if(SeviiRubyMapSources.Contains(file.Path)&&FirstSeviiSource(file,file.Path)&&
            (file.Path is "data/maps/MtEmber_RubyPath_B4F/scripts.inc" or "data/maps/MtEmber_RubyPath_B5F/scripts.inc")&&
            opcode is "braillemessage" or "getbraillestringwidth")
        {
            if(operands.Length!=1||!operands[0].StartsWith("Braille_Text_",StringComparison.Ordinal))
                throw new InvalidDataException("sevii_ruby_braille_operands");
            return(StoryOp.Special,1,1);
        }
        if(FirstSeviiSource(file,"data/scripts/trainers.inc")&&
            (label,opcode) is
                ("OneIsland_KindleRoad_EventScript_Kia","trainerbattle_double") or
                ("OneIsland_KindleRoad_EventScript_Mik","trainerbattle_double") or
                ("OneIsland_KindleRoad_EventScript_KiaRematch","trainerbattle_rematch_double") or
                ("OneIsland_KindleRoad_EventScript_MikRematch","trainerbattle_rematch_double"))
        {
            if(operands.Length!=4||operands[0]!="TRAINER_CRUSH_KIN_MIK_KIA")
                throw new InvalidDataException("sevii_ruby_double_operands");
            return(StoryOp.TrainerBattle,4,4);
        }
        return null;
    }
}
