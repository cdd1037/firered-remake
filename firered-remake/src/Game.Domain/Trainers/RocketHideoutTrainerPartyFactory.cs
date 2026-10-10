using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All thirteen initial Rocket Hideout singles teams, with exact source NPC construction.</summary>
public static class RocketHideoutTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-rocket-hideout-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path, string[] Items, string? PostScript, string? Music);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_7")] = new(357,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("RATICATE",20,20,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","SCARY_FACE"]),new("ZUBAT",41,20,0,["LEECH_LIFE","ASTONISH","SUPERSONIC","BITE"])],"CeladonCity_GameCorner_EventScript_RocketGrunt","CeladonCity_GameCorner_Text_GruntIntro","CeladonCity_GameCorner_Text_GruntDefeat","CeladonCity_GameCorner_Text_GruntPostBattle","data/maps/CeladonCity_GameCorner/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"CeladonCity_GameCorner_Text_DefeatedGrunt",null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_8")] = new(358,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("DROWZEE",96,21,0,["DISABLE","CONFUSION","HEADBUTT","POISON_GAS"]),new("MACHOP",66,21,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"])],"RocketHideout_B1F_EventScript_Grunt1","RocketHideout_B1F_Text_Grunt1Intro","RocketHideout_B1F_Text_Grunt1Defeat","RocketHideout_B1F_Text_Grunt1PostBattle","data/maps/RocketHideout_B1F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_9")] = new(359,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("RATICATE",20,21,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","SCARY_FACE"]),new("RATICATE",20,21,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","SCARY_FACE"])],"RocketHideout_B1F_EventScript_Grunt2","RocketHideout_B1F_Text_Grunt2Intro","RocketHideout_B1F_Text_Grunt2Defeat","RocketHideout_B1F_Text_Grunt2PostBattle","data/maps/RocketHideout_B1F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_10")] = new(360,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,1,1,[new("GRIMER",88,20,0,["MINIMIZE","SLUDGE","DISABLE","HARDEN"]),new("KOFFING",109,20,0,["SMOG","TACKLE","POISON_GAS","NONE"]),new("KOFFING",109,20,0,["SMOG","TACKLE","POISON_GAS","NONE"])],"RocketHideout_B1F_EventScript_Grunt3","RocketHideout_B1F_Text_Grunt3Intro","RocketHideout_B1F_Text_Grunt3Defeat","RocketHideout_B1F_Text_Grunt3PostBattle","data/maps/RocketHideout_B1F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_11")] = new(361,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("RATTATA",19,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("RATICATE",20,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("RATICATE",20,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("RATTATA",19,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"])],"RocketHideout_B1F_EventScript_Grunt4","RocketHideout_B1F_Text_Grunt4Intro","RocketHideout_B1F_Text_Grunt4Defeat","RocketHideout_B1F_Text_Grunt4PostBattle","data/maps/RocketHideout_B1F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_12")] = new(362,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,1,1,[new("GRIMER",88,22,0,["MINIMIZE","SLUDGE","DISABLE","HARDEN"]),new("KOFFING",109,22,0,["SLUDGE","SMOG","TACKLE","POISON_GAS"])],"RocketHideout_B1F_EventScript_Grunt5","RocketHideout_B1F_Text_Grunt5Intro","RocketHideout_B1F_Text_Grunt5Defeat","RocketHideout_B1F_Text_Grunt5PostBattle","data/maps/RocketHideout_B1F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"RocketHideout_B1F_EventScript_DefeatedGrunt5",null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_13")] = new(363,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,1,1,[new("ZUBAT",41,17,0,["BITE","ASTONISH","SUPERSONIC","LEECH_LIFE"]),new("KOFFING",109,17,0,["SMOG","TACKLE","POISON_GAS","NONE"]),new("GRIMER",88,17,0,["SLUDGE","DISABLE","HARDEN","POUND"]),new("ZUBAT",41,17,0,["BITE","ASTONISH","SUPERSONIC","LEECH_LIFE"]),new("RATICATE",20,17,0,["HYPER_FANG","QUICK_ATTACK","TAIL_WHIP","TACKLE"])],"RocketHideout_B2F_EventScript_Grunt","RocketHideout_B2F_Text_GruntIntro","RocketHideout_B2F_Text_GruntDefeat","RocketHideout_B2F_Text_GruntPostBattle","data/maps/RocketHideout_B2F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_14")] = new(364,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("RATTATA",19,20,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","FOCUS_ENERGY"]),new("RATICATE",20,20,0,["TAIL_WHIP","QUICK_ATTACK","HYPER_FANG","SCARY_FACE"]),new("DROWZEE",96,20,0,["HYPNOSIS","DISABLE","CONFUSION","HEADBUTT"])],"RocketHideout_B3F_EventScript_Grunt1","RocketHideout_B3F_Text_Grunt1Intro","RocketHideout_B3F_Text_Grunt1Defeat","RocketHideout_B3F_Text_Grunt1PostBattle","data/maps/RocketHideout_B3F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_15")] = new(365,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("MACHOP",66,21,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"]),new("MACHOP",66,21,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"])],"RocketHideout_B3F_EventScript_Grunt2","RocketHideout_B3F_Text_Grunt2Intro","RocketHideout_B3F_Text_Grunt2Defeat","RocketHideout_B3F_Text_Grunt2PostBattle","data/maps/RocketHideout_B3F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_16")] = new(366,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("SANDSHREW",27,23,0,["DEFENSE_CURL","SAND_ATTACK","POISON_STING","SLASH"]),new("EKANS",23,23,0,["LEER","POISON_STING","BITE","GLARE"]),new("SANDSLASH",28,23,0,["SCRATCH","DEFENSE_CURL","SAND_ATTACK","POISON_STING"])],"RocketHideout_B4F_EventScript_Grunt2","RocketHideout_B4F_Text_Grunt2Intro","RocketHideout_B4F_Text_Grunt2Defeat","RocketHideout_B4F_Text_Grunt2PostBattle","data/maps/RocketHideout_B4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"RocketHideout_B4F_EventScript_DefeatedGrunt2",null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_17")] = new(367,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("EKANS",23,23,0,["LEER","POISON_STING","BITE","GLARE"]),new("SANDSHREW",27,23,0,["DEFENSE_CURL","SAND_ATTACK","POISON_STING","SLASH"]),new("ARBOK",24,23,0,["LEER","POISON_STING","BITE","GLARE"])],"RocketHideout_B4F_EventScript_Grunt3","RocketHideout_B4F_Text_Grunt3Intro","RocketHideout_B4F_Text_Grunt3Defeat","RocketHideout_B4F_Text_Grunt3PostBattle","data/maps/RocketHideout_B4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"RocketHideout_B4F_EventScript_DefeatedGrunt3",null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_18")] = new(368,"GRUNT","TRAINER_CLASS_TEAM_ROCKET",85,"TRAINER_PIC_ROCKET_GRUNT_M",109,6,8,0,1,[new("KOFFING",109,21,0,["TACKLE","SMOG","SELF_DESTRUCT","SLUDGE"]),new("ZUBAT",41,21,0,["ASTONISH","SUPERSONIC","BITE","WING_ATTACK"])],"RocketHideout_B4F_EventScript_Grunt1","RocketHideout_B4F_Text_Grunt1Intro","RocketHideout_B4F_Text_Grunt1Defeat","RocketHideout_B4F_Text_Grunt1PostBattle","data/maps/RocketHideout_B4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],"RocketHideout_B4F_EventScript_DefeatedGrunt1",null),
        [new("pret:TRAINER_BOSS_GIOVANNI")] = new(348,"GIOVANNI","TRAINER_CLASS_BOSS",83,"TRAINER_PIC_LEADER_GIOVANNI",108,6,25,0,1,[new("ONIX",95,25,250,["BIND","ROCK_THROW","HARDEN","RAGE"]),new("RHYHORN",111,24,250,["TAIL_WHIP","STOMP","FURY_ATTACK","SCARY_FACE"]),new("KANGASKHAN",115,29,250,["BITE","TAIL_WHIP","FAKE_OUT","MEGA_PUNCH"])],"RocketHideout_B4F_EventScript_Giovanni","","RocketHideout_B4F_Text_GiovanniDefeat","RocketHideout_B4F_Text_GiovanniPostBattle","data/maps/RocketHideout_B4F/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
    }.ToImmutableDictionary();
    public static TrainerId Giovanni { get; } = new("pret:TRAINER_BOSS_GIOVANNI");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(RocketHideoutTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng, bool towerRules = false)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != (towerRules ? 101 : 99) || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("RocketHideout requires the exact ninety-nine-species catalog and one initial source RocketHideout team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !RocketHideoutBattleScope.ValidSharedSpecies(species))
                throw new NotSupportedException("RocketHideout trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("RocketHideout initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !RocketHideoutBattleScope.ValidDefinition(m)))
                throw new NotSupportedException("RocketHideout initial move definition differs.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(RocketHideoutTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=13 || catalog.Battles.Count!=13)
            throw new NotSupportedException("Exact thirteen-team RocketHideout catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(e.Items) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("RocketHideout trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("RocketHideout source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified RocketHideout battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(b.Trainer==Giovanni ? 3 : e.PostScript is null ? 0 : 2) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(b.Trainer==Giovanni ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music || b.Source.Path!=e.Path)
            throw new NotSupportedException("RocketHideout initial battle source differs.");
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
