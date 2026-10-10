using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record VermilionGymTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>All four initial Vermilion Gym teams, with exact source NPC construction.</summary>
public static class VermilionGymTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-vermilion-gym-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path)
    {
        public bool Rival => Id is >= 426 and <= 428;
    }
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_SAILOR_DWAYNE")] = new(141,"DWAYNE","TRAINER_CLASS_SAILOR",60,"TRAINER_PIC_SAILOR",85,0,8,0,1,[new("PIKACHU",25,21,0,["THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM","SLAM"]),new("PIKACHU",25,21,0,["THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM","SLAM"])],"VermilionCity_Gym_EventScript_Dwayne","VermilionCity_Gym_Text_DwayneIntro","VermilionCity_Gym_Text_DwayneDefeat","VermilionCity_Gym_Text_DwaynePostBattle","data/maps/VermilionCity_Gym/scripts.inc"),
        [new("pret:TRAINER_ENGINEER_BAILY")] = new(220,"BAILY","TRAINER_CLASS_ENGINEER",68,"TRAINER_PIC_ENGINEER",93,0,12,0,1,[new("VOLTORB",100,21,0,["TACKLE","SCREECH","SONIC_BOOM","SPARK"]),new("MAGNEMITE",81,21,0,["THUNDER_SHOCK","SUPERSONIC","SONIC_BOOM","THUNDER_WAVE"])],"VermilionCity_Gym_EventScript_Baily","VermilionCity_Gym_Text_BailyIntro","VermilionCity_Gym_Text_BailyDefeat","VermilionCity_Gym_Text_BailyPostBattle","data/maps/VermilionCity_Gym/scripts.inc"),
        [new("pret:TRAINER_GENTLEMAN_TUCKER")] = new(423,"TUCKER","TRAINER_CLASS_GENTLEMAN",88,"TRAINER_PIC_GENTLEMAN",123,13,18,0,1,[new("PIKACHU",25,23,0,["THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM","SLAM"])],"VermilionCity_Gym_EventScript_Tucker","VermilionCity_Gym_Text_TuckerIntro","VermilionCity_Gym_Text_TuckerDefeat","VermilionCity_Gym_Text_TuckerPostBattle","data/maps/VermilionCity_Gym/scripts.inc"),
        [new("pret:TRAINER_LEADER_LT_SURGE")] = new(416,"LT. SURGE","TRAINER_CLASS_LEADER",84,"TRAINER_PIC_LEADER_LT_SURGE",118,0,25,1,7,[new("VOLTORB",100,21,0,["SONIC_BOOM","TACKLE","SCREECH","SHOCK_WAVE"]),new("PIKACHU",25,18,0,["QUICK_ATTACK","THUNDER_WAVE","DOUBLE_TEAM","SHOCK_WAVE"]),new("RAICHU",26,24,0,["QUICK_ATTACK","THUNDER_WAVE","DOUBLE_TEAM","SHOCK_WAVE"])],"VermilionCity_Gym_EventScript_LtSurge","VermilionCity_Gym_Text_LtSurgeIntro","VermilionCity_Gym_Text_LtSurgeDefeat","VermilionCity_Gym_Text_LtSurgePostBattle","data/maps/VermilionCity_Gym/scripts.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerId LtSurge { get; } = new("pret:TRAINER_LEADER_LT_SURGE");
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(VermilionGymTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng, bool route11Rules = false, bool rockTunnelRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool towerRules = false, bool fuchsiaRules = false)
    {
        towerRules |= fuchsiaRules;
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        if ((Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 98) != (celadonGymRules && !rocketHideoutRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 99) != (rocketHideoutRules && !towerRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) is 101 or 102) != (towerRules && !fuchsiaRules) || (pokemon.Species.Count == 107) != fuchsiaRules) throw new NotSupportedException("Explicit Celadon Gym trainer factory profile required.");
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(pokemon) : route11Rules ? 79 : 73) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("VermilionGym requires the exact seventy-three-species catalog and one source Gym team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !VermilionGymBattleScope.ValidSpecies(species))
                throw new NotSupportedException("VermilionGym trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("VermilionGym initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !VermilionGymBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("VermilionGym initial move definition differs.");
        }
        // Source custom-move parties and natural-move parties share exact NPC construction.
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(VermilionGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=4 || catalog.Battles.Count!=4)
            throw new NotSupportedException("Exact four-team Vermilion Gym catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(id == LtSurge ? new[]{"ITEM_SUPER_POTION","ITEM_FULL_HEAL","ITEM_NONE","ITEM_NONE"} : Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("VermilionGym trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("VermilionGym source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified VermilionGym battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Rival?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Rival?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != (b.Trainer == LtSurge ? "VermilionCity_Gym_EventScript_DefeatedLtSurge" : b.Trainer.Value == "pret:TRAINER_GENTLEMAN_TUCKER" ? "VermilionCity_Gym_EventScript_DefeatedTucker" : null) || b.MusicOption != (b.Trainer == LtSurge ? "NO_MUSIC" : null) || b.Source.Path!=e.Path)
            throw new NotSupportedException("VermilionGym initial battle source differs.");
    }
    internal static bool ValidPartyMember(PokemonCatalog catalog, PokemonInstance mon, TrainerId id, uint? flags=null)
    {
        if(!Expected.TryGetValue(id,out var e) || flags is {} f && f!=e.AiFlags) return false;
        uint hash=0;
        foreach(var p in e.Party)
        {
            foreach(int c in EncodeName(e.Name+SpeciesName(p.Symbol))) hash+=(uint)c;
            uint personality=((e.Gender&128)!=0?0x78u:0x88u)+(hash<<8); int iv=p.Iv*31/255;
            if(mon.Species.Value!="pret:SPECIES_"+p.Symbol || mon.SourceSpeciesId!=p.Species || mon.Level!=p.Level || mon.Personality!=personality || mon.Ivs!=new SixStats(iv,iv,iv,iv,iv,iv) ||
                mon.Evs!=SixStats.Zero || mon.Moves.Any(m=>m.PpUps!=0)) continue;
            if(mon.Moves.Select(m=>m.Move.Value).SequenceEqual(p.Moves.TakeWhile(x=>x!="NONE").Select(x=>"pret:MOVE_"+x))) return true;
        }
        return false;
    }
    internal static int[] EncodeName(string value) => value.Select(c => c switch { ' ' => 0, '.' => 0xad, '♀' => 0xb6, '♂' => 0xb5, >= 'A' and <= 'Z' => 0xbb+c-'A', _ => throw new NotSupportedException("Unsupported Gym source name.") }).ToArray();
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(EncodeName(text));
}
