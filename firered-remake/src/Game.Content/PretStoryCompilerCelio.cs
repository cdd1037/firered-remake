using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static (StoryOp, int, int)? CelioInstructionShape(PretSourceScriptFile file, string label,
        string opcode, ImmutableArray<string> operands)
    {
        // Global specials are not globally admitted: only the original handoff may publish them.
        bool handoffSpecial = opcode == "special" && operands.Length == 1 && operands[0] is "SetPostgameFlags" or "InitRoamer";
        bool owned = PretCelioStory.ProgramLabels.Contains(label);
        if (!owned && !handoffSpecial) return null;
        if (!owned || !FirstSeviiSource(file, PretCelioStory.SourcePath, PretCelioStory.SourceSha256))
            return (StoryOp.Unsupported, 0, int.MaxValue);
        if (handoffSpecial && label != PretCelioStory.HandoffLabel) return (StoryOp.Unsupported, 0, int.MaxValue);
        if (opcode == "random")
        {
            if (label != PretCelioStory.Prefix + "EventScript_CelioGiveBillFact" || !operands.SequenceEqual(new[] { "3" }))
                throw new InvalidDataException("celio_bill_random_operands");
            return (StoryOp.Special, 1, 1);
        }
        // Restore the normal typed operations after Five's intentionally closed handoff gate.
        // Nothing is swallowed: Special must be handled by Application, or it faults.
        return Shape(opcode, false, false, false, false, false, false, false, false, false, false);
    }
}
