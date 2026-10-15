using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned 8-team, 20-member SaffronGym recipe; creation reuses the source NPC factory.</summary>
public static class SaffronGymTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-saffron-gym-trainers-v1";
    public static TrainerId Sabrina { get; } = new("pret:TRAINER_LEADER_SABRINA");
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, int[] Encoded, string[] Items, ExpectedMember[] Party);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_LEADER_SABRINA")] = new("SABRINA",129,7u,[205, 187, 188, 204, 195, 200, 187],["ITEM_HYPER_POTION", "ITEM_HYPER_POTION", "ITEM_FULL_HEAL", "ITEM_NONE"],[new("pret:SPECIES_KADABRA",64,"KADABRA",38,0,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_PSYBEAM", "pret:MOVE_REFLECT", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_CALM_MIND"]),new("pret:SPECIES_MR_MIME",122,"MR. MIME",37,0,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_BARRIER", "pret:MOVE_PSYBEAM", "pret:MOVE_BATON_PASS", "pret:MOVE_CALM_MIND"]),new("pret:SPECIES_VENOMOTH",49,"VENOMOTH",38,0,[208, 191, 200, 201, 199, 201, 206, 194],["pret:MOVE_PSYBEAM", "pret:MOVE_GUST", "pret:MOVE_LEECH_LIFE", "pret:MOVE_SUPERSONIC"]),new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",43,0,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_CALM_MIND"])]),
        [new("pret:TRAINER_PSYCHIC_JOHAN")] = new("JOHAN",4,1u,[196, 201, 194, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KADABRA",64,"KADABRA",31,50,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_PSYBEAM", "pret:MOVE_REFLECT", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT"]),new("pret:SPECIES_SLOWPOKE",79,"SLOWPOKE",31,50,[205, 198, 201, 209, 202, 201, 197, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"]),new("pret:SPECIES_MR_MIME",122,"MR. MIME",31,50,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_REFLECT", "pret:MOVE_MAGICAL_LEAF", "pret:MOVE_ENCORE", "pret:MOVE_PSYBEAM"]),new("pret:SPECIES_KADABRA",64,"KADABRA",31,50,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_PSYBEAM", "pret:MOVE_REFLECT", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT"])]),
        [new("pret:TRAINER_PSYCHIC_TYRON")] = new("TYRON",4,1u,[206, 211, 204, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MR_MIME",122,"MR. MIME",34,50,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_MAGICAL_LEAF", "pret:MOVE_ENCORE", "pret:MOVE_PSYBEAM", "pret:MOVE_RECYCLE"]),new("pret:SPECIES_KADABRA",64,"KADABRA",34,50,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_REFLECT", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_ROLE_PLAY"])]),
        [new("pret:TRAINER_PSYCHIC_CAMERON")] = new("CAMERON",4,1u,[189, 187, 199, 191, 204, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SLOWPOKE",79,"SLOWPOKE",33,50,[205, 198, 201, 209, 202, 201, 197, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"]),new("pret:SPECIES_SLOWPOKE",79,"SLOWPOKE",33,50,[205, 198, 201, 209, 202, 201, 197, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"]),new("pret:SPECIES_SLOWBRO",80,"SLOWBRO",33,50,[205, 198, 201, 209, 188, 204, 201],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"])]),
        [new("pret:TRAINER_PSYCHIC_PRESTON")] = new("PRESTON",4,1u,[202, 204, 191, 205, 206, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SLOWBRO",80,"SLOWBRO",38,50,[205, 198, 201, 209, 188, 204, 201],["pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT", "pret:MOVE_AMNESIA", "pret:MOVE_WITHDRAW"])]),
        [new("pret:TRAINER_CHANNELER_AMANDA")] = new("AMANDA",131,1u,[187, 199, 187, 200, 190, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GASTLY",92,"GASTLY",34,0,[193, 187, 205, 206, 198, 211],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_DREAM_EATER", "pret:MOVE_DESTINY_BOND"]),new("pret:SPECIES_HAUNTER",93,"HAUNTER",34,0,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SHADOW_PUNCH", "pret:MOVE_DREAM_EATER"])]),
        [new("pret:TRAINER_CHANNELER_STACY")] = new("STACY",131,1u,[205, 206, 187, 189, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HAUNTER",93,"HAUNTER",38,0,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SHADOW_PUNCH", "pret:MOVE_DREAM_EATER"])]),
        [new("pret:TRAINER_CHANNELER_TASHA")] = new("TASHA",131,1u,[206, 187, 205, 194, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GASTLY",92,"GASTLY",33,0,[193, 187, 205, 206, 198, 211],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_DREAM_EATER", "pret:MOVE_DESTINY_BOND"]),new("pret:SPECIES_GASTLY",92,"GASTLY",33,0,[193, 187, 205, 206, 198, 211],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_DREAM_EATER", "pret:MOVE_DESTINY_BOND"]),new("pret:SPECIES_HAUNTER",93,"HAUNTER",33,0,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SHADOW_PUNCH", "pret:MOVE_DREAM_EATER"])]),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(t => t.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(SaffronGymTrainerCatalog trainers, SaffronGymOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SaffronGymOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("saffron_gym_trainer_catalog");
        var source = opponents.Mechanics; var expected = Expected[id];
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("saffron_gym_trainer_species");
            var moves = expected.Party[i].Moves.Select(m => new MoveId(m));
            if (trainer.PartyFlags == 0 && !PokemonMoveRules.InitialMovesCore(source.Catalog, source.Catalog.Species[member.Species], member.Level)
                    .Select(m => m.Move).SequenceEqual(moves)) throw new NotSupportedException("saffron_gym_trainer_natural_moves");
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("saffron_gym_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if (trainer.PartyFlags == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SaffronGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 8 || catalog.Battles.Count != 8)
            throw new NotSupportedException("saffron_gym_trainer_scope");
        foreach (var (id, expected) in Expected)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("saffron_gym_trainer_source");
            if (!catalog.Battles.TryGetValue(id, out var battle)) throw new NotSupportedException("saffron_gym_trainer_battle_missing");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !Expected.TryGetValue(battle.Trainer, out var expected))
            throw new NotSupportedException("saffron_gym_trainer_battle_source");
    }
}
