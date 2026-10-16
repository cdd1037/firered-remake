using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>Five original Saffron Dojo single trainers, including Koichi’s continuation callback.</summary>
public sealed record FightingDojoTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);
