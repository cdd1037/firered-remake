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
    private static readonly ImmutableDictionary<TrainerId, string> TrainerHashes = new Dictionary<TrainerId, string>
    {
        [new("pret:TRAINER_BLACK_BELT_HUGH")] = "8fdb1d4251d29cef9eb370fa08ec66644e24e4dd25a7dca1fbc579a6801cf850",
        [new("pret:TRAINER_BLACK_BELT_SHEA")] = "748fcf9264f4c359d1c20dfb218eaab223adff98c274ac66b4b7d7b1155fa707",
        [new("pret:TRAINER_CAMPER_BRYCE")] = "54c4380258f097bf191a0c814c78364525dee802e675ab1a9438c6ed01efcfd3",
        [new("pret:TRAINER_CRUSH_GIRL_JOCELYN")] = "5e2b0db59a74f0b75513cff31675e24ffa4f124671fe622916a57e0c698a508c",
        [new("pret:TRAINER_CRUSH_GIRL_SHARON")] = "74f3005d478ef9c0ae52339f4b03ed367ed470a85f5996d72c82e5ea23b41381",
        [new("pret:TRAINER_CRUSH_GIRL_TANYA")] = "6709f9a06ff9ab8e5151307d7126d5793523c0955d7b0e02c9381cd225b89728",
        [new("pret:TRAINER_CRUSH_KIN_MIK_KIA")] = "6d9a22fd7c43e087babf85a40abe279d099241a2d3e27541b38a99aa34315035",
        [new("pret:TRAINER_FISHERMAN_TOMMY")] = "e0f06a517ee4c03e0b08e363866461067763e50fb9caee19bffbe376d0b29e9b",
        [new("pret:TRAINER_PICNICKER_CLAIRE")] = "583015bfcec9e7cbb41278bb3d8ae50748ec41ae133327f6d3f27701af8bf429",
        [new("pret:TRAINER_PKMN_RANGER_BETH")] = "f0e2d475810c911524ab2cd98576ef01ae88832b51939eb3ae2bac0393c08ae7",
        [new("pret:TRAINER_PKMN_RANGER_LOGAN")] = "15fa3edc36cf9242d56925337c20155ad9872cfb0d8f40b3cc7bda86745a09ea",
        [new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL")] = "c67039efa81699fcc3caff7837513068f67237e86855e3d045296b8772fd0d5b",
        [new("pret:TRAINER_SWIMMER_FEMALE_MARIA")] = "a882b4f2dcf7429033852581906cf63221486cc4b145b37afac8c4a349dd7cfc",
        [new("pret:TRAINER_SWIMMER_MALE_FINN")] = "1af6c90e217e52c3bfd8cf62c9ba73324032985da72b347e71a2dab0d2213850",
        [new("pret:TRAINER_SWIMMER_MALE_GARRETT")] = "3228cf644f67b0f582bfda40fe038595ff4fc4050b23bd52eaa4d7fdfc848cfa",
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_43")] = "8156501edb3deeeeda3236310d375a921b590c68372d540977ff01612d01710d",
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")] = "bd1287c3e0191f89c43151d5191957c8cf5bbda6f5ccabc9b45e27fccad49216",
    }.ToImmutableDictionary();
    private static readonly ImmutableDictionary<string, string> BattleHashes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MtEmber_Exterior_EventScript_BattleGrunt1"] = "fc6eaa0b1585cb114bcd7a79fcbc06b6417e64bf74328c6cc67443478a1efb31",
        ["MtEmber_Exterior_EventScript_BattleGrunt2"] = "9d0dbe83b03c87e490d730bb0c2a0c9055c311ec1c1af0c811f03d8e39b28dfc",
        ["MtEmber_Exterior_EventScript_Beth"] = "0833e23ee0676f9b7e22b828dd57ae10e1f016e3b31f506bd6be08e6bf9ea6f2",
        ["MtEmber_Exterior_EventScript_Jocelyn"] = "c17255064065bcd3aa35d91d26ec9d1a75693ca08e81a19202f238e31aee9531",
        ["MtEmber_Exterior_EventScript_Logan"] = "96079c0bffbf66410457e543ae0017718126b796148be53bb54c3d5e55f3c97a",
        ["OneIsland_KindleRoad_EventScript_Abigail"] = "961ead59d152624ae419007c889be9ac9f0363621fc9cfafe28eff8d3feaa719",
        ["OneIsland_KindleRoad_EventScript_Bryce"] = "26af0c5387960f2121208a1e572b599c9baf0cd184fbc7d1ab368ebfd1cd4bd5",
        ["OneIsland_KindleRoad_EventScript_Claire"] = "2fe618b40e1f6dd9611a94f0e0e192da46de410121706601dfdd769dbfe4d7ee",
        ["OneIsland_KindleRoad_EventScript_Finn"] = "0d48098e84e6ce7fc7a1664ed13124fd88f5b47d12009653b4f23e6978fd431a",
        ["OneIsland_KindleRoad_EventScript_Garrett"] = "3288a12856d5fc15ee55be67171b43967816cd3c4fd6177107517349915e642b",
        ["OneIsland_KindleRoad_EventScript_Hugh"] = "94a2358c8a871a37e8377935324297cf4f6920e384014589c0783dbd32fb86c1",
        ["OneIsland_KindleRoad_EventScript_Kia"] = "bfd78367e5ae741a61d55ed4bb3a63217743a19a0d757aa59f150bd7b99fed48",
        ["OneIsland_KindleRoad_EventScript_Maria"] = "c6d0f9c293b923ea2cc14503c252156468c1e605c79b1ffc97e35da7208a92f9",
        ["OneIsland_KindleRoad_EventScript_Mik"] = "fe76be8c66d3b07ca3490f212ac2dc05b4cf5a2eb699d2fef86d94768920e434",
        ["OneIsland_KindleRoad_EventScript_Sharon"] = "bf0f917f33c8aa12bf87bd94c4ea013aa7702cc37e55699760580327c55974f9",
        ["OneIsland_KindleRoad_EventScript_Shea"] = "d00ca3f56387baf856b1911875f0c22e415ffadf9b259f4521ea009e0e771419",
        ["OneIsland_KindleRoad_EventScript_Tanya"] = "9e689bba9d045386b7275ada5b9bcd49a65482571707ee136824fae20ce09d49",
        ["OneIsland_KindleRoad_EventScript_Tommy"] = "63dcc1de745ce43b91d1becec300b59c899b18547895a186de36220e9288f5cc",
    }.ToImmutableDictionary(StringComparer.Ordinal);
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [new("pret:TRAINER_BLACK_BELT_HUGH"),new("pret:TRAINER_BLACK_BELT_SHEA"),new("pret:TRAINER_CAMPER_BRYCE"),new("pret:TRAINER_CRUSH_GIRL_JOCELYN"),new("pret:TRAINER_CRUSH_GIRL_SHARON"),new("pret:TRAINER_CRUSH_GIRL_TANYA"),new("pret:TRAINER_CRUSH_KIN_MIK_KIA"),new("pret:TRAINER_FISHERMAN_TOMMY"),new("pret:TRAINER_PICNICKER_CLAIRE"),new("pret:TRAINER_PKMN_RANGER_BETH"),new("pret:TRAINER_PKMN_RANGER_LOGAN"),new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL"),new("pret:TRAINER_SWIMMER_FEMALE_MARIA"),new("pret:TRAINER_SWIMMER_MALE_FINN"),new("pret:TRAINER_SWIMMER_MALE_GARRETT"),new("pret:TRAINER_TEAM_ROCKET_GRUNT_43"),new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")];
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize, string TrainerSha256);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BLACK_BELT_HUGH")] = new("HUGH",4,1u,false,[194, 207, 193, 194],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOP",66,"MACHOP",37,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",37,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT")],888,"8fdb1d4251d29cef9eb370fa08ec66644e24e4dd25a7dca1fbc579a6801cf850"),
        [new("pret:TRAINER_BLACK_BELT_SHEA")] = new("SHEA",4,1u,false,[205, 194, 191, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOP",66,"MACHOP",38,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",38,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT")],912,"748fcf9264f4c359d1c20dfb218eaab223adff98c274ac66b4b7d7b1155fa707"),
        [new("pret:TRAINER_CAMPER_BRYCE")] = new("BRYCE",0,1u,false,[188, 204, 211, 189, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_NIDORINO",33,"NIDORINO",36,0,[200, 195, 190, 201, 204, 195, 200, 201],["pret:MOVE_POISON_STING", "pret:MOVE_HORN_ATTACK", "pret:MOVE_HELPING_HAND", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_RATICATE",20,"RATICATE",36,0,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_QUICK_ATTACK", "pret:MOVE_HYPER_FANG", "pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT"],"ITEM_NONE"),new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",36,0,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_SLASH", "pret:MOVE_SWIFT"],"ITEM_NONE")],720,"54c4380258f097bf191a0c814c78364525dee802e675ab1a9438c6ed01efcfd3"),
        [new("pret:TRAINER_CRUSH_GIRL_JOCELYN")] = new("JOCELYN",4,1u,false,[196, 201, 189, 191, 198, 211, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",38,50,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_ICE_PUNCH", "pret:MOVE_FIRE_PUNCH", "pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MEGA_PUNCH"],"ITEM_BLACK_BELT"),new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",38,50,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_ICE_PUNCH", "pret:MOVE_FIRE_PUNCH", "pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MEGA_PUNCH"],"ITEM_BLACK_BELT")],912,"5e2b0db59a74f0b75513cff31675e24ffa4f124671fe622916a57e0c698a508c"),
        [new("pret:TRAINER_CRUSH_GIRL_SHARON")] = new("SHARON",4,1u,false,[205, 194, 187, 204, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MANKEY",56,"MANKEY",37,50,[199, 187, 200, 197, 191, 211],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_SEISMIC_TOSS", "pret:MOVE_CROSS_CHOP", "pret:MOVE_SWAGGER"],"ITEM_BLACK_BELT"),new("pret:SPECIES_PRIMEAPE",57,"PRIMEAPE",37,50,[202, 204, 195, 199, 191, 187, 202, 191],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_SEISMIC_TOSS", "pret:MOVE_RAGE", "pret:MOVE_CROSS_CHOP"],"ITEM_BLACK_BELT")],888,"74f3005d478ef9c0ae52339f4b03ed367ed470a85f5996d72c82e5ea23b41381"),
        [new("pret:TRAINER_CRUSH_GIRL_TANYA")] = new("TANYA",4,1u,false,[206, 187, 200, 211, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HITMONLEE",106,"HITMONLEE",38,50,[194, 195, 206, 199, 201, 200, 198, 191, 191],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_HI_JUMP_KICK", "pret:MOVE_MIND_READER", "pret:MOVE_FORESIGHT"],"ITEM_BLACK_BELT"),new("pret:SPECIES_HITMONCHAN",107,"HITMONCHAN",38,50,[194, 195, 206, 199, 201, 200, 189, 194, 187, 200],["pret:MOVE_ICE_PUNCH", "pret:MOVE_FIRE_PUNCH", "pret:MOVE_SKY_UPPERCUT", "pret:MOVE_MEGA_PUNCH"],"ITEM_BLACK_BELT")],912,"6709f9a06ff9ab8e5151307d7126d5793523c0955d7b0e02c9381cd225b89728"),
        [new("pret:TRAINER_CRUSH_KIN_MIK_KIA")] = new("MIK & KIA",4,1u,true,[199, 195, 197, 0, 45, 0, 197, 195, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOKE",67,"MACHOKE",39,50,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT"),new("pret:SPECIES_PRIMEAPE",57,"PRIMEAPE",39,50,[202, 204, 195, 199, 191, 187, 202, 191],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_SEISMIC_TOSS", "pret:MOVE_RAGE", "pret:MOVE_CROSS_CHOP"],"ITEM_BLACK_BELT")],1872,"6d9a22fd7c43e087babf85a40abe279d099241a2d3e27541b38a99aa34315035"),
        [new("pret:TRAINER_FISHERMAN_TOMMY")] = new("TOMMY",11,1u,false,[206, 201, 199, 199, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",33,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_GOLDEEN",118,"GOLDEEN",33,0,[193, 201, 198, 190, 191, 191, 200],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_SEAKING",119,"SEAKING",35,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_SEAKING",119,"SEAKING",35,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE"),new("pret:SPECIES_SEAKING",119,"SEAKING",35,0,[205, 191, 187, 197, 195, 200, 193],["pret:MOVE_SUPERSONIC", "pret:MOVE_HORN_ATTACK", "pret:MOVE_FLAIL", "pret:MOVE_FURY_ATTACK"],"ITEM_NONE")],1260,"e0f06a517ee4c03e0b08e363866461067763e50fb9caee19bffbe376d0b29e9b"),
        [new("pret:TRAINER_PICNICKER_CLAIRE")] = new("CLAIRE",130,1u,false,[189, 198, 187, 195, 204, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MEOWTH",52,"MEOWTH",35,0,[199, 191, 201, 209, 206, 194],["pret:MOVE_BITE", "pret:MOVE_PAY_DAY", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_SCREECH"],"ITEM_NONE"),new("pret:SPECIES_MEOWTH",52,"MEOWTH",35,0,[199, 191, 201, 209, 206, 194],["pret:MOVE_BITE", "pret:MOVE_PAY_DAY", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_SCREECH"],"ITEM_NONE"),new("pret:SPECIES_PIKACHU",25,"PIKACHU",35,0,[202, 195, 197, 187, 189, 194, 207],["pret:MOVE_DOUBLE_TEAM", "pret:MOVE_SLAM", "pret:MOVE_THUNDERBOLT", "pret:MOVE_AGILITY"],"ITEM_NONE"),new("pret:SPECIES_CLEFAIRY",35,"CLEFAIRY",35,0,[189, 198, 191, 192, 187, 195, 204, 211],["pret:MOVE_MINIMIZE", "pret:MOVE_DEFENSE_CURL", "pret:MOVE_METRONOME", "pret:MOVE_COSMIC_POWER"],"ITEM_NONE")],700,"583015bfcec9e7cbb41278bb3d8ae50748ec41ae133327f6d3f27701af8bf429"),
        [new("pret:TRAINER_PKMN_RANGER_BETH")] = new("BETH",133,7u,false,[188, 191, 206, 194],["ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_BELLSPROUT",69,"BELLSPROUT",38,100,[188, 191, 198, 198, 205, 202, 204, 201, 207, 206],["pret:MOVE_STUN_SPORE", "pret:MOVE_ACID", "pret:MOVE_SWEET_SCENT", "pret:MOVE_RAZOR_LEAF"],"ITEM_NONE"),new("pret:SPECIES_GLOOM",44,"GLOOM",38,100,[193, 198, 201, 201, 199],["pret:MOVE_STUN_SPORE", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_ACID", "pret:MOVE_MOONLIGHT"],"ITEM_NONE"),new("pret:SPECIES_GLOOM",44,"GLOOM",38,100,[193, 198, 201, 201, 199],["pret:MOVE_STUN_SPORE", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_ACID", "pret:MOVE_MOONLIGHT"],"ITEM_NONE")],1368,"f0e2d475810c911524ab2cd98576ef01ae88832b51939eb3ae2bac0393c08ae7"),
        [new("pret:TRAINER_PKMN_RANGER_LOGAN")] = new("LOGAN",5,7u,false,[198, 201, 193, 187, 200],["ITEM_FULL_RESTORE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_EXEGGCUTE",102,"EXEGGCUTE",37,100,[191, 210, 191, 193, 193, 189, 207, 206, 191],["pret:MOVE_SLEEP_POWDER", "pret:MOVE_POISON_POWDER", "pret:MOVE_STUN_SPORE", "pret:MOVE_CONFUSION"],"ITEM_NONE"),new("pret:SPECIES_EXEGGUTOR",103,"EXEGGUTOR",40,100,[191, 210, 191, 193, 193, 207, 206, 201, 204],["pret:MOVE_EGG_BOMB", "pret:MOVE_STOMP", "pret:MOVE_CONFUSION", "pret:MOVE_HYPNOSIS"],"ITEM_NONE")],1440,"15fa3edc36cf9242d56925337c20155ad9872cfb0d8f40b3cc7bda86745a09ea"),
        [new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL")] = new("ABIGAIL",129,1u,false,[187, 188, 195, 193, 187, 195, 198],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PSYDUCK",54,"PSYDUCK",35,0,[202, 205, 211, 190, 207, 189, 197],["pret:MOVE_DISABLE", "pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP"],"ITEM_NONE"),new("pret:SPECIES_PSYDUCK",54,"PSYDUCK",36,0,[202, 205, 211, 190, 207, 189, 197],["pret:MOVE_DISABLE", "pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP"],"ITEM_NONE"),new("pret:SPECIES_GOLDUCK",55,"GOLDUCK",37,0,[193, 201, 198, 190, 207, 189, 197],["pret:MOVE_DISABLE", "pret:MOVE_CONFUSION", "pret:MOVE_SCREECH", "pret:MOVE_PSYCH_UP"],"ITEM_NONE")],148,"c67039efa81699fcc3caff7837513068f67237e86855e3d045296b8772fd0d5b"),
        [new("pret:TRAINER_SWIMMER_FEMALE_MARIA")] = new("MARIA",129,1u,false,[199, 187, 204, 195, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SEADRA",117,"SEADRA",37,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"],"ITEM_NONE"),new("pret:SPECIES_SEADRA",117,"SEADRA",37,0,[205, 191, 187, 190, 204, 187],["pret:MOVE_SMOKESCREEN", "pret:MOVE_LEER", "pret:MOVE_WATER_GUN", "pret:MOVE_TWISTER"],"ITEM_NONE")],148,"a882b4f2dcf7429033852581906cf63221486cc4b145b37afac8c4a349dd7cfc"),
        [new("pret:TRAINER_SWIMMER_MALE_FINN")] = new("FINN",8,1u,false,[192, 195, 200, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_STARMIE",121,"STARMIE",38,0,[205, 206, 187, 204, 199, 195, 191],["pret:MOVE_RAPID_SPIN", "pret:MOVE_RECOVER", "pret:MOVE_SWIFT", "pret:MOVE_CONFUSE_RAY"],"ITEM_NONE")],152,"1af6c90e217e52c3bfd8cf62c9ba73324032985da72b347e71a2dab0d2213850"),
        [new("pret:TRAINER_SWIMMER_MALE_GARRETT")] = new("GARRETT",8,1u,false,[193, 187, 204, 204, 191, 206, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SHELLDER",90,"SHELLDER",35,0,[205, 194, 191, 198, 198, 190, 191, 204],["pret:MOVE_ICICLE_SPEAR", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"],"ITEM_NONE"),new("pret:SPECIES_CLOYSTER",91,"CLOYSTER",35,0,[189, 198, 201, 211, 205, 206, 191, 204],["pret:MOVE_WITHDRAW", "pret:MOVE_SUPERSONIC", "pret:MOVE_AURORA_BEAM", "pret:MOVE_PROTECT"],"ITEM_NONE"),new("pret:SPECIES_WARTORTLE",8,"WARTORTLE",38,0,[209, 187, 204, 206, 201, 204, 206, 198, 191],["pret:MOVE_BITE", "pret:MOVE_RAPID_SPIN", "pret:MOVE_PROTECT", "pret:MOVE_RAIN_DANCE"],"ITEM_NONE")],152,"3228cf644f67b0f582bfda40fe038595ff4fc4050b23bd52eaa4d7fdfc848cfa"),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_43")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CUBONE",104,"CUBONE",37,0,[189, 207, 188, 201, 200, 191],["pret:MOVE_BONEMERANG", "pret:MOVE_RAGE", "pret:MOVE_FALSE_SWIPE", "pret:MOVE_THRASH"],"ITEM_NONE"),new("pret:SPECIES_MAROWAK",105,"MAROWAK",37,0,[199, 187, 204, 201, 209, 187, 197],["pret:MOVE_LEER", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_BONEMERANG", "pret:MOVE_RAGE"],"ITEM_NONE")],1184,"8156501edb3deeeeda3236310d375a921b590c68372d540977ff01612d01710d"),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RATTATA",19,"RATTATA",35,0,[204, 187, 206, 206, 187, 206, 187],["pret:MOVE_HYPER_FANG", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_PURSUIT", "pret:MOVE_SUPER_FANG"],"ITEM_NONE"),new("pret:SPECIES_RATICATE",20,"RATICATE",35,0,[204, 187, 206, 195, 189, 187, 206, 191],["pret:MOVE_QUICK_ATTACK", "pret:MOVE_HYPER_FANG", "pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT"],"ITEM_NONE"),new("pret:SPECIES_SANDSHREW",27,"SANDSHREW",35,0,[205, 187, 200, 190, 205, 194, 204, 191, 209],["pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_SLASH", "pret:MOVE_SWIFT"],"ITEM_NONE"),new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",35,0,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_SLASH", "pret:MOVE_SWIFT"],"ITEM_NONE")],1120,"bd1287c3e0191f89c43151d5191957c8cf5bbda6f5ccabc9b45e27fccad49216"),
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
        SeviiRubyOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SeviiRubyOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("sevii_ruby_trainer_catalog");
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("sevii_ruby_trainer_species");
            if (member.CustomMoves.Any(m => !opponents.Mechanics.Catalog.Moves.TryGetValue(m, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
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
        foreach (var (id, hash) in TrainerHashes)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault ||
                    m.EncodedSpeciesName.IsDefaultOrEmpty) || trainer.Items.IsDefault ||
                trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != hash)
                throw new NotSupportedException("sevii_ruby_trainer_source");
        }
        foreach (var (script, hash) in BattleHashes)
        {
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script ||
                CampaignSourceFingerprint.Compute(battle) != hash)
                throw new NotSupportedException("sevii_ruby_trainer_battle_source");
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleHashes.TryGetValue(battle.ScriptEntry, out var hash) || CampaignSourceFingerprint.Compute(battle) != hash)
            throw new NotSupportedException("sevii_ruby_trainer_battle_source");
    }
}
