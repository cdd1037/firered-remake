using Game.Domain.Ids;

namespace Game.Domain.Trainers;

public sealed record ForestTrainerBattleDefinition(TrainerId Trainer, int Mode, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string PostBattleText,
    string EntryScript, string Callback, TrainerSourceRef Source);
