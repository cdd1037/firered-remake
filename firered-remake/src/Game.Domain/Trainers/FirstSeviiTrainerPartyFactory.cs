using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned 10-team, 21-member FirstSevii recipe; creation reuses the source NPC factory.</summary>
public static class FirstSeviiTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-first-sevii-trainers-v1";
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party,
        string TrainerSha256);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_AROMA_LADY_NIKKI")] = new("NIKKI",129,1u,false,[200, 195, 197, 197, 195],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BELLSPROUT",69,"BELLSPROUT",37,0,[188, 191, 198, 198, 205, 202, 204, 201, 207, 206],["pret:MOVE_STUN_SPORE", "pret:MOVE_ACID", "pret:MOVE_SWEET_SCENT", "pret:MOVE_RAZOR_LEAF"]),new("pret:SPECIES_WEEPINBELL",70,"WEEPINBELL",37,0,[209, 191, 191, 202, 195, 200, 188, 191, 198, 198],["pret:MOVE_POISON_POWDER", "pret:MOVE_STUN_SPORE", "pret:MOVE_ACID", "pret:MOVE_SWEET_SCENT"])],"e53722bceeac8cf0e950eeb8b23bfd79b7b6c94267ca46d9a5264457e5027a05"),
        [new("pret:TRAINER_AROMA_LADY_VIOLET")] = new("VIOLET",129,1u,false,[208, 195, 201, 198, 191, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BULBASAUR",1,"BULBASAUR",36,0,[188, 207, 198, 188, 187, 205, 187, 207, 204],["pret:MOVE_SLEEP_POWDER", "pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT", "pret:MOVE_GROWTH"]),new("pret:SPECIES_IVYSAUR",2,"IVYSAUR",36,0,[195, 208, 211, 205, 187, 207, 204],["pret:MOVE_POISON_POWDER", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT"]),new("pret:SPECIES_IVYSAUR",2,"IVYSAUR",36,0,[195, 208, 211, 205, 187, 207, 204],["pret:MOVE_POISON_POWDER", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT"])],"906a6177a2d905e6aae73c27b8520cdfe75f8033bd400cc11c4858a9599154e2"),
        [new("pret:TRAINER_BIKER_GOON")] = new("GOON",0,1u,false,[193, 201, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KOFFING",109,"KOFFING",37,0,[197, 201, 192, 192, 195, 200, 193],["pret:MOVE_HAZE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SLUDGE", "pret:MOVE_TACKLE"]),new("pret:SPECIES_GRIMER",88,"GRIMER",37,0,[193, 204, 195, 199, 191, 204],["pret:MOVE_ACID_ARMOR", "pret:MOVE_SCREECH", "pret:MOVE_MINIMIZE", "pret:MOVE_SLUDGE"])],"c029540fa6cf245c7e43310bc990e884c05010f07bc0a30f227dd91585f5a58a"),
        [new("pret:TRAINER_BIKER_GOON_2")] = new("GOON",0,1u,false,[193, 201, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KOFFING",109,"KOFFING",38,0,[197, 201, 192, 192, 195, 200, 193],["pret:MOVE_HAZE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SLUDGE", "pret:MOVE_TACKLE"])],"f81633804b0498e503af33dbe9d0afd7fe498d08fab097534de20a0393172413"),
        [new("pret:TRAINER_BIKER_GOON_3")] = new("GOON",0,1u,false,[193, 201, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GRIMER",88,"GRIMER",38,0,[193, 204, 195, 199, 191, 204],["pret:MOVE_SLUDGE", "pret:MOVE_MINIMIZE", "pret:MOVE_SCREECH", "pret:MOVE_ACID_ARMOR"])],"575c968bae5c81e75daf5ed480feea3262e6c318e513bb9a8c600ac3d303346a"),
        [new("pret:TRAINER_CUE_BALL_PAXTON")] = new("PAXTON",0,1u,false,[202, 187, 210, 206, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_WEEZING",110,"WEEZING",39,50,[209, 191, 191, 212, 195, 200, 193],["pret:MOVE_SELF_DESTRUCT", "pret:MOVE_SLUDGE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_HAZE"]),new("pret:SPECIES_MUK",89,"MUK",39,50,[199, 207, 197],["pret:MOVE_SLUDGE", "pret:MOVE_MINIMIZE", "pret:MOVE_SCREECH", "pret:MOVE_ACID_ARMOR"])],"91facdd129f07835e30b9e839ef9d706a1d74503f4150c58a44f94dd34d2e4ee"),
        [new("pret:TRAINER_SWIMMER_FEMALE_TISHA")] = new("TISHA",129,1u,false,[206, 195, 205, 194, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KINGLER",99,"KINGLER",38,0,[197, 195, 200, 193, 198, 191, 204],["pret:MOVE_HARDEN", "pret:MOVE_MUD_SHOT", "pret:MOVE_STOMP", "pret:MOVE_GUILLOTINE"])],"ea2cea050c9530d71851b4f543f78da8a2d1ce0b4a7c309b938a71198603cd8f"),
        [new("pret:TRAINER_TUBER_ALEXIS")] = new("ALEXIS",2,1u,false,[187, 198, 191, 210, 195, 205],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARYU",120,"STARYU",34,0,[205, 206, 187, 204, 211, 207],["pret:MOVE_CAMOUFLAGE", "pret:MOVE_SWIFT", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_MINIMIZE"]),new("pret:SPECIES_STARYU",120,"STARYU",34,0,[205, 206, 187, 204, 211, 207],["pret:MOVE_CAMOUFLAGE", "pret:MOVE_SWIFT", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_MINIMIZE"]),new("pret:SPECIES_KRABBY",98,"KRABBY",34,0,[197, 204, 187, 188, 188, 211],["pret:MOVE_HARDEN", "pret:MOVE_MUD_SHOT", "pret:MOVE_STOMP", "pret:MOVE_GUILLOTINE"]),new("pret:SPECIES_KRABBY",98,"KRABBY",34,0,[197, 204, 187, 188, 188, 211],["pret:MOVE_HARDEN", "pret:MOVE_MUD_SHOT", "pret:MOVE_STOMP", "pret:MOVE_GUILLOTINE"])],"0af31e883f615c6f655d7151e8b275f14354ad45da56b87a611548abe9724a27"),
        [new("pret:TRAINER_TUBER_AMIRA")] = new("AMIRA",2,1u,false,[187, 199, 195, 204, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_POLIWAG",60,"POLIWAG",34,0,[202, 201, 198, 195, 209, 187, 193],["pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BODY_SLAM"]),new("pret:SPECIES_POLIWHIRL",61,"POLIWHIRL",35,0,[202, 201, 198, 195, 209, 194, 195, 204, 198],["pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BODY_SLAM"]),new("pret:SPECIES_POLIWAG",60,"POLIWAG",34,0,[202, 201, 198, 195, 209, 187, 193],["pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BODY_SLAM"])],"4fa83cc5e1dba5baccb41440b93cb821f8536a1baff65fe2bd3b2000191c8af6"),
        [new("pret:TRAINER_TWINS_JOY_MEG")] = new("JOY & MEG",9,1u,true,[196, 201, 211, 0, 45, 0, 199, 191, 193],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",37,0,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_DEFENSE_CURL", "pret:MOVE_METRONOME", "pret:MOVE_COSMIC_POWER", "pret:MOVE_MOONLIGHT"]),new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",37,0,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_DEFENSE_CURL", "pret:MOVE_METRONOME", "pret:MOVE_COSMIC_POWER", "pret:MOVE_MOONLIGHT"])],"1cc2d06eafa030ce9ca6b576244714357e5cdff6d3966133f8385d9b60bc17a7"),
    }.ToImmutableDictionary();
    private static readonly ImmutableDictionary<FirstSeviiBattleKey, string> ExpectedBattles = new Dictionary<FirstSeviiBattleKey, string>
    {
        [new("ThreeIsland_BondBridge_EventScript_Alexis",2174)] = "0e7ef6817248e410ddfc4ffcae3d6ad7ef82c9704bb8aed4a13cf7d1060ec77b",
        [new("ThreeIsland_BondBridge_EventScript_Amira",2162)] = "4c9decc47ab7395b6dfd0e010e500b07d1e9eb9d4a45c860c0f2953365d8cfbf",
        [new("ThreeIsland_BondBridge_EventScript_Joy",2198)] = "ea39bf59a65005a18effc01dea8992198fdf312fc7ad1f691b3bbc4eea5fb096",
        [new("ThreeIsland_BondBridge_EventScript_Meg",2210)] = "e3f9d8844665d188739239048fd1bde9dca8bfddb67352ece9d17592683196bf",
        [new("ThreeIsland_BondBridge_EventScript_Nikki",2138)] = "57212858b581e6016f2b679ec006074644104dcc6c8d70765d5356ec9e7a9906",
        [new("ThreeIsland_BondBridge_EventScript_Tisha",2186)] = "7114e1c5696d357cda6eee928a056af7ce9754976b5c2bb9fd7db2804084579a",
        [new("ThreeIsland_BondBridge_EventScript_Violet",2150)] = "9c68975b1efd934e25813c6671e5a94ab94c971c1f8c60f271b81e82171cee43",
        [new("ThreeIsland_EventScript_BattleBikersScene",211)] = "21fe3a1884f3b9c9f11c71aef2d2d1c1465495c0e18ce237030378d9fb49f63b",
        [new("ThreeIsland_EventScript_BattleBikersScene",221)] = "b3d82c8b7bf23db2626d009cf57a72db273a324bac93a0e382bf2dc45f1bb491",
        [new("ThreeIsland_EventScript_BattleBikersScene",231)] = "982ccd6a6f09a1193e4a8539e45d2bdf529797da50464f82dad64681a0914505",
        [new("ThreeIsland_EventScript_BattleBikersScene",248)] = "6452d34c084f836445a8a3c8d8eb524724beb104932a285efb98a95a73893aab",
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> AllTrainers { get; } = Expected.Keys.OrderBy(t => t.Value, StringComparer.Ordinal).ToImmutableArray();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t => !Expected[t].DoubleBattle).ToImmutableArray();
    public static TrainerPartyCreationResult Create(FirstSeviiTrainerCatalog trainers, CinnabarOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("first_sevii_trainer_catalog");
        var source = opponents.Mechanics; var expected = Expected[id];
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("first_sevii_trainer_species");
            var moves = expected.Party[i].Moves.Select(m => new MoveId(m));
            if (trainer.PartyFlags == 0 && !PokemonMoveRules.InitialMovesCore(source.Catalog, source.Catalog.Species[member.Species], member.Level)
                    .Select(m => m.Move).SequenceEqual(moves)) throw new NotSupportedException("first_sevii_trainer_natural_moves");
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
                throw new NotSupportedException("first_sevii_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if (trainer.PartyFlags == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(FirstSeviiTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesBySource is null || catalog.Trainers.Count != 10 || catalog.BattlesBySource.Count != 11)
            throw new NotSupportedException("first_sevii_trainer_scope");
        foreach (var (id, expected) in Expected)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != expected.TrainerSha256)
                throw new NotSupportedException("first_sevii_trainer_source");

        }
        foreach (var (script, battle) in catalog.BattlesBySource)
        {
            if (battle is null || script != FirstSeviiBattleKey.For(battle)) throw new NotSupportedException("first_sevii_trainer_battle_key");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattles.TryGetValue(FirstSeviiBattleKey.For(battle), out var expected) ||
            CampaignSourceFingerprint.Compute(battle) != expected)
            throw new NotSupportedException("first_sevii_trainer_battle_source");
    }
}
