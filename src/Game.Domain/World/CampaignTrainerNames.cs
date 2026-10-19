using Game.Domain.Trainers;

namespace Game.Domain.World;

/// <summary>The saved rival name is literal player data; other names use the current display catalog.</summary>
public static class CampaignTrainerNames
{
    public static string TrainerDisplayName(this ICampaignCatalog? catalog, TrainerDefinition trainer, string rivalName) =>
        trainer.TrainerClass is "TRAINER_CLASS_RIVAL_EARLY" or "TRAINER_CLASS_RIVAL_LATE" or "TRAINER_CLASS_CHAMPION"
            ? rivalName : catalog?.TrainerDisplayName(trainer.Id) ?? trainer.SourceTrainerName;
}
