using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Three exact six-member late Route22 teams; source names determine PID, IVs and no-shiny OT draws.</summary>
public static class LeagueRivalTrainerPartyFactory
{


    private static readonly ImmutableHashSet<TrainerId> ExpectedBattleKeys = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_RIVAL_ROUTE22_LATE_SQUIRTLE"),
        new("pret:TRAINER_RIVAL_ROUTE22_LATE_BULBASAUR"),
        new("pret:TRAINER_RIVAL_ROUTE22_LATE_CHARMANDER")]);
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        [new("pret:TRAINER_RIVAL_ROUTE22_LATE_SQUIRTLE"), new("pret:TRAINER_RIVAL_ROUTE22_LATE_BULBASAUR"),
         new("pret:TRAINER_RIVAL_ROUTE22_LATE_CHARMANDER")];
    public static ImmutableArray<TrainerId> AllTrainers => SupportedTrainers;

    public static int BasePrize(LeagueRivalTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("league_rival_trainer_id");
        return TrainerPartyFactory.BasePrize(trainer);
    }

    public static TrainerPartyCreationResult Create(LeagueRivalTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }

    public static void ValidateCatalog(LeagueRivalTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattleKeys.Contains(battle.Trainer))
            throw new NotSupportedException("league_rival_trainer_battle_source");
    }
}
