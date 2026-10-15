using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned 24-team, 59-member Cinnabar recipe; creation reuses the source NPC factory.</summary>
public static class CinnabarTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-cinnabar-trainers-v1";
    public static TrainerId Blaine { get; } = new("pret:TRAINER_LEADER_BLAINE");
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BURGLAR_ARNIE")] = new("ARNIE",3,1u,false,[187, 204, 200, 195, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CHARMANDER",4,"CHARMANDER",34,0,[189, 194, 187, 204, 199, 187, 200, 190, 191, 204],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"]),new("pret:SPECIES_CHARMELEON",5,"CHARMELEON",34,0,[189, 194, 187, 204, 199, 191, 198, 191, 201, 200],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"])]),
        [new("pret:TRAINER_BURGLAR_DUSTY")] = new("DUSTY",3,1u,false,[190, 207, 205, 206, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_VULPIX",37,"VULPIX",37,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD", "pret:MOVE_GRUDGE"]),new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",37,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL", "pret:MOVE_HELPING_HAND"])]),
        [new("pret:TRAINER_BURGLAR_LEWIS")] = new("LEWIS",3,1u,false,[198, 191, 209, 195, 205],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",34,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_LEER", "pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL"]),new("pret:SPECIES_PONYTA",77,"PONYTA",34,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_EMBER", "pret:MOVE_STOMP", "pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN"])]),
        [new("pret:TRAINER_BURGLAR_QUINN")] = new("QUINN",3,1u,false,[203, 207, 195, 200, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",36,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_LEER", "pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL"]),new("pret:SPECIES_VULPIX",37,"VULPIX",36,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_NINETALES",38,"NINETALES",36,0,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_EMBER", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SAFEGUARD"])]),
        [new("pret:TRAINER_BURGLAR_RAMON")] = new("RAMON",3,1u,false,[204, 187, 199, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PONYTA",77,"PONYTA",41,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_STOMP", "pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN", "pret:MOVE_AGILITY"])]),
        [new("pret:TRAINER_BURGLAR_SIMON")] = new("SIMON",3,1u,false,[205, 195, 199, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_NINETALES",38,"NINETALES",38,0,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_EMBER", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SAFEGUARD"])]),
        [new("pret:TRAINER_FISHERMAN_CLAUDE")] = new("CLAUDE",11,1u,false,[189, 198, 187, 207, 190, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SHELLDER",90,"SHELLDER",31,0,[205, 194, 191, 198, 198, 190, 191, 204],["pret:MOVE_ICICLE_SPEAR", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"]),new("pret:SPECIES_CLOYSTER",91,"CLOYSTER",31,0,[189, 198, 201, 211, 205, 206, 191, 204],["pret:MOVE_WITHDRAW", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"])]),
        [new("pret:TRAINER_FISHERMAN_NOLAN")] = new("NOLAN",11,1u,false,[200, 201, 198, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEAKING",119,"SEAKING",33,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"]),new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",33,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"])]),
        [new("pret:TRAINER_FISHERMAN_RONALD")] = new("RONALD",11,1u,false,[204, 201, 200, 187, 198, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEAKING",119,"SEAKING",28,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"]),new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",28,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"]),new("pret:SPECIES_SEAKING",119,"SEAKING",28,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"]),new("pret:SPECIES_SEAKING",119,"SEAKING",28,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"])]),
        [new("pret:TRAINER_FISHERMAN_WADE")] = new("WADE",11,1u,false,[209, 187, 190, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"])]),
        [new("pret:TRAINER_LEADER_BLAINE")] = new("BLAINE",0,7u,false,[188, 198, 187, 195, 200, 191],["ITEM_HYPER_POTION", "ITEM_HYPER_POTION", "ITEM_FULL_HEAL", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",42,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_BITE", "pret:MOVE_ROAR", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FIRE_BLAST"]),new("pret:SPECIES_PONYTA",77,"PONYTA",40,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_STOMP", "pret:MOVE_BOUNCE", "pret:MOVE_FIRE_SPIN", "pret:MOVE_FIRE_BLAST"]),new("pret:SPECIES_RAPIDASH",78,"RAPIDASH",42,0,[204, 187, 202, 195, 190, 187, 205, 194],["pret:MOVE_STOMP", "pret:MOVE_BOUNCE", "pret:MOVE_FIRE_SPIN", "pret:MOVE_FIRE_BLAST"]),new("pret:SPECIES_ARCANINE",59,"ARCANINE",47,0,[187, 204, 189, 187, 200, 195, 200, 191],["pret:MOVE_BITE", "pret:MOVE_ROAR", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FIRE_BLAST"])]),
        [new("pret:TRAINER_SCIENTIST_BRAYDON")] = new("BRAYDON",3,1u,false,[188, 204, 187, 211, 190, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAGNEMITE",81,"MAGNEMITE",33,0,[199, 187, 193, 200, 191, 199, 195, 206, 191],["pret:MOVE_SONIC_BOOM", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SPARK", "pret:MOVE_LOCK_ON"]),new("pret:SPECIES_MAGNETON",82,"MAGNETON",33,0,[199, 187, 193, 200, 191, 206, 201, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_SONIC_BOOM", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SPARK"]),new("pret:SPECIES_VOLTORB",100,"VOLTORB",33,0,[208, 201, 198, 206, 201, 204, 188],["pret:MOVE_SONIC_BOOM", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT"])]),
        [new("pret:TRAINER_SCIENTIST_IVAN")] = new("IVAN",3,1u,false,[195, 208, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAGNEMITE",81,"MAGNEMITE",34,0,[199, 187, 193, 200, 191, 199, 195, 206, 191],["pret:MOVE_SONIC_BOOM", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SPARK", "pret:MOVE_LOCK_ON"]),new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",34,0,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SONIC_BOOM", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT"])]),
        [new("pret:TRAINER_SCIENTIST_TED")] = new("TED",3,1u,false,[206, 191, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",29,0,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SCREECH", "pret:MOVE_SONIC_BOOM", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT"]),new("pret:SPECIES_WEEZING",110,"WEEZING",29,0,[209, 191, 191, 212, 195, 200, 193],["pret:MOVE_SMOG", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_SLUDGE", "pret:MOVE_SMOKESCREEN"])]),
        [new("pret:TRAINER_SIS_AND_BRO_LIL_IAN")] = new("LIL & IAN",8,1u,true,[198, 195, 198, 0, 45, 0, 195, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEADRA",117,"SEADRA",33,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"]),new("pret:SPECIES_STARMIE",121,"STARMIE",33,0,[205, 206, 187, 204, 199, 195, 191],["pret:MOVE_RAPID_SPIN", "pret:MOVE_RECOVER", "pret:MOVE_SWIFT", "pret:MOVE_CONFUSE_RAY"])]),
        [new("pret:TRAINER_SUPER_NERD_AVERY")] = new("AVERY",3,1u,false,[187, 208, 191, 204, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PONYTA",77,"PONYTA",34,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_EMBER", "pret:MOVE_STOMP", "pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN"]),new("pret:SPECIES_CHARMANDER",4,"CHARMANDER",34,0,[189, 194, 187, 204, 199, 187, 200, 190, 191, 204],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"]),new("pret:SPECIES_VULPIX",37,"VULPIX",34,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",34,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_LEER", "pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL"])]),
        [new("pret:TRAINER_SUPER_NERD_DEREK")] = new("DEREK",3,1u,false,[190, 191, 204, 191, 197],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RAPIDASH",78,"RAPIDASH",41,0,[204, 187, 202, 195, 190, 187, 205, 194],["pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN", "pret:MOVE_AGILITY", "pret:MOVE_FURY_ATTACK"])]),
        [new("pret:TRAINER_SUPER_NERD_ERIK")] = new("ERIK",3,1u,false,[191, 204, 195, 197],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_VULPIX",37,"VULPIX",36,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_VULPIX",37,"VULPIX",36,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_NINETALES",38,"NINETALES",36,0,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_EMBER", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SAFEGUARD"])]),
        [new("pret:TRAINER_SUPER_NERD_ZAC")] = new("ZAC",3,1u,false,[212, 187, 189],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",37,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL", "pret:MOVE_HELPING_HAND"]),new("pret:SPECIES_VULPIX",37,"VULPIX",37,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD", "pret:MOVE_GRUDGE"])]),
        [new("pret:TRAINER_SWIMMER_MALE_JACK")] = new("JACK",8,1u,false,[196, 187, 189, 197],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARMIE",121,"STARMIE",37,0,[205, 206, 187, 204, 199, 195, 191],["pret:MOVE_RAPID_SPIN", "pret:MOVE_RECOVER", "pret:MOVE_SWIFT", "pret:MOVE_CONFUSE_RAY"])]),
        [new("pret:TRAINER_SWIMMER_MALE_JEROME")] = new("JEROME",8,1u,false,[196, 191, 204, 201, 199, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARYU",120,"STARYU",33,0,[205, 206, 187, 204, 211, 207],["pret:MOVE_CAMOUFLAGE", "pret:MOVE_SWIFT", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_MINIMIZE"]),new("pret:SPECIES_WARTORTLE",8,"WARTORTLE",33,0,[209, 187, 204, 206, 201, 204, 206, 198, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN", "pret:MOVE_PROTECT"])]),
        [new("pret:TRAINER_SWIMMER_MALE_ROLAND")] = new("ROLAND",8,1u,false,[204, 201, 198, 187, 200, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_POLIWHIRL",61,"POLIWHIRL",32,0,[202, 201, 198, 195, 209, 194, 195, 204, 198],["pret:MOVE_HYPNOSIS", "pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE"]),new("pret:SPECIES_TENTACOOL",72,"TENTACOOL",32,0,[206, 191, 200, 206, 187, 189, 201, 201, 198],["pret:MOVE_CONSTRICT", "pret:MOVE_ACID", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_WRAP"]),new("pret:SPECIES_SEADRA",117,"SEADRA",32,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"])]),
        [new("pret:TRAINER_SWIMMER_MALE_SPENCER")] = new("SPENCER",8,1u,false,[205, 202, 191, 200, 189, 191, 204],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEADRA",117,"SEADRA",33,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"]),new("pret:SPECIES_TENTACRUEL",73,"TENTACRUEL",33,0,[206, 191, 200, 206, 187, 189, 204, 207, 191, 198],["pret:MOVE_CONSTRICT", "pret:MOVE_ACID", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_WRAP"])]),
        [new("pret:TRAINER_YOUNGSTER_JOHNSON")] = new("JOHNSON",0,1u,false,[196, 201, 194, 200, 205, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_EKANS",23,"EKANS",33,0,[191, 197, 187, 200, 205],["pret:MOVE_BITE", "pret:MOVE_GLARE", "pret:MOVE_SCREECH", "pret:MOVE_ACID"]),new("pret:SPECIES_EKANS",23,"EKANS",33,0,[191, 197, 187, 200, 205],["pret:MOVE_BITE", "pret:MOVE_GLARE", "pret:MOVE_SCREECH", "pret:MOVE_ACID"]),new("pret:SPECIES_RATICATE",20,"RATICATE",34,0,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_QUICK_ATTACK", "pret:MOVE_HYPER_FANG", "pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT"])]),
    }.ToImmutableDictionary();
    private static readonly ImmutableHashSet<string> ExpectedBattleKeys = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "CinnabarIsland_Gym_EventScript_Avery",
        "CinnabarIsland_Gym_EventScript_BattleAvery",
        "CinnabarIsland_Gym_EventScript_BattleDerek",
        "CinnabarIsland_Gym_EventScript_BattleDusty",
        "CinnabarIsland_Gym_EventScript_BattleQuinn",
        "CinnabarIsland_Gym_EventScript_BattleRamon",
        "CinnabarIsland_Gym_EventScript_BattleZac",
        "CinnabarIsland_Gym_EventScript_Blaine",
        "CinnabarIsland_Gym_EventScript_Derek",
        "CinnabarIsland_Gym_EventScript_Dusty",
        "CinnabarIsland_Gym_EventScript_Erik",
        "CinnabarIsland_Gym_EventScript_Quinn",
        "CinnabarIsland_Gym_EventScript_Ramon",
        "CinnabarIsland_Gym_EventScript_Zac",
        "PokemonMansion_1F_EventScript_Johnson",
        "PokemonMansion_1F_EventScript_Ted",
        "PokemonMansion_2F_EventScript_Arnie",
        "PokemonMansion_3F_EventScript_Braydon",
        "PokemonMansion_3F_EventScript_Simon",
        "PokemonMansion_B1F_EventScript_Ivan",
        "PokemonMansion_B1F_EventScript_Lewis",
        "Route21_North_EventScript_Ian",
        "Route21_North_EventScript_Lil",
        "Route21_North_EventScript_Ronald",
        "Route21_North_EventScript_Spencer",
        "Route21_North_EventScript_Wade",
        "Route21_South_EventScript_Claude",
        "Route21_South_EventScript_Jack",
        "Route21_South_EventScript_Jerome",
        "Route21_South_EventScript_Nolan",
        "Route21_South_EventScript_Roland");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = Expected.Keys.OrderBy(t => t.Value, StringComparer.Ordinal).ToImmutableArray();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t => !Expected[t].DoubleBattle).ToImmutableArray();
    public static ImmutableArray<TrainerId> GymTrainers { get; } = [
        new("pret:TRAINER_BURGLAR_DUSTY"),
        new("pret:TRAINER_BURGLAR_QUINN"),
        new("pret:TRAINER_BURGLAR_RAMON"),
        new("pret:TRAINER_LEADER_BLAINE"),
        new("pret:TRAINER_SUPER_NERD_AVERY"),
        new("pret:TRAINER_SUPER_NERD_DEREK"),
        new("pret:TRAINER_SUPER_NERD_ERIK"),
        new("pret:TRAINER_SUPER_NERD_ZAC")
    ];
    public static TrainerPartyCreationResult Create(CinnabarTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("cinnabar_trainer_catalog");
        var source = opponents.Mechanics; var expected = Expected[id];
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("cinnabar_trainer_species");
            var moves = expected.Party[i].Moves.Select(m => new MoveId(m));
            if (trainer.PartyFlags == 0 && !PokemonMoveRules.InitialMovesCore(source.Catalog, source.Catalog.Species[member.Species], member.Level)
                    .Select(m => m.Move).SequenceEqual(moves)) throw new NotSupportedException("cinnabar_trainer_natural_moves");
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("cinnabar_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if (trainer.PartyFlags == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(CinnabarTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 24 || catalog.BattlesByScript.Count != 31)
            throw new NotSupportedException("cinnabar_trainer_scope");
        foreach (var (id, expected) in Expected)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("cinnabar_trainer_source");

        }
        foreach (var (script, battle) in catalog.BattlesByScript)
        {
            if (battle is null || script != battle.ScriptEntry) throw new NotSupportedException("cinnabar_trainer_battle_key");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattleKeys.Contains(battle.ScriptEntry))
            throw new NotSupportedException("cinnabar_trainer_battle_source");
    }
}
