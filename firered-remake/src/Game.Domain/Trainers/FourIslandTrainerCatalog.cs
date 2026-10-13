using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>The single source Grunt45 team and Lorelei scene battle entry.</summary>
public sealed record FourIslandTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);
