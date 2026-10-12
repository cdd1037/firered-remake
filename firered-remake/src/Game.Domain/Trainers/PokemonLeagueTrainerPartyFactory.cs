using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Seven exact first League teams; source names determine PID, IVs and no-shiny OT draws.</summary>
public static class PokemonLeagueTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-pokemon-league-trainers-v1";
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv,
        int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle,
        int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize, string TrainerSha256);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_ELITE_FOUR_LORELEI")] = new("LORELEI",138,7u,false,[198, 201, 204, 191, 198, 191, 195],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_DEWGONG",87,"DEWGONG",52,250,[190, 191, 209, 193, 201, 200, 193],["pret:MOVE_ICE_BEAM", "pret:MOVE_SURF", "pret:MOVE_HAIL", "pret:MOVE_SAFEGUARD"],"ITEM_NONE"),
                new("pret:SPECIES_CLOYSTER",91,"CLOYSTER",51,250,[189, 198, 201, 211, 205, 206, 191, 204],["pret:MOVE_SPIKES", "pret:MOVE_PROTECT", "pret:MOVE_HAIL", "pret:MOVE_DIVE"],"ITEM_NONE"),
                new("pret:SPECIES_SLOWBRO",80,"SLOWBRO",52,250,[205, 198, 201, 209, 188, 204, 201],["pret:MOVE_ICE_BEAM", "pret:MOVE_SURF", "pret:MOVE_AMNESIA", "pret:MOVE_YAWN"],"ITEM_NONE"),
                new("pret:SPECIES_JYNX",124,"JYNX",54,250,[196, 211, 200, 210],["pret:MOVE_ICE_PUNCH", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_LOVELY_KISS", "pret:MOVE_ATTRACT"],"ITEM_NONE"),
                new("pret:SPECIES_LAPRAS",131,"LAPRAS",54,250,[198, 187, 202, 204, 187, 205],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_ICE_BEAM", "pret:MOVE_SURF", "pret:MOVE_BODY_SLAM"],"ITEM_SITRUS_BERRY"),
            ], 5400, "0ca45b976ec99cfc92fe987784a61f40276819a8d4a8a0e433f477bd37e0b100"),
        [new("pret:TRAINER_ELITE_FOUR_BRUNO")] = new("BRUNO",10,7u,false,[188, 204, 207, 200, 201],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_ONIX",95,"ONIX",51,250,[201, 200, 195, 210],["pret:MOVE_EARTHQUAKE", "pret:MOVE_ROCK_TOMB", "pret:MOVE_IRON_TAIL", "pret:MOVE_ROAR"],"ITEM_NONE"),
                new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",53,250,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MACH_PUNCH", "pret:MOVE_ROCK_TOMB", "pret:MOVE_COUNTER"],"ITEM_NONE"),
                new("pret:SPECIES_HITMONLEE",106,"HITMONLEE",53,250,[194, 195, 206, 199, 201, 200, 198, 191, 191],["pret:MOVE_MEGA_KICK", "pret:MOVE_FORESIGHT", "pret:MOVE_BRICK_BREAK", "pret:MOVE_FACADE"],"ITEM_NONE"),
                new("pret:SPECIES_ONIX",95,"ONIX",54,250,[201, 200, 195, 210],["pret:MOVE_DOUBLE_EDGE", "pret:MOVE_EARTHQUAKE", "pret:MOVE_IRON_TAIL", "pret:MOVE_SAND_TOMB"],"ITEM_NONE"),
                new("pret:SPECIES_MACHAMP",68,"MACHAMP",56,250,[199, 187, 189, 194, 187, 199, 202],["pret:MOVE_CROSS_CHOP", "pret:MOVE_BULK_UP", "pret:MOVE_SCARY_FACE", "pret:MOVE_ROCK_TOMB"],"ITEM_SITRUS_BERRY"),
            ], 5600, "561861da4da01655eec1bcae43e19f3a89276acd5b67bb12d66a9460f3fa48ce"),
        [new("pret:TRAINER_ELITE_FOUR_AGATHA")] = new("AGATHA",138,7u,false,[187, 193, 187, 206, 194, 187],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_GENGAR",94,"GENGAR",54,250,[193, 191, 200, 193, 187, 204],["pret:MOVE_SHADOW_PUNCH", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_TOXIC", "pret:MOVE_DOUBLE_TEAM"],"ITEM_NONE"),
                new("pret:SPECIES_GOLBAT",42,"GOLBAT",54,250,[193, 201, 198, 188, 187, 206],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_POISON_FANG", "pret:MOVE_AIR_CUTTER", "pret:MOVE_BITE"],"ITEM_NONE"),
                new("pret:SPECIES_HAUNTER",93,"HAUNTER",53,250,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_HYPNOSIS", "pret:MOVE_DREAM_EATER", "pret:MOVE_CURSE", "pret:MOVE_MEAN_LOOK"],"ITEM_NONE"),
                new("pret:SPECIES_ARBOK",24,"ARBOK",56,250,[187, 204, 188, 201, 197],["pret:MOVE_SLUDGE_BOMB", "pret:MOVE_SCREECH", "pret:MOVE_IRON_TAIL", "pret:MOVE_BITE"],"ITEM_NONE"),
                new("pret:SPECIES_GENGAR",94,"GENGAR",58,250,[193, 191, 200, 193, 187, 204],["pret:MOVE_SHADOW_BALL", "pret:MOVE_SLUDGE_BOMB", "pret:MOVE_HYPNOSIS", "pret:MOVE_NIGHTMARE"],"ITEM_SITRUS_BERRY"),
            ], 5800, "7c3c24425689bafc5a35e55579e40983b25cd2e189dadfc2988de3a3cbf343dd"),
        [new("pret:TRAINER_ELITE_FOUR_LANCE")] = new("LANCE",10,7u,false,[198, 187, 200, 189, 191],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_GYARADOS",130,"GYARADOS",56,250,[193, 211, 187, 204, 187, 190, 201, 205],["pret:MOVE_HYPER_BEAM", "pret:MOVE_DRAGON_RAGE", "pret:MOVE_TWISTER", "pret:MOVE_BITE"],"ITEM_NONE"),
                new("pret:SPECIES_DRAGONAIR",148,"DRAGONAIR",54,250,[190, 204, 187, 193, 201, 200, 187, 195, 204],["pret:MOVE_HYPER_BEAM", "pret:MOVE_SAFEGUARD", "pret:MOVE_DRAGON_RAGE", "pret:MOVE_OUTRAGE"],"ITEM_NONE"),
                new("pret:SPECIES_DRAGONAIR",148,"DRAGONAIR",54,250,[190, 204, 187, 193, 201, 200, 187, 195, 204],["pret:MOVE_HYPER_BEAM", "pret:MOVE_SAFEGUARD", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_OUTRAGE"],"ITEM_NONE"),
                new("pret:SPECIES_AERODACTYL",142,"AERODACTYL",58,250,[187, 191, 204, 201, 190, 187, 189, 206, 211, 198],["pret:MOVE_HYPER_BEAM", "pret:MOVE_ANCIENT_POWER", "pret:MOVE_WING_ATTACK", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),
                new("pret:SPECIES_DRAGONITE",149,"DRAGONITE",60,250,[190, 204, 187, 193, 201, 200, 195, 206, 191],["pret:MOVE_HYPER_BEAM", "pret:MOVE_SAFEGUARD", "pret:MOVE_OUTRAGE", "pret:MOVE_WING_ATTACK"],"ITEM_SITRUS_BERRY"),
            ], 6000, "30153ce53d9c8f377173523aae935cd3c7abe7d56a8613d3bbb60722c676b9c8"),
        [new("pret:TRAINER_CHAMPION_FIRST_SQUIRTLE")] = new("TERRY",0,7u,false,[206, 191, 204, 204, 211],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE"],
            [
                new("pret:SPECIES_PIDGEOT",18,"PIDGEOT",59,255,[202, 195, 190, 193, 191, 201, 206],["pret:MOVE_AERIAL_ACE", "pret:MOVE_FEATHER_DANCE", "pret:MOVE_SAND_ATTACK", "pret:MOVE_WHIRLWIND"],"ITEM_NONE"),
                new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",57,255,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_RECOVER", "pret:MOVE_REFLECT"],"ITEM_NONE"),
                new("pret:SPECIES_RHYDON",112,"RHYDON",59,255,[204, 194, 211, 190, 201, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_EARTHQUAKE", "pret:MOVE_ROCK_TOMB", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),
                new("pret:SPECIES_ARCANINE",59,"ARCANINE",59,255,[187, 204, 189, 187, 200, 195, 200, 191],["pret:MOVE_EXTREME_SPEED", "pret:MOVE_FLAMETHROWER", "pret:MOVE_ROAR", "pret:MOVE_BITE"],"ITEM_NONE"),
                new("pret:SPECIES_EXEGGUTOR",103,"EXEGGUTOR",61,255,[191, 210, 191, 193, 193, 207, 206, 201, 204],["pret:MOVE_GIGA_DRAIN", "pret:MOVE_EGG_BOMB", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_LIGHT_SCREEN"],"ITEM_NONE"),
                new("pret:SPECIES_BLASTOISE",9,"BLASTOISE",63,255,[188, 198, 187, 205, 206, 201, 195, 205, 191],["pret:MOVE_HYDRO_PUMP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_SKULL_BASH", "pret:MOVE_BITE"],"ITEM_SITRUS_BERRY"),
            ], 6300, "1148fc196a8a1078881d6b46c5e8adcf520e2128b9247bd730bf82f3b716192e"),
        [new("pret:TRAINER_CHAMPION_FIRST_BULBASAUR")] = new("TERRY",0,7u,false,[206, 191, 204, 204, 211],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE"],
            [
                new("pret:SPECIES_PIDGEOT",18,"PIDGEOT",59,255,[202, 195, 190, 193, 191, 201, 206],["pret:MOVE_AERIAL_ACE", "pret:MOVE_FEATHER_DANCE", "pret:MOVE_SAND_ATTACK", "pret:MOVE_WHIRLWIND"],"ITEM_NONE"),
                new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",57,255,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_RECOVER", "pret:MOVE_REFLECT"],"ITEM_NONE"),
                new("pret:SPECIES_RHYDON",112,"RHYDON",59,255,[204, 194, 211, 190, 201, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_EARTHQUAKE", "pret:MOVE_ROCK_TOMB", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),
                new("pret:SPECIES_GYARADOS",130,"GYARADOS",59,255,[193, 211, 187, 204, 187, 190, 201, 205],["pret:MOVE_HYDRO_PUMP", "pret:MOVE_DRAGON_RAGE", "pret:MOVE_BITE", "pret:MOVE_THRASH"],"ITEM_NONE"),
                new("pret:SPECIES_ARCANINE",59,"ARCANINE",61,255,[187, 204, 189, 187, 200, 195, 200, 191],["pret:MOVE_EXTREME_SPEED", "pret:MOVE_FLAMETHROWER", "pret:MOVE_ROAR", "pret:MOVE_BITE"],"ITEM_NONE"),
                new("pret:SPECIES_VENUSAUR",3,"VENUSAUR",63,255,[208, 191, 200, 207, 205, 187, 207, 204],["pret:MOVE_SOLAR_BEAM", "pret:MOVE_SYNTHESIS", "pret:MOVE_SUNNY_DAY", "pret:MOVE_GROWTH"],"ITEM_SITRUS_BERRY"),
            ], 6300, "aa1ac0addd4773f1d5bb0e896170902e35a996b0e9e2acd5624df36ec9f358d3"),
        [new("pret:TRAINER_CHAMPION_FIRST_CHARMANDER")] = new("TERRY",0,7u,false,[206, 191, 204, 204, 211],["ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE", "ITEM_FULL_RESTORE"],
            [
                new("pret:SPECIES_PIDGEOT",18,"PIDGEOT",59,255,[202, 195, 190, 193, 191, 201, 206],["pret:MOVE_AERIAL_ACE", "pret:MOVE_FEATHER_DANCE", "pret:MOVE_SAND_ATTACK", "pret:MOVE_WHIRLWIND"],"ITEM_NONE"),
                new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",57,255,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_RECOVER", "pret:MOVE_REFLECT"],"ITEM_NONE"),
                new("pret:SPECIES_RHYDON",112,"RHYDON",59,255,[204, 194, 211, 190, 201, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_EARTHQUAKE", "pret:MOVE_ROCK_TOMB", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),
                new("pret:SPECIES_EXEGGUTOR",103,"EXEGGUTOR",59,255,[191, 210, 191, 193, 193, 207, 206, 201, 204],["pret:MOVE_GIGA_DRAIN", "pret:MOVE_EGG_BOMB", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_LIGHT_SCREEN"],"ITEM_NONE"),
                new("pret:SPECIES_GYARADOS",130,"GYARADOS",61,255,[193, 211, 187, 204, 187, 190, 201, 205],["pret:MOVE_HYDRO_PUMP", "pret:MOVE_DRAGON_RAGE", "pret:MOVE_BITE", "pret:MOVE_THRASH"],"ITEM_NONE"),
                new("pret:SPECIES_CHARIZARD",6,"CHARIZARD",63,255,[189, 194, 187, 204, 195, 212, 187, 204, 190],["pret:MOVE_FIRE_BLAST", "pret:MOVE_AERIAL_ACE", "pret:MOVE_SLASH", "pret:MOVE_FIRE_SPIN"],"ITEM_SITRUS_BERRY"),
            ], 6300, "456dfd4c357582c2c2209a37ad23b970487cd77c66458b5e3d6384dc0140f8ee"),
    }.ToImmutableDictionary();
    private static readonly ImmutableDictionary<TrainerId, string> ExpectedBattles = new Dictionary<TrainerId, string>
    {
        [new("pret:TRAINER_ELITE_FOUR_LORELEI")] = "ed86bc1903f2b2a78e534f25fdb3ff441a1f1851a61979be5de28ccd5a9a22ab",
        [new("pret:TRAINER_ELITE_FOUR_BRUNO")] = "b9a9862efe6ad766ce233129da761a62416fd3c2a18c0b263e4f027fc409360f",
        [new("pret:TRAINER_ELITE_FOUR_AGATHA")] = "b2b740083281cb8b1f98cca836bcdc7c4ed08d50f9998baf157e9992643df9e6",
        [new("pret:TRAINER_ELITE_FOUR_LANCE")] = "046f522cceadf87adf77192e7beccd0a9ed628938382ff45b7196724821a1358",
        [new("pret:TRAINER_CHAMPION_FIRST_SQUIRTLE")] = "c37d24b7608b5513593d3f719cf2a22ea47cdb60a9c81d12254d9056ec246d76",
        [new("pret:TRAINER_CHAMPION_FIRST_BULBASAUR")] = "8b0af404cd166528251c79bbd5644e940db08a020ee788c0e93f41362d84e0fc",
        [new("pret:TRAINER_CHAMPION_FIRST_CHARMANDER")] = "8ecf38f3d5d51eabd245abfb0fb8da70c26f6991a3b67a34b81aa11d0f49f98c",
    }.ToImmutableDictionary();
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
        if (!Expected.TryGetValue(id, out var expected)) throw new NotSupportedException("pokemon_league_trainer_id");
        return expected.BasePrize;
    }

    public static TrainerPartyCreationResult Create(PokemonLeagueTrainerCatalog trainers,
        PokemonLeagueOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(PokemonLeagueOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("pokemon_league_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species))
                throw new NotSupportedException("pokemon_league_trainer_species");
            if (member.CustomMoves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
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
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 7 || catalog.Battles.Count != 7)
            throw new NotSupportedException("pokemon_league_trainer_scope");
        foreach (var (id, expected) in Expected)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != expected.TrainerSha256)
                throw new NotSupportedException("pokemon_league_trainer_source");
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new NotSupportedException("pokemon_league_trainer_battle_key");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattles.TryGetValue(battle.Trainer, out var expected) ||
            CampaignSourceFingerprint.Compute(battle) != expected)
            throw new NotSupportedException("pokemon_league_trainer_battle_source");
    }
}
