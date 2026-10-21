using Game.Domain.Ids;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSixIslandDoubleTrainer(TrainerId trainer) =>
        _content.SixIslandTrainers?.Trainers.TryGetValue(trainer, out var definition) == true && definition.DoubleBattle;
    private bool IsSixIslandDoubleScript(string script) =>
        _content.SixIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Mode == 4 && IsSixIslandDoubleTrainer(source.Trainer);
}
