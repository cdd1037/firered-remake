using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Encounters;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static (StoryOp,int,int)? SourceScriptedEncounterInstructionShape(
        PretSourceScriptFile file, string label, string opcode, ImmutableArray<string> operands)
    {
        bool legendaryBattle = opcode == "special" && operands.Length > 0 && operands[0] == "StartLegendaryBattle";
        if (opcode is not ("setwildbattle" or "dowildbattle") && !legendaryBattle) return null;

        // The shared fixed-encounter definition owns the species/level and battle kind.
        // Source identity belongs to this import boundary, never to the runtime catalog.
        static string SourcePath(SourceScriptedEncounterDefinition definition) =>
            "data/maps/" + definition.ScriptId.Split("_EventScript_", StringSplitOptions.None)[0] + "/scripts.inc";
        var definition = SourceScriptedEncounterRegistry.Definitions.SingleOrDefault(d => d.ScriptId == label);
        if (definition is null && !SourceScriptedEncounterRegistry.Definitions.Any(d => SourcePath(d) == file.Path))
            return null;
        if (definition is null || file.Path != SourcePath(definition) || file.SourceRef.Path != file.Path ||
            file.SourceRef.Kind != SourceKind.Pret || file.SourceRef.Repository != "pret/pokefirered" ||
            file.SourceRef.Commit != "037335f4c725d7c9aecdac87066f2002b4bd7e14")
            return (StoryOp.Unsupported,0,int.MaxValue);

        if (opcode == "setwildbattle")
        {
            if (!operands.SequenceEqual(new[] { definition.Species.Value[5..], definition.Level.ToString(CultureInfo.InvariantCulture) }))
                throw new InvalidDataException("scripted_encounter_source_operands");
            return (StoryOp.Special,2,2);
        }
        if (legendaryBattle)
            return definition.Legendary && operands.Length == 1
                ? (StoryOp.TrainerBattle,1,1) : (StoryOp.Unsupported,0,int.MaxValue);
        return !definition.Legendary && operands.IsEmpty
            ? (StoryOp.TrainerBattle,0,0) : (StoryOp.Unsupported,0,int.MaxValue);
    }
}
