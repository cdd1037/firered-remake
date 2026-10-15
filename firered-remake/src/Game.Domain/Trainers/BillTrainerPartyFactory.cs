using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record BillTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>Exact nineteen initial Bill-route teams, no rematches. Reuses original NPC construction.</summary>
public static class BillTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-bill-trainers-v1";
    public static TrainerId Rocket { get; } = new("pret:TRAINER_TEAM_ROCKET_GRUNT_6");
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BUG_CATCHER_CALE")] = new(110,"CALE","TRAINER_CLASS_BUG_CATCHER",58,"TRAINER_PIC_BUG_CATCHER",83,0,3,0,[new("CATERPIE",10,10,0,["TACKLE","STRING_SHOT"]),new("WEEDLE",13,10,0,["POISON_STING","STRING_SHOT"]),new("METAPOD",11,10,0,["HARDEN"]),new("KAKUNA",14,10,0,["HARDEN"])],"Route24_EventScript_Cale","Route24_Text_CaleIntro","Route24_Text_CaleDefeat","Route24_Text_CalePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_ETHAN")] = new(144,"ETHAN","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,[new("MANKEY",56,18,0,["LEER","LOW_KICK","KARATE_CHOP","FURY_SWIPES"])],"Route24_EventScript_Ethan","Route24_Text_EthanIntro","Route24_Text_EthanDefeat","Route24_Text_EthanPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_FLINT")] = new(471,"FLINT","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,[new("RATTATA",19,14,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("EKANS",23,14,0,["WRAP","LEER","POISON_STING","BITE"])],"Route25_EventScript_Flint","Route25_Text_FlintIntro","Route25_Text_FlintDefeat","Route25_Text_FlintPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_SHANE")] = new(143,"SHANE","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,[new("RATTATA",19,14,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("EKANS",23,14,0,["WRAP","LEER","POISON_STING","BITE"])],"Route24_EventScript_Shane","Route24_Text_ShaneIntro","Route24_Text_ShaneDefeat","Route24_Text_ShanePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_FRANKLIN")] = new(182,"FRANKLIN","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,[new("MACHOP",66,15,0,["LOW_KICK","LEER","FOCUS_ENERGY","KARATE_CHOP"]),new("GEODUDE",74,15,0,["TACKLE","DEFENSE_CURL","MUD_SPORT","ROCK_THROW"])],"Route25_EventScript_Franklin","Route25_Text_FranklinIntro","Route25_Text_FranklinDefeat","Route25_Text_FranklinPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_NOB")] = new(183,"NOB","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,[new("GEODUDE",74,13,0,["TACKLE","DEFENSE_CURL","MUD_SPORT","ROCK_THROW"]),new("GEODUDE",74,13,0,["TACKLE","DEFENSE_CURL","MUD_SPORT","ROCK_THROW"]),new("MACHOP",66,13,0,["LOW_KICK","LEER","FOCUS_ENERGY","KARATE_CHOP"]),new("GEODUDE",74,13,0,["TACKLE","DEFENSE_CURL","MUD_SPORT","ROCK_THROW"])],"Route25_EventScript_Nob","Route25_Text_NobIntro","Route25_Text_NobDefeat","Route25_Text_NobPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_WAYNE")] = new(184,"WAYNE","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,[new("ONIX",95,17,0,["TACKLE","SCREECH","BIND","ROCK_THROW"])],"Route25_EventScript_Wayne","Route25_Text_WayneIntro","Route25_Text_WayneDefeat","Route25_Text_WaynePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_LASS_ALI")] = new(123,"ALI","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,[new("PIDGEY",16,12,0,["TACKLE","SAND_ATTACK","GUST"]),new("ODDISH",43,12,0,["ABSORB","SWEET_SCENT"]),new("BELLSPROUT",69,12,0,["VINE_WHIP","GROWTH","WRAP"])],"Route24_EventScript_Ali","Route24_Text_AliIntro","Route24_Text_AliDefeat","Route24_Text_AliPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_LASS_HALEY")] = new(125,"HALEY","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,[new("ODDISH",43,13,0,["ABSORB","SWEET_SCENT"]),new("PIDGEY",16,13,0,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("ODDISH",43,13,0,["ABSORB","SWEET_SCENT"])],"Route25_EventScript_Haley","Route25_Text_HaleyIntro","Route25_Text_HaleyDefeat","Route25_Text_HaleyPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_LASS_RELI")] = new(122,"RELI","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,[new("NIDORAN_M",32,16,0,["LEER","PECK","FOCUS_ENERGY","DOUBLE_KICK"]),new("NIDORAN_F",29,16,0,["GROWL","SCRATCH","TAIL_WHIP","DOUBLE_KICK"])],"Route24_EventScript_Reli","Route24_Text_ReliIntro","Route24_Text_ReliDefeat","Route24_Text_ReliPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_KELSEY")] = new(153,"KELSEY","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,[new("NIDORAN_M",32,15,0,["LEER","PECK","FOCUS_ENERGY","DOUBLE_KICK"]),new("NIDORAN_F",29,15,0,["GROWL","SCRATCH","TAIL_WHIP","DOUBLE_KICK"])],"Route25_EventScript_Kelsey","Route25_Text_KelseyIntro","Route25_Text_KelseyDefeat","Route25_Text_KelseyPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_RIVAL_CERULEAN_BULBASAUR")] = new(333,"TERRY","TRAINER_CLASS_RIVAL_EARLY",81,"TRAINER_PIC_RIVAL_EARLY",106,0,4,1,[new("PIDGEOTTO",17,17,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("ABRA",63,16,50,["TELEPORT","NONE","NONE","NONE"]),new("RATTATA",19,15,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","NONE"]),new("BULBASAUR",1,18,100,["SLEEP_POWDER","POISON_POWDER","VINE_WHIP","LEECH_SEED"])],"CeruleanCity_EventScript_RivalBulbasaur","","CeruleanCity_Text_RivalDefeat","","data/maps/CeruleanCity/scripts.inc"),
        [new("pret:TRAINER_RIVAL_CERULEAN_CHARMANDER")] = new(334,"TERRY","TRAINER_CLASS_RIVAL_EARLY",81,"TRAINER_PIC_RIVAL_EARLY",106,0,4,1,[new("PIDGEOTTO",17,17,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("ABRA",63,16,50,["TELEPORT","NONE","NONE","NONE"]),new("RATTATA",19,15,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","NONE"]),new("CHARMANDER",4,18,100,["METAL_CLAW","EMBER","GROWL","SCRATCH"])],"CeruleanCity_EventScript_RivalCharmander","","CeruleanCity_Text_RivalDefeat","","data/maps/CeruleanCity/scripts.inc"),
        [new("pret:TRAINER_RIVAL_CERULEAN_SQUIRTLE")] = new(332,"TERRY","TRAINER_CLASS_RIVAL_EARLY",81,"TRAINER_PIC_RIVAL_EARLY",106,0,4,1,[new("PIDGEOTTO",17,17,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("ABRA",63,16,50,["TELEPORT","NONE","NONE","NONE"]),new("RATTATA",19,15,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","NONE"]),new("SQUIRTLE",7,18,100,["TACKLE","TAIL_WHIP","WITHDRAW","WATER_GUN"])],"CeruleanCity_EventScript_RivalSquirtle","","CeruleanCity_Text_RivalDefeat","","data/maps/CeruleanCity/scripts.inc"),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_6")] = new(356,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,[new("EKANS",23,15,0,["WRAP","LEER","POISON_STING","BITE"]),new("ZUBAT",41,15,0,["LEECH_LIFE","ASTONISH","SUPERSONIC"])],"Route24_EventScript_BattleRocket","","Route24_Text_RocketDefeat","Route24_Text_YoudBecomeTopRocketLeader","data/maps/Route24/scripts.inc"),
        [new("pret:TRAINER_YOUNGSTER_CHAD")] = new(95,"CHAD","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,[new("EKANS",23,14,0,["WRAP","LEER","POISON_STING","BITE"]),new("SANDSHREW",27,14,0,["SCRATCH","DEFENSE_CURL","SAND_ATTACK"])],"Route25_EventScript_Chad","Route25_Text_ChadIntro","Route25_Text_ChadDefeat","Route25_Text_ChadPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_YOUNGSTER_DAN")] = new(94,"DAN","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,[new("SLOWPOKE",79,17,0,["TACKLE","GROWL","WATER_GUN","CONFUSION"])],"Route25_EventScript_Dan","Route25_Text_DanIntro","Route25_Text_DanDefeat","Route25_Text_DanPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_YOUNGSTER_JOEY")] = new(93,"JOEY","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,[new("RATTATA",19,15,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("SPEAROW",21,15,0,["PECK","GROWL","LEER","FURY_ATTACK"])],"Route25_EventScript_Joey","Route25_Text_JoeyIntro","Route25_Text_JoeyDefeat","Route25_Text_JoeyPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_YOUNGSTER_TIMMY")] = new(92,"TIMMY","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,[new("SANDSHREW",27,14,0,["SCRATCH","DEFENSE_CURL","SAND_ATTACK"]),new("EKANS",23,14,0,["WRAP","LEER","POISON_STING","BITE"])],"Route24_EventScript_Timmy","Route24_Text_TimmyIntro","Route24_Text_TimmyDefeat","Route24_Text_TimmyPostBattle","data/scripts/trainers.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerId ForPlayerStarter(SpeciesId starter) => CeruleanTrainerPartyFactory.ForPlayerStarter(starter);
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(BillTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("Bill requires the exact sixty-eight-species catalog and one initial team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i]; var species=pokemon.Species[member.Species];
            if (!BillBattleScope.ValidSpecies(species)) throw new NotSupportedException("Bill trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("Bill initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !BillBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("Bill initial move definition differs.");
        }
        var created=TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        return trainer.PartyFlags==0 ? created : created with { Party=created.Party.Select((m,i)=>m with { Pokemon=m.Pokemon with {
            Moves=trainer.Party[i].CustomMoves.TakeWhile(x=>x.Value!="pret:MOVE_NONE").Select(x=>new PokemonMoveSlot(x,pokemon.Moves[x].Pp,pokemon.Moves[x].Pp)).ToImmutableArray()
        }}).ToImmutableArray() };
    }
    public static void ValidateCatalog(BillTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=19 || catalog.Battles.Count!=19)
            throw new NotSupportedException("Exact nineteen-team Bill catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=(e.Flags==1?7u:1u) || t.Items.IsDefault ||
                !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.Flags==1?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("Bill trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("Bill source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle)) throw new NotSupportedException("Missing Bill battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Intro==""?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Intro==""?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null || b.Source.Path!=e.Path)
            throw new NotSupportedException("Bill initial battle source differs.");
    }
    internal static bool ValidPartyMember(PokemonCatalog catalog, PokemonInstance mon, TrainerId id, uint? flags=null)
    {
        if(!Expected.TryGetValue(id,out var e) || flags is {} f && f!=(e.Flags==1?7u:1u)) return false;
        uint hash=0;
        foreach(var p in e.Party)
        {
            foreach(int c in Route3TrainerPartyFactory.Encode(e.Name+SpeciesName(p.Symbol))) hash+=(uint)c;
            uint personality=((e.Gender&128)!=0?0x78u:0x88u)+(hash<<8); int iv=p.Iv*31/255;
            if(mon.SourceSpeciesId!=p.Species || mon.Level!=p.Level || mon.Personality!=personality || mon.Ivs!=new SixStats(iv,iv,iv,iv,iv,iv) ||
                mon.Evs!=SixStats.Zero || mon.Moves.Any(m=>m.PpUps!=0)) continue;
            if(mon.Moves.Select(m=>m.Move.Value).SequenceEqual(p.Moves.TakeWhile(x=>x!="NONE").Select(x=>"pret:MOVE_"+x))) return true;
        }
        return false;
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
