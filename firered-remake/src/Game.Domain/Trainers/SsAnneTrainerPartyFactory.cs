using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record SsAnneTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>All sixteen ordinary S.S. Anne teams and three rival variants, with exact source NPC construction.</summary>
public static class SsAnneTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-ssanne-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path)
    {
        public bool Rival => Id is >= 426 and <= 428;
    }
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_YOUNGSTER_TYLER")] = new(96,"TYLER","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,1,[new("NIDORAN_M",32,21,0,["FOCUS_ENERGY","DOUBLE_KICK","POISON_STING","HORN_ATTACK"])],"SSAnne_1F_Room2_EventScript_Tyler","SSAnne_1F_Room2_Text_TylerIntro","SSAnne_1F_Room2_Text_TylerDefeat","SSAnne_1F_Room2_Text_TylerPostBattle","data/maps/SSAnne_1F_Room2/scripts.inc"),
        [new("pret:TRAINER_LASS_ANN")] = new(126,"ANN","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("PIDGEY",16,18,0,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("NIDORAN_F",29,18,0,["SCRATCH","TAIL_WHIP","DOUBLE_KICK","POISON_STING"])],"SSAnne_1F_Room2_EventScript_Ann","SSAnne_1F_Room2_Text_AnnIntro","SSAnne_1F_Room2_Text_AnnDefeat","SSAnne_1F_Room2_Text_AnnPostBattle","data/maps/SSAnne_1F_Room2/scripts.inc"),
        [new("pret:TRAINER_LASS_DAWN")] = new(127,"DAWN","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("RATTATA",19,18,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("PIKACHU",25,18,0,["TAIL_WHIP","THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM"])],"SSAnne_2F_Room4_EventScript_Dawn","SSAnne_2F_Room4_Text_DawnIntro","SSAnne_2F_Room4_Text_DawnDefeat","SSAnne_2F_Room4_Text_DawnPostBattle","data/maps/SSAnne_2F_Room4/scripts.inc"),
        [new("pret:TRAINER_SAILOR_EDMOND")] = new(134,"EDMOND","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("MACHOP",66,18,0,["LOW_KICK","LEER","FOCUS_ENERGY","KARATE_CHOP"]),new("SHELLDER",90,18,0,["TACKLE","WITHDRAW","ICICLE_SPEAR","SUPERSONIC"])],"SSAnne_Deck_EventScript_Edmond","SSAnne_Deck_Text_EdmondIntro","SSAnne_Deck_Text_EdmondDefeat","SSAnne_Deck_Text_EdmondPostBattle","data/maps/SSAnne_Deck/scripts.inc"),
        [new("pret:TRAINER_SAILOR_TREVOR")] = new(135,"TREVOR","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("MACHOP",66,17,0,["LOW_KICK","LEER","FOCUS_ENERGY","KARATE_CHOP"]),new("TENTACOOL",72,17,0,["POISON_STING","SUPERSONIC","CONSTRICT"])],"SSAnne_Deck_EventScript_Trevor","SSAnne_Deck_Text_TrevorIntro","SSAnne_Deck_Text_TrevorDefeat","SSAnne_Deck_Text_TrevorPostBattle","data/maps/SSAnne_Deck/scripts.inc"),
        [new("pret:TRAINER_SAILOR_LEONARD")] = new(136,"LEONARD","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("SHELLDER",90,21,0,["TACKLE","WITHDRAW","ICICLE_SPEAR","SUPERSONIC"])],"SSAnne_B1F_Room4_EventScript_Leonard","SSAnne_B1F_Room4_Text_LeonardIntro","SSAnne_B1F_Room4_Text_LeonardDefeat","SSAnne_B1F_Room4_Text_LeonardPostBattle","data/maps/SSAnne_B1F_Room4/scripts.inc"),
        [new("pret:TRAINER_SAILOR_DUNCAN")] = new(137,"DUNCAN","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("HORSEA",116,17,0,["BUBBLE","SMOKESCREEN","LEER"]),new("SHELLDER",90,17,0,["TACKLE","WITHDRAW","ICICLE_SPEAR","SUPERSONIC"]),new("TENTACOOL",72,17,0,["POISON_STING","SUPERSONIC","CONSTRICT"])],"SSAnne_B1F_Room4_EventScript_Duncan","SSAnne_B1F_Room4_Text_DuncanIntro","SSAnne_B1F_Room4_Text_DuncanDefeat","SSAnne_B1F_Room4_Text_DuncanPostBattle","data/maps/SSAnne_B1F_Room4/scripts.inc"),
        [new("pret:TRAINER_SAILOR_HUEY")] = new(138,"HUEY","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("TENTACOOL",72,18,0,["POISON_STING","SUPERSONIC","CONSTRICT"]),new("STARYU",120,18,0,["HARDEN","WATER_GUN","RAPID_SPIN","RECOVER"])],"SSAnne_B1F_Room2_EventScript_Huey","SSAnne_B1F_Room2_Text_HueyIntro","SSAnne_B1F_Room2_Text_HueyDefeat","SSAnne_B1F_Room2_Text_HueyPostBattle","data/maps/SSAnne_B1F_Room2/scripts.inc"),
        [new("pret:TRAINER_SAILOR_DYLAN")] = new(139,"DYLAN","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("HORSEA",116,17,0,["BUBBLE","SMOKESCREEN","LEER"]),new("HORSEA",116,17,0,["BUBBLE","SMOKESCREEN","LEER"]),new("HORSEA",116,17,0,["BUBBLE","SMOKESCREEN","LEER"])],"SSAnne_B1F_Room3_EventScript_Dylan","SSAnne_B1F_Room3_Text_DylanIntro","SSAnne_B1F_Room3_Text_DylanDefeat","SSAnne_B1F_Room3_Text_DylanPostBattle","data/maps/SSAnne_B1F_Room3/scripts.inc"),
        [new("pret:TRAINER_SAILOR_PHILLIP")] = new(140,"PHILLIP","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("MACHOP",66,20,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"])],"SSAnne_B1F_Room1_EventScript_Phillip","SSAnne_B1F_Room1_Text_PhillipIntro","SSAnne_B1F_Room1_Text_PhillipDefeat","SSAnne_B1F_Room1_Text_PhillipPostBattle","data/maps/SSAnne_B1F_Room1/scripts.inc"),
        [new("pret:TRAINER_FISHERMAN_DALE")] = new(223,"DALE","TRAINER_CLASS_FISHERMAN",69,"TRAINER_PIC_FISHERMAN",94,11,9,0,1,[new("GOLDEEN",118,17,0,["TAIL_WHIP","WATER_SPORT","SUPERSONIC","HORN_ATTACK"]),new("TENTACOOL",72,17,0,["POISON_STING","SUPERSONIC","CONSTRICT"]),new("GOLDEEN",118,17,0,["TAIL_WHIP","WATER_SPORT","SUPERSONIC","HORN_ATTACK"])],"SSAnne_2F_Room2_EventScript_Dale","SSAnne_2F_Room2_Text_DaleIntro","SSAnne_2F_Room2_Text_DaleDefeat","SSAnne_2F_Room2_Text_DalePostBattle","data/maps/SSAnne_2F_Room2/scripts.inc"),
        [new("pret:TRAINER_FISHERMAN_BARNY")] = new(224,"BARNY","TRAINER_CLASS_FISHERMAN",69,"TRAINER_PIC_FISHERMAN",94,11,9,0,1,[new("TENTACOOL",72,17,0,["POISON_STING","SUPERSONIC","CONSTRICT"]),new("STARYU",120,17,0,["HARDEN","WATER_GUN","RAPID_SPIN","RECOVER"]),new("SHELLDER",90,17,0,["TACKLE","WITHDRAW","ICICLE_SPEAR","SUPERSONIC"])],"SSAnne_B1F_Room1_EventScript_Barny","SSAnne_B1F_Room1_Text_BarnyIntro","SSAnne_B1F_Room1_Text_BarnyDefeat","SSAnne_B1F_Room1_Text_BarnyPostBattle","data/maps/SSAnne_B1F_Room1/scripts.inc"),
        [new("pret:TRAINER_GENTLEMAN_THOMAS")] = new(421,"THOMAS","TRAINER_CLASS_GENTLEMAN",88,"TRAINER_PIC_GENTLEMAN",123,13,18,0,1,[new("GROWLITHE",58,18,0,["BITE","ROAR","EMBER","LEER"]),new("GROWLITHE",58,18,0,["BITE","ROAR","EMBER","LEER"])],"SSAnne_1F_Room7_EventScript_Thomas","SSAnne_1F_Room7_Text_ThomasIntro","SSAnne_1F_Room7_Text_ThomasDefeat","SSAnne_1F_Room7_Text_ThomasPostBattle","data/maps/SSAnne_1F_Room7/scripts.inc"),
        [new("pret:TRAINER_GENTLEMAN_ARTHUR")] = new(422,"ARTHUR","TRAINER_CLASS_GENTLEMAN",88,"TRAINER_PIC_GENTLEMAN",123,13,18,0,1,[new("NIDORAN_M",32,19,0,["PECK","FOCUS_ENERGY","DOUBLE_KICK","POISON_STING"]),new("NIDORAN_F",29,19,0,["SCRATCH","TAIL_WHIP","DOUBLE_KICK","POISON_STING"])],"SSAnne_1F_Room5_EventScript_Arthur","SSAnne_1F_Room5_Text_ArthurIntro","SSAnne_1F_Room5_Text_ArthurDefeat","SSAnne_1F_Room5_Text_ArthurPostBattle","data/maps/SSAnne_1F_Room5/scripts.inc"),
        [new("pret:TRAINER_RIVAL_SS_ANNE_SQUIRTLE")] = new(426,"TERRY","TRAINER_CLASS_RIVAL_LATE",89,"TRAINER_PIC_RIVAL_LATE",124,0,9,0,7,[new("PIDGEOTTO",17,19,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("RATICATE",20,16,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("KADABRA",64,18,50,["TELEPORT","KINESIS","CONFUSION","DISABLE"]),new("WARTORTLE",8,20,100,["BUBBLE","WITHDRAW","WATER_GUN","BITE"])],"SSAnne_2F_Corridor_EventScript_RivalSquirtle","SSAnne_2F_Corridor_Text_RivalIntro","SSAnne_2F_Corridor_Text_RivalDefeat","SSAnne_2F_Corridor_Text_RivalPostBattle","data/maps/SSAnne_2F_Corridor/scripts.inc"),
        [new("pret:TRAINER_RIVAL_SS_ANNE_BULBASAUR")] = new(427,"TERRY","TRAINER_CLASS_RIVAL_LATE",89,"TRAINER_PIC_RIVAL_LATE",124,0,9,0,7,[new("PIDGEOTTO",17,19,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("RATICATE",20,16,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("KADABRA",64,18,50,["TELEPORT","KINESIS","CONFUSION","DISABLE"]),new("IVYSAUR",2,20,100,["LEECH_SEED","VINE_WHIP","POISON_POWDER","SLEEP_POWDER"])],"SSAnne_2F_Corridor_EventScript_RivalBulbasaur","SSAnne_2F_Corridor_Text_RivalIntro","SSAnne_2F_Corridor_Text_RivalDefeat","SSAnne_2F_Corridor_Text_RivalPostBattle","data/maps/SSAnne_2F_Corridor/scripts.inc"),
        [new("pret:TRAINER_RIVAL_SS_ANNE_CHARMANDER")] = new(428,"TERRY","TRAINER_CLASS_RIVAL_LATE",89,"TRAINER_PIC_RIVAL_LATE",124,0,9,0,7,[new("PIDGEOTTO",17,19,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("RATICATE",20,16,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("KADABRA",64,18,50,["TELEPORT","KINESIS","CONFUSION","DISABLE"]),new("CHARMELEON",5,20,100,["GROWL","EMBER","METAL_CLAW","SMOKESCREEN"])],"SSAnne_2F_Corridor_EventScript_RivalCharmander","SSAnne_2F_Corridor_Text_RivalIntro","SSAnne_2F_Corridor_Text_RivalDefeat","SSAnne_2F_Corridor_Text_RivalPostBattle","data/maps/SSAnne_2F_Corridor/scripts.inc"),
        [new("pret:TRAINER_GENTLEMAN_BROOKS")] = new(482,"BROOKS","TRAINER_CLASS_GENTLEMAN",88,"TRAINER_PIC_GENTLEMAN",123,13,18,0,1,[new("PIKACHU",25,23,0,["THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM","SLAM"])],"SSAnne_2F_Room2_EventScript_Brooks","SSAnne_2F_Room2_Text_BrooksIntro","SSAnne_2F_Room2_Text_BrooksDefeat","SSAnne_2F_Room2_Text_BrooksPostBattle","data/maps/SSAnne_2F_Room2/scripts.inc"),
        [new("pret:TRAINER_GENTLEMAN_LAMAR")] = new(483,"LAMAR","TRAINER_CLASS_GENTLEMAN",88,"TRAINER_PIC_GENTLEMAN",123,13,18,0,1,[new("GROWLITHE",58,17,0,["BITE","ROAR","EMBER","LEER"]),new("PONYTA",77,17,0,["QUICK_ATTACK","GROWL","TAIL_WHIP","EMBER"])],"SSAnne_2F_Room4_EventScript_Lamar","SSAnne_2F_Room4_Text_LamarIntro","SSAnne_2F_Room4_Text_LamarDefeat","SSAnne_2F_Room4_Text_LamarPostBattle","data/maps/SSAnne_2F_Room4/scripts.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerId ForPlayerStarter(SpeciesId starter) => starter.Value switch
    {
        "pret:SPECIES_BULBASAUR" => new("pret:TRAINER_RIVAL_SS_ANNE_SQUIRTLE"),
        "pret:SPECIES_CHARMANDER" => new("pret:TRAINER_RIVAL_SS_ANNE_BULBASAUR"),
        "pret:SPECIES_SQUIRTLE" => new("pret:TRAINER_RIVAL_SS_ANNE_CHARMANDER"),
        _ => throw new NotSupportedException("Unknown original starter.")
    };
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(SsAnneTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng, bool route11Rules = false, bool rockTunnelRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool towerRules = false, bool fuchsiaRules = false)
    {
        towerRules |= fuchsiaRules;
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        if ((Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 98) != (celadonGymRules && !rocketHideoutRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 99) != (rocketHideoutRules && !towerRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) is 101 or 102) != (towerRules && !fuchsiaRules) || (pokemon.Species.Count == 107) != fuchsiaRules) throw new NotSupportedException("Explicit Celadon Gym trainer factory profile required.");
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(pokemon) : route11Rules ? 79 : 73) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("SsAnne requires the exact seventy-three-species catalog and one source ship team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !SsAnneBattleScope.ValidSpecies(species))
                throw new NotSupportedException("SsAnne trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("SsAnne initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !SsAnneBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("SsAnne initial move definition differs.");
        }
        // The ship rival also uses the default-move party format. AI flags are independent.
        return TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
    }
    public static void ValidateCatalog(SsAnneTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=19 || catalog.Battles.Count!=19)
            throw new NotSupportedException("Exact nineteen-team S.S. Anne catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("SsAnne trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("SsAnne source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified SsAnne battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Rival?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Rival?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null || b.Source.Path!=e.Path)
            throw new NotSupportedException("SsAnne initial battle source differs.");
    }
    internal static bool ValidPartyMember(PokemonCatalog catalog, PokemonInstance mon, TrainerId id, uint? flags=null)
    {
        if(!Expected.TryGetValue(id,out var e) || flags is {} f && f!=e.AiFlags) return false;
        uint hash=0;
        foreach(var p in e.Party)
        {
            foreach(int c in Route3TrainerPartyFactory.Encode(e.Name+SpeciesName(p.Symbol))) hash+=(uint)c;
            uint personality=((e.Gender&128)!=0?0x78u:0x88u)+(hash<<8); int iv=p.Iv*31/255;
            if(mon.Species.Value!="pret:SPECIES_"+p.Symbol || mon.SourceSpeciesId!=p.Species || mon.Level!=p.Level || mon.Personality!=personality || mon.Ivs!=new SixStats(iv,iv,iv,iv,iv,iv) ||
                mon.Evs!=SixStats.Zero || mon.Moves.Any(m=>m.PpUps!=0)) continue;
            if(mon.Moves.Select(m=>m.Move.Value).SequenceEqual(p.Moves.TakeWhile(x=>x!="NONE").Select(x=>"pret:MOVE_"+x))) return true;
        }
        return false;
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
