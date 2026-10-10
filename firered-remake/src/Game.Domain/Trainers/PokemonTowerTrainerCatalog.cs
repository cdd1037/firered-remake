using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>Exact nineteen initial Pokemon Tower source teams. Does not enable battles.</summary>
public sealed record PokemonTowerTrainerCatalog(string SourceCommit,string Profile,bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId,CeruleanTrainerBattleDefinition> Battles);
