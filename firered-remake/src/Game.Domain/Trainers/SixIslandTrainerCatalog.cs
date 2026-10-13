using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>Eleven original trainers, including one shared Miu/Mia double, across twelve actor scripts.</summary>
public sealed record SixIslandTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);
