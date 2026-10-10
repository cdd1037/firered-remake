using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All eight initial Route8 singles teams, with exact source NPC construction.</summary>
public static class CeladonGymTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-celadon-gym-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path, string[] Items, string? PostScript, string? Music);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_LASS_KAY")] = new(132,"KAY","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("BELLSPROUT",69,23,0,["SLEEP_POWDER","POISON_POWDER","STUN_SPORE","ACID"]),new("WEEPINBELL",70,23,0,["WRAP","SLEEP_POWDER","POISON_POWDER","STUN_SPORE"])],"CeladonCity_Gym_EventScript_Kay","CeladonCity_Gym_Text_KayIntro","CeladonCity_Gym_Text_KayDefeat","CeladonCity_Gym_Text_KayPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_BEAUTY_BRIDGET")] = new(265,"BRIDGET","TRAINER_CLASS_BEAUTY",73,"TRAINER_PIC_BEAUTY",98,129,18,0,1,[new("ODDISH",43,21,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"]),new("BELLSPROUT",69,21,0,["WRAP","SLEEP_POWDER","POISON_POWDER","STUN_SPORE"]),new("ODDISH",43,21,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"]),new("BELLSPROUT",69,21,0,["WRAP","SLEEP_POWDER","POISON_POWDER","STUN_SPORE"])],"CeladonCity_Gym_EventScript_Bridget","CeladonCity_Gym_Text_BridgetIntro","CeladonCity_Gym_Text_BridgetDefeat","CeladonCity_Gym_Text_BridgetPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_PICNICKER_TINA")] = new(160,"TINA","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("BULBASAUR",1,24,0,["VINE_WHIP","POISON_POWDER","SLEEP_POWDER","RAZOR_LEAF"]),new("IVYSAUR",2,24,0,["VINE_WHIP","POISON_POWDER","SLEEP_POWDER","RAZOR_LEAF"])],"CeladonCity_Gym_EventScript_Tina","CeladonCity_Gym_Text_TinaIntro","CeladonCity_Gym_Text_TinaDefeat","CeladonCity_Gym_Text_TinaPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_BEAUTY_TAMIA")] = new(266,"TAMIA","TRAINER_CLASS_BEAUTY",73,"TRAINER_PIC_BEAUTY",98,129,18,0,1,[new("BELLSPROUT",69,24,0,["SLEEP_POWDER","POISON_POWDER","STUN_SPORE","ACID"]),new("BELLSPROUT",69,24,0,["SLEEP_POWDER","POISON_POWDER","STUN_SPORE","ACID"])],"CeladonCity_Gym_EventScript_Tamia","CeladonCity_Gym_Text_TamiaIntro","CeladonCity_Gym_Text_TamiaDefeat","CeladonCity_Gym_Text_TamiaPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_BEAUTY_LORI")] = new(267,"LORI","TRAINER_CLASS_BEAUTY",73,"TRAINER_PIC_BEAUTY",98,129,18,0,1,[new("EXEGGCUTE",102,24,0,["HYPNOSIS","REFLECT","LEECH_SEED","CONFUSION"])],"CeladonCity_Gym_EventScript_Lori","CeladonCity_Gym_Text_LoriIntro","CeladonCity_Gym_Text_LoriDefeat","CeladonCity_Gym_Text_LoriPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_LASS_LISA")] = new(133,"LISA","TRAINER_CLASS_LASS",59,"TRAINER_PIC_LASS",84,129,4,0,1,[new("ODDISH",43,23,0,["POISON_POWDER","STUN_SPORE","SLEEP_POWDER","ACID"]),new("GLOOM",44,23,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"])],"CeladonCity_Gym_EventScript_Lisa","CeladonCity_Gym_Text_LisaIntro","CeladonCity_Gym_Text_LisaDefeat","CeladonCity_Gym_Text_LisaPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_COOLTRAINER_MARY")] = new(402,"MARY","TRAINER_CLASS_COOLTRAINER",86,"TRAINER_PIC_COOLTRAINER_F",111,133,9,1,7,[new("BELLSPROUT",69,22,100,["WRAP","STUN_SPORE","POISON_POWDER","GROWTH"]),new("ODDISH",43,22,100,["POISON_POWDER","STUN_SPORE","ABSORB","SWEET_SCENT"]),new("WEEPINBELL",70,22,100,["VINE_WHIP","STUN_SPORE","POISON_POWDER","GROWTH"]),new("GLOOM",44,22,100,["ABSORB","STUN_SPORE","POISON_POWDER","SWEET_SCENT"]),new("IVYSAUR",2,22,100,["RAZOR_LEAF","SLEEP_POWDER","VINE_WHIP","LEECH_SEED"])],"CeladonCity_Gym_EventScript_Mary","CeladonCity_Gym_Text_MaryIntro","CeladonCity_Gym_Text_MaryDefeat","CeladonCity_Gym_Text_MaryPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_SUPER_POTION","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_LEADER_ERIKA")] = new(417,"ERIKA","TRAINER_CLASS_LEADER",84,"TRAINER_PIC_LEADER_ERIKA",119,129,25,1,7,[new("VICTREEBEL",71,29,0,["STUN_SPORE","ACID","POISON_POWDER","GIGA_DRAIN"]),new("TANGELA",114,24,0,["POISON_POWDER","CONSTRICT","INGRAIN","GIGA_DRAIN"]),new("VILEPLUME",45,29,0,["SLEEP_POWDER","ACID","STUN_SPORE","GIGA_DRAIN"])],"CeladonCity_Gym_EventScript_Erika","CeladonCity_Gym_Text_ErikaIntro","CeladonCity_Gym_Text_ErikaDefeat","CeladonCity_Gym_Text_ErikaPostBattle","data/maps/CeladonCity_Gym/scripts.inc",["ITEM_HYPER_POTION","ITEM_FULL_HEAL","ITEM_NONE","ITEM_NONE"],"CeladonCity_Gym_EventScript_DefeatedErika","NO_MUSIC"),
    }.ToImmutableDictionary();
    public static TrainerId Erika { get; } = new("pret:TRAINER_LEADER_ERIKA");
    public static TrainerId Mary { get; } = new("pret:TRAINER_COOLTRAINER_MARY");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(CeladonGymTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng, bool rocketHideoutRules = false, bool towerRules = false, bool fuchsiaRules = false)
    {
        towerRules |= fuchsiaRules;
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) != (fuchsiaRules ? 107 : towerRules ? (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 102 ? 102 : 101) : rocketHideoutRules ? 99 : 98) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("CeladonGym requires the exact ninety-eight-species catalog and one initial source CeladonGym team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !RocketHideoutBattleScope.ValidSharedSpecies(species))
                throw new NotSupportedException("CeladonGym trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("CeladonGym initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !CeladonGymBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("CeladonGym initial move definition differs.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(CeladonGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=8 || catalog.Battles.Count!=8)
            throw new NotSupportedException("Exact eight-team CeladonGym catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(e.Items) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("CeladonGym trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("CeladonGym source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified CeladonGym battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(b.Trainer==Erika ? 1 : 0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music || b.Source.Path!=e.Path)
            throw new NotSupportedException("CeladonGym initial battle source differs.");
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
