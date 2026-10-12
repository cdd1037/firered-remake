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
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, int[] Encoded, string[] Items, ExpectedMember[] Party,
        string TrainerSha256, string BattleSha256);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_LEADER_SABRINA")] = new("SABRINA",129,7u,[205, 187, 188, 204, 195, 200, 187],["ITEM_HYPER_POTION", "ITEM_HYPER_POTION", "ITEM_FULL_HEAL", "ITEM_NONE"],[new("pret:SPECIES_KADABRA",64,"KADABRA",38,0,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_PSYBEAM", "pret:MOVE_REFLECT", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_CALM_MIND"]),new("pret:SPECIES_MR_MIME",122,"MR. MIME",37,0,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_BARRIER", "pret:MOVE_PSYBEAM", "pret:MOVE_BATON_PASS", "pret:MOVE_CALM_MIND"]),new("pret:SPECIES_VENOMOTH",49,"VENOMOTH",38,0,[208, 191, 200, 201, 199, 201, 206, 194],["pret:MOVE_PSYBEAM", "pret:MOVE_GUST", "pret:MOVE_LEECH_LIFE", "pret:MOVE_SUPERSONIC"]),new("pret:SPECIES_ALAKAZAM",65,"ALAKAZAM",43,0,[187, 198, 187, 197, 187, 212, 187, 199],["pret:MOVE_PSYCHIC", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_CALM_MIND"])],"deb664453854f82464d6faccba5ac72b0328889c27ff6572461b4d6cb1e4416f","c3129603d8ea89ed83dca4c974c42bbc59ee9d3dfbd5272850354b767edfb4e1"),
        [new("pret:TRAINER_PSYCHIC_JOHAN")] = new("JOHAN",4,1u,[196, 201, 194, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KADABRA",64,"KADABRA",31,50,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_PSYBEAM", "pret:MOVE_REFLECT", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT"]),new("pret:SPECIES_SLOWPOKE",79,"SLOWPOKE",31,50,[205, 198, 201, 209, 202, 201, 197, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"]),new("pret:SPECIES_MR_MIME",122,"MR. MIME",31,50,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_REFLECT", "pret:MOVE_MAGICAL_LEAF", "pret:MOVE_ENCORE", "pret:MOVE_PSYBEAM"]),new("pret:SPECIES_KADABRA",64,"KADABRA",31,50,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_PSYBEAM", "pret:MOVE_REFLECT", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT"])],"0f6364130bfb43d75dd2e4eb49e417aa2b6470688ecbd512e22bf20ee613cc4b","6b9f80866a223192c49f943992a6026b12575bc77fa56404702af6e93b41f3bc"),
        [new("pret:TRAINER_PSYCHIC_TYRON")] = new("TYRON",4,1u,[206, 211, 204, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MR_MIME",122,"MR. MIME",34,50,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_MAGICAL_LEAF", "pret:MOVE_ENCORE", "pret:MOVE_PSYBEAM", "pret:MOVE_RECYCLE"]),new("pret:SPECIES_KADABRA",64,"KADABRA",34,50,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_REFLECT", "pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_ROLE_PLAY"])],"72ee9bc57ce1d2d9c0c55d1c5b2ba86668b0c9de416a8176873c0d535b01431e","f8b34a81fc842696418b039ad6b48a30a189d2976bdd7c0ea1140b20dd8dd6c5"),
        [new("pret:TRAINER_PSYCHIC_CAMERON")] = new("CAMERON",4,1u,[189, 187, 199, 191, 204, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SLOWPOKE",79,"SLOWPOKE",33,50,[205, 198, 201, 209, 202, 201, 197, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"]),new("pret:SPECIES_SLOWPOKE",79,"SLOWPOKE",33,50,[205, 198, 201, 209, 202, 201, 197, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"]),new("pret:SPECIES_SLOWBRO",80,"SLOWBRO",33,50,[205, 198, 201, 209, 188, 204, 201],["pret:MOVE_WATER_GUN", "pret:MOVE_CONFUSION", "pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT"])],"395b82361ba9dc569de08ee10cd2af93a460c3bafc3a69becd9380278cae4537","fc18521b5f6959854e2a4481c911484c31e064dbd4f4286b9831612b51a78706"),
        [new("pret:TRAINER_PSYCHIC_PRESTON")] = new("PRESTON",4,1u,[202, 204, 191, 205, 206, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SLOWBRO",80,"SLOWBRO",38,50,[205, 198, 201, 209, 188, 204, 201],["pret:MOVE_DISABLE", "pret:MOVE_HEADBUTT", "pret:MOVE_AMNESIA", "pret:MOVE_WITHDRAW"])],"771d63fc1ddc7e6e4ad40b2d9180abc7b9fc33d5fab6d35b8b7bdef0cb229824","3539512d7d56f59dafb7fa1a9593c08d52a8c00419263ecbdf63b5d5e7c285f0"),
        [new("pret:TRAINER_CHANNELER_AMANDA")] = new("AMANDA",131,1u,[187, 199, 187, 200, 190, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GASTLY",92,"GASTLY",34,0,[193, 187, 205, 206, 198, 211],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_DREAM_EATER", "pret:MOVE_DESTINY_BOND"]),new("pret:SPECIES_HAUNTER",93,"HAUNTER",34,0,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SHADOW_PUNCH", "pret:MOVE_DREAM_EATER"])],"3169c8562c46f40ec55e45c0dd2bd6ad6aef6344c7cf9ac27ce5fa54ad68731b","05905b8d04980ee0ce48e801d32b0cf8ee9d516ad728c78f170630f70baea510"),
        [new("pret:TRAINER_CHANNELER_STACY")] = new("STACY",131,1u,[205, 206, 187, 189, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HAUNTER",93,"HAUNTER",38,0,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SHADOW_PUNCH", "pret:MOVE_DREAM_EATER"])],"492c3fc90bdf838da458f0f3774fd307395dd4de2f4d64f161dce0cbebb895aa","d9d699e65559ba7bd26d583a0b90639bfc8618edfb1e4e5334046c8d5a27e64a"),
        [new("pret:TRAINER_CHANNELER_TASHA")] = new("TASHA",131,1u,[206, 187, 205, 194, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GASTLY",92,"GASTLY",33,0,[193, 187, 205, 206, 198, 211],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_DREAM_EATER", "pret:MOVE_DESTINY_BOND"]),new("pret:SPECIES_GASTLY",92,"GASTLY",33,0,[193, 187, 205, 206, 198, 211],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_DREAM_EATER", "pret:MOVE_DESTINY_BOND"]),new("pret:SPECIES_HAUNTER",93,"HAUNTER",33,0,[194, 187, 207, 200, 206, 191, 204],["pret:MOVE_NIGHT_SHADE", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SHADOW_PUNCH", "pret:MOVE_DREAM_EATER"])],"d739b2ee331b52b709955267b7f19c1b2dc4a0346e2c8051ae8cfa95477afd78","55e395c6c653e43f79671d98212021652765a73bec8dd10128b10a89f01a0ca0"),
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
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
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
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != expected.TrainerSha256)
                throw new NotSupportedException("saffron_gym_trainer_source");
            if (!catalog.Battles.TryGetValue(id, out var battle)) throw new NotSupportedException("saffron_gym_trainer_battle_missing");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !Expected.TryGetValue(battle.Trainer, out var expected) ||
            CampaignSourceFingerprint.Compute(battle) != expected.BattleSha256)
            throw new NotSupportedException("saffron_gym_trainer_battle_source");
    }
}
