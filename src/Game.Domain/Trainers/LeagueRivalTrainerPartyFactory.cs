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
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }

    public static TrainerPartyCreationResult Create(LeagueRivalTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("league_rival_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species))
                throw new NotSupportedException("league_rival_trainer_species");
            if (member.CustomMoves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("league_rival_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // Source NO_ITEM_CUSTOM_MOVES replaces all four natural moves only after CreateMon.
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
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
