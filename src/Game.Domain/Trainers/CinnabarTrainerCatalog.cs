using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>24 exact teams; script keys preserve both Lil/Ian entries and six alternate quiz battles.</summary>
public sealed record CinnabarTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);
