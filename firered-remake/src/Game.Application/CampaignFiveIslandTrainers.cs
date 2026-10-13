using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsFiveIslandTrainer(TrainerId trainer) => _content.FiveIslandEnabled &&
        _content.FiveIslandTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private ForestTrainerBattleDefinition FiveIslandTrainerScript(TrainerId trainer, string? script)
    {
        if (!IsFiveIslandTrainer(trainer)) throw new InvalidDataException("five_island_single_trainer_missing");
        var source = script is null
            ? _content.FiveIslandTrainers!.BattlesByScript.Values.Single(b => b.Trainer == trainer)
            : _content.FiveIslandTrainers!.BattlesByScript.GetValueOrDefault(script) ?? throw new InvalidDataException("five_island_trainer_script_missing");
        FiveIslandTrainerPartyFactory.ValidateBattle(source);
        if (source.Trainer != trainer || source.Mode != 0 || source.BattleTypeFlags != 8)
            throw new InvalidDataException("five_island_single_script_mismatch");
        return new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    }

    private int FiveIslandTrainerSourceLine(string script)
    {
        if (_content.FiveIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) != true || source is null)
            throw new InvalidDataException("five_island_trainer_script_missing");
        FiveIslandTrainerPartyFactory.ValidateBattle(source);
        var instruction = _content.FindProgram(script)?.Instructions.SingleOrDefault(i => i.Op == StoryOp.TrainerBattle);
        string[] args = source.PostBattleScript is { } callback
            ? [source.Trainer.Value[5..], source.IntroText, source.DefeatText, callback]
            : [source.Trainer.Value[5..], source.IntroText, source.DefeatText];
        if (instruction is null || instruction.Opcode != "trainerbattle_single" || instruction.Source.Path != source.Source.Path ||
            instruction.Source.Symbol != script || instruction.Source.Line != source.Source.StartLine + 1 ||
            !instruction.Args.SequenceEqual(args)) throw new InvalidDataException("five_island_trainer_program_missing");
        return instruction.Source.Line;
    }

    private void RequireFiveIslandTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        if (!IsFiveIslandTrainer(trainer) || noIntro || source.Mode != 0 ||
            state.World.Player.MapId != OrdinaryTrainerMap(trainer) || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            actor.ScriptId != source.ScriptEntry || state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException("five_island_trainer_source_occurrence");
    }

    private bool FiveIslandTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        IsFiveIslandTrainer(new(history.TrainerId)) && history.SourceScriptEntry is { } script &&
        _content.FiveIslandTrainers!.BattlesByScript.TryGetValue(script, out var source) &&
        source.Trainer.Value == history.TrainerId && source.Mode == 0 && history.SourceScriptLine == FiveIslandTrainerSourceLine(script);
}
