using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record Route11TrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>All ten initial Route11 teams, with exact source NPC construction.</summary>
public static class Route11TrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-route11-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_YOUNGSTER_EDDIE")] = new(97,"EDDIE","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,1,[new("EKANS",23,21,0,["LEER","POISON_STING","BITE","GLARE"])],"Route11_EventScript_Eddie","Route11_Text_EddieIntro","Route11_Text_EddieDefeat","Route11_Text_EddiePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_GAMER_HUGO")] = new(258,"HUGO","TRAINER_CLASS_GAMER",72,"TRAINER_PIC_GAMER",97,0,18,0,1,[new("POLIWAG",60,18,0,["BUBBLE","HYPNOSIS","WATER_GUN"]),new("HORSEA",116,18,0,["BUBBLE","SMOKESCREEN","LEER"])],"Route11_EventScript_Hugo","Route11_Text_HugoIntro","Route11_Text_HugoDefeat","Route11_Text_HugoPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_YOUNGSTER_DILLON")] = new(98,"DILLON","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,1,[new("SANDSHREW",27,19,0,["SCRATCH","DEFENSE_CURL","SAND_ATTACK","POISON_STING"]),new("ZUBAT",41,19,0,["LEECH_LIFE","ASTONISH","SUPERSONIC","BITE"])],"Route11_EventScript_Dillon","Route11_Text_DillonIntro","Route11_Text_DillonDefeat","Route11_Text_DillonPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_YOUNGSTER_DAVE")] = new(100,"DAVE","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,1,[new("NIDORAN_M",32,18,0,["PECK","FOCUS_ENERGY","DOUBLE_KICK","POISON_STING"]),new("NIDORINO",33,18,0,["PECK","FOCUS_ENERGY","DOUBLE_KICK","POISON_STING"])],"Route11_EventScript_Dave","Route11_Text_DaveIntro","Route11_Text_DaveDefeat","Route11_Text_DavePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_GAMER_JASPER")] = new(259,"JASPER","TRAINER_CLASS_GAMER",72,"TRAINER_PIC_GAMER",97,0,18,0,1,[new("BELLSPROUT",69,18,0,["GROWTH","WRAP","SLEEP_POWDER","POISON_POWDER"]),new("ODDISH",43,18,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"])],"Route11_EventScript_Jasper","Route11_Text_JasperIntro","Route11_Text_JasperDefeat","Route11_Text_JasperPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_GAMER_DARIAN")] = new(261,"DARIAN","TRAINER_CLASS_GAMER",72,"TRAINER_PIC_GAMER",97,0,18,0,1,[new("GROWLITHE",58,18,0,["BITE","ROAR","EMBER","LEER"]),new("VULPIX",37,18,0,["TAIL_WHIP","ROAR","QUICK_ATTACK","WILL_O_WISP"])],"Route11_EventScript_Darian","Route11_Text_DarianIntro","Route11_Text_DarianDefeat","Route11_Text_DarianPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_ENGINEER_BRAXTON")] = new(221,"BRAXTON","TRAINER_CLASS_ENGINEER",68,"TRAINER_PIC_ENGINEER",93,0,12,0,1,[new("MAGNEMITE",81,21,0,["THUNDER_SHOCK","SUPERSONIC","SONIC_BOOM","THUNDER_WAVE"])],"Route11_EventScript_Braxton","Route11_Text_BraxtonIntro","Route11_Text_BraxtonDefeat","Route11_Text_BraxtonPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_YOUNGSTER_YASU")] = new(99,"YASU","TRAINER_CLASS_YOUNGSTER",57,"TRAINER_PIC_YOUNGSTER",82,0,4,0,1,[new("RATTATA",19,17,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("RATTATA",19,17,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("RATICATE",20,17,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"])],"Route11_EventScript_Yasu","Route11_Text_YasuIntro","Route11_Text_YasuDefeat","Route11_Text_YasuPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_GAMER_DIRK")] = new(260,"DIRK","TRAINER_CLASS_GAMER",72,"TRAINER_PIC_GAMER",97,0,18,0,1,[new("VOLTORB",100,18,0,["CHARGE","TACKLE","SCREECH","SONIC_BOOM"]),new("MAGNEMITE",81,18,0,["TACKLE","THUNDER_SHOCK","SUPERSONIC","SONIC_BOOM"])],"Route11_EventScript_Dirk","Route11_Text_DirkIntro","Route11_Text_DirkDefeat","Route11_Text_DirkPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_ENGINEER_BERNIE")] = new(222,"BERNIE","TRAINER_CLASS_ENGINEER",68,"TRAINER_PIC_ENGINEER",93,0,12,0,1,[new("MAGNEMITE",81,18,0,["TACKLE","THUNDER_SHOCK","SUPERSONIC","SONIC_BOOM"]),new("MAGNEMITE",81,18,0,["TACKLE","THUNDER_SHOCK","SUPERSONIC","SONIC_BOOM"]),new("MAGNETON",82,18,0,["TACKLE","THUNDER_SHOCK","SUPERSONIC","SONIC_BOOM"])],"Route11_EventScript_Bernie","Route11_Text_BernieIntro","Route11_Text_BernieDefeat","Route11_Text_BerniePostBattle","data/scripts/trainers.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(Route11TrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng, bool rockTunnelRules = false)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(pokemon) : 79) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("Route11 requires the exact seventy-nine-species catalog and one initial source Route11 team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !Route11BattleScope.ValidSpecies(species))
                throw new NotSupportedException("Route11 trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("Route11 initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !Route11BattleScope.ValidDefinition(m)))
                throw new NotSupportedException("Route11 initial move definition differs.");
        }
        // These initial route parties use source default moves; no custom or rematch party is admitted.
        return TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
    }
    public static void ValidateCatalog(Route11TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=10 || catalog.Battles.Count!=10)
            throw new NotSupportedException("Exact ten-team Route11 catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("Route11 trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("Route11 source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified Route11 battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=0 || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null || b.Source.Path!=e.Path)
            throw new NotSupportedException("Route11 initial battle source differs.");
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
