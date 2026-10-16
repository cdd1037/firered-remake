using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static (StoryOp,int,int)? PowerPlantInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> args)
    {
        const string path="data/maps/PowerPlant/scripts.inc";
        if(file.Path!=path)return null;
        if(!FirstSeviiSource(file,path))
            return (StoryOp.Unsupported,0,int.MaxValue);
        bool zapdos=label=="PowerPlant_EventScript_Zapdos";
        bool electrode=label is "PowerPlant_EventScript_Electrode1" or "PowerPlant_EventScript_Electrode2";
        if(opcode=="setwildbattle")
        {
            if(!(zapdos&&args.SequenceEqual(new[]{"SPECIES_ZAPDOS","50"})||electrode&&args.SequenceEqual(new[]{"SPECIES_ELECTRODE","34"})))
                throw new InvalidDataException("power_plant_static_source_operands");
            return (StoryOp.Special,2,2);
        }
        if(opcode=="special"&&args.SequenceEqual(new[]{"StartLegendaryBattle"}))
            return zapdos?(StoryOp.TrainerBattle,1,1):(StoryOp.Unsupported,0,int.MaxValue);
        if(opcode=="dowildbattle")
            return electrode&&args.IsEmpty?(StoryOp.TrainerBattle,0,0):(StoryOp.Unsupported,0,int.MaxValue);
        return null;
    }
}
