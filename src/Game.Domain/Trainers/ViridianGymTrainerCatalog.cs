using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>The nine original single battles in Viridian Gym, including leader Giovanni (350).</summary>
public sealed record ViridianGymTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);
