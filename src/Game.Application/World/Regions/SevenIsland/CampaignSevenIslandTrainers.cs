using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSevenIslandDoubleTrainer(TrainerId id) => SevenIslandDoubleTrainerEncounter.TrainerIds.Contains(id);
    private bool IsSevenIslandDoubleScript(string script) => _content.SevenIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Mode == 4 && _content.SevenIslandTrainers.Trainers[source.Trainer].DoubleBattle;

}
