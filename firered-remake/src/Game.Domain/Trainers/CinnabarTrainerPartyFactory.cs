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
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party,
        string TrainerSha256);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BURGLAR_ARNIE")] = new("ARNIE",3,1u,false,[187, 204, 200, 195, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CHARMANDER",4,"CHARMANDER",34,0,[189, 194, 187, 204, 199, 187, 200, 190, 191, 204],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"]),new("pret:SPECIES_CHARMELEON",5,"CHARMELEON",34,0,[189, 194, 187, 204, 199, 191, 198, 191, 201, 200],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"])],"ab42e81ce22465f3200ff4980d6c6a386b76362c4b47059b0866051c98e80548"),
        [new("pret:TRAINER_BURGLAR_DUSTY")] = new("DUSTY",3,1u,false,[190, 207, 205, 206, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_VULPIX",37,"VULPIX",37,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD", "pret:MOVE_GRUDGE"]),new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",37,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL", "pret:MOVE_HELPING_HAND"])],"90d8796e1945d3ee9ebb0677188ccb91ba18d9b3368d975ddd33aa3dfb108710"),
        [new("pret:TRAINER_BURGLAR_LEWIS")] = new("LEWIS",3,1u,false,[198, 191, 209, 195, 205],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",34,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_LEER", "pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL"]),new("pret:SPECIES_PONYTA",77,"PONYTA",34,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_EMBER", "pret:MOVE_STOMP", "pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN"])],"0a9a229136745cb0cff4429b31bd92f96dc201d9c9f55aff7b1430600f2efbe4"),
        [new("pret:TRAINER_BURGLAR_QUINN")] = new("QUINN",3,1u,false,[203, 207, 195, 200, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",36,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_LEER", "pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL"]),new("pret:SPECIES_VULPIX",37,"VULPIX",36,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_NINETALES",38,"NINETALES",36,0,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_EMBER", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SAFEGUARD"])],"8912139200cb3bcfdc14a1d9391d74f391acd71892a2ee46be4e5e40becd8924"),
        [new("pret:TRAINER_BURGLAR_RAMON")] = new("RAMON",3,1u,false,[204, 187, 199, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PONYTA",77,"PONYTA",41,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_STOMP", "pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN", "pret:MOVE_AGILITY"])],"85497c251a6d93a9e26149a821b86975f01821ff11e3824b9bc3f993406a5cad"),
        [new("pret:TRAINER_BURGLAR_SIMON")] = new("SIMON",3,1u,false,[205, 195, 199, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_NINETALES",38,"NINETALES",38,0,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_EMBER", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SAFEGUARD"])],"f7fe35c1534c7d90afa946898979ec0fe13c6a12d4f5f55a256f91932fe9ffe2"),
        [new("pret:TRAINER_FISHERMAN_CLAUDE")] = new("CLAUDE",11,1u,false,[189, 198, 187, 207, 190, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SHELLDER",90,"SHELLDER",31,0,[205, 194, 191, 198, 198, 190, 191, 204],["pret:MOVE_ICICLE_SPEAR", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"]),new("pret:SPECIES_CLOYSTER",91,"CLOYSTER",31,0,[189, 198, 201, 211, 205, 206, 191, 204],["pret:MOVE_WITHDRAW", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"])],"a94b5b48f5339c1453b6279924c97be9975796bc6f4fdf312fcbfc9e00b3df78"),
        [new("pret:TRAINER_FISHERMAN_NOLAN")] = new("NOLAN",11,1u,false,[200, 201, 198, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEAKING",119,"SEAKING",33,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"]),new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",33,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"])],"de5108ce11aa45e3f395dfc3b3936f9b97a0d21bf60842d18261c0e4a6a9ae7d"),
        [new("pret:TRAINER_FISHERMAN_RONALD")] = new("RONALD",11,1u,false,[204, 201, 200, 187, 198, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEAKING",119,"SEAKING",28,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"]),new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",28,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"]),new("pret:SPECIES_SEAKING",119,"SEAKING",28,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"]),new("pret:SPECIES_SEAKING",119,"SEAKING",28,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_WATER_SPORT", "pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL"])],"965762bcfc1a5405e7017a393b15b35d0c52eb3a5e0f8a95ed0d180f1bc819e6"),
        [new("pret:TRAINER_FISHERMAN_WADE")] = new("WADE",11,1u,false,[209, 187, 190, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"]),new("pret:SPECIES_MAGIKARP",129,"MAGIKARP",27,0,[199, 187, 193, 195, 197, 187, 204, 202],["pret:MOVE_SPLASH", "pret:MOVE_TACKLE"])],"121d804f66bd139ed9bf3bb4a66aed8c4158d9a88c2c9c57300f702d71d61892"),
        [new("pret:TRAINER_LEADER_BLAINE")] = new("BLAINE",0,7u,false,[188, 198, 187, 195, 200, 191],["ITEM_HYPER_POTION", "ITEM_HYPER_POTION", "ITEM_FULL_HEAL", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",42,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_BITE", "pret:MOVE_ROAR", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FIRE_BLAST"]),new("pret:SPECIES_PONYTA",77,"PONYTA",40,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_STOMP", "pret:MOVE_BOUNCE", "pret:MOVE_FIRE_SPIN", "pret:MOVE_FIRE_BLAST"]),new("pret:SPECIES_RAPIDASH",78,"RAPIDASH",42,0,[204, 187, 202, 195, 190, 187, 205, 194],["pret:MOVE_STOMP", "pret:MOVE_BOUNCE", "pret:MOVE_FIRE_SPIN", "pret:MOVE_FIRE_BLAST"]),new("pret:SPECIES_ARCANINE",59,"ARCANINE",47,0,[187, 204, 189, 187, 200, 195, 200, 191],["pret:MOVE_BITE", "pret:MOVE_ROAR", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FIRE_BLAST"])],"7b5f900df3d7bfc99e5591c86bd1b8a7ec128065470c00c1951d56a947d91a77"),
        [new("pret:TRAINER_SCIENTIST_BRAYDON")] = new("BRAYDON",3,1u,false,[188, 204, 187, 211, 190, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAGNEMITE",81,"MAGNEMITE",33,0,[199, 187, 193, 200, 191, 199, 195, 206, 191],["pret:MOVE_SONIC_BOOM", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SPARK", "pret:MOVE_LOCK_ON"]),new("pret:SPECIES_MAGNETON",82,"MAGNETON",33,0,[199, 187, 193, 200, 191, 206, 201, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_SONIC_BOOM", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SPARK"]),new("pret:SPECIES_VOLTORB",100,"VOLTORB",33,0,[208, 201, 198, 206, 201, 204, 188],["pret:MOVE_SONIC_BOOM", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT"])],"67ca5c14827d759339a6daf02b2e50de2fe2e3b0ca4ba069766d0806d6034e77"),
        [new("pret:TRAINER_SCIENTIST_IVAN")] = new("IVAN",3,1u,false,[195, 208, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAGNEMITE",81,"MAGNEMITE",34,0,[199, 187, 193, 200, 191, 199, 195, 206, 191],["pret:MOVE_SONIC_BOOM", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SPARK", "pret:MOVE_LOCK_ON"]),new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",34,0,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SONIC_BOOM", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT"])],"059716e66a1c29893af452a7d629f37658a684610fc40351b1ee20fe60313fc1"),
        [new("pret:TRAINER_SCIENTIST_TED")] = new("TED",3,1u,false,[206, 191, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",29,0,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SCREECH", "pret:MOVE_SONIC_BOOM", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT"]),new("pret:SPECIES_WEEZING",110,"WEEZING",29,0,[209, 191, 191, 212, 195, 200, 193],["pret:MOVE_SMOG", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_SLUDGE", "pret:MOVE_SMOKESCREEN"])],"574b7042fe9a402a2cb05be847a13fbe7a8460ff72253b1ee4adb5e86a41f754"),
        [new("pret:TRAINER_SIS_AND_BRO_LIL_IAN")] = new("LIL & IAN",8,1u,true,[198, 195, 198, 0, 45, 0, 195, 187, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEADRA",117,"SEADRA",33,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"]),new("pret:SPECIES_STARMIE",121,"STARMIE",33,0,[205, 206, 187, 204, 199, 195, 191],["pret:MOVE_RAPID_SPIN", "pret:MOVE_RECOVER", "pret:MOVE_SWIFT", "pret:MOVE_CONFUSE_RAY"])],"bb05b752ede44d3f6341b3116c9b09f6f12c4ed39d9af1a66d97760997531e72"),
        [new("pret:TRAINER_SUPER_NERD_AVERY")] = new("AVERY",3,1u,false,[187, 208, 191, 204, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PONYTA",77,"PONYTA",34,0,[202, 201, 200, 211, 206, 187],["pret:MOVE_EMBER", "pret:MOVE_STOMP", "pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN"]),new("pret:SPECIES_CHARMANDER",4,"CHARMANDER",34,0,[189, 194, 187, 204, 199, 187, 200, 190, 191, 204],["pret:MOVE_METAL_CLAW", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SCARY_FACE", "pret:MOVE_FLAMETHROWER"]),new("pret:SPECIES_VULPIX",37,"VULPIX",34,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",34,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_LEER", "pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL"])],"081b1f0b6964a3667d90a925e1e5ee24ac7e76b83e9ecfac7d383eefc3d6090d"),
        [new("pret:TRAINER_SUPER_NERD_DEREK")] = new("DEREK",3,1u,false,[190, 191, 204, 191, 197],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RAPIDASH",78,"RAPIDASH",41,0,[204, 187, 202, 195, 190, 187, 205, 194],["pret:MOVE_FIRE_SPIN", "pret:MOVE_TAKE_DOWN", "pret:MOVE_AGILITY", "pret:MOVE_FURY_ATTACK"])],"d5e882b6816634e0a77395f6c9f5f790dffdbf14ab9883c40ca04fe92888606a"),
        [new("pret:TRAINER_SUPER_NERD_ERIK")] = new("ERIK",3,1u,false,[191, 204, 195, 197],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_VULPIX",37,"VULPIX",36,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_VULPIX",37,"VULPIX",36,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD"]),new("pret:SPECIES_NINETALES",38,"NINETALES",36,0,[200, 195, 200, 191, 206, 187, 198, 191, 205],["pret:MOVE_EMBER", "pret:MOVE_QUICK_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_SAFEGUARD"])],"cdf27cb22c78be2d7dd211b7e6bd71e9fc5e0bb948a2fd0a0c9fcd1208010faf"),
        [new("pret:TRAINER_SUPER_NERD_ZAC")] = new("ZAC",3,1u,false,[212, 187, 189],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GROWLITHE",58,"GROWLITHE",37,0,[193, 204, 201, 209, 198, 195, 206, 194, 191],["pret:MOVE_ODOR_SLEUTH", "pret:MOVE_TAKE_DOWN", "pret:MOVE_FLAME_WHEEL", "pret:MOVE_HELPING_HAND"]),new("pret:SPECIES_VULPIX",37,"VULPIX",37,0,[208, 207, 198, 202, 195, 210],["pret:MOVE_IMPRISON", "pret:MOVE_FLAMETHROWER", "pret:MOVE_SAFEGUARD", "pret:MOVE_GRUDGE"])],"293365e3598bc95a68a1ea38822a44c3964d72fecf5fb3a424d345fd0777e608"),
        [new("pret:TRAINER_SWIMMER_MALE_JACK")] = new("JACK",8,1u,false,[196, 187, 189, 197],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARMIE",121,"STARMIE",37,0,[205, 206, 187, 204, 199, 195, 191],["pret:MOVE_RAPID_SPIN", "pret:MOVE_RECOVER", "pret:MOVE_SWIFT", "pret:MOVE_CONFUSE_RAY"])],"fd93ea9f579a8c905b5e0a57ddcd43bf091d0127eb0e9679e1fbb0c9f26b7ede"),
        [new("pret:TRAINER_SWIMMER_MALE_JEROME")] = new("JEROME",8,1u,false,[196, 191, 204, 201, 199, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARYU",120,"STARYU",33,0,[205, 206, 187, 204, 211, 207],["pret:MOVE_CAMOUFLAGE", "pret:MOVE_SWIFT", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_MINIMIZE"]),new("pret:SPECIES_WARTORTLE",8,"WARTORTLE",33,0,[209, 187, 204, 206, 201, 204, 206, 198, 191],["pret:MOVE_WATER_GUN", "pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN", "pret:MOVE_PROTECT"])],"b088322ffba1a979021f8e9b95b074a0a4a1a3f2141e70bfa0534d1bdebf41f4"),
        [new("pret:TRAINER_SWIMMER_MALE_ROLAND")] = new("ROLAND",8,1u,false,[204, 201, 198, 187, 200, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_POLIWHIRL",61,"POLIWHIRL",32,0,[202, 201, 198, 195, 209, 194, 195, 204, 198],["pret:MOVE_HYPNOSIS", "pret:MOVE_WATER_GUN", "pret:MOVE_DOUBLE_SLAP", "pret:MOVE_RAIN_DANCE"]),new("pret:SPECIES_TENTACOOL",72,"TENTACOOL",32,0,[206, 191, 200, 206, 187, 189, 201, 201, 198],["pret:MOVE_CONSTRICT", "pret:MOVE_ACID", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_WRAP"]),new("pret:SPECIES_SEADRA",117,"SEADRA",32,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"])],"ce0fdbaf98f919c6cab2465260c6d4f1b5ca1eca310e29d4306ebe1662f0b491"),
        [new("pret:TRAINER_SWIMMER_MALE_SPENCER")] = new("SPENCER",8,1u,false,[205, 202, 191, 200, 189, 191, 204],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEADRA",117,"SEADRA",33,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"]),new("pret:SPECIES_TENTACRUEL",73,"TENTACRUEL",33,0,[206, 191, 200, 206, 187, 189, 204, 207, 191, 198],["pret:MOVE_CONSTRICT", "pret:MOVE_ACID", "pret:MOVE_BUBBLE_BEAM", "pret:MOVE_WRAP"])],"fbee4976f344a94feefb6d82fbd6f91d1f37f4be87a16dfed32570cc50e5c1ac"),
        [new("pret:TRAINER_YOUNGSTER_JOHNSON")] = new("JOHNSON",0,1u,false,[196, 201, 194, 200, 205, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_EKANS",23,"EKANS",33,0,[191, 197, 187, 200, 205],["pret:MOVE_BITE", "pret:MOVE_GLARE", "pret:MOVE_SCREECH", "pret:MOVE_ACID"]),new("pret:SPECIES_EKANS",23,"EKANS",33,0,[191, 197, 187, 200, 205],["pret:MOVE_BITE", "pret:MOVE_GLARE", "pret:MOVE_SCREECH", "pret:MOVE_ACID"]),new("pret:SPECIES_RATICATE",20,"RATICATE",34,0,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_QUICK_ATTACK", "pret:MOVE_HYPER_FANG", "pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT"])],"ea14e6c37c822b807e1dd9763acc82682559e0844b128efac38c991b6f3559e4"),
    }.ToImmutableDictionary();
    private static readonly ImmutableDictionary<string, string> ExpectedBattles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CinnabarIsland_Gym_EventScript_Avery"] = "32208e7b9339fe95515b7d772576983a3f0fa95ad5c2f06cad0eecd6e955cad7",
        ["CinnabarIsland_Gym_EventScript_BattleAvery"] = "7d79d07d5cce4dd1700eee4e40b2d62e5bcae399574805d4cc5f19c53f1eb07b",
        ["CinnabarIsland_Gym_EventScript_BattleDerek"] = "e27e45fb02f9fa13db43d34a76e5dfb37106d4be5a37f76b53b4e92a03bb6a0c",
        ["CinnabarIsland_Gym_EventScript_BattleDusty"] = "e08759d51c694aa909cbd3d246e8a7ae3ddba382978abfa8c5c7eeb0af153bb2",
        ["CinnabarIsland_Gym_EventScript_BattleQuinn"] = "b90a8f346dc2ff2fc8181224958e6787549610706ed46879266e52de0f08c830",
        ["CinnabarIsland_Gym_EventScript_BattleRamon"] = "b79117f1497a222849db58cbadc98d43de556037d4f46ec93d8b2728a6f2c37f",
        ["CinnabarIsland_Gym_EventScript_BattleZac"] = "21d4c58883ab8da8445409dabbf44ef03cc4e7a0e3b87829aa72af131792ff11",
        ["CinnabarIsland_Gym_EventScript_Blaine"] = "462b4e9b954d3c2bb2101df64a210383bfb0c56054ca09d028a2f1a373615803",
        ["CinnabarIsland_Gym_EventScript_Derek"] = "8962998345c8507b47a2833781813c21e8008410721de255d4ba82a876b7b123",
        ["CinnabarIsland_Gym_EventScript_Dusty"] = "3de599e6fc81310725312ff7ecae178a1e7de54f97ae0a75ad8ed18a794a6ec4",
        ["CinnabarIsland_Gym_EventScript_Erik"] = "a42c9b543e174915a01d579b5291721e5f3dc89fd69ed1cadf713633739885c9",
        ["CinnabarIsland_Gym_EventScript_Quinn"] = "ff4beabe13069a43a02c954b62c90602c192f4b53614d7d67628e03bd1004272",
        ["CinnabarIsland_Gym_EventScript_Ramon"] = "0cdc0447887f90228b705ad445539ebcf7d06cce4148dfbe2e2951544baae89e",
        ["CinnabarIsland_Gym_EventScript_Zac"] = "9213cdea050f54cdf890f646b755683f35754db1da0078ff1d683035367a378d",
        ["PokemonMansion_1F_EventScript_Johnson"] = "956440ba58778f380925c5dc1cf79189d367951f5bdf87fc01e8646cf925ca44",
        ["PokemonMansion_1F_EventScript_Ted"] = "bfee6f47559f727b019a1fadfd44e1e7af021adf618fb3679741304126e4a31a",
        ["PokemonMansion_2F_EventScript_Arnie"] = "6adc3d00493dc8b0688aaf8b1f69e46aea732acba700ab4e05d03b2178d08373",
        ["PokemonMansion_3F_EventScript_Braydon"] = "745033eb1ccb63cba93786134ffdc646df0d48447e1ed7b7e08f13febf8aaeee",
        ["PokemonMansion_3F_EventScript_Simon"] = "ff117689573dc9b50c3a19a64ee6a30fb03f7b280c0ae849b65cfa0aed8985b2",
        ["PokemonMansion_B1F_EventScript_Ivan"] = "b1255a06996a0635920354aa822e8ff3c6caadeddcd4d4f6ffc707b151d0fc1d",
        ["PokemonMansion_B1F_EventScript_Lewis"] = "2720bd5c846f7a479f4189650a55d196f9b9109f75097f877a57a850015069e5",
        ["Route21_North_EventScript_Ian"] = "f27a1dd1c3469ea055cdf0237bbef531539330dd613ed8877691e4de99e35a7a",
        ["Route21_North_EventScript_Lil"] = "d809e6922736511d442836b16f33f30aae7fd6374eb94d3bb5d0d9f03e58612d",
        ["Route21_North_EventScript_Ronald"] = "18d12c34fcd6b6e95a164b5c2cc3623b0eef5718b474cb7b2c67b37138246727",
        ["Route21_North_EventScript_Spencer"] = "b97d223def48e1a5d89a0135c39639fe5117f2f109913222228439e5ccda499c",
        ["Route21_North_EventScript_Wade"] = "25616ee1ec472421303ee0385ed1019980499b4015b35d0c9cefeb98ba151dee",
        ["Route21_South_EventScript_Claude"] = "95e22fa69ffff19e386cd1ebfebd39989f7243ed4b45222c7494aa379f223ccf",
        ["Route21_South_EventScript_Jack"] = "133dbada93b55d696b4c82c136c51a05084a8c0e474b1f2c65532326f2df82c0",
        ["Route21_South_EventScript_Jerome"] = "b1a462fae6820281edeb921c6bdd8bf5248960236cc9bdf35d03dd73131b03ad",
        ["Route21_South_EventScript_Nolan"] = "93d3a2464172553bd48f06769caa52b08a2256abf91394edf1ac757d85c005d4",
        ["Route21_South_EventScript_Roland"] = "65952b5938de3cf7448fa4fdeddc7e60718c8359141fb30f35b4990b8952ed28",
    }.ToImmutableDictionary(StringComparer.Ordinal);
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
    public static TrainerPartyCreationResult Create(CinnabarTrainerCatalog trainers, CinnabarOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("cinnabar_trainer_catalog");
        var source = opponents.Mechanics; var expected = Expected[id];
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("cinnabar_trainer_species");
            var moves = expected.Party[i].Moves.Select(m => new MoveId(m));
            if (trainer.PartyFlags == 0 && !PokemonMoveRules.InitialMovesCore(source.Catalog, source.Catalog.Species[member.Species], member.Level)
                    .Select(m => m.Move).SequenceEqual(moves)) throw new NotSupportedException("cinnabar_trainer_natural_moves");
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
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
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != expected.TrainerSha256)
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
        if (battle is null || !ExpectedBattles.TryGetValue(battle.ScriptEntry, out var expected) ||
            CampaignSourceFingerprint.Compute(battle) != expected)
            throw new NotSupportedException("cinnabar_trainer_battle_source");
    }
}
