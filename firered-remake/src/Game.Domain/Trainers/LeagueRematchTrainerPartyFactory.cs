using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Source-authenticated upgraded League teams through the shared trainer construction kernel.</summary>
public static class LeagueRematchTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-league-rematch-trainers-v1";
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
        LeagueRematchOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(LeagueRematchOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("league_rematch_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species))
                throw new NotSupportedException("league_rematch_trainer_species");
            if (member.CustomMoves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("league_rematch_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // ITEM_CUSTOM_MOVES replaces the natural moves only after CreateMon consumes its OT draws.
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }

    public static void ValidateCatalog(LeagueRematchTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 7 || catalog.Battles.Count != 7)
            throw new NotSupportedException("league_rematch_trainer_scope");
        foreach (var id in SupportedTrainers)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("league_rematch_trainer_source");
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new NotSupportedException("league_rematch_trainer_battle_key");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !SupportedTrainers.Contains(battle.Trainer))
            throw new NotSupportedException("league_rematch_trainer_battle_source");
    }
}
