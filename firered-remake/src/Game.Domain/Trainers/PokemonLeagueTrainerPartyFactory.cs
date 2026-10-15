using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Seven exact first League teams; source names determine PID, IVs and no-shiny OT draws.</summary>
public static class PokemonLeagueTrainerPartyFactory
{


    private static readonly ImmutableHashSet<TrainerId> ExpectedBattleKeys = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_ELITE_FOUR_LORELEI"),
        new("pret:TRAINER_ELITE_FOUR_BRUNO"),
        new("pret:TRAINER_ELITE_FOUR_AGATHA"),
        new("pret:TRAINER_ELITE_FOUR_LANCE"),
        new("pret:TRAINER_CHAMPION_FIRST_SQUIRTLE"),
        new("pret:TRAINER_CHAMPION_FIRST_BULBASAUR"),
        new("pret:TRAINER_CHAMPION_FIRST_CHARMANDER")]);
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        [new("pret:TRAINER_ELITE_FOUR_LORELEI"), new("pret:TRAINER_ELITE_FOUR_BRUNO"), new("pret:TRAINER_ELITE_FOUR_AGATHA"), new("pret:TRAINER_ELITE_FOUR_LANCE"), new("pret:TRAINER_CHAMPION_FIRST_SQUIRTLE"), new("pret:TRAINER_CHAMPION_FIRST_BULBASAUR"), new("pret:TRAINER_CHAMPION_FIRST_CHARMANDER")];
    public static ImmutableArray<TrainerId> AllTrainers => SupportedTrainers;

    public static TrainerId ChampionForPlayerStarter(SpeciesId starter) => starter.Value switch
    {
        "pret:SPECIES_BULBASAUR" => new("pret:TRAINER_CHAMPION_FIRST_CHARMANDER"),
        "pret:SPECIES_SQUIRTLE" => new("pret:TRAINER_CHAMPION_FIRST_BULBASAUR"),
        "pret:SPECIES_CHARMANDER" => new("pret:TRAINER_CHAMPION_FIRST_SQUIRTLE"),
        _ => throw new NotSupportedException("pokemon_league_player_starter")
    };

    public static int BasePrize(PokemonLeagueTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("pokemon_league_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }

    public static TrainerPartyCreationResult Create(PokemonLeagueTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("pokemon_league_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species))
                throw new NotSupportedException("pokemon_league_trainer_species");
            if (member.CustomMoves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("pokemon_league_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // Source ITEM_CUSTOM_MOVES replaces all four natural moves only after CreateMon.
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }

    public static void ValidateCatalog(PokemonLeagueTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattleKeys.Contains(battle.Trainer))
            throw new NotSupportedException("pokemon_league_trainer_battle_source");
    }
}
