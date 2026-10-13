using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static (StoryOp, int, int)? CeruleanCaveInstructionShape(PretSourceScriptFile file,
        string label, string opcode, ImmutableArray<string> operands)
    {
        if (!FirstSeviiSource(file, "data/maps/CeruleanCave_B1F/scripts.inc", "c454a14b2ea0868fe3ba3e3e47918b3201625113e9c381b673787ce1522f144a"))
            return null;
        if (label != "CeruleanCave_B1F_EventScript_Mewtwo") return null;
        if (opcode == "setwildbattle")
        {
            if (!operands.SequenceEqual(new[] { "SPECIES_MEWTWO", "70" }))
                throw new InvalidDataException("cerulean_cave_mewtwo_source_operands");
            return (StoryOp.Special, 2, 2);
        }
        if (opcode == "special" && operands.SequenceEqual(new[] { "StartLegendaryBattle" }))
            return (StoryOp.TrainerBattle, 1, 1);
        return null;
    }
}
