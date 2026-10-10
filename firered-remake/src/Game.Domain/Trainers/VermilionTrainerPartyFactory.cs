using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record VermilionTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>Exact six initial Vermilion-route teams, no rematches. Reuses original NPC construction.</summary>
public static class VermilionTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-vermilion-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_BUG_CATCHER_KEIGO")] = new(111,"KEIGO","TRAINER_CLASS_BUG_CATCHER",58,"TRAINER_PIC_BUG_CATCHER",83,0,3,0,[new("WEEDLE",13,16,0,["POISON_STING","STRING_SHOT"]),new("CATERPIE",10,16,0,["TACKLE","STRING_SHOT"]),new("WEEDLE",13,16,0,["POISON_STING","STRING_SHOT"])],"Route6_EventScript_Keigo","Route6_Text_KeigoIntro","Route6_Text_KeigoDefeat","Route6_Text_KeigoPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_RICKY")] = new(145,"RICKY","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,[new("SQUIRTLE",7,20,0,["BUBBLE","WITHDRAW","WATER_GUN","BITE"])],"Route6_EventScript_Ricky","Route6_Text_RickyIntro","Route6_Text_RickyDefeat","Route6_Text_RickyPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_NANCY")] = new(151,"NANCY","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,[new("RATTATA",19,16,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("PIKACHU",25,16,0,["TAIL_WHIP","THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM"])],"Route6_EventScript_Nancy","Route6_Text_NancyIntro","Route6_Text_NancyDefeat","Route6_Text_NancyPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_BUG_CATCHER_ELIJAH")] = new(112,"ELIJAH","TRAINER_CLASS_BUG_CATCHER",58,"TRAINER_PIC_BUG_CATCHER",83,0,3,0,[new("BUTTERFREE",12,20,0,["POISON_POWDER","STUN_SPORE","SLEEP_POWDER","SUPERSONIC"])],"Route6_EventScript_Elijah","Route6_Text_ElijahIntro","Route6_Text_ElijahDefeat","Route6_Text_ElijahPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_ISABELLE")] = new(152,"ISABELLE","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,[new("PIDGEY",16,16,0,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("PIDGEY",16,16,0,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),new("PIDGEY",16,16,0,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"])],"Route6_EventScript_Isabelle","Route6_Text_IsabelleIntro","Route6_Text_IsabelleDefeat","Route6_Text_IsabellePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_JEFF")] = new(146,"JEFF","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,[new("SPEAROW",21,16,0,["PECK","GROWL","LEER","FURY_ATTACK"]),new("RATICATE",20,16,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"])],"Route6_EventScript_Jeff","Route6_Text_JeffIntro","Route6_Text_JeffDefeat","Route6_Text_JeffPostBattle","data/scripts/trainers.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(VermilionTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false)
    {
        celadonGymRules |= rocketHideoutRules;
        if ((pokemon.Species.Count == 98) != (celadonGymRules && !rocketHideoutRules) || (pokemon.Species.Count == 99) != rocketHideoutRules) throw new NotSupportedException("Explicit Celadon Gym trainer factory profile required.");
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(pokemon) : route11Rules ? 79 : ssAnneRules ? 73 : 70) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("Vermilion requires the exact seventy-species catalog and one initial team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i]; var species=pokemon.Species[member.Species];
            if (!VermilionBattleScope.ValidSpecies(species)) throw new NotSupportedException("Vermilion trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("Vermilion initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !VermilionBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("Vermilion initial move definition differs.");
        }
        var created=TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        return trainer.PartyFlags==0 ? created : created with { Party=created.Party.Select((m,i)=>m with { Pokemon=m.Pokemon with {
            Moves=trainer.Party[i].CustomMoves.TakeWhile(x=>x.Value!="pret:MOVE_NONE").Select(x=>new PokemonMoveSlot(x,pokemon.Moves[x].Pp,pokemon.Moves[x].Pp)).ToImmutableArray()
        }}).ToImmutableArray() };
    }
    public static void ValidateCatalog(VermilionTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=6 || catalog.Battles.Count!=6)
            throw new NotSupportedException("Exact six-team Vermilion catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=(e.Flags==1?7u:1u) || t.Items.IsDefault ||
                !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.Flags==1?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("Vermilion trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("Vermilion source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle)) throw new NotSupportedException("Missing Vermilion battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Intro==""?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Intro==""?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null || b.Source.Path!=e.Path)
            throw new NotSupportedException("Vermilion initial battle source differs.");
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
