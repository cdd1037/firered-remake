using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>Exact seven source teams. Party creation and action admission are separate.</summary>
public sealed record FuchsiaGymTrainerCatalog(ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId,CeruleanTrainerBattleDefinition> Battles);
