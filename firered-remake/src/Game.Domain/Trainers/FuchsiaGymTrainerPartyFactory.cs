using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>Seven original Fuchsia Gym teams, with exact source NPC construction. Creation does not admit battle effects.</summary>
public static class FuchsiaGymTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-fuchsia-gym-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path, string[] Items, string? PostScript, string? Music);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_JUGGLER_KAYDEN")] = new(292,"KAYDEN","TRAINER_CLASS_JUGGLER",77,"TRAINER_PIC_JUGGLER",102,11,10,0,1,[new("HYPNO",97,38,0,["HEADBUTT","POISON_GAS","MEDITATE","PSYCHIC"])],"FuchsiaCity_Gym_EventScript_Kayden","FuchsiaCity_Gym_Text_KaydenIntro","FuchsiaCity_Gym_Text_KaydenDefeat","FuchsiaCity_Gym_Text_KaydenPostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_JUGGLER_SHAWN")] = new(289,"SHAWN","TRAINER_CLASS_JUGGLER",77,"TRAINER_PIC_JUGGLER",102,11,10,0,1,[new("DROWZEE",96,34,0,["HEADBUTT","POISON_GAS","MEDITATE","PSYCHIC"]),new("HYPNO",97,34,0,["CONFUSION","HEADBUTT","POISON_GAS","MEDITATE"])],"FuchsiaCity_Gym_EventScript_Shawn","FuchsiaCity_Gym_Text_ShawnIntro","FuchsiaCity_Gym_Text_ShawnDefeat","FuchsiaCity_Gym_Text_ShawnPostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_JUGGLER_KIRK")] = new(288,"KIRK","TRAINER_CLASS_JUGGLER",77,"TRAINER_PIC_JUGGLER",102,11,10,0,1,[new("DROWZEE",96,31,0,["HEADBUTT","POISON_GAS","MEDITATE","PSYCHIC"]),new("DROWZEE",96,31,0,["HEADBUTT","POISON_GAS","MEDITATE","PSYCHIC"]),new("KADABRA",64,31,0,["PSYBEAM","REFLECT","RECOVER","FUTURE_SIGHT"]),new("DROWZEE",96,31,0,["HEADBUTT","POISON_GAS","MEDITATE","PSYCHIC"])],"FuchsiaCity_Gym_EventScript_Kirk","FuchsiaCity_Gym_Text_KirkIntro","FuchsiaCity_Gym_Text_KirkDefeat","FuchsiaCity_Gym_Text_KirkPostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TAMER_EDGAR")] = new(295,"EDGAR","TRAINER_CLASS_TAMER",78,"TRAINER_PIC_TAMER",103,11,10,0,1,[new("ARBOK",24,33,40,["POISON_STING","BITE","GLARE","SCREECH"]),new("SANDSLASH",28,33,40,["SAND_ATTACK","POISON_STING","SLASH","SWIFT"]),new("ARBOK",24,33,40,["POISON_STING","BITE","GLARE","SCREECH"])],"FuchsiaCity_Gym_EventScript_Edgar","FuchsiaCity_Gym_Text_EdgarIntro","FuchsiaCity_Gym_Text_EdgarDefeat","FuchsiaCity_Gym_Text_EdgarPostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_TAMER_PHIL")] = new(294,"PHIL","TRAINER_CLASS_TAMER",78,"TRAINER_PIC_TAMER",103,11,10,0,1,[new("SANDSLASH",28,34,40,["SAND_ATTACK","POISON_STING","SLASH","SWIFT"]),new("ARBOK",24,34,40,["POISON_STING","BITE","GLARE","SCREECH"])],"FuchsiaCity_Gym_EventScript_Phil","FuchsiaCity_Gym_Text_PhilIntro","FuchsiaCity_Gym_Text_PhilDefeat","FuchsiaCity_Gym_Text_PhilPostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_JUGGLER_NATE")] = new(293,"NATE","TRAINER_CLASS_JUGGLER",77,"TRAINER_PIC_JUGGLER",102,11,10,0,1,[new("DROWZEE",96,34,0,["HEADBUTT","POISON_GAS","MEDITATE","PSYCHIC"]),new("KADABRA",64,34,0,["REFLECT","RECOVER","FUTURE_SIGHT","ROLE_PLAY"])],"FuchsiaCity_Gym_EventScript_Nate","FuchsiaCity_Gym_Text_NateIntro","FuchsiaCity_Gym_Text_NateDefeat","FuchsiaCity_Gym_Text_NatePostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"],null,null),
        [new("pret:TRAINER_LEADER_KOGA")] = new(418,"KOGA","TRAINER_CLASS_LEADER",84,"TRAINER_PIC_LEADER_KOGA",120,0,25,1,7,[new("KOFFING",109,37,0,["SELF_DESTRUCT","SLUDGE","SMOKESCREEN","TOXIC"]),new("MUK",89,39,0,["MINIMIZE","SLUDGE","ACID_ARMOR","TOXIC"]),new("KOFFING",109,37,0,["SELF_DESTRUCT","SLUDGE","SMOKESCREEN","TOXIC"]),new("WEEZING",110,43,0,["TACKLE","SLUDGE","SMOKESCREEN","TOXIC"])],"FuchsiaCity_Gym_EventScript_Koga","FuchsiaCity_Gym_Text_KogaIntro","FuchsiaCity_Gym_Text_KogaDefeat","FuchsiaCity_Gym_Text_KogaPostBattle","data/maps/FuchsiaCity_Gym/scripts.inc",["ITEM_HYPER_POTION","ITEM_HYPER_POTION","ITEM_FULL_HEAL","ITEM_NONE"],"FuchsiaCity_Gym_EventScript_DefeatedKoga","NO_MUSIC"),
    }.ToImmutableDictionary();
    public static TrainerId Koga { get; } = new("pret:TRAINER_LEADER_KOGA");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(FuchsiaGymTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) != 107 || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("FuchsiaGym requires the exact107-species catalog and one initial source FuchsiaGym team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !FuchsiaBattleScope.ValidSpecies(species))
                throw new NotSupportedException("FuchsiaGym trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("FuchsiaGym initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !MetronomeSourceMoveDefinition.Matches(m)))
                throw new NotSupportedException("FuchsiaGym initial move definition differs.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(FuchsiaGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=7 || catalog.Battles.Count!=7)
            throw new NotSupportedException("Exact seven-team FuchsiaGym catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(e.Items) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("FuchsiaGym trainer metadata differs from source.");
            ValidateSource(t.Source);ValidateSource(t.MoneySource);
            if(t.Source.Symbol!=id.Value[5..] || t.MoneySource.Symbol!=e.Class)throw new NotSupportedException("Fuchsia Gym source binding differs.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null)throw new NotSupportedException("Fuchsia Gym party member missing.");
                ValidateSource(a.Source);ValidateSource(a.SpeciesNameSource);
                if(a.Source.Symbol!=PartySourceSymbols[id] || a.SpeciesNameSource.Symbol!="SPECIES_"+b.Symbol || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("FuchsiaGym source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified FuchsiaGym battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is not null)ValidateSource(b.Source);
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=(b.Trainer==Koga ? 1 : 0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music || b.Source.Path!=e.Path || b.Source.Symbol!=e.Entry)
            throw new NotSupportedException("FuchsiaGym initial battle source differs.");
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
    private static readonly ImmutableDictionary<TrainerId,string> PartySourceSymbols = new Dictionary<TrainerId,string> {
        [new("pret:TRAINER_JUGGLER_KAYDEN")]="sParty_JugglerKayden",
        [new("pret:TRAINER_JUGGLER_SHAWN")]="sParty_JugglerShawn",
        [new("pret:TRAINER_JUGGLER_KIRK")]="sParty_JugglerKirk",
        [new("pret:TRAINER_TAMER_EDGAR")]="sParty_TamerEdgar",
        [new("pret:TRAINER_TAMER_PHIL")]="sParty_TamerPhil",
        [new("pret:TRAINER_JUGGLER_NATE")]="sParty_JugglerNate",
        [new("pret:TRAINER_LEADER_KOGA")]="sParty_LeaderKoga",
    }.ToImmutableDictionary();
    private static readonly ImmutableHashSet<TrainerSourceRef> SourceReferences = [
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Edgar",41,46,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Kayden",57,62,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Kirk",46,52,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Koga",4,12,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Nate",62,67,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Phil",36,41,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("data/maps/FuchsiaCity_Gym/scripts.inc","FuchsiaCity_Gym_EventScript_Shawn",52,57,"5af256d09bf8768431c502381d40543a3bbf136e2cf3feacebe3bf85a2b62502"),
        new("src/battle_main.c","TRAINER_CLASS_JUGGLER",477,477,"c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928"),
        new("src/battle_main.c","TRAINER_CLASS_LEADER",453,453,"c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928"),
        new("src/battle_main.c","TRAINER_CLASS_TAMER",481,481,"c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928"),
        new("src/data/text/species_names.h","SPECIES_ARBOK",26,26,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_DROWZEE",98,98,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_HYPNO",99,99,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_KADABRA",66,66,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_KOFFING",111,111,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_MUK",91,91,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_SANDSLASH",30,30,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/text/species_names.h","SPECIES_WEEZING",112,112,"73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
        new("src/data/trainer_parties.h","sParty_JugglerKayden",3168,3174,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainer_parties.h","sParty_JugglerKirk",3096,3117,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainer_parties.h","sParty_JugglerNate",3176,3187,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainer_parties.h","sParty_JugglerShawn",3119,3130,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainer_parties.h","sParty_LeaderKoga",5676,5701,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainer_parties.h","sParty_TamerEdgar",3202,3218,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainer_parties.h","sParty_TamerPhil",3189,3200,"cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
        new("src/data/trainers.h","TRAINER_JUGGLER_KAYDEN",2915,2924,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
        new("src/data/trainers.h","TRAINER_JUGGLER_KIRK",2875,2884,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
        new("src/data/trainers.h","TRAINER_JUGGLER_NATE",2925,2934,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
        new("src/data/trainers.h","TRAINER_JUGGLER_SHAWN",2885,2894,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
        new("src/data/trainers.h","TRAINER_LEADER_KOGA",4175,4184,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
        new("src/data/trainers.h","TRAINER_TAMER_EDGAR",2945,2954,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
        new("src/data/trainers.h","TRAINER_TAMER_PHIL",2935,2944,"b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
    ];
    private static void ValidateSource(TrainerSourceRef source) { if(!SourceReferences.Contains(source))throw new NotSupportedException("Fuchsia Gym source provenance differs."); }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
