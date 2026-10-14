using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string, string> FourIslandMapSourceHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["data/maps/FourIsland/scripts.inc"] = "e9d41ce813c9c853df0ccd4d54dc03f3b0bd2a18f75645f32fa19785372d7e76",
        ["data/maps/FourIsland_Harbor/scripts.inc"] = "6717495b6275d0c21bb92dcf5c6f5acd05da75e943ee5b2f3916761cd5cf2e0d",
        ["data/maps/FourIsland_IcefallCave_1F/scripts.inc"] = "bbd6ea036ca2baa41dfb33d0897403990a52dcf6c591eb33e5b73e4466140cc2",
        ["data/maps/FourIsland_IcefallCave_B1F/scripts.inc"] = "5322c95f792bf6d5e100813a05dfa11e221544c4f491bb832340b7142bcfd17e",
        ["data/maps/FourIsland_IcefallCave_Back/scripts.inc"] = "e83b7285e7772152d7ef1c0660868244387be9669ea13c5d51a34d3dd95d5dd6",
        ["data/maps/FourIsland_IcefallCave_Entrance/scripts.inc"] = "3b86b1a14f73b1911b4805935d97922933b531a579122a00d65c8a876616ba91",
        ["data/maps/FourIsland_Mart/scripts.inc"] = "80edae5f1f9299b74933ac4f533f41d8bccae66bc3a09069e14b597ef295b733",
        ["data/maps/FourIsland_PokemonCenter_1F/scripts.inc"] = "6782364292bc1ee241845232e113aabcd7028ff04fb6ca6102e68327bf69bf72"
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private static bool IsFourIslandMapSource(PretSourceScriptFile file) =>
        FourIslandMapSourceHashes.TryGetValue(file.Path, out var hash) && PokemonLeagueSource(file, file.Path, hash);

    private static bool IsFourIslandAssemblyData(PretSourceScriptFile file, string label, string[] body)
    {
        if (!IsFourIslandMapSource(file)) return false;
        if (label == file.Path.Split('/')[2] + "_MapScripts" && body.SequenceEqual(new[] { ".byte 0" })) return true;
        return label == "FourIsland_Mart_Items" && file.Path == "data/maps/FourIsland_Mart/scripts.inc" &&
            body.SequenceEqual(new[] { ".2byte ITEM_ULTRA_BALL", ".2byte ITEM_FULL_RESTORE", ".2byte ITEM_MAX_POTION",
                ".2byte ITEM_REVIVE", ".2byte ITEM_ICE_HEAL", ".2byte ITEM_FULL_HEAL", ".2byte ITEM_ESCAPE_ROPE",
                ".2byte ITEM_MAX_REPEL", ".2byte ITEM_NONE", "release", "end" });
    }

    private static (StoryOp, int, int)? FourIslandInstructionShape(PretSourceScriptFile file, string label, string opcode,
        ImmutableArray<string> operands)
    {
        if (!IsFourIslandMapSource(file)) return null;
        if (opcode == "trainerbattle_no_intro" && label == "FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene")
        {
            if (!operands.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_GRUNT_45", "FourIsland_IcefallCave_Back_Text_GruntDefeat" }))
                throw new InvalidDataException("four_island_trainer_operands");
            return (StoryOp.TrainerBattle, 2, 2);
        }
        if (opcode == "setstepcallback" && label == "FourIsland_IcefallCave_1F_OnResume")
        {
            if (!operands.SequenceEqual(new[] { "STEP_CB_ICE" })) throw new InvalidDataException("four_island_ice_callback_operands");
            return (StoryOp.Special, 1, 1);
        }
        if (opcode == "warphole" && label == "FourIsland_IcefallCave_1F_EventScript_FallDownHole")
        {
            if (!operands.SequenceEqual(new[] { "MAP_FOUR_ISLAND_ICEFALL_CAVE_B1F" })) throw new InvalidDataException("four_island_hole_operands");
            return (StoryOp.Special, 1, 1);
        }
        return null;
    }
}
