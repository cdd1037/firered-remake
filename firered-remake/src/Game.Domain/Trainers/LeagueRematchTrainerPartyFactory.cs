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
    private static readonly ImmutableDictionary<TrainerId, (string Trainer, string Battle)> Expected =
        new Dictionary<TrainerId, (string Trainer, string Battle)>
    {
        [new("pret:TRAINER_ELITE_FOUR_LORELEI_2")] = ("6db15fe4062b1d26458dcb176cac71c840e135338bc110dc2587b2766f44ec82", "8025de184dfd5728acf8850ebe966b6a0d90f8f410ca9c4609a4d005865e0789"),
        [new("pret:TRAINER_ELITE_FOUR_BRUNO_2")] = ("d281fcaa6a9d69205054f33242118275e59d7eba2f7773f82071f210b3fa3efe", "65ac37a36581d12fad88f5dd27a314b5224c5246da80acf07e4a9e64c265f3ba"),
        [new("pret:TRAINER_ELITE_FOUR_AGATHA_2")] = ("b520e89b840e8f8e384a676379e21c8c0fac9ab2c9a99b52c16da8f5ec0d840b", "a28b5d944687025e4dca3dbb2bc040f41561eaae652aabd123e396e24698abb2"),
        [new("pret:TRAINER_ELITE_FOUR_LANCE_2")] = ("54d9a03ce5afe2b87642cb9a5de1d100ba3ab12df7a77a76c80755953b073b27", "ef808d79af8a7a5995315aa81df5ff4657f240963a44a1833908fcb0e4348b80"),
        [new("pret:TRAINER_CHAMPION_REMATCH_SQUIRTLE")] = ("c06403539eec8136d3de14bc743068b98b40329edfbc098b46f531e96a659d3a", "e35b0d3d42586277c8a12ecd070bdaa4f5f83c783e7c03ee996305d1fdfae4c0"),
        [new("pret:TRAINER_CHAMPION_REMATCH_BULBASAUR")] = ("b8dad4bc575e25a78035fdca7cb7b62c31fc732569e176830f6716299df1cf6e", "03f2aeaf5634bacc00cca055cfa8fc81035685ee637a2b51b2d441f8c0266ed5"),
        [new("pret:TRAINER_CHAMPION_REMATCH_CHARMANDER")] = ("693e61533b2b1245f91eb39a6c64bec9064583ad72b1a24175c48ffecdef58bd", "457bd7666c1dda285b60d16a15c9009cce6b8c7cc68aa5fb59d2271fe5b54308"),
    }.ToImmutableDictionary();
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
            if (member.CustomMoves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
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
        foreach (var (id, expected) in Expected)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != expected.Trainer)
                throw new NotSupportedException("league_rematch_trainer_source");
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new NotSupportedException("league_rematch_trainer_battle_key");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !Expected.TryGetValue(battle.Trainer, out var expected) ||
            CampaignSourceFingerprint.Compute(battle) != expected.Battle)
            throw new NotSupportedException("league_rematch_trainer_battle_source");
    }
}
