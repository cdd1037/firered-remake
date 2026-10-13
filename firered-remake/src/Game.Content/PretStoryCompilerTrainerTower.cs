using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string, string> TrainerTowerScriptHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["data/scripts/trainer_tower.inc"] = "6d9b5d9d01b293e03f7010452032b2d880d7aab751bc763ec5357551e5638db5",
        ["data/maps/TrainerTower_Lobby/scripts.inc"] = "3fddc6d076cf542b55459b29969e326176ba58e350f9dead3a0fb37959592c0e",
        ["data/maps/TrainerTower_1F/scripts.inc"] = "85cde9e74e62dc2d81c28c9a5481fa423790dfa8b255f950f6146cfcf6c84a02",
        ["data/maps/TrainerTower_2F/scripts.inc"] = "d9733db6140c194e341d2f2c483d1cbe25abf1084047ad72cd822825a05f650a",
        ["data/maps/TrainerTower_3F/scripts.inc"] = "1d8cac1b8c55f301568ef93a64db35a54f5da2ee8f99965d58365fe161188a76",
        ["data/maps/TrainerTower_4F/scripts.inc"] = "6fd1287174e3cbd0cc8cceeb8f070ec5550bdb77647e715ed4d559b8667f78d6",
        ["data/maps/TrainerTower_5F/scripts.inc"] = "1e026ab7be1ac8d86f992bb0c6fcdcb5f846417e17e27150d86c943c9fbde151",
        ["data/maps/TrainerTower_6F/scripts.inc"] = "a8a9bce7b825c420e6ae970e03320eca2d2349dbd877b2387be917e7972b27a3",
        ["data/maps/TrainerTower_7F/scripts.inc"] = "4e50407518159cb4623ff06827cb5e38a476d787adce78e434812a5348ef402a",
        ["data/maps/TrainerTower_8F/scripts.inc"] = "06a71c1e0f6f221b177dcee7813faf8b09bd202d98eae79d43ebf8397fef58f6",
        ["data/maps/TrainerTower_Roof/scripts.inc"] = "92052dd30d59cf932779b7ef70752deb165dcfac5158bb40e554ca0d5c0bc47f",
        ["data/maps/TrainerTower_Elevator/scripts.inc"] = "143f5a03534e833cc3cb6e107d76667316d39fc716eb0977160857d2c7f292d8"
    }.ToImmutableDictionary(StringComparer.Ordinal);

    // asm/macros/trainer_tower.inc, SHA-256 9d758f6ee2ee2e1466febd9ac732ad0c1cebfe5b971aaf38f8a90cd978655fdb.
    // Native functions retain the macro's arguments and source call-site; they are never global opcode aliases.
    private static readonly ImmutableHashSet<string> TrainerTowerNullaryMacros = new[]
    {
        "ttower_initfloor", "ttower_dobattle", "ttower_getchallengetype", "ttower_clearedfloor",
        "ttower_isfloorcleared", "ttower_startchallenge", "ttower_getownerstate", "ttower_giveprize",
        "ttower_checkfinaltime", "ttower_resumetimer", "ttower_setlost", "ttower_getchallengestatus",
        "ttower_gettime", "ttower_showresults", "ttower_closeresults", "ttower_checkdoubles",
        "ttower_getnumfloors", "ttower_shouldexit", "ttower_encountermusic", "ttower_getbeatchallenge"
    }.ToImmutableHashSet(StringComparer.Ordinal);

    private static bool IsTrainerTowerScript(PretSourceScriptFile file) =>
        TrainerTowerScriptHashes.TryGetValue(file.Path, out var hash) && FirstSeviiSource(file, file.Path, hash);

    private static bool IsTrainerTowerAssemblyData(PretSourceScriptFile file, string label, string[] body) =>
        IsTrainerTowerScript(file) && label == "TrainerTower_Lobby_Mart_Items" && body.SequenceEqual(new[]
        {
            ".2byte ITEM_ULTRA_BALL", ".2byte ITEM_GREAT_BALL", ".2byte ITEM_FULL_RESTORE", ".2byte ITEM_MAX_POTION",
            ".2byte ITEM_HYPER_POTION", ".2byte ITEM_REVIVE", ".2byte ITEM_FULL_HEAL", ".2byte ITEM_ESCAPE_ROPE",
            ".2byte ITEM_MAX_REPEL", ".2byte ITEM_NONE", "release", "end"
        });

    private static bool TryCompileTrainerTowerInstruction(PretSourceScriptFile file, string label, int line,
        string opcode, ImmutableArray<string> args, out StoryInstruction? instruction)
    {
        instruction = null;
        bool towerPath = file.Path == "data/scripts/trainer_tower.inc" || file.Path.StartsWith("data/maps/TrainerTower_", StringComparison.Ordinal);
        if (!IsTrainerTowerScript(file))
        {
            if (!towerPath && !opcode.StartsWith("ttower_", StringComparison.Ordinal)) return false;
            instruction = new(StoryOp.Unsupported, opcode, args, new(file.Path, label, line));
            return true;
        }
        if (opcode.StartsWith("ttower_", StringComparison.Ordinal))
        {
            if (opcode == "ttower_getspeech" ? args.Length is < 1 or > 2 : !TrainerTowerNullaryMacros.Contains(opcode) || !args.IsEmpty)
                throw ContentJson.Error("trainer_tower_macro_source", $"{file.Path}:{line}", opcode);
            instruction = opcode == "ttower_dobattle"
                ? new(StoryOp.TrainerBattle, opcode, ["TRAINER_TOWER"], new(file.Path, label, line))
                : new(StoryOp.Special, "TrainerTower:" + opcode, args, new(file.Path, label, line));
            return true;
        }
        if (opcode == "waitstate" && label == "TrainerTower_EventScript_DoBattle")
        {
            if (file.Path != "data/scripts/trainer_tower.inc" || line != 173 || !args.IsEmpty)
                throw new InvalidDataException("trainer_tower_battle_wait_source");
            return true;
        }
        if (opcode == "setdynamicwarp")
        {
            if (label != "TrainerTower_Elevator_EventScript_SelectLobby" ||
                !args.SequenceEqual(new[] { "MAP_TRAINER_TOWER_LOBBY", "17", "8" }))
                throw new InvalidDataException("trainer_tower_elevator_warp_source");
            instruction = new(StoryOp.Special, opcode, args, new(file.Path, label, line));
            return true;
        }
        if (opcode == "setobjectxy")
        {
            if (label != "TrainerTower_Lobby_OnResume" || !args.SequenceEqual(new[] { "LOCALID_PLAYER", "9", "7" }))
                throw new InvalidDataException("trainer_tower_resume_position_source");
            instruction = new(StoryOp.Special, opcode, args, new(file.Path, label, line));
            return true;
        }
        if (opcode is "getplayerxy" or "addvar" or "multichoicedefault")
        {
            int arity = opcode == "multichoicedefault" ? 5 : 2;
            if (args.Length != arity) throw ContentJson.Error("story_opcode_arity", $"{file.Path}:{line}", opcode);
            instruction = new(opcode == "multichoicedefault" ? StoryOp.Multichoice : StoryOp.Special,
                opcode, args, new(file.Path, label, line));
            return true;
        }
        return false;
    }

    private static void CompleteTrainerTowerSourceBlocks(PretSourceScriptFile file, Dictionary<string, List<(int Line, string Text)>> blocks)
    {
        if (!IsTrainerTowerScript(file)) return;
        var labels = blocks.Keys.ToArray();
        for (int index = labels.Length - 1; index >= 0; index--)
        {
            string label = labels[index]; var body = blocks[label];
            if (body.Count == 0 || label.EndsWith("_MapScripts", StringComparison.Ordinal) ||
                body.Any(x => x.Text.StartsWith("map_script", StringComparison.Ordinal)) || body[0].Text.StartsWith(".", StringComparison.Ordinal)) continue;
            bool movement = label.Contains("_Movement_", StringComparison.Ordinal);
            string last = body[^1].Text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0];
            if (last is "step_end" or "end" or "return" or "goto" or "waitstate") continue;
            if (index + 1 >= labels.Length) throw new InvalidDataException("trainer_tower_source_fallthrough_end:" + label);
            string next = labels[index + 1];
            if (movement)
            {
                if (!next.Contains("_Movement_", StringComparison.Ordinal) || blocks[next][^1].Text != "step_end")
                    throw new InvalidDataException("trainer_tower_movement_fallthrough:" + label);
                body.AddRange(blocks[next]);
            }
            else body.Add((body[^1].Line, "goto " + next));
        }
    }
}
