using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string, string> FiveIslandMapSourceHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["data/maps/FiveIsland/scripts.inc"] = "83e9d96d3e4e2038b117d389a5cc66d1d0ab7f048d38e677a77f3a65ff461ec0",
        ["data/maps/FiveIsland_Harbor/scripts.inc"] = "166e97ba11e1a684af5d3349f83ec13ddfe6e77e5bdd1d721c799f51dff63216",
        ["data/maps/FiveIsland_Meadow/scripts.inc"] = "f7d580566f280c0575d2107824849402bfe69ebe5d40db3d01e60cb8a70e89da",
        ["data/maps/FiveIsland_RocketWarehouse/scripts.inc"] = "b449c6890230455482311d27df2a70c2d818bcb067cf62fdf967da4e23f591f9",
        ["data/maps/FiveIsland_PokemonCenter_1F/scripts.inc"] = "76ced550e3c6272ac15be7c3a98fd9208b71fe872ff01ce26d7ace946037b400",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private static bool IsFiveIslandMapSource(PretSourceScriptFile file) =>
        FiveIslandMapSourceHashes.TryGetValue(file.Path, out var hash) && FirstSeviiSource(file, file.Path, hash);

    private static bool IsFiveIslandAssemblyData(PretSourceScriptFile file, string label, string[] body) =>
        IsFiveIslandMapSource(file) && label == file.Path.Split('/')[2] + "_MapScripts" &&
        body.SequenceEqual(new[] { ".byte 0" });

    private static (StoryOp, int, int)? FiveIslandInstructionShape(PretSourceScriptFile file, string label,
        string opcode, ImmutableArray<string> operands)
    {
        // The recovery endpoint does not activate Celio's network or initialize a roamer.
        // Fault at the branch entry, before its item removal or other effects can publish.
        if (file.Path == "data/maps/OneIsland_PokemonCenter_1F/scripts.inc" &&
            label == "OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire")
            return (StoryOp.Unsupported, 0, int.MaxValue);

        if (!file.Path.StartsWith("data/maps/FiveIsland", StringComparison.Ordinal)) return null;
        if (!IsFiveIslandMapSource(file)) return (StoryOp.Unsupported, 0, int.MaxValue);

        void Exact(params string[] args)
        {
            if (!operands.SequenceEqual(args)) throw new InvalidDataException("five_island_story_operands: " + label + ":" + opcode);
        }
        if (opcode == "trainerbattle_single")
        {
            var expected = label switch
            {
                "FiveIsland_Meadow_EventScript_Rocket1" => new[] { "TRAINER_TEAM_ROCKET_GRUNT_49", "FiveIsland_Meadow_Text_Rocket1Intro", "FiveIsland_Meadow_Text_Rocket1Defeat" },
                "FiveIsland_Meadow_EventScript_Rocket2" => new[] { "TRAINER_TEAM_ROCKET_GRUNT_50", "FiveIsland_Meadow_Text_Rocket2Intro", "FiveIsland_Meadow_Text_Rocket2Defeat" },
                "FiveIsland_Meadow_EventScript_Rocket3" => new[] { "TRAINER_TEAM_ROCKET_GRUNT_51", "FiveIsland_Meadow_Text_Rocket3Intro", "FiveIsland_Meadow_Text_Rocket3Defeat" },
                "FiveIsland_RocketWarehouse_EventScript_Grunt1" => new[] { "TRAINER_TEAM_ROCKET_GRUNT_42", "FiveIsland_RocketWarehouse_Text_Grunt1Intro", "FiveIsland_RocketWarehouse_Text_Grunt1Defeat" },
                "FiveIsland_RocketWarehouse_EventScript_Grunt2" => new[] { "TRAINER_TEAM_ROCKET_GRUNT_47", "FiveIsland_RocketWarehouse_Text_Grunt2Intro", "FiveIsland_RocketWarehouse_Text_Grunt2Defeat" },
                "FiveIsland_RocketWarehouse_EventScript_Grunt3" => new[] { "TRAINER_TEAM_ROCKET_GRUNT_48", "FiveIsland_RocketWarehouse_Text_Grunt3Intro", "FiveIsland_RocketWarehouse_Text_Grunt3Defeat" },
                "FiveIsland_RocketWarehouse_EventScript_Admin1" => new[] { "TRAINER_TEAM_ROCKET_ADMIN", "FiveIsland_RocketWarehouse_Text_Admin1Intro", "FiveIsland_RocketWarehouse_Text_Admin1Defeat", "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1" },
                "FiveIsland_RocketWarehouse_EventScript_Admin2" => new[] { "TRAINER_TEAM_ROCKET_ADMIN_2", "FiveIsland_RocketWarehouse_Text_Admin2Intro", "FiveIsland_RocketWarehouse_Text_Admin2Defeat", "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin2" },
                "FiveIsland_RocketWarehouse_EventScript_Gideon" => new[] { "TRAINER_SCIENTIST_GIDEON", "FiveIsland_RocketWarehouse_Text_GideonIntro", "FiveIsland_RocketWarehouse_Text_GideonDefeat", "FiveIsland_RocketWarehouse_EventScript_DefeatedGideon" },
                _ => throw new InvalidDataException("five_island_trainer_entry")
            };
            Exact(expected); return (StoryOp.TrainerBattle, expected.Length, expected.Length);
        }
        if (opcode == "getplayerxy")
        {
            if (label is not "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1" and not "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin2")
                return (StoryOp.Unsupported, 0, int.MaxValue);
            Exact("VAR_0x8004", "VAR_0x8005"); return (StoryOp.Special, 2, 2);
        }
        if (opcode == "call_if_defeated")
        {
            if (label != "FiveIsland_RocketWarehouse_OnLoad") return (StoryOp.Unsupported, 0, int.MaxValue);
            Exact("TRAINER_TEAM_ROCKET_ADMIN", "FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry");
            return (StoryOp.Special, 2, 2);
        }
        if (opcode == "specialvar")
        {
            if (label != "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1") return (StoryOp.Unsupported, 0, int.MaxValue);
            Exact("VAR_RESULT", "GetPlayerFacingDirection"); return (StoryOp.Special, 2, 2);
        }
        if (opcode == "special")
        {
            if (label is not "FiveIsland_Meadow_EventScript_OpenWarehouseDoor" and not "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1")
                return (StoryOp.Unsupported, 0, int.MaxValue);
            Exact("DrawWholeMapView"); return (StoryOp.Special, 1, 1);
        }
        if (opcode == "setmetatile")
        {
            if (label == "FiveIsland_Meadow_EventScript_SetWarehouseDoorUnlocked")
                Exact("12", "21", "METATILE_SeviiIslands45_RocketWarehouseDoor_Unlocked", "1");
            else if (label == "FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry" && operands.Length == 4 && operands[0] is "26" or "27")
                Exact(operands[0], "20", "METATILE_SilphCo_Arrow_Up", "0");
            else return (StoryOp.Unsupported, 0, int.MaxValue);
            return (StoryOp.Special, 4, 4);
        }
        return null;
    }
}
