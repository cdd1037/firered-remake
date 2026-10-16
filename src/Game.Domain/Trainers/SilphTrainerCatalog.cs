using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>Exact 34 source teams; catalog validation does not itself enter a battle.</summary>
public sealed record SilphTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
