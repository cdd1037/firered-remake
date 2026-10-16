using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class FirstSeviiTrainerPartyFactory
{

    public static TrainerPartyCreationResult Create(FirstSeviiTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }
    public static void ValidateCatalog(FirstSeviiTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesBySource.Values, null);
        if (catalog.BattlesBySource.Any(pair => pair.Key != FirstSeviiBattleKey.For(pair.Value)))
            throw new ArgumentException("first_sevii_trainer_occurrence_key");
    }
}
