using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Source-authenticated upgraded League teams through the shared trainer construction kernel.</summary>
public static class LeagueRematchTrainerPartyFactory
{
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        [new("pret:TRAINER_ELITE_FOUR_LORELEI_2"), new("pret:TRAINER_ELITE_FOUR_BRUNO_2"),
         new("pret:TRAINER_ELITE_FOUR_AGATHA_2"), new("pret:TRAINER_ELITE_FOUR_LANCE_2"),
         new("pret:TRAINER_CHAMPION_REMATCH_SQUIRTLE"), new("pret:TRAINER_CHAMPION_REMATCH_BULBASAUR"),
         new("pret:TRAINER_CHAMPION_REMATCH_CHARMANDER")];
    public static ImmutableArray<TrainerId> AllTrainers => SupportedTrainers;

    public static TrainerId ChampionForPlayerStarter(SpeciesId starter) => starter.Value switch
    {
        "pret:SPECIES_BULBASAUR" => new("pret:TRAINER_CHAMPION_REMATCH_CHARMANDER"),
        "pret:SPECIES_SQUIRTLE" => new("pret:TRAINER_CHAMPION_REMATCH_BULBASAUR"),
        "pret:SPECIES_CHARMANDER" => new("pret:TRAINER_CHAMPION_REMATCH_SQUIRTLE"),
        _ => throw new NotSupportedException("league_rematch_player_starter")
    };

    public static int BasePrize(LeagueRematchTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("league_rematch_trainer_id");
        return checked(4 * trainer.BaseMoney * trainer.Party[^1].Level);
    }

    public static TrainerPartyCreationResult Create(LeagueRematchTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }

    public static void ValidateCatalog(LeagueRematchTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !SupportedTrainers.Contains(battle.Trainer))
            throw new NotSupportedException("league_rematch_trainer_battle_source");
    }
}
