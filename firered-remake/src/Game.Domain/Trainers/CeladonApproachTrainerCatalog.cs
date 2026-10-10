using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>Authenticated initial Route8 singles source teams; field availability is separate.</summary>
public sealed record CeladonApproachTrainerCatalog(string SourceCommit,string Profile,bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId,CeruleanTrainerBattleDefinition> Battles);
