using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class SilphTrainerPartyFactory
{
    public static TrainerId Giovanni { get; } = new("pret:TRAINER_BOSS_GIOVANNI_2");

    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = [
        new("pret:TRAINER_BOSS_GIOVANNI_2"),
        new("pret:TRAINER_JUGGLER_DALTON"),
        new("pret:TRAINER_RIVAL_SILPH_BULBASAUR"),
        new("pret:TRAINER_RIVAL_SILPH_CHARMANDER"),
        new("pret:TRAINER_RIVAL_SILPH_SQUIRTLE"),
        new("pret:TRAINER_SCIENTIST_BEAU"),
        new("pret:TRAINER_SCIENTIST_CONNOR"),
        new("pret:TRAINER_SCIENTIST_ED"),
        new("pret:TRAINER_SCIENTIST_JERRY"),
        new("pret:TRAINER_SCIENTIST_JOSE"),
        new("pret:TRAINER_SCIENTIST_JOSHUA"),
        new("pret:TRAINER_SCIENTIST_PARKER"),
        new("pret:TRAINER_SCIENTIST_RODNEY"),
        new("pret:TRAINER_SCIENTIST_TAYLOR"),
        new("pret:TRAINER_SCIENTIST_TRAVIS"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_23"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_24"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_25"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_26"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_27"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_28"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_29"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_30"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_31"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_32"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_33"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_34"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_35"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_36"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_37"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_38"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_39"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_40"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_41")
    ];
    public static TrainerPartyCreationResult Create(SilphTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }
    public static void ValidateCatalog(SilphTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !SupportedTrainers.Contains(battle.Trainer))
            throw new NotSupportedException("silph_trainer_battle_source");
    }
}
