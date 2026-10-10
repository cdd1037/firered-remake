using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All eleven initial Route8 singles teams, with exact source NPC construction.</summary>
public static class CeladonApproachTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-celadon-approach-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_LASS_JULIA")] = new(131,"JULIA","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("CLEFAIRY",35,22,0,["SING","DOUBLE_SLAP","FOLLOW_ME","MINIMIZE"]),new("CLEFAIRY",35,22,0,["SING","DOUBLE_SLAP","FOLLOW_ME","MINIMIZE"])],"Route8_EventScript_Julia","Route8_Text_JuliaIntro","Route8_Text_JuliaDefeat","Route8_Text_JuliaPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_GAMER_RICH")] = new(264,"RICH","TRAINER_CLASS_GAMER",72,"TRAINER_PIC_GAMER",97,0,18,0,1,[new("GROWLITHE",58,24,0,["ROAR","EMBER","LEER","ODOR_SLEUTH"]),new("VULPIX",37,24,0,["ROAR","QUICK_ATTACK","WILL_O_WISP","CONFUSE_RAY"])],"Route8_EventScript_Rich","Route8_Text_RichIntro","Route8_Text_RichDefeat","Route8_Text_RichPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_SUPER_NERD_GLENN")] = new(172,"GLENN","TRAINER_CLASS_SUPER_NERD",64,"TRAINER_PIC_SUPER_NERD",89,3,6,0,1,[new("GRIMER",88,22,0,["HARDEN","DISABLE","SLUDGE","MINIMIZE"]),new("MUK",89,22,0,["HARDEN","DISABLE","SLUDGE","MINIMIZE"]),new("GRIMER",88,22,0,["HARDEN","DISABLE","SLUDGE","MINIMIZE"])],"Route8_EventScript_Glenn","Route8_Text_GlennIntro","Route8_Text_GlennDefeat","Route8_Text_GlennPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_LASS_PAIGE")] = new(128,"PAIGE","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("NIDORAN_F",29,23,0,["DOUBLE_KICK","POISON_STING","BITE","HELPING_HAND"]),new("NIDORINA",30,23,0,["TAIL_WHIP","DOUBLE_KICK","POISON_STING","BITE"])],"Route8_EventScript_Paige","Route8_Text_PaigeIntro","Route8_Text_PaigeDefeat","Route8_Text_PaigePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_SUPER_NERD_LESLIE")] = new(173,"LESLIE","TRAINER_CLASS_SUPER_NERD",64,"TRAINER_PIC_SUPER_NERD",89,3,6,1,1,[new("KOFFING",109,26,0,["SLUDGE","SMOKESCREEN","SMOG","SELF_DESTRUCT"])],"Route8_EventScript_Leslie","Route8_Text_LeslieIntro","Route8_Text_LeslieDefeat","Route8_Text_LesliePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_LASS_ANDREA")] = new(129,"ANDREA","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("MEOWTH",52,24,0,["SCRATCH","GROWL","BITE","PAY_DAY"]),new("MEOWTH",52,24,0,["SCRATCH","GROWL","BITE","PAY_DAY"]),new("MEOWTH",52,24,0,["SCRATCH","GROWL","BITE","PAY_DAY"])],"Route8_EventScript_Andrea","Route8_Text_AndreaIntro","Route8_Text_AndreaDefeat","Route8_Text_AndreaPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_LASS_MEGAN")] = new(130,"MEGAN","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("PIDGEY",16,19,0,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("RATTATA",19,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("NIDORAN_M",32,19,0,["PECK","FOCUS_ENERGY","DOUBLE_KICK","POISON_STING"]),new("MEOWTH",52,19,0,["SCRATCH","GROWL","BITE","PAY_DAY"]),new("PIKACHU",25,19,0,["TAIL_WHIP","THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM"])],"Route8_EventScript_Megan","Route8_Text_MeganIntro","Route8_Text_MeganDefeat","Route8_Text_MeganPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_GAMER_STAN")] = new(262,"STAN","TRAINER_CLASS_GAMER",72,"TRAINER_PIC_GAMER",97,0,18,0,1,[new("POLIWAG",60,22,0,["BUBBLE","HYPNOSIS","WATER_GUN","DOUBLE_SLAP"]),new("POLIWAG",60,22,0,["BUBBLE","HYPNOSIS","WATER_GUN","DOUBLE_SLAP"]),new("POLIWHIRL",61,22,0,["BUBBLE","HYPNOSIS","WATER_GUN","DOUBLE_SLAP"])],"Route8_EventScript_Stan","Route8_Text_StanIntro","Route8_Text_StanDefeat","Route8_Text_StanPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_SUPER_NERD_AIDAN")] = new(171,"AIDAN","TRAINER_CLASS_SUPER_NERD",64,"TRAINER_PIC_SUPER_NERD",89,3,6,1,1,[new("VOLTORB",100,20,0,["SONIC_BOOM","SCREECH","TACKLE","CHARGE"]),new("KOFFING",109,20,0,["SLUDGE","SMOG","TACKLE","POISON_GAS"]),new("VOLTORB",100,20,0,["SONIC_BOOM","SCREECH","TACKLE","CHARGE"]),new("MAGNEMITE",81,20,0,["SONIC_BOOM","SUPERSONIC","THUNDER_SHOCK","TACKLE"])],"Route8_EventScript_Aidan","Route8_Text_AidanIntro","Route8_Text_AidanDefeat","Route8_Text_AidanPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_BIKER_RICARDO")] = new(535,"RICARDO","TRAINER_CLASS_BIKER",66,"TRAINER_PIC_BIKER",91,0,5,1,1,[new("KOFFING",109,22,0,["SLUDGE","SMOG","TACKLE","POISON_GAS"]),new("KOFFING",109,22,0,["SLUDGE","SMOG","TACKLE","POISON_GAS"]),new("GRIMER",88,23,0,["MINIMIZE","SLUDGE","DISABLE","POUND"])],"Route8_EventScript_Ricardo","Route8_Text_RicardoIntro","Route8_Text_RicardoDefeat","Route8_Text_RicardoPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_BIKER_JAREN")] = new(536,"JAREN","TRAINER_CLASS_BIKER",66,"TRAINER_PIC_BIKER",91,0,5,0,1,[new("GRIMER",88,24,0,["HARDEN","DISABLE","SLUDGE","MINIMIZE"]),new("GRIMER",88,24,0,["HARDEN","DISABLE","SLUDGE","MINIMIZE"])],"Route8_EventScript_Jaren","Route8_Text_JarenIntro","Route8_Text_JarenDefeat","Route8_Text_JarenPostBattle","data/scripts/trainers.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(CeladonApproachTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != 94 || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("CeladonApproach requires the exact ninety-four-species catalog and one initial source CeladonApproach team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !CeladonApproachBattleScope.ValidSpecies(species))
                throw new NotSupportedException("CeladonApproach trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("CeladonApproach initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !CeladonApproachBattleScope.ValidDefinitionForCatalog(pokemon, m)))
                throw new NotSupportedException("CeladonApproach initial move definition differs.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(CeladonApproachTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=11 || catalog.Battles.Count!=11)
            throw new NotSupportedException("Exact eleven-team CeladonApproach catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("CeladonApproach trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("CeladonApproach source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified CeladonApproach battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=0 || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null || b.Source.Path!=e.Path)
            throw new NotSupportedException("CeladonApproach initial battle source differs.");
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
