using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableHashSet<string>OriginalTmAcquisitionScriptPaths =
new[] {"data/maps/Route12_NorthEntrance_2F/scripts.inc","data/maps/SaffronCity_MrPsychicsHouse/scripts.inc","data/maps/FiveIsland_MemorialPillar/scripts.inc"}.ToImmutableHashSet(StringComparer.Ordinal);

    private static bool IsOriginalTmAcquisitionSource(PretSourceScriptFile file) =>
        OriginalTmAcquisitionScriptPaths.Contains(file.Path) && FirstSeviiSource(file,file.Path);

    private static bool IsOriginalTmAcquisitionAssemblyData(PretSourceScriptFile file,string label,string[] body) =>
        IsOriginalTmAcquisitionSource(file) && label == file.Path.Split('/')[2] + "_MapScripts" &&
        body.SequenceEqual([".byte 0"]);

    private static (StoryOp, int, int)? OriginalTmAcquisitionInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if (!OriginalTmAcquisitionScriptPaths.Contains(file.Path)) return null;
        if (!IsOriginalTmAcquisitionSource(file)) return (StoryOp.Unsupported,0,int.MaxValue);
        if (opcode == "special" && (label != "FiveIsland_MemorialPillar_EventScript_Memorial" ||
            !operands.SequenceEqual(["QuestLog_CutRecording"])))
            return (StoryOp.Unsupported,0,int.MaxValue);
        // Recover only the ordinary existing IR shapes for these three exact source files.
        // In particular this does not weaken the broader FiveIsland source boundary.
        return Shape(opcode, includeGiftOpcodes: true, includeRoute3Opcodes: false,
            includeMtMoonOpcodes: false, includeBillOpcodes: false, includeSsAnneOpcodes: false,
            includeVermilionGymOpcodes: false, includeRockTunnelOpcodes: false,
            includeRocketHideoutOpcodes: false, includePokemonTowerOpcodes: false, includeSnorlaxOpcodes: false);
    }
}
