using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>The three starter-dependent late Route22 single battles, source trainer IDs 435–437.</summary>
public sealed record LeagueRivalTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
