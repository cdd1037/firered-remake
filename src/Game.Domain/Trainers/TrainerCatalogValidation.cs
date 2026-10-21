using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>Regional membership and battle references, independent of import history.</summary>
public static class TrainerCatalogValidation
{
    public static void Validate<TBattle>(ImmutableDictionary<TrainerId, TrainerDefinition> trainers,
        IEnumerable<TBattle> battles, Action<TBattle>? validateBattle)
    {
        ArgumentNullException.ThrowIfNull(trainers);
        ArgumentNullException.ThrowIfNull(battles);
        if (trainers.IsEmpty || trainers.Any(pair => pair.Value is null || pair.Key != pair.Value.Id || pair.Value.Party.IsDefaultOrEmpty))
            throw new ArgumentException("trainer_catalog_membership");
        bool hasBattle = false;
        foreach (var battle in battles)
        {
            TrainerId id = battle switch
            {
                CeruleanTrainerBattleDefinition normal => normal.Trainer,
                EarlyRivalBattleDefinition rival => rival.Trainer,
                ForestTrainerBattleDefinition forest => forest.Trainer,
                Route22TrainerBattleDefinition route22 => route22.Trainer,
                _ => throw new ArgumentException("trainer_catalog_battle")
            };
            if (!trainers.ContainsKey(id)) throw new ArgumentException("trainer_catalog_battle_reference");
            validateBattle?.Invoke(battle);
            hasBattle = true;
        }
        if (!hasBattle) throw new ArgumentException("trainer_catalog_battles_empty");
    }
}
