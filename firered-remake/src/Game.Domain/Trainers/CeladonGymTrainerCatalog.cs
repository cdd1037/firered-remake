using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>Exact eight initial Celadon Gym source teams. Does not enable battles.</summary>
public sealed record CeladonGymTrainerCatalog(ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId,CeruleanTrainerBattleDefinition> Battles);
