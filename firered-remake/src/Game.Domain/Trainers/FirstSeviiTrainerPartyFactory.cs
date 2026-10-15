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
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_AROMA_LADY_NIKKI")] = new("NIKKI",129,1u,false,[200, 195, 197, 197, 195],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BELLSPROUT",69,"BELLSPROUT",37,0,[188, 191, 198, 198, 205, 202, 204, 201, 207, 206],["pret:MOVE_STUN_SPORE", "pret:MOVE_ACID", "pret:MOVE_SWEET_SCENT", "pret:MOVE_RAZOR_LEAF"]),new("pret:SPECIES_WEEPINBELL",70,"WEEPINBELL",37,0,[209, 191, 191, 202, 195, 200, 188, 191, 198, 198],["pret:MOVE_POISON_POWDER", "pret:MOVE_STUN_SPORE", "pret:MOVE_ACID", "pret:MOVE_SWEET_SCENT"])]),
        [new("pret:TRAINER_AROMA_LADY_VIOLET")] = new("VIOLET",129,1u,false,[208, 195, 201, 198, 191, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BULBASAUR",1,"BULBASAUR",36,0,[188, 207, 198, 188, 187, 205, 187, 207, 204],["pret:MOVE_SLEEP_POWDER", "pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT", "pret:MOVE_GROWTH"]),new("pret:SPECIES_IVYSAUR",2,"IVYSAUR",36,0,[195, 208, 211, 205, 187, 207, 204],["pret:MOVE_POISON_POWDER", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT"]),new("pret:SPECIES_IVYSAUR",2,"IVYSAUR",36,0,[195, 208, 211, 205, 187, 207, 204],["pret:MOVE_POISON_POWDER", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_RAZOR_LEAF", "pret:MOVE_SWEET_SCENT"])]),
        [new("pret:TRAINER_BIKER_GOON")] = new("GOON",0,1u,false,[193, 201, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KOFFING",109,"KOFFING",37,0,[197, 201, 192, 192, 195, 200, 193],["pret:MOVE_HAZE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SLUDGE", "pret:MOVE_TACKLE"]),new("pret:SPECIES_GRIMER",88,"GRIMER",37,0,[193, 204, 195, 199, 191, 204],["pret:MOVE_ACID_ARMOR", "pret:MOVE_SCREECH", "pret:MOVE_MINIMIZE", "pret:MOVE_SLUDGE"])]),
        [new("pret:TRAINER_BIKER_GOON_2")] = new("GOON",0,1u,false,[193, 201, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KOFFING",109,"KOFFING",38,0,[197, 201, 192, 192, 195, 200, 193],["pret:MOVE_HAZE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SLUDGE", "pret:MOVE_TACKLE"])]),
        [new("pret:TRAINER_BIKER_GOON_3")] = new("GOON",0,1u,false,[193, 201, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GRIMER",88,"GRIMER",38,0,[193, 204, 195, 199, 191, 204],["pret:MOVE_SLUDGE", "pret:MOVE_MINIMIZE", "pret:MOVE_SCREECH", "pret:MOVE_ACID_ARMOR"])]),
        [new("pret:TRAINER_CUE_BALL_PAXTON")] = new("PAXTON",0,1u,false,[202, 187, 210, 206, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_WEEZING",110,"WEEZING",39,50,[209, 191, 191, 212, 195, 200, 193],["pret:MOVE_SELF_DESTRUCT", "pret:MOVE_SLUDGE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_HAZE"]),new("pret:SPECIES_MUK",89,"MUK",39,50,[199, 207, 197],["pret:MOVE_SLUDGE", "pret:MOVE_MINIMIZE", "pret:MOVE_SCREECH", "pret:MOVE_ACID_ARMOR"])]),
        [new("pret:TRAINER_SWIMMER_FEMALE_TISHA")] = new("TISHA",129,1u,false,[206, 195, 205, 194, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KINGLER",99,"KINGLER",38,0,[197, 195, 200, 193, 198, 191, 204],["pret:MOVE_HARDEN", "pret:MOVE_MUD_SHOT", "pret:MOVE_STOMP", "pret:MOVE_GUILLOTINE"])]),
        [new("pret:TRAINER_TUBER_ALEXIS")] = new("ALEXIS",2,1u,false,[187, 198, 191, 210, 195, 205],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARYU",120,"STARYU",34,0,[205, 206, 187, 204, 211, 207],["pret:MOVE_CAMOUFLAGE", "pret:MOVE_SWIFT", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_MINIMIZE"]),new("pret:SPECIES_STARYU",120,"STARYU",34,0,[205, 206, 187, 204, 211, 207],["pret:MOVE_CAMOUFLAGE", "pret:MOVE_SWIFT", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_MINIMIZE"]),new("pret:SPECIES_KRABBY",98,"KRABBY",34,0,[197, 204, 187, 188, 188, 211],["pret:MOVE_HARDEN", "pret:MOVE_MUD_SHOT", "pret:MOVE_STOMP", "pret:MOVE_GUILLOTINE"]),new("pret:SPECIES_KRABBY",98,"KRABBY",34,0,[197, 204, 187, 188, 188, 211],["pret:MOVE_HARDEN", "pret:MOVE_MUD_SHOT", "pret:MOVE_STOMP", "pret:MOVE_GUILLOTINE"])]),
        [new("pret:TRAINER_TUBER_AMIRA")] = new("AMIRA",2,1u,false,[187, 199, 195, 204, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_POLIWAG",60,"POLIWAG",34,0,[202, 201, 198, 195, 209, 187, 193],["pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BODY_SLAM"]),new("pret:SPECIES_POLIWHIRL",61,"POLIWHIRL",35,0,[202, 201, 198, 195, 209, 194, 195, 204, 198],["pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BODY_SLAM"]),new("pret:SPECIES_POLIWAG",60,"POLIWAG",34,0,[202, 201, 198, 195, 209, 187, 193],["pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_BODY_SLAM"])]),
        [new("pret:TRAINER_TWINS_JOY_MEG")] = new("JOY & MEG",9,1u,true,[196, 201, 211, 0, 45, 0, 199, 191, 193],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",37,0,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_DEFENSE_CURL", "pret:MOVE_METRONOME", "pret:MOVE_COSMIC_POWER", "pret:MOVE_MOONLIGHT"]),new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",37,0,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_DEFENSE_CURL", "pret:MOVE_METRONOME", "pret:MOVE_COSMIC_POWER", "pret:MOVE_MOONLIGHT"])]),
    }.ToImmutableDictionary();
    private static readonly ImmutableHashSet<FirstSeviiBattleKey> ExpectedBattleKeys = ImmutableHashSet.CreateRange<FirstSeviiBattleKey>([
        new("ThreeIsland_BondBridge_EventScript_Alexis",2174),
        new("ThreeIsland_BondBridge_EventScript_Amira",2162),
        new("ThreeIsland_BondBridge_EventScript_Joy",2198),
        new("ThreeIsland_BondBridge_EventScript_Meg",2210),
        new("ThreeIsland_BondBridge_EventScript_Nikki",2138),
        new("ThreeIsland_BondBridge_EventScript_Tisha",2186),
        new("ThreeIsland_BondBridge_EventScript_Violet",2150),
        new("ThreeIsland_EventScript_BattleBikersScene",211),
        new("ThreeIsland_EventScript_BattleBikersScene",221),
        new("ThreeIsland_EventScript_BattleBikersScene",231),
        new("ThreeIsland_EventScript_BattleBikersScene",248)]);
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
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
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
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
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
        if (battle is null || !ExpectedBattleKeys.Contains(FirstSeviiBattleKey.For(battle)))
            throw new NotSupportedException("first_sevii_trainer_battle_source");
    }
}
