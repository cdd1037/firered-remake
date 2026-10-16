using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class SaffronGymTrainerPartyFactory
{
    public static TrainerId Sabrina { get; } = new("pret:TRAINER_LEADER_SABRINA");

    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = [
        new("pret:TRAINER_CHANNELER_AMANDA"),
        new("pret:TRAINER_CHANNELER_STACY"),
        new("pret:TRAINER_CHANNELER_TASHA"),
        new("pret:TRAINER_LEADER_SABRINA"),
        new("pret:TRAINER_PSYCHIC_CAMERON"),
        new("pret:TRAINER_PSYCHIC_JOHAN"),
        new("pret:TRAINER_PSYCHIC_PRESTON"),
        new("pret:TRAINER_PSYCHIC_TYRON")
    ];
    public static TrainerPartyCreationResult Create(SaffronGymTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }
    public static void ValidateCatalog(SaffronGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !SupportedTrainers.Contains(battle.Trainer))
            throw new NotSupportedException("saffron_gym_trainer_battle_source");
    }
}
