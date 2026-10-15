using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Exact initial Ruby-region teams; shared source creation retains held items and double PID.</summary>
public static class SeviiRubyTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-sevii-ruby-trainers-v1";
    private static readonly ImmutableHashSet<TrainerId> TrainerIds = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_BLACK_BELT_HUGH"),
        new("pret:TRAINER_BLACK_BELT_SHEA"),
        new("pret:TRAINER_CAMPER_BRYCE"),
        new("pret:TRAINER_CRUSH_GIRL_JOCELYN"),
        new("pret:TRAINER_CRUSH_GIRL_SHARON"),
        new("pret:TRAINER_CRUSH_GIRL_TANYA"),
        new("pret:TRAINER_CRUSH_KIN_MIK_KIA"),
        new("pret:TRAINER_FISHERMAN_TOMMY"),
        new("pret:TRAINER_PICNICKER_CLAIRE"),
        new("pret:TRAINER_PKMN_RANGER_BETH"),
        new("pret:TRAINER_PKMN_RANGER_LOGAN"),
        new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL"),
        new("pret:TRAINER_SWIMMER_FEMALE_MARIA"),
        new("pret:TRAINER_SWIMMER_MALE_FINN"),
        new("pret:TRAINER_SWIMMER_MALE_GARRETT"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_43"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")]);
    private static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "MtEmber_Exterior_EventScript_BattleGrunt1",
        "MtEmber_Exterior_EventScript_BattleGrunt2",
        "MtEmber_Exterior_EventScript_Beth",
        "MtEmber_Exterior_EventScript_Jocelyn",
        "MtEmber_Exterior_EventScript_Logan",
        "OneIsland_KindleRoad_EventScript_Abigail",
        "OneIsland_KindleRoad_EventScript_Bryce",
        "OneIsland_KindleRoad_EventScript_Claire",
        "OneIsland_KindleRoad_EventScript_Finn",
        "OneIsland_KindleRoad_EventScript_Garrett",
        "OneIsland_KindleRoad_EventScript_Hugh",
        "OneIsland_KindleRoad_EventScript_Kia",
        "OneIsland_KindleRoad_EventScript_Maria",
        "OneIsland_KindleRoad_EventScript_Mik",
        "OneIsland_KindleRoad_EventScript_Sharon",
        "OneIsland_KindleRoad_EventScript_Shea",
        "OneIsland_KindleRoad_EventScript_Tanya",
        "OneIsland_KindleRoad_EventScript_Tommy");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [new("pret:TRAINER_BLACK_BELT_HUGH"),new("pret:TRAINER_BLACK_BELT_SHEA"),new("pret:TRAINER_CAMPER_BRYCE"),new("pret:TRAINER_CRUSH_GIRL_JOCELYN"),new("pret:TRAINER_CRUSH_GIRL_SHARON"),new("pret:TRAINER_CRUSH_GIRL_TANYA"),new("pret:TRAINER_CRUSH_KIN_MIK_KIA"),new("pret:TRAINER_FISHERMAN_TOMMY"),new("pret:TRAINER_PICNICKER_CLAIRE"),new("pret:TRAINER_PKMN_RANGER_BETH"),new("pret:TRAINER_PKMN_RANGER_LOGAN"),new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL"),new("pret:TRAINER_SWIMMER_FEMALE_MARIA"),new("pret:TRAINER_SWIMMER_MALE_FINN"),new("pret:TRAINER_SWIMMER_MALE_GARRETT"),new("pret:TRAINER_TEAM_ROCKET_GRUNT_43"),new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")];
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BLACK_BELT_HUGH")] = new("HUGH",4,1u,false,[194, 207, 193, 194],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOP",66,"MACHOP",37,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",37,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT")],888),
        [new("pret:TRAINER_BLACK_BELT_SHEA")] = new("SHEA",4,1u,false,[205, 194, 191, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOP",66,"MACHOP",38,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",38,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT")],912),
        [new("pret:TRAINER_CAMPER_BRYCE")] = new("BRYCE",0,1u,false,[188, 204, 211, 189, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_NIDORINO",33,"NIDORINO",36,0,[200, 195, 190, 201, 204, 195, 200, 201],["pret:MOVE_POISON_STING", "pret:MOVE_HORN_ATTACK", "pret:MOVE_HELPING_HAND", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_RATICATE",20,"RATICATE",36,0,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_QUICK_ATTACK", "pret:MOVE_HYPER_FANG", "pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT"],"ITEM_NONE"),new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",36,0,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_SLASH", "pret:MOVE_SWIFT"],"ITEM_NONE")],720),
        [new("pret:TRAINER_CRUSH_GIRL_JOCELYN")] = new("JOCELYN",4,1u,false,[196, 201, 189, 191, 198, 211, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",38,50,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_ICE_PUNCH", "pret:MOVE_FIRE_PUNCH", "pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MEGA_PUNCH"],"ITEM_BLACK_BELT"),new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",38,50,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_ICE_PUNCH", "pret:MOVE_FIRE_PUNCH", "pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MEGA_PUNCH"],"ITEM_BLACK_BELT")],912),
        [new("pret:TRAINER_CRUSH_GIRL_SHARON")] = new("SHARON",4,1u,false,[205, 194, 187, 204, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MANKEY",56,"MANKEY",37,50,[199, 187, 200, 197, 191, 211],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_SEISMIC_TOSS", "pret:MOVE_CROSS_CHOP", "pret:MOVE_SWAGGER"],"ITEM_BLACK_BELT"),new("pret:SPECIES_PRIMEAPE",57,"PRIMEAPE",37,50,[202, 204, 195, 199, 191, 187, 202, 191],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_SEISMIC_TOSS", "pret:MOVE_RAGE", "pret:MOVE_CROSS_CHOP"],"ITEM_BLACK_BELT")],888),
        [new("pret:TRAINER_CRUSH_GIRL_TANYA")] = new("TANYA",4,1u,false,[206, 187, 200, 211, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HITMONLEE",106,"HITMONLEE",38,50,[194, 195, 206, 199, 201, 200, 198, 191, 191],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_HI_JUMP_KICK", "pret:MOVE_MIND_READER", "pret:MOVE_FORESIGHT"],"ITEM_BLACK_BELT"),new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",38,50,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_ICE_PUNCH", "pret:MOVE_FIRE_PUNCH", "pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MEGA_PUNCH"],"ITEM_BLACK_BELT")],912),
        [new("pret:TRAINER_CRUSH_KIN_MIK_KIA")] = new("MIK & KIA",4,1u,true,[199, 195, 197, 0, 45, 0, 197, 195, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOKE",67,"MACHOKE",39,50,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT"),new("pret:SPECIES_PRIMEAPE",57,"PRIMEAPE",39,50,[202, 204, 195, 199, 191, 187, 202, 191],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_SEISMIC_TOSS", "pret:MOVE_RAGE", "pret:MOVE_CROSS_CHOP"],"ITEM_BLACK_BELT")],1872),
        [new("pret:TRAINER_FISHERMAN_TOMMY")] = new("TOMMY",11,1u,false,[206, 201, 199, 199, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",33,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",33,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_SEAKING",119,"SEAKING",35,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_SEAKING",119,"SEAKING",35,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_SEAKING",119,"SEAKING",35,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE")],1260),
        [new("pret:TRAINER_PICNICKER_CLAIRE")] = new("CLAIRE",130,1u,false,[189, 198, 187, 195, 204, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MEOWTH",52,"MEOWTH",35,0,[199, 191, 201, 209, 206, 194],["pret:MOVE_BITE", "pret:MOVE_PAY_DAY", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_SCREECH"],"ITEM_NONE"),new("pret:SPECIES_MEOWTH",52,"MEOWTH",35,0,[199, 191, 201, 209, 206, 194],["pret:MOVE_BITE", "pret:MOVE_PAY_DAY", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_SCREECH"],"ITEM_NONE"),new("pret:SPECIES_PIKACHU",25,"PIKACHU",35,0,[202, 195, 197, 187, 189, 194, 207],["pret:MOVE_DOUBLE_TEAM", "pret:MOVE_SLAM", "pret:MOVE_THUNDERBOLT", "pret:MOVE_AGILITY"],"ITEM_NONE"),new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",35,0,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_MINIMIZE", "pret:MOVE_DEFENSE_CURL", "pret:MOVE_METRONOME", "pret:MOVE_COSMIC_POWER"],"ITEM_NONE")],700),
        [new("pret:TRAINER_PKMN_RANGER_BETH")] = new("BETH",133,7u,false,[188, 191, 206, 194],["ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BELLSPROUT",69,"BELLSPROUT",38,100,[188, 191, 198, 198, 205, 202, 204, 201, 207, 206],["pret:MOVE_STUN_SPORE", "pret:MOVE_ACID", "pret:MOVE_SWEET_SCENT", "pret:MOVE_RAZOR_LEAF"],"ITEM_NONE"),new("pret:SPECIES_GLOOM",44,"GLOOM",38,100,[193, 198, 201, 201, 199],["pret:MOVE_STUN_SPORE", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_ACID", "pret:MOVE_MOONLIGHT"],"ITEM_NONE"),new("pret:SPECIES_GLOOM",44,"GLOOM",38,100,[193, 198, 201, 201, 199],["pret:MOVE_STUN_SPORE", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_ACID", "pret:MOVE_MOONLIGHT"],"ITEM_NONE")],1368),
        [new("pret:TRAINER_PKMN_RANGER_LOGAN")] = new("LOGAN",5,7u,false,[198, 201, 193, 187, 200],["ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_EXEGGCUTE",102,"EXEGGCUTE",37,100,[191, 210, 191, 193, 193, 189, 207, 206, 191],["pret:MOVE_SLEEP_POWDER", "pret:MOVE_POISON_POWDER", "pret:MOVE_STUN_SPORE", "pret:MOVE_CONFUSION"],"ITEM_NONE"),new("pret:SPECIES_EXEGGUTOR",103,"EXEGGUTOR",40,100,[191, 210, 191, 193, 193, 207, 206, 201, 204],["pret:MOVE_EGG_BOMB", "pret:MOVE_STOMP", "pret:MOVE_CONFUSION", "pret:MOVE_HYPNOSIS"],"ITEM_NONE")],1440),
        [new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL")] = new("ABIGAIL",129,1u,false,[187, 188, 195, 193, 187, 195, 198],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PSYDUCK",54,"PSYDUCK",35,0,[202, 205, 211, 190, 207, 189, 197],["pret:MOVE_DISABLE", "pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP"],"ITEM_NONE"),new("pret:SPECIES_PSYDUCK",54,"PSYDUCK",36,0,[202, 205, 211, 190, 207, 189, 197],["pret:MOVE_DISABLE", "pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP"],"ITEM_NONE"),new("pret:SPECIES_GOLDUCK",55,"GOLDUCK",37,0,[193, 201, 198, 190, 207, 189, 197],["pret:MOVE_DISABLE", "pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP"],"ITEM_NONE")],148),
        [new("pret:TRAINER_SWIMMER_FEMALE_MARIA")] = new("MARIA",129,1u,false,[199, 187, 204, 195, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEADRA",117,"SEADRA",37,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"],"ITEM_NONE"),new("pret:SPECIES_SEADRA",117,"SEADRA",37,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"],"ITEM_NONE")],148),
        [new("pret:TRAINER_SWIMMER_MALE_FINN")] = new("FINN",8,1u,false,[192, 195, 200, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARMIE",121,"STARMIE",38,0,[205, 206, 187, 204, 199, 195, 191],["pret:MOVE_RAPID_SPIN", "pret:MOVE_RECOVER", "pret:MOVE_SWIFT", "pret:MOVE_CONFUSE_RAY"],"ITEM_NONE")],152),
        [new("pret:TRAINER_SWIMMER_MALE_GARRETT")] = new("GARRETT",8,1u,false,[193, 187, 204, 204, 191, 206, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SHELLDER",90,"SHELLDER",35,0,[205, 194, 191, 198, 198, 190, 191, 204],["pret:MOVE_ICICLE_SPEAR", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"],"ITEM_NONE"),new("pret:SPECIES_CLOYSTER",91,"CLOYSTER",35,0,[189, 198, 201, 211, 205, 206, 191, 204],["pret:MOVE_WITHDRAW", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"],"ITEM_NONE"),new("pret:SPECIES_WARTORTLE",8,"WARTORTLE",38,0,[209, 187, 204, 206, 201, 204, 206, 198, 191],["pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN", "pret:MOVE_PROTECT", "pret:MOVE_RAIN_DANCE"],"ITEM_NONE")],152),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_43")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CUBONE",104,"CUBONE",37,0,[189, 207, 188, 201, 200, 191],["pret:MOVE_BONEMERANG", "pret:MOVE_RAGE", "pret:MOVE_FALSE_SWIPE", "pret:MOVE_THRASH"],"ITEM_NONE"),new("pret:SPECIES_MAROWAK",105,"MAROWAK",37,0,[199, 187, 204, 201, 209, 187, 197],["pret:MOVE_LEER", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_BONEMERANG", "pret:MOVE_RAGE"],"ITEM_NONE")],1184),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RATTATA",19,"RATTATA",35,0,[204, 187, 206, 206, 187, 206, 187],["pret:MOVE_HYPER_FANG", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_PURSUIT", "pret:MOVE_SUPER_FANG"],"ITEM_NONE"),new("pret:SPECIES_RATICATE",20,"RATICATE",35,0,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_QUICK_ATTACK", "pret:MOVE_HYPER_FANG", "pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT"],"ITEM_NONE"),new("pret:SPECIES_SANDSHREW",27,"SANDSHREW",35,0,[205, 187, 200, 190, 205, 194, 204, 191, 209],["pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_SLASH", "pret:MOVE_SWIFT"],"ITEM_NONE"),new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",35,0,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_SLASH", "pret:MOVE_SWIFT"],"ITEM_NONE")],1120),
    }.ToImmutableDictionary();
    public static TrainerId DoubleTrainer { get; } = new("pret:TRAINER_CRUSH_KIN_MIK_KIA");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(id => id != DoubleTrainer).ToImmutableArray();

    public static int BasePrize(SeviiRubyTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("sevii_ruby_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }

    public static TrainerPartyCreationResult Create(SeviiRubyTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("sevii_ruby_trainer_catalog");
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("sevii_ruby_trainer_species");
            if (member.CustomMoves.Any(m => !opponents.Mechanics.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("sevii_ruby_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
        // Party flag bit0 controls custom moves independently of bit1 held items.
        // Logan has original custom moves; all six Black Belt teams use natural moves.
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                opponents.Mechanics.Catalog.Moves[m].Pp, opponents.Mechanics.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }

    public static void ValidateCatalog(SeviiRubyTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 17 || catalog.BattlesByScript.Count != 18)
            throw new NotSupportedException("sevii_ruby_trainer_scope");
        foreach (var id in TrainerIds)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault ||
                    m.EncodedSpeciesName.IsDefaultOrEmpty) || trainer.Items.IsDefault ||
                trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("sevii_ruby_trainer_source");
        }
        foreach (var script in BattleScripts)
        {
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script)
                throw new NotSupportedException("sevii_ruby_trainer_battle_source");
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("sevii_ruby_trainer_battle_source");
    }
}
