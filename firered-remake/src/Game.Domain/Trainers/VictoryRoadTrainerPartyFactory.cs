using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned twelve-team, 45-member Victory Road roster. Default moves and held items are independent bits.</summary>
public static class VictoryRoadTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-victory-road-trainers-v1";
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party,
        int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BLACK_BELT_DAISUKE")] = new("DAISUKE",4,1u,false,[190, 187, 195, 205, 207, 197, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOKE",67,"MACHOKE",43,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOP",66,"MACHOP",43,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP", "pret:MOVE_SCARY_FACE"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",43,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT")],1032),
        [new("pret:TRAINER_COOLTRAINER_ALEXA")] = new("ALEXA",133,7u,false,[187, 198, 191, 210, 187],["ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",42,100,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_LIGHT_SCREEN", "pret:MOVE_COSMIC_POWER", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_ENCORE"],"ITEM_NONE"),new("pret:SPECIES_JIGGLYPUFF",39,"JIGGLYPUFF",42,100,[196, 195, 193, 193, 198, 211, 202, 207, 192, 192],["pret:MOVE_SING", "pret:MOVE_BODY_SLAM", "pret:MOVE_ROLLOUT", "pret:MOVE_DISABLE"],"ITEM_NONE"),new("pret:SPECIES_PERSIAN",53,"PERSIAN",42,100,[202, 191, 204, 205, 195, 187, 200],["pret:MOVE_PAY_DAY", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_BITE", "pret:MOVE_SCREECH"],"ITEM_NONE"),new("pret:SPECIES_DEWGONG",87,"DEWGONG",42,100,[190, 191, 209, 193, 201, 200, 193],["pret:MOVE_SHEER_COLD", "pret:MOVE_TAKE_DOWN", "pret:MOVE_REST", "pret:MOVE_AURORA_BEAM"],"ITEM_NONE"),new("pret:SPECIES_CHANSEY",113,"CHANSEY",42,100,[189, 194, 187, 200, 205, 191, 211],["pret:MOVE_SING", "pret:MOVE_EGG_BOMB", "pret:MOVE_SOFT_BOILED", "pret:MOVE_MINIMIZE"],"ITEM_NONE")],1512),
        [new("pret:TRAINER_COOLTRAINER_CAROLINE")] = new("CAROLINE",133,7u,false,[189, 187, 204, 201, 198, 195, 200, 191],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BELLSPROUT",69,"BELLSPROUT",42,100,[188, 191, 198, 198, 205, 202, 204, 201, 207, 206],["pret:MOVE_RAZOR_LEAF", "pret:MOVE_ACID", "pret:MOVE_STUN_SPORE", "pret:MOVE_POISON_POWDER"],"ITEM_NONE"),new("pret:SPECIES_WEEPINBELL",70,"WEEPINBELL",42,100,[209, 191, 191, 202, 195, 200, 188, 191, 198, 198],["pret:MOVE_RAZOR_LEAF", "pret:MOVE_ACID", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_POISON_POWDER"],"ITEM_NONE"),new("pret:SPECIES_VICTREEBEL",71,"VICTREEBEL",42,100,[208, 195, 189, 206, 204, 191, 191, 188, 191, 198],["pret:MOVE_RAZOR_LEAF", "pret:MOVE_ACID", "pret:MOVE_STUN_SPORE", "pret:MOVE_SLEEP_POWDER"],"ITEM_NONE"),new("pret:SPECIES_PARAS",46,"PARAS",42,100,[202, 187, 204, 187, 205],["pret:MOVE_GROWTH", "pret:MOVE_SLASH", "pret:MOVE_LEECH_LIFE", "pret:MOVE_STUN_SPORE"],"ITEM_NONE"),new("pret:SPECIES_PARASECT",47,"PARASECT",42,100,[202, 187, 204, 187, 205, 191, 189, 206],["pret:MOVE_SPORE", "pret:MOVE_SLASH", "pret:MOVE_LEECH_LIFE", "pret:MOVE_POISON_POWDER"],"ITEM_NONE")],1512),
        [new("pret:TRAINER_COOLTRAINER_COLBY")] = new("COLBY",5,7u,false,[189, 201, 198, 188, 211],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KINGLER",99,"KINGLER",41,100,[197, 195, 200, 193, 198, 191, 204],["pret:MOVE_GUILLOTINE", "pret:MOVE_STOMP", "pret:MOVE_MUD_SHOT", "pret:MOVE_BUBBLE"],"ITEM_NONE"),new("pret:SPECIES_POLIWHIRL",61,"POLIWHIRL",42,100,[202, 201, 198, 195, 209, 194, 195, 204, 198],["pret:MOVE_BODY_SLAM", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_WATER_GUN", "pret:MOVE_HYPNOSIS"],"ITEM_NONE"),new("pret:SPECIES_TENTACRUEL",73,"TENTACRUEL",42,100,[206, 191, 200, 206, 187, 189, 204, 207, 191, 198],["pret:MOVE_BARRIER", "pret:MOVE_WRAP", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_ACID"],"ITEM_NONE"),new("pret:SPECIES_SEADRA",117,"SEADRA",42,100,[205, 191, 187, 190, 204, 187],["pret:MOVE_WATER_GUN", "pret:MOVE_SMOKESCREEN", "pret:MOVE_TWISTER", "pret:MOVE_LEER"],"ITEM_NONE"),new("pret:SPECIES_BLASTOISE",9,"BLASTOISE",43,100,[188, 198, 187, 205, 206, 201, 195, 205, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN", "pret:MOVE_RAIN_DANCE"],"ITEM_NONE")],1548),
        [new("pret:TRAINER_COOLTRAINER_GEORGE")] = new("GEORGE",5,7u,false,[193, 191, 201, 204, 193, 191],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_EXEGGUTOR",103,"EXEGGUTOR",42,100,[191, 210, 191, 193, 193, 207, 206, 201, 204],["pret:MOVE_EGG_BOMB", "pret:MOVE_CONFUSION", "pret:MOVE_STUN_SPORE", "pret:MOVE_SLEEP_POWDER"],"ITEM_NONE"),new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",42,100,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_FURY_SWIPES", "pret:MOVE_SWIFT", "pret:MOVE_POISON_STING", "pret:MOVE_SAND_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_CLOYSTER",91,"CLOYSTER",42,100,[189, 198, 201, 211, 205, 206, 191, 204],["pret:MOVE_SPIKE_CANNON", "pret:MOVE_SPIKES", "pret:MOVE_AURORA_BEAM", "pret:MOVE_SUPERSONIC"],"ITEM_NONE"),new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",42,100,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SPARK", "pret:MOVE_SONIC_BOOM", "pret:MOVE_SCREECH", "pret:MOVE_LIGHT_SCREEN"],"ITEM_NONE"),new("pret:SPECIES_ARCANINE",59,"ARCANINE",42,100,[187, 204, 189, 187, 200, 195, 200, 191],["pret:MOVE_FLAME_WHEEL", "pret:MOVE_ROAR", "pret:MOVE_BITE", "pret:MOVE_TAKE_DOWN"],"ITEM_NONE")],1512),
        [new("pret:TRAINER_COOLTRAINER_NAOMI")] = new("NAOMI",133,7u,false,[200, 187, 201, 199, 195],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PERSIAN",53,"PERSIAN",42,100,[202, 191, 204, 205, 195, 187, 200],["pret:MOVE_FURY_SWIPES", "pret:MOVE_SCREECH", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_PAY_DAY"],"ITEM_NONE"),new("pret:SPECIES_PONYTA",77,"PONYTA",42,100,[202, 201, 200, 211, 206, 187],["pret:MOVE_AGILITY", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FIRE_SPIN", "pret:MOVE_STOMP"],"ITEM_NONE"),new("pret:SPECIES_RAPIDASH",78,"RAPIDASH",42,100,[204, 187, 202, 195, 190, 187, 205, 194],["pret:MOVE_FURY_ATTACK", "pret:MOVE_FIRE_SPIN", "pret:MOVE_STOMP", "pret:MOVE_GROWL"],"ITEM_NONE"),new("pret:SPECIES_VULPIX",37,"VULPIX",42,100,[208, 207, 198, 202, 195, 210],["pret:MOVE_FLAMETHROWER", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_IMPRISON"],"ITEM_NONE"),new("pret:SPECIES_NINETALES",38,"NINETALES",42,100,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_SAFEGUARD", "pret:MOVE_WILL_O_WISP", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_FIRE_SPIN"],"ITEM_NONE")],1512),
        [new("pret:TRAINER_COOLTRAINER_ROLANDO")] = new("ROLANDO",5,7u,false,[204, 201, 198, 187, 200, 190, 201],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RATICATE",20,"RATICATE",42,100,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_SUPER_FANG", "pret:MOVE_PURSUIT", "pret:MOVE_SCARY_FACE", "pret:MOVE_QUICK_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_IVYSAUR",2,"IVYSAUR",42,100,[195, 208, 211, 205, 187, 207, 204],["pret:MOVE_RAZOR_LEAF", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_SWEET_SCENT", "pret:MOVE_SYNTHESIS"],"ITEM_NONE"),new("pret:SPECIES_WARTORTLE",8,"WARTORTLE",42,100,[209, 187, 204, 206, 201, 204, 206, 198, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN", "pret:MOVE_TAIL_WHIP"],"ITEM_NONE"),new("pret:SPECIES_CHARMELEON",5,"CHARMELEON",42,100,[189, 194, 187, 204, 199, 191, 198, 191, 201, 200],["pret:MOVE_FLAMETHROWER", "pret:MOVE_SLASH", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),new("pret:SPECIES_CHARIZARD",6,"CHARIZARD",42,100,[189, 194, 187, 204, 195, 212, 187, 204, 190],["pret:MOVE_FLAMETHROWER", "pret:MOVE_WING_ATTACK", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE"],"ITEM_NONE")],1512),
        [new("pret:TRAINER_COOL_COUPLE_RAY_TYRA")] = new("RAY & TYRA",5,7u,true,[204, 187, 211, 0, 45, 0, 206, 211, 204, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_NIDOQUEEN",31,"NIDOQUEEN",45,100,[200, 195, 190, 201, 203, 207, 191, 191, 200],["pret:MOVE_SUPERPOWER", "pret:MOVE_BODY_SLAM", "pret:MOVE_DOUBLE_KICK", "pret:MOVE_POISON_STING"],"ITEM_NONE"),new("pret:SPECIES_NIDOKING",34,"NIDOKING",45,100,[200, 195, 190, 201, 197, 195, 200, 193],["pret:MOVE_MEGAHORN", "pret:MOVE_THRASH", "pret:MOVE_DOUBLE_KICK", "pret:MOVE_POISON_STING"],"ITEM_NONE")],2160),
        [new("pret:TRAINER_JUGGLER_GREGORY")] = new("GREGORY",11,1u,false,[193, 204, 191, 193, 201, 204, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MR_MIME",122,"MR. MIME",48,0,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_PSYCHIC", "pret:MOVE_ROLE_PLAY", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_ENCORE"],"ITEM_NONE")],1920),
        [new("pret:TRAINER_JUGGLER_NELSON")] = new("NELSON",11,1u,false,[200, 191, 198, 205, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_DROWZEE",96,"DROWZEE",41,0,[190, 204, 201, 209, 212, 191, 191],["pret:MOVE_MEDITATE", "pret:MOVE_PSYCHIC", "pret:MOVE_PSYCH_UP", "pret:MOVE_SWAGGER"],"ITEM_NONE"),new("pret:SPECIES_HYPNO",97,"HYPNO",41,0,[194, 211, 202, 200, 201],["pret:MOVE_HEADBUTT", "pret:MOVE_POISON_GAS", "pret:MOVE_MEDITATE", "pret:MOVE_PSYCHIC"],"ITEM_NONE"),new("pret:SPECIES_KADABRA",64,"KADABRA",41,0,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_ROLE_PLAY", "pret:MOVE_PSYCHIC"],"ITEM_NONE"),new("pret:SPECIES_KADABRA",64,"KADABRA",41,0,[197, 187, 190, 187, 188, 204, 187],["pret:MOVE_RECOVER", "pret:MOVE_FUTURE_SIGHT", "pret:MOVE_ROLE_PLAY", "pret:MOVE_PSYCHIC"],"ITEM_NONE")],1640),
        [new("pret:TRAINER_POKEMANIAC_DAWSON")] = new("DAWSON",3,1u,false,[190, 187, 209, 205, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CHARMELEON",5,"CHARMELEON",40,30,[189, 194, 187, 204, 199, 191, 198, 191, 201, 200],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"],"ITEM_NONE"),new("pret:SPECIES_LAPRAS",131,"LAPRAS",40,30,[198, 187, 202, 204, 187, 205],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_PERISH_SONG", "pret:MOVE_ICE_BEAM", "pret:MOVE_RAIN_DANCE"],"ITEM_NONE"),new("pret:SPECIES_LICKITUNG",108,"LICKITUNG",40,30,[198, 195, 189, 197, 195, 206, 207, 200, 193],["pret:MOVE_STOMP", "pret:MOVE_WRAP", "pret:MOVE_DISABLE", "pret:MOVE_SLAM"],"ITEM_NONE")],1920),
        [new("pret:TRAINER_TAMER_VINCENT")] = new("VINCENT",11,1u,false,[208, 195, 200, 189, 191, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PERSIAN",53,"PERSIAN",44,40,[202, 191, 204, 205, 195, 187, 200],["pret:MOVE_PAY_DAY", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_SCREECH", "pret:MOVE_FURY_SWIPES"],"ITEM_NONE"),new("pret:SPECIES_GOLDUCK",55,"GOLDUCK",44,40,[193, 201, 198, 190, 207, 189, 197],["pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP", "pret:MOVE_FURY_SWIPES"],"ITEM_NONE")],1760),
    }.ToImmutableDictionary();
    private static readonly ImmutableHashSet<string> ExpectedBattleKeys = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "VictoryRoad_1F_EventScript_Rolando",
        "VictoryRoad_1F_EventScript_Naomi",
        "VictoryRoad_2F_EventScript_Dawson",
        "VictoryRoad_2F_EventScript_Daisuke",
        "VictoryRoad_2F_EventScript_Nelson",
        "VictoryRoad_2F_EventScript_Vincent",
        "VictoryRoad_2F_EventScript_Gregory",
        "VictoryRoad_3F_EventScript_George",
        "VictoryRoad_3F_EventScript_Alexa",
        "VictoryRoad_3F_EventScript_Colby",
        "VictoryRoad_3F_EventScript_Caroline",
        "VictoryRoad_3F_EventScript_Ray",
        "VictoryRoad_3F_EventScript_Tyra");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = Expected.Keys.OrderBy(t=>t.Value,StringComparer.Ordinal).ToImmutableArray();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t=>!Expected[t].DoubleBattle).ToImmutableArray();
    public static int BasePrize(VictoryRoadTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!Expected.TryGetValue(id, out var expected)) throw new NotSupportedException("victory_road_trainer_id");
        return expected.BasePrize;
    }
    /// <summary>Preserves the existing Cinnabar-scoped Ray/Tyra construction and predecessor callers.</summary>
    public static TrainerPartyCreationResult Create(VictoryRoadTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        if (opponents is null)
            throw new NotSupportedException("victory_road_trainer_catalog");
        return CreateCore(trainers, opponents.Catalog, opponents.Mechanics, id, rng);
    }

    private static TrainerPartyCreationResult CreateCore(VictoryRoadTrainerCatalog trainers, PokemonCatalog opponents,
        SourceMechanicsCatalog source, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("victory_road_trainer_catalog");
        var expected = Expected[id];
        bool customMoves = (trainer.PartyFlags & 1) != 0;
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!opponents.Species.ContainsKey(member.Species)) throw new NotSupportedException("victory_road_trainer_species");
            var moves = expected.Party[i].Moves.Select(m => new MoveId(m));
            if (!customMoves && !PokemonMoveRules.InitialMovesCore(source.Catalog, source.Catalog.Species[member.Species], member.Level)
                .Select(m => m.Move).SequenceEqual(moves)) throw new NotSupportedException("victory_road_trainer_natural_moves");
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("victory_road_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // PartyFlags=2 has a held item but retains the NPC factory's level-up moves.
        if (!customMoves) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(VictoryRoadTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 12 || catalog.BattlesByScript.Count != 13)
            throw new NotSupportedException("victory_road_trainer_scope");
        foreach (var (id, expected) in Expected)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("victory_road_trainer_source");
        foreach (var (script, battle) in catalog.BattlesByScript)
        {
            if (battle is null || script != battle.ScriptEntry) throw new NotSupportedException("victory_road_trainer_battle_key");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattleKeys.Contains(battle.ScriptEntry))
            throw new NotSupportedException("victory_road_trainer_battle_source");
    }
}
