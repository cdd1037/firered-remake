using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All nineteen initial Pokemon Tower singles teams, with exact source NPC construction.</summary>
public static class PokemonTowerTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-pokemon-tower-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path, string[] Items, string? PostScript, string? Music);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_RIVAL_POKEMON_TOWER_SQUIRTLE")] = new(429,"TERRY","TRAINER_CLASS_RIVAL_LATE",89,"TRAINER_PIC_RIVAL_LATE",124,0,9,0,7,[new("PIDGEOTTO",17,25,100,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("GROWLITHE",58,23,100,["ROAR","EMBER","LEER","ODOR_SLEUTH"]),new("EXEGGCUTE",102,22,100,["HYPNOSIS","REFLECT","LEECH_SEED","CONFUSION"]),new("KADABRA",64,20,100,["TELEPORT","KINESIS","CONFUSION","DISABLE"]),new("WARTORTLE",8,25,100,["WITHDRAW","WATER_GUN","BITE","RAPID_SPIN"])],"PokemonTower_2F_EventScript_RivalSquirtle","PokemonTower_2F_Text_RivalIntro","PokemonTower_2F_Text_RivalDefeat","PokemonTower_2F_Text_RivalPostBattle","data/maps/PokemonTower_2F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_RIVAL_POKEMON_TOWER_BULBASAUR")] = new(430,"TERRY","TRAINER_CLASS_RIVAL_LATE",89,"TRAINER_PIC_RIVAL_LATE",124,0,9,0,7,[new("PIDGEOTTO",17,25,100,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("GYARADOS",130,23,100,["THRASH","BITE"]),new("GROWLITHE",58,22,100,["ROAR","EMBER","LEER","ODOR_SLEUTH"]),new("KADABRA",64,20,100,["TELEPORT","KINESIS","CONFUSION","DISABLE"]),new("IVYSAUR",2,25,100,["VINE_WHIP","POISON_POWDER","SLEEP_POWDER","RAZOR_LEAF"])],"PokemonTower_2F_EventScript_RivalBulbasaur","PokemonTower_2F_Text_RivalIntro","PokemonTower_2F_Text_RivalDefeat","PokemonTower_2F_Text_RivalPostBattle","data/maps/PokemonTower_2F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_RIVAL_POKEMON_TOWER_CHARMANDER")] = new(431,"TERRY","TRAINER_CLASS_RIVAL_LATE",89,"TRAINER_PIC_RIVAL_LATE",124,0,9,0,7,[new("PIDGEOTTO",17,25,100,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("EXEGGCUTE",102,23,100,["HYPNOSIS","REFLECT","LEECH_SEED","CONFUSION"]),new("GYARADOS",130,22,100,["THRASH","BITE"]),new("KADABRA",64,20,100,["TELEPORT","KINESIS","CONFUSION","DISABLE"]),new("CHARMELEON",5,25,100,["GROWL","EMBER","METAL_CLAW","SMOKESCREEN"])],"PokemonTower_2F_EventScript_RivalCharmander","PokemonTower_2F_Text_RivalIntro","PokemonTower_2F_Text_RivalDefeat","PokemonTower_2F_Text_RivalPostBattle","data/maps/PokemonTower_2F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_PATRICIA")] = new(441,"PATRICIA","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_3F_EventScript_Patricia","PokemonTower_3F_Text_PatriciaIntro","PokemonTower_3F_Text_PatriciaDefeat","PokemonTower_3F_Text_PatriciaPostBattle","data/maps/PokemonTower_3F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_CARLY")] = new(442,"CARLY","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,24,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_3F_EventScript_Carly","PokemonTower_3F_Text_CarlyIntro","PokemonTower_3F_Text_CarlyDefeat","PokemonTower_3F_Text_CarlyPostBattle","data/maps/PokemonTower_3F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_HOPE")] = new(443,"HOPE","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,23,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_3F_EventScript_Hope","PokemonTower_3F_Text_HopeIntro","PokemonTower_3F_Text_HopeDefeat","PokemonTower_3F_Text_HopePostBattle","data/maps/PokemonTower_3F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_PAULA")] = new(444,"PAULA","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,24,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_4F_EventScript_Paula","PokemonTower_4F_Text_PaulaIntro","PokemonTower_4F_Text_PaulaDefeat","PokemonTower_4F_Text_PaulaPostBattle","data/maps/PokemonTower_4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_LAUREL")] = new(445,"LAUREL","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,23,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"]),new("GASTLY",92,23,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_4F_EventScript_Laurel","PokemonTower_4F_Text_LaurelIntro","PokemonTower_4F_Text_LaurelDefeat","PokemonTower_4F_Text_LaurelPostBattle","data/maps/PokemonTower_4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_JODY")] = new(446,"JODY","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_4F_EventScript_Jody","PokemonTower_4F_Text_JodyIntro","PokemonTower_4F_Text_JodyDefeat","PokemonTower_4F_Text_JodyPostBattle","data/maps/PokemonTower_4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_TAMMY")] = new(447,"TAMMY","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("HAUNTER",93,23,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_5F_EventScript_Tammy","PokemonTower_5F_Text_TammyIntro","PokemonTower_5F_Text_TammyDefeat","PokemonTower_5F_Text_TammyPostBattle","data/maps/PokemonTower_5F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_RUTH")] = new(448,"RUTH","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_5F_EventScript_Ruth","PokemonTower_5F_Text_RuthIntro","PokemonTower_5F_Text_RuthDefeat","PokemonTower_5F_Text_RuthPostBattle","data/maps/PokemonTower_5F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_KARINA")] = new(449,"KARINA","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,24,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_5F_EventScript_Karina","PokemonTower_5F_Text_KarinaIntro","PokemonTower_5F_Text_KarinaDefeat","PokemonTower_5F_Text_KarinaPostBattle","data/maps/PokemonTower_5F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_JANAE")] = new(450,"JANAE","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_5F_EventScript_Janae","PokemonTower_5F_Text_JanaeIntro","PokemonTower_5F_Text_JanaeDefeat","PokemonTower_5F_Text_JanaePostBattle","data/maps/PokemonTower_5F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_ANGELICA")] = new(451,"ANGELICA","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"]),new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"]),new("GASTLY",92,22,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_6F_EventScript_Angelica","PokemonTower_6F_Text_AngelicaIntro","PokemonTower_6F_Text_AngelicaDefeat","PokemonTower_6F_Text_AngelicaPostBattle","data/maps/PokemonTower_6F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_EMILIA")] = new(452,"EMILIA","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,24,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_6F_EventScript_Emilia","PokemonTower_6F_Text_EmiliaIntro","PokemonTower_6F_Text_EmiliaDefeat","PokemonTower_6F_Text_EmiliaPostBattle","data/maps/PokemonTower_6F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_CHANNELER_JENNIFER")] = new(453,"JENNIFER","TRAINER_CLASS_CHANNELER",91,"TRAINER_PIC_CHANNELER",126,131,8,0,1,[new("GASTLY",92,24,0,["SPITE","CURSE","NIGHT_SHADE","CONFUSE_RAY"])],"PokemonTower_6F_EventScript_Jennifer","PokemonTower_6F_Text_JenniferIntro","PokemonTower_6F_Text_JenniferDefeat","PokemonTower_6F_Text_JenniferPostBattle","data/maps/PokemonTower_6F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_19")] = new(369,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("ZUBAT",41,25,0,["ASTONISH","SUPERSONIC","BITE","WING_ATTACK"]),new("ZUBAT",41,25,0,["ASTONISH","SUPERSONIC","BITE","WING_ATTACK"]),new("GOLBAT",42,25,0,["ASTONISH","SUPERSONIC","BITE","WING_ATTACK"])],"PokemonTower_7F_EventScript_Grunt1","PokemonTower_7F_Text_Grunt1Intro","PokemonTower_7F_Text_Grunt1Defeat","PokemonTower_7F_Text_Grunt1PostBattle","data/maps/PokemonTower_7F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"PokemonTower_7F_EventScript_DefeatedGrunt1",null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_20")] = new(370,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("KOFFING",109,26,0,["SMOG","SELF_DESTRUCT","SLUDGE","SMOKESCREEN"]),new("DROWZEE",96,26,0,["DISABLE","CONFUSION","HEADBUTT","POISON_GAS"])],"PokemonTower_7F_EventScript_Grunt2","PokemonTower_7F_Text_Grunt2Intro","PokemonTower_7F_Text_Grunt2Defeat","PokemonTower_7F_Text_Grunt2PostBattle","data/maps/PokemonTower_7F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"PokemonTower_7F_EventScript_DefeatedGrunt2",null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_21")] = new(371,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("ZUBAT",41,23,0,["ASTONISH","SUPERSONIC","BITE","WING_ATTACK"]),new("RATTATA",19,23,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","FOCUS_ENERGY"]),new("RATICATE",20,23,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","SCARY_FACE"]),new("ZUBAT",41,23,0,["ASTONISH","SUPERSONIC","BITE","WING_ATTACK"])],"PokemonTower_7F_EventScript_Grunt3","PokemonTower_7F_Text_Grunt3Intro","PokemonTower_7F_Text_Grunt3Defeat","PokemonTower_7F_Text_Grunt3PostBattle","data/maps/PokemonTower_7F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"PokemonTower_7F_EventScript_DefeatedGrunt3",null),
    }.ToImmutableDictionary();
    public static TrainerId RivalForStarter(int sourceStarterValue) => new("pret:TRAINER_RIVAL_POKEMON_TOWER_" + (sourceStarterValue switch { 0 => "CHARMANDER", 1 => "BULBASAUR", 2 => "SQUIRTLE", _ => throw new ArgumentOutOfRangeException(nameof(sourceStarterValue)) }));
    public static bool IsRival(TrainerId id) => id.Value is "pret:TRAINER_RIVAL_POKEMON_TOWER_SQUIRTLE" or "pret:TRAINER_RIVAL_POKEMON_TOWER_BULBASAUR" or "pret:TRAINER_RIVAL_POKEMON_TOWER_CHARMANDER";
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(PokemonTowerTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count is not (101 or 102) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("PokemonTower requires the exact one-hundred-and-one-species catalog and one initial source PokemonTower team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !PokemonTowerBattleScope.ValidSpecies(species))
                throw new NotSupportedException("PokemonTower trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("PokemonTower initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !PokemonTowerBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("PokemonTower initial move definition differs.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(PokemonTowerTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=19 || catalog.Battles.Count!=19)
            throw new NotSupportedException("Exact nineteen-team PokemonTower catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(e.Items) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("PokemonTower trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("PokemonTower source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified PokemonTower battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(IsRival(b.Trainer) ? 3 : e.PostScript is null ? 0 : 2) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(IsRival(b.Trainer) ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music || b.Source.Path!=e.Path)
            throw new NotSupportedException("PokemonTower initial battle source differs.");
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
