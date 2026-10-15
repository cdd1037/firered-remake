using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static (StoryOp, int, int)? CeruleanCaveInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if (!FirstSeviiSource(file,"data/maps/CeruleanCave_B1F/scripts.inc"))
            return null;
        if (label != "CeruleanCave_B1F_EventScript_Mewtwo") return null;
        if (opcode == "setwildbattle")
        {
            if (!operands.SequenceEqual(new[] { "SPECIES_MEWTWO","70" }))
                throw new InvalidDataException("cerulean_cave_mewtwo_source_operands");
            return (StoryOp.Special,2,2);
        }
        if (opcode == "special" && operands.SequenceEqual(new[] { "StartLegendaryBattle" }))
            return (StoryOp.TrainerBattle,1,1);
        return null;
    }
}
