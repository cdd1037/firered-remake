using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>Seven original upgraded League teams, trainer IDs 735–741; no full-roster claim.</summary>
public sealed record LeagueRematchTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
