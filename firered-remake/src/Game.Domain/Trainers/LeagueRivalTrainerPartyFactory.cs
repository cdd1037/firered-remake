using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Three exact six-member late Route22 teams; source names determine PID, IVs and no-shiny OT draws.</summary>
public static class LeagueRivalTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-league-rival-trainers-v1";
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv,
        int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle,
        int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_RIVAL_ROUTE22_LATE_SQUIRTLE")] = new("TERRY", 0, 7u, false, [206, 191, 204, 204, 211], ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_PIDGEOT",18,"PIDGEOT",47,150,[202, 195, 190, 193, 191, 201, 206],["pret:MOVE_FEATHER_DANCE", "pret:MOVE_WING_ATTACK", "pret:MOVE_GUST", "pret:MOVE_QUICK_ATTACK"],"ITEM_NONE"),
                new("pret:SPECIES_RHYHORN",111,"RHYHORN",45,150,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_HORN_DRILL", "pret:MOVE_ROCK_BLAST", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),
                new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",45,150,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_FLAME_WHEEL", "pret:MOVE_TAKE_DOWN", "pret:MOVE_LEER", "pret:MOVE_AGILITY"],"ITEM_NONE"),
                new("pret:SPECIES_EXEGGCUTE",102,"EXEGGCUTE",45,150,[191, 210, 191, 193, 193, 189, 207, 206, 191],["pret:MOVE_SOLAR_BEAM", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_POISON_POWDER", "pret:MOVE_STUN_SPORE"],"ITEM_NONE"),
                new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",47,150,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_CALM_MIND", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_DISABLE"],"ITEM_NONE"),
                new("pret:SPECIES_BLASTOISE",9,"BLASTOISE",53,250,[188, 198, 187, 205, 206, 201, 195, 205, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN"],"ITEM_NONE")
            ], 1908),
        [new("pret:TRAINER_RIVAL_ROUTE22_LATE_BULBASAUR")] = new("TERRY", 0, 7u, false, [206, 191, 204, 204, 211], ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_PIDGEOT",18,"PIDGEOT",47,150,[202, 195, 190, 193, 191, 201, 206],["pret:MOVE_FEATHER_DANCE", "pret:MOVE_WING_ATTACK", "pret:MOVE_GUST", "pret:MOVE_QUICK_ATTACK"],"ITEM_NONE"),
                new("pret:SPECIES_RHYHORN",111,"RHYHORN",45,150,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_HORN_DRILL", "pret:MOVE_ROCK_BLAST", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),
                new("pret:SPECIES_GYARADOS",130,"GYARADOS",45,150,[193, 211, 187, 204, 187, 190, 201, 205],["pret:MOVE_HYDRO_PUMP", "pret:MOVE_TWISTER", "pret:MOVE_LEER", "pret:MOVE_RAIN_DANCE"],"ITEM_NONE"),
                new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",45,150,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_FLAME_WHEEL", "pret:MOVE_TAKE_DOWN", "pret:MOVE_LEER", "pret:MOVE_AGILITY"],"ITEM_NONE"),
                new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",47,150,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_CALM_MIND", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_DISABLE"],"ITEM_NONE"),
                new("pret:SPECIES_VENUSAUR",3,"VENUSAUR",53,250,[208, 191, 200, 207, 205, 187, 207, 204],["pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT", "pret:MOVE_GROWTH", "pret:MOVE_SYNTHESIS"],"ITEM_NONE")
            ], 1908),
        [new("pret:TRAINER_RIVAL_ROUTE22_LATE_CHARMANDER")] = new("TERRY", 0, 7u, false, [206, 191, 204, 204, 211], ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],
            [
                new("pret:SPECIES_PIDGEOT",18,"PIDGEOT",47,150,[202, 195, 190, 193, 191, 201, 206],["pret:MOVE_FEATHER_DANCE", "pret:MOVE_WING_ATTACK", "pret:MOVE_GUST", "pret:MOVE_QUICK_ATTACK"],"ITEM_NONE"),
                new("pret:SPECIES_RHYHORN",111,"RHYHORN",45,150,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_HORN_DRILL", "pret:MOVE_ROCK_BLAST", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),
                new("pret:SPECIES_EXEGGCUTE",102,"EXEGGCUTE",45,150,[191, 210, 191, 193, 193, 189, 207, 206, 191],["pret:MOVE_SOLAR_BEAM", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_POISON_POWDER", "pret:MOVE_STUN_SPORE"],"ITEM_NONE"),
                new("pret:SPECIES_GYARADOS",130,"GYARADOS",45,150,[193, 211, 187, 204, 187, 190, 201, 205],["pret:MOVE_HYDRO_PUMP", "pret:MOVE_TWISTER", "pret:MOVE_LEER", "pret:MOVE_RAIN_DANCE"],"ITEM_NONE"),
                new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",47,150,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_CALM_MIND", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_DISABLE"],"ITEM_NONE"),
                new("pret:SPECIES_CHARIZARD",6,"CHARIZARD",53,250,[189, 194, 187, 204, 195, 212, 187, 204, 190],["pret:MOVE_FLAMETHROWER", "pret:MOVE_WING_ATTACK", "pret:MOVE_SLASH", "pret:MOVE_SCARY_FACE"],"ITEM_NONE")
            ], 1908),
    }.ToImmutableDictionary();
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
        if (!Expected.TryGetValue(id, out var expected)) throw new NotSupportedException("league_rival_trainer_id");
        return expected.BasePrize;
    }

    public static TrainerPartyCreationResult Create(LeagueRivalTrainerCatalog trainers,
        CinnabarOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("league_rival_trainer_catalog");
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
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 3 || catalog.Battles.Count != 3)
            throw new NotSupportedException("league_rival_trainer_scope");
        foreach (var (id, expected) in Expected)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("league_rival_trainer_source");
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new NotSupportedException("league_rival_trainer_battle_key");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattleKeys.Contains(battle.Trainer))
            throw new NotSupportedException("league_rival_trainer_battle_source");
    }
}
