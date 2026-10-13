using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsFiveIslandTrainer(TrainerId trainer) => _content.FiveIslandEnabled &&
        _content.FiveIslandTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private int FiveIslandTrainerSourceLine(string script)
    {
        if (_content.FiveIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) != true || source is null)
            throw new InvalidDataException("five_island_trainer_script_missing");
        FiveIslandTrainerPartyFactory.ValidateBattle(source);
        string[] args = source.PostBattleScript is { } callback
            ? [source.Trainer.Value[5..], source.IntroText, source.DefeatText, callback]
            : [source.Trainer.Value[5..], source.IntroText, source.DefeatText];
        return RequireTrainerInstruction(source, script, "trainerbattle_single", args, source.Source.StartLine + 1, "five_island_trainer_program_missing");
    }

    private void RequireFiveIslandTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro) =>
        RequireOrdinaryTrainerOccurrence(state, trainer, source, noIntro, TrainerRegion.FiveIsland, "five_island_trainer_source_occurrence");

    private bool FiveIslandTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        OrdinaryTrainerHistoryOriginMatches(history, TrainerRegion.FiveIsland);
}
