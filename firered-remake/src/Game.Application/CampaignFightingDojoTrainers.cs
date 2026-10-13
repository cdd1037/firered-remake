using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string FightingDojoMasterCallback = "SaffronCity_Dojo_EventScript_DefeatedMasterKoichi";

    private bool IsFightingDojoTrainer(TrainerId trainer) => _content.FightingDojoEnabled &&
        _content.FightingDojoTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    // map.json object order is the numeric local ID, including the named master.
    private static (string Actor, int SourceLocalId) FightingDojoTrainerActor(string script) => script switch
    {
        "SaffronCity_Dojo_EventScript_Hitoshi" => ("local:1", 1),
        "SaffronCity_Dojo_EventScript_Hideki" => ("local:2", 2),
        "SaffronCity_Dojo_EventScript_Aaron" => ("local:3", 3),
        "SaffronCity_Dojo_EventScript_Mike" => ("local:4", 4),
        "SaffronCity_Dojo_EventScript_MasterKoichi" => ("LOCALID_KARATE_MASTER", 5),
        _ => throw new InvalidDataException("fighting_dojo_trainer_script_missing")
    };

    private int FightingDojoTrainerSourceLine(string script)
    {
        if (_content.FightingDojoTrainers?.BattlesByScript.TryGetValue(script, out var source) != true || source is null)
            throw new InvalidDataException("fighting_dojo_trainer_script_missing");
        FightingDojoTrainerPartyFactory.ValidateBattle(source);
        bool master = FightingDojoTrainerActor(script).SourceLocalId == 5;
        if (source.Source.Path != FightingDojoSource || source.Mode != (master ? 2 : 0) ||
            source.PostBattleScript != (master ? FightingDojoMasterCallback : null) || source.MusicOption is not null)
            throw new InvalidDataException("fighting_dojo_trainer_callback_mismatch");
        string[] args = master
            ? [source.Trainer.Value[5..], source.IntroText, source.DefeatText, FightingDojoMasterCallback]
            : [source.Trainer.Value[5..], source.IntroText, source.DefeatText];
        // Return the verified compiled occurrence, rather than retaining a guessed
        // label offset in the battle or its persistent history.
        int line = RequireTrainerInstruction(source, script, "trainerbattle_single", args,
            source.Source.StartLine + 1, "fighting_dojo_trainer_program_missing");
        if (master) RequireFightingDojoMasterCallback();
        return line;
    }

    private void RequireFightingDojoMasterCallback()
    {
        var program = _content.FindProgram(FightingDojoMasterCallback);
        if (program is null || program.Instructions.Length != 3 ||
            program.Instructions.Where((instruction, index) => instruction.Source.Path != FightingDojoSource ||
                instruction.Source.Symbol != FightingDojoMasterCallback || instruction.Source.Line != 148 + index).Any() ||
            program.Instructions[0].Op != StoryOp.SetVar || program.Instructions[0].Opcode != "setvar" ||
            !program.Instructions[0].Args.SequenceEqual(["VAR_MAP_SCENE_SAFFRON_CITY_DOJO", "1"]) ||
            program.Instructions[1].Op != StoryOp.Release || program.Instructions[1].Opcode != "release" || !program.Instructions[1].Args.IsEmpty ||
            program.Instructions[2].Op != StoryOp.End || program.Instructions[2].Opcode != "end" || !program.Instructions[2].Args.IsEmpty)
            throw new InvalidDataException("fighting_dojo_master_callback_program");
    }

    private void RequireFightingDojoTrainerArguments(StoryEffect effect, ForestTrainerBattleDefinition definition)
    {
        var source = _content.FightingDojoTrainers!.BattlesByScript[definition.ScriptEntry];
        int expected = source.PostBattleScript is null ? 3 : 4;
        if (effect.Opcode != "trainerbattle_single" || effect.Args.Length != expected ||
            expected == 4 && effect.Args[3] != source.PostBattleScript)
            throw new InvalidDataException("fighting_dojo_trainer_optional_arguments_mismatch");
    }

    private void RequireFightingDojoTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, StoryEffect effect)
    {
        var expected = FightingDojoTrainerActor(source.ScriptEntry);
        if (!OrdinaryTrainerBindings.TryGetValue(trainer, out var binding) || binding.Scripts.Region != TrainerRegion.FightingDojo ||
            state.World.Player.MapId.Value != FightingDojoMap || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            state.LastTalked != expected.Actor || !state.Actors.TryGetValue(expected.Actor, out var actor) ||
            !actor.Active || actor.LocalId != expected.Actor || actor.SourceLocalId != expected.SourceLocalId ||
            actor.ScriptId != source.ScriptEntry || state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != expected.SourceLocalId ||
            state.Script.Stack.IsDefaultOrEmpty)
            throw new InvalidDataException("fighting_dojo_trainer_source_occurrence");
        var frame = state.Script.Stack[^1];
        var program = _content.FindProgram(source.ScriptEntry)!;
        int index = frame.InstructionIndex - 1;
        if (frame.ProgramId != source.ScriptEntry || index < 0 || index >= program.Instructions.Length ||
            program.Instructions[index].Op != StoryOp.TrainerBattle || program.Instructions[index].Source != effect.Source ||
            !program.Instructions[index].Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("fighting_dojo_trainer_source_occurrence");
    }

    private bool FightingDojoTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        history.SourceScriptEntry is { } script &&
        _content.FightingDojoTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Trainer.Value == history.TrainerId && history.SourceScriptLine == FightingDojoTrainerSourceLine(script);

    private CampaignState CompleteFightingDojoMasterTurn(CampaignState before, CampaignState after)
    {
        if (!_content.FightingDojoEnabled || before.World.Player.MapId.Value != FightingDojoMap ||
            after.World.Player.MapId != before.World.Player.MapId || before.Script.Wait?.Kind != StoryWaitKind.Presentation ||
            before.Script.Stack.Length != 1 || !after.Script.Completed || after.Script.Wait is not null)
            return after;
        var frame = before.Script.Stack[^1];
        int line = frame.ProgramId switch
        {
            "SaffronCity_Dojo_EventScript_TriggerMasterBattleLeft" => 8,
            "SaffronCity_Dojo_EventScript_TriggerMasterBattleRight" => 16,
            _ => 0
        };
        if (line == 0) return after;
        var program = _content.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        if (program is null || index < 0 || index >= program.Instructions.Length ||
            program.Instructions[index].Op != StoryOp.WaitMovement || program.Instructions[index].Opcode != "waitmovement" ||
            program.Instructions[index].Source != new StorySource(FightingDojoSource, frame.ProgramId, line) ||
            !program.Instructions[index].Args.SequenceEqual(["0"]))
            throw new InvalidDataException("fighting_dojo_master_turn_source");
        // These coordinate scripts only turn Koichi and release. Resume ordinary
        // sight after that turn, before another player action can leave his sight.
        return CheckForestTrainerSight(after);
    }
}
