using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string, string> SixIslandMapSourceHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["data/maps/SixIsland/scripts.inc"] = "5dfd28f0fbad872ca4da6cbc4f7bdc348eec3f07d483a4e0d342a9209648c5f7",
        ["data/maps/SixIsland_Harbor/scripts.inc"] = "9053b82beefc4d05323825f2f04c224247f30ce2d9e9714a34664b9cb8734098",
        ["data/maps/SixIsland_WaterPath/scripts.inc"] = "dfe32359cfeed82b92ea0fbe18999d743f724006cdffe51996ec4eb7ced68479",
        ["data/maps/SixIsland_RuinValley/scripts.inc"] = "b9b8908abec9c93265d2025c61f8ffc678bd3d1834381ca8634874efbfac6541",
        ["data/maps/SixIsland_DottedHole_1F/scripts.inc"] = "373b94ca425aab08f196e3da11a57cb9ad81cd6129bec34b98aed84ae1e92bac",
        ["data/maps/SixIsland_DottedHole_B1F/scripts.inc"] = "34c59604f52ab15c584a5bec9d66aca193474506018cf4b21dbd27d4f0123dec",
        ["data/maps/SixIsland_DottedHole_B2F/scripts.inc"] = "bb4108f597f15099bfb4d47a595048ef298631e426a68ed4cecc0bc18172f958",
        ["data/maps/SixIsland_DottedHole_B3F/scripts.inc"] = "1360ebc10cf081c68504d47de14733eddb7ffaa98f9e85bd4ccbe784f4ab20f0",
        ["data/maps/SixIsland_DottedHole_B4F/scripts.inc"] = "387c18027dab82c385df51d2eddbf179eeb29f1ecd41cc6ca26a2c263e9dcf44",
        ["data/maps/SixIsland_DottedHole_SapphireRoom/scripts.inc"] = "20f5ed7586e571df91c869afb138a2c2cfc6f96f3604705bfd3a21ce0adfb100",
        ["data/maps/SixIsland_PokemonCenter_1F/scripts.inc"] = "ffd9e0746c05366a663df2c5a439a8dbcb0669b421577dd7730a43600e942023",
        ["data/maps/SixIsland_Mart/scripts.inc"] = "a46cba7116d85e23cb27ad1810b06dfa7f62a3c83a7247d309158a34a6c6d996",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private static bool IsSixIslandMapSource(PretSourceScriptFile file) =>
        SixIslandMapSourceHashes.TryGetValue(file.Path, out var hash) && FirstSeviiSource(file, file.Path, hash);

    private static bool IsSixIslandAssemblyData(PretSourceScriptFile file, string label, string[] body)
    {
        if (!IsSixIslandMapSource(file)) return false;
        if (label == file.Path.Split('/')[2] + "_MapScripts" && body.SequenceEqual(new[] { ".byte 0" })) return true;
        return label == "SixIsland_Mart_Items" && file.Path == "data/maps/SixIsland_Mart/scripts.inc" &&
            body.SequenceEqual(new[] { ".2byte ITEM_ULTRA_BALL", ".2byte ITEM_FULL_RESTORE", ".2byte ITEM_MAX_POTION",
                ".2byte ITEM_REVIVE", ".2byte ITEM_FULL_HEAL", ".2byte ITEM_ESCAPE_ROPE", ".2byte ITEM_MAX_REPEL",
                ".2byte ITEM_DREAM_MAIL", ".2byte ITEM_NONE", "release", "end" });
    }

    private static bool IsSixIslandBrailleWait(PretSourceScriptFile file, string label) =>
        IsSixIslandMapSource(file) && file.Path == "data/maps/SixIsland_DottedHole_SapphireRoom/scripts.inc" &&
        label == "SixIsland_DottedHole_SapphireRoom_EventScript_BrailleMessage";

    private static (StoryOp, int, int)? SixIslandInstructionShape(PretSourceScriptFile file, string label, string opcode,
        ImmutableArray<string> operands)
    {
        if (IsSixIslandMapSource(file) && opcode is "braillemessage" or "getbraillestringwidth")
        {
            if (operands.Length != 1 || !operands[0].StartsWith("Braille_Text_", StringComparison.Ordinal))
                throw new InvalidDataException("six_island_braille_operands");
            return (StoryOp.Special, 1, 1);
        }
        if (FirstSeviiSource(file, "data/scripts/trainers.inc", "d145a6d45e4143e3b215fdff82de07b78fd361aa695855e4065c043ffba36945") &&
            (label, opcode) is
                ("SixIsland_WaterPath_EventScript_Miu", "trainerbattle_double") or
                ("SixIsland_WaterPath_EventScript_Mia", "trainerbattle_double") or
                ("SixIsland_WaterPath_EventScript_MiuRematch", "trainerbattle_rematch_double") or
                ("SixIsland_WaterPath_EventScript_MiaRematch", "trainerbattle_rematch_double"))
        {
            if (operands.Length != 4 || operands[0] != "TRAINER_TWINS_MIU_MIA")
                throw new InvalidDataException("six_island_double_operands");
            return (StoryOp.TrainerBattle, 4, 4);
        }
        // Unsupported adjacent maps never become runnable merely because another source
        // file references their entry. Preserve explicit fault nodes for deferred services.
        if (file.Path.StartsWith("data/maps/SixIsland_", StringComparison.Ordinal) &&
            !SixIslandMapSourceHashes.ContainsKey(file.Path)) return (StoryOp.Unsupported, 0, int.MaxValue);
        return null;
    }
}
