using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string, string> OriginalTmAcquisitionScriptHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data/maps/Route12_NorthEntrance_2F/scripts.inc"] = "b61fe6b42d235281721e81539aad8b3f2983125a0e58577b225afac37c26fd92",
            ["data/maps/SaffronCity_MrPsychicsHouse/scripts.inc"] = "02d8ecaf4a526b8aee3cd995815f3c110cb3105e5e15e244ed64791ab107d81f",
            ["data/maps/FiveIsland_MemorialPillar/scripts.inc"] = "b5deaeaded7179de008adc49973afaf6d8d065f0729ec5539eb8d82372746a10"
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static bool IsOriginalTmAcquisitionSource(PretSourceScriptFile file) =>
        OriginalTmAcquisitionScriptHashes.TryGetValue(file.Path, out var hash) && FirstSeviiSource(file, file.Path, hash);

    private static bool IsOriginalTmAcquisitionAssemblyData(PretSourceScriptFile file, string label, string[] body) =>
        IsOriginalTmAcquisitionSource(file) && label == file.Path.Split('/')[2] + "_MapScripts" &&
        body.SequenceEqual([".byte 0"]);

    private static (StoryOp, int, int)? OriginalTmAcquisitionInstructionShape(PretSourceScriptFile file,
        string label, string opcode, ImmutableArray<string> operands)
    {
        if (!OriginalTmAcquisitionScriptHashes.ContainsKey(file.Path)) return null;
        if (!IsOriginalTmAcquisitionSource(file)) return (StoryOp.Unsupported, 0, int.MaxValue);
        if (opcode == "special" && (label != "FiveIsland_MemorialPillar_EventScript_Memorial" ||
            !operands.SequenceEqual(["QuestLog_CutRecording"])))
            return (StoryOp.Unsupported, 0, int.MaxValue);
        // Recover only the ordinary existing IR shapes for these three exact source files.
        // In particular this does not weaken the broader FiveIsland source boundary.
        return Shape(opcode, includeGiftOpcodes: true, includeRoute3Opcodes: false,
            includeMtMoonOpcodes: false, includeBillOpcodes: false, includeSsAnneOpcodes: false,
            includeVermilionGymOpcodes: false, includeRockTunnelOpcodes: false,
            includeRocketHideoutOpcodes: false, includePokemonTowerOpcodes: false, includeSnorlaxOpcodes: false);
    }
}
