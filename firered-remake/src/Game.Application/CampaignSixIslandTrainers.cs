using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSixIslandTrainer(TrainerId trainer) => _content.SixIslandEnabled &&
        _content.SixIslandTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private bool IsSixIslandDoubleScript(string script) => _content.SixIslandEnabled &&
        _content.SixIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Trainer == SixIslandTrainerPartyFactory.DoubleTrainer && source.Mode == 4;

    private ForestTrainerBattleDefinition SixIslandTrainerScript(TrainerId trainer, string? script)
    {
        if (!IsSixIslandTrainer(trainer)) throw new InvalidDataException("six_island_single_trainer_missing");
        var source = script is null
            ? _content.SixIslandTrainers!.BattlesByScript.Values.Single(b => b.Trainer == trainer && b.Mode == 0)
            : _content.SixIslandTrainers!.BattlesByScript.GetValueOrDefault(script) ?? throw new InvalidDataException("six_island_trainer_script_missing");
        SixIslandTrainerPartyFactory.ValidateBattle(source);
        if (source.Trainer != trainer || source.Mode != 0 || source.BattleTypeFlags != 8)
            throw new InvalidDataException("six_island_single_script_mismatch");
        return new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    }

    private int SixIslandTrainerSourceLine(string script)
    {
        bool rematch = script.EndsWith("Rematch", StringComparison.Ordinal);
        var entry = rematch ? script[..^7] : script;
        if (_content.SixIslandTrainers?.BattlesByScript.TryGetValue(entry, out var source) != true || source is null)
            throw new InvalidDataException("six_island_trainer_script_missing");
        SixIslandTrainerPartyFactory.ValidateBattle(source);
        var instruction = _content.FindProgram(script)?.Instructions.SingleOrDefault(i => i.Op == StoryOp.TrainerBattle);
        string opcode = source.Mode == 4 ? rematch ? "trainerbattle_rematch_double" : "trainerbattle_double"
            : rematch ? "trainerbattle_rematch" : "trainerbattle_single";
        string[] args = source.Mode == 4
            ? [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText, source.NotEnoughMonsText!]
            : [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText];
        // Every authenticated first wrapper is followed by its own original rematch
        // wrapper. The campaign has no VS Seeker; those branches only show post text.
        if (instruction is null || instruction.Opcode != opcode || instruction.Source.Path != source.Source.Path ||
            instruction.Source.Symbol != script || instruction.Source.Line != source.Source.StartLine + (rematch ? 8 : 1) ||
            !instruction.Args.SequenceEqual(args)) throw new InvalidDataException("six_island_trainer_program_missing");
        return instruction.Source.Line;
    }

    private void RequireSixIslandTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        if (!IsSixIslandTrainer(trainer) || noIntro || source.Mode != 0 ||
            state.World.Player.MapId != OrdinaryTrainerMap(trainer) || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            actor.ScriptId != source.ScriptEntry || state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException("six_island_trainer_source_occurrence");
    }

    private void RequireSixIslandDoubleOccurrence(CampaignState state, StoryEffect effect, string script, bool rematch)
    {
        if (!IsSixIslandDoubleScript(script) || state.World.Player.MapId.Value != "map.six-island-water-path" ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.ScriptId != script ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            effect.Source.Line != SixIslandTrainerSourceLine(script + (rematch ? "Rematch" : "")))
            throw new InvalidDataException("six_island_double_source_occurrence");
    }

    private bool SixIslandTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        IsSixIslandTrainer(new(history.TrainerId)) && history.SourceScriptEntry is { } script &&
        _content.SixIslandTrainers!.BattlesByScript.TryGetValue(script, out var source) &&
        source.Trainer.Value == history.TrainerId && source.Mode == 0 && history.SourceScriptLine == SixIslandTrainerSourceLine(script);
}
