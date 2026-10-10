using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All thirty initial Route9, Route10 and RockTunnel teams, with exact source NPC construction.</summary>
public static class RockTunnelTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-rocktunnel-trainers-v1";
    internal sealed record ExpectedMember(string Symbol, int Species, int Level, int Iv, string[] Moves);
    internal sealed record ExpectedTrainer(int Id, string Name, string Class, int ClassId, string Pic, int PicId, int Gender,
        int Money, int Flags, uint AiFlags, ExpectedMember[] Party, string Entry, string Intro, string Defeat, string Post, string Path);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_PICNICKER_ALICIA")] = new(154,"ALICIA","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("ODDISH",43,18,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"]),new("BELLSPROUT",69,18,0,["GROWTH","WRAP","SLEEP_POWDER","POISON_POWDER"]),new("ODDISH",43,18,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"]),new("BELLSPROUT",69,18,0,["GROWTH","WRAP","SLEEP_POWDER","POISON_POWDER"])],"Route9_EventScript_Alicia","Route9_Text_AliciaIntro","Route9_Text_AliciaDefeat","Route9_Text_AliciaPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_JEREMY")] = new(465,"JEREMY","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,1,[new("MACHOP",66,20,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"]),new("ONIX",95,20,0,["SCREECH","BIND","ROCK_THROW","HARDEN"])],"Route9_EventScript_Jeremy","Route9_Text_JeremyIntro","Route9_Text_JeremyDefeat","Route9_Text_JeremyPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_ALAN")] = new(185,"ALAN","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,1,1,[new("GEODUDE",74,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"]),new("ONIX",95,21,0,["HARDEN","ROCK_THROW","BIND","SCREECH"])],"Route9_EventScript_Alan","Route9_Text_AlanIntro","Route9_Text_AlanDefeat","Route9_Text_AlanPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_CHRIS")] = new(148,"CHRIS","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,1,[new("GROWLITHE",58,21,0,["ROAR","EMBER","LEER","ODOR_SLEUTH"]),new("CHARMANDER",4,21,0,["GROWL","EMBER","METAL_CLAW","SMOKESCREEN"])],"Route9_EventScript_Chris","Route9_Text_ChrisIntro","Route9_Text_ChrisDefeat","Route9_Text_ChrisPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_BUG_CATCHER_BRENT")] = new(114,"BRENT","TRAINER_CLASS_BUG_CATCHER",58,"TRAINER_PIC_BUG_CATCHER",83,0,3,0,1,[new("BEEDRILL",15,19,0,["FURY_ATTACK","FOCUS_ENERGY"]),new("BEEDRILL",15,19,0,["FURY_ATTACK","FOCUS_ENERGY"])],"Route9_EventScript_Brent","Route9_Text_BrentIntro","Route9_Text_BrentDefeat","Route9_Text_BrentPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_BUG_CATCHER_CONNER")] = new(115,"CONNER","TRAINER_CLASS_BUG_CATCHER",58,"TRAINER_PIC_BUG_CATCHER",83,0,3,0,1,[new("CATERPIE",10,20,0,["TACKLE","STRING_SHOT"]),new("WEEDLE",13,20,0,["POISON_STING","STRING_SHOT"]),new("VENONAT",48,20,0,["FORESIGHT","SUPERSONIC","CONFUSION","POISON_POWDER"])],"Route9_EventScript_Conner","Route9_Text_ConnerIntro","Route9_Text_ConnerDefeat","Route9_Text_ConnerPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_BRICE")] = new(186,"BRICE","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,1,[new("GEODUDE",74,20,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"]),new("MACHOP",66,20,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"]),new("GEODUDE",74,20,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"])],"Route9_EventScript_Brice","Route9_Text_BriceIntro","Route9_Text_BriceDefeat","Route9_Text_BricePostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_CAITLIN")] = new(155,"CAITLIN","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("MEOWTH",52,23,0,["SCRATCH","GROWL","BITE","PAY_DAY"])],"Route9_EventScript_Caitlin","Route9_Text_CaitlinIntro","Route9_Text_CaitlinDefeat","Route9_Text_CaitlinPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_CAMPER_DREW")] = new(149,"DREW","TRAINER_CLASS_CAMPER",61,"TRAINER_PIC_CAMPER",86,0,5,0,1,[new("RATTATA",19,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("SANDSHREW",27,19,0,["SCRATCH","DEFENSE_CURL","SAND_ATTACK","POISON_STING"]),new("EKANS",23,19,0,["WRAP","LEER","POISON_STING","BITE"]),new("SANDSHREW",27,19,0,["SCRATCH","DEFENSE_CURL","SAND_ATTACK","POISON_STING"])],"Route9_EventScript_Drew","Route9_Text_DrewIntro","Route9_Text_DrewDefeat","Route9_Text_DrewPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_CAROL")] = new(157,"CAROL","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("PIDGEY",16,21,0,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("PIDGEOTTO",17,21,0,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"])],"Route10_EventScript_Carol","Route10_Text_CarolIntro","Route10_Text_CarolDefeat","Route10_Text_CarolPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_CLARK")] = new(187,"CLARK","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,1,1,[new("GEODUDE",74,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"]),new("ONIX",95,21,0,["HARDEN","ROCK_THROW","BIND","SCREECH"])],"Route10_EventScript_Clark","Route10_Text_ClarkIntro","Route10_Text_ClarkDefeat","Route10_Text_ClarkPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_POKEMANIAC_HERMAN")] = new(163,"HERMAN","TRAINER_CLASS_POKEMANIAC",63,"TRAINER_PIC_POKEMANIAC",88,3,12,0,1,[new("CUBONE",104,20,30,["TAIL_WHIP","BONE_CLUB","HEADBUTT","LEER"]),new("SLOWPOKE",79,20,30,["TACKLE","GROWL","WATER_GUN","CONFUSION"])],"Route10_EventScript_Herman","Route10_Text_HermanIntro","Route10_Text_HermanDefeat","Route10_Text_HermanPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_HIKER_TRENT")] = new(188,"TRENT","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,1,[new("ONIX",95,19,0,["SCREECH","BIND","ROCK_THROW","HARDEN"]),new("GRAVELER",75,19,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"])],"Route10_EventScript_Trent","Route10_Text_TrentIntro","Route10_Text_TrentDefeat","Route10_Text_TrentPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_POKEMANIAC_MARK")] = new(162,"MARK","TRAINER_CLASS_POKEMANIAC",63,"TRAINER_PIC_POKEMANIAC",88,3,12,0,1,[new("RHYHORN",111,29,30,["STOMP","FURY_ATTACK","SCARY_FACE","ROCK_BLAST"]),new("LICKITUNG",108,29,30,["DEFENSE_CURL","KNOCK_OFF","STOMP","WRAP"])],"Route10_EventScript_Mark","Route10_Text_MarkIntro","Route10_Text_MarkDefeat","Route10_Text_MarkPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_HEIDI")] = new(156,"HEIDI","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("PIKACHU",25,20,0,["THUNDER_WAVE","QUICK_ATTACK","DOUBLE_TEAM","SLAM"]),new("CLEFAIRY",35,20,0,["ENCORE","SING","DOUBLE_SLAP","FOLLOW_ME"])],"Route10_EventScript_Heidi","Route10_Text_HeidiIntro","Route10_Text_HeidiDefeat","Route10_Text_HeidiPostBattle","data/scripts/trainers.inc"),
        [new("pret:TRAINER_PICNICKER_DANA")] = new(474,"DANA","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("MEOWTH",52,20,0,["SCRATCH","GROWL","BITE","PAY_DAY"]),new("ODDISH",43,20,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"]),new("PIDGEY",16,20,0,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"])],"RockTunnel_1F_EventScript_Dana","RockTunnel_1F_Text_DanaIntro","RockTunnel_1F_Text_DanaDefeat","RockTunnel_1F_Text_DanaPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_PICNICKER_ARIANA")] = new(475,"ARIANA","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("PIDGEY",16,19,0,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("RATTATA",19,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("RATTATA",19,19,0,["TACKLE","TAIL_WHIP","QUICK_ATTACK","HYPER_FANG"]),new("BELLSPROUT",69,19,0,["WRAP","SLEEP_POWDER","POISON_POWDER","STUN_SPORE"])],"RockTunnel_1F_EventScript_Ariana","RockTunnel_1F_Text_ArianaIntro","RockTunnel_1F_Text_ArianaDefeat","RockTunnel_1F_Text_ArianaPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_PICNICKER_LEAH")] = new(476,"LEAH","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("BELLSPROUT",69,22,0,["WRAP","SLEEP_POWDER","POISON_POWDER","STUN_SPORE"]),new("CLEFAIRY",35,22,0,["SING","DOUBLE_SLAP","FOLLOW_ME","MINIMIZE"])],"RockTunnel_1F_EventScript_Leah","RockTunnel_1F_Text_LeahIntro","RockTunnel_1F_Text_LeahDefeat","RockTunnel_1F_Text_LeahPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_HIKER_LUCAS")] = new(194,"LUCAS","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,1,1,[new("GEODUDE",74,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"]),new("GRAVELER",75,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"])],"RockTunnel_1F_EventScript_Lucas","RockTunnel_1F_Text_LucasIntro","RockTunnel_1F_Text_LucasDefeat","RockTunnel_1F_Text_LucasPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_HIKER_OLIVER")] = new(193,"OLIVER","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,1,[new("ONIX",95,20,0,["SCREECH","BIND","ROCK_THROW","HARDEN"]),new("ONIX",95,20,0,["SCREECH","BIND","ROCK_THROW","HARDEN"]),new("GEODUDE",74,20,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"])],"RockTunnel_1F_EventScript_Oliver","RockTunnel_1F_Text_OliverIntro","RockTunnel_1F_Text_OliverDefeat","RockTunnel_1F_Text_OliverPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_HIKER_LENNY")] = new(192,"LENNY","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,1,[new("GEODUDE",74,19,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"]),new("MACHOP",66,19,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"]),new("GEODUDE",74,19,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"]),new("GEODUDE",74,19,0,["DEFENSE_CURL","MUD_SPORT","ROCK_THROW","MAGNITUDE"])],"RockTunnel_1F_EventScript_Lenny","RockTunnel_1F_Text_LennyIntro","RockTunnel_1F_Text_LennyDefeat","RockTunnel_1F_Text_LennyPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_POKEMANIAC_ASHTON")] = new(168,"ASHTON","TRAINER_CLASS_POKEMANIAC",63,"TRAINER_PIC_POKEMANIAC",88,3,12,0,1,[new("CUBONE",104,23,30,["BONE_CLUB","HEADBUTT","LEER","FOCUS_ENERGY"]),new("SLOWPOKE",79,23,30,["TACKLE","GROWL","WATER_GUN","CONFUSION"])],"RockTunnel_1F_EventScript_Ashton","RockTunnel_1F_Text_AshtonIntro","RockTunnel_1F_Text_AshtonDefeat","RockTunnel_1F_Text_AshtonPostBattle","data/maps/RockTunnel_1F/scripts.inc"),
        [new("pret:TRAINER_POKEMANIAC_COOPER")] = new(164,"COOPER","TRAINER_CLASS_POKEMANIAC",63,"TRAINER_PIC_POKEMANIAC",88,3,12,0,1,[new("SLOWPOKE",79,20,30,["TACKLE","GROWL","WATER_GUN","CONFUSION"]),new("SLOWPOKE",79,20,30,["TACKLE","GROWL","WATER_GUN","CONFUSION"]),new("SLOWPOKE",79,20,30,["TACKLE","GROWL","WATER_GUN","CONFUSION"])],"RockTunnel_B1F_EventScript_Cooper","RockTunnel_B1F_Text_CooperIntro","RockTunnel_B1F_Text_CooperDefeat","RockTunnel_B1F_Text_CooperPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_HIKER_DUDLEY")] = new(189,"DUDLEY","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,1,1,[new("GEODUDE",74,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"]),new("GEODUDE",74,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"]),new("GRAVELER",75,21,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"])],"RockTunnel_B1F_EventScript_Dudley","RockTunnel_B1F_Text_DudleyIntro","RockTunnel_B1F_Text_DudleyDefeat","RockTunnel_B1F_Text_DudleyPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_PICNICKER_SOFIA")] = new(158,"SOFIA","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("JIGGLYPUFF",39,21,0,["DEFENSE_CURL","POUND","DISABLE","ROLLOUT"]),new("PIDGEY",16,21,0,["SAND_ATTACK","GUST","QUICK_ATTACK","WHIRLWIND"]),new("MEOWTH",52,21,0,["SCRATCH","GROWL","BITE","PAY_DAY"])],"RockTunnel_B1F_EventScript_Sofia","RockTunnel_B1F_Text_SofiaIntro","RockTunnel_B1F_Text_SofiaDefeat","RockTunnel_B1F_Text_SofiaPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_HIKER_ALLEN")] = new(190,"ALLEN","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,1,1,[new("GEODUDE",74,25,0,["MAGNITUDE","ROCK_THROW","MUD_SPORT","DEFENSE_CURL"])],"RockTunnel_B1F_EventScript_Allen","RockTunnel_B1F_Text_AllenIntro","RockTunnel_B1F_Text_AllenDefeat","RockTunnel_B1F_Text_AllenPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_HIKER_ERIC")] = new(191,"ERIC","TRAINER_CLASS_HIKER",65,"TRAINER_PIC_HIKER",90,11,9,0,1,[new("MACHOP",66,20,0,["LEER","FOCUS_ENERGY","KARATE_CHOP","SEISMIC_TOSS"]),new("ONIX",95,20,0,["SCREECH","BIND","ROCK_THROW","HARDEN"])],"RockTunnel_B1F_EventScript_Eric","RockTunnel_B1F_Text_EricIntro","RockTunnel_B1F_Text_EricDefeat","RockTunnel_B1F_Text_EricPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_POKEMANIAC_STEVE")] = new(165,"STEVE","TRAINER_CLASS_POKEMANIAC",63,"TRAINER_PIC_POKEMANIAC",88,3,12,0,1,[new("CHARMANDER",4,22,30,["GROWL","EMBER","METAL_CLAW","SMOKESCREEN"]),new("CUBONE",104,22,30,["BONE_CLUB","HEADBUTT","LEER","FOCUS_ENERGY"])],"RockTunnel_B1F_EventScript_Steve","RockTunnel_B1F_Text_SteveIntro","RockTunnel_B1F_Text_SteveDefeat","RockTunnel_B1F_Text_StevePostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_PICNICKER_MARTHA")] = new(159,"MARTHA","TRAINER_CLASS_PICNICKER",62,"TRAINER_PIC_PICNICKER",87,130,5,0,1,[new("ODDISH",43,22,0,["SWEET_SCENT","POISON_POWDER","STUN_SPORE","SLEEP_POWDER"]),new("BULBASAUR",1,22,0,["VINE_WHIP","POISON_POWDER","SLEEP_POWDER","RAZOR_LEAF"])],"RockTunnel_B1F_EventScript_Martha","RockTunnel_B1F_Text_MarthaIntro","RockTunnel_B1F_Text_MarthaDefeat","RockTunnel_B1F_Text_MarthaPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
        [new("pret:TRAINER_POKEMANIAC_WINSTON")] = new(166,"WINSTON","TRAINER_CLASS_POKEMANIAC",63,"TRAINER_PIC_POKEMANIAC",88,3,12,0,1,[new("SLOWPOKE",79,25,30,["GROWL","WATER_GUN","CONFUSION","DISABLE"])],"RockTunnel_B1F_EventScript_Winston","RockTunnel_B1F_Text_WinstonIntro","RockTunnel_B1F_Text_WinstonDefeat","RockTunnel_B1F_Text_WinstonPostBattle","data/maps/RockTunnel_B1F/scripts.inc"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = Expected.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    internal static string SpeciesName(string symbol) => symbol == "NIDORAN_F" ? "NIDORAN♀" : symbol == "NIDORAN_M" ? "NIDORAN♂" : symbol;
    public static TrainerPartyCreationResult Create(RockTunnelTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != 93 || !trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("RockTunnel requires the exact ninety-three-species catalog and one initial source RockTunnel team.");
        var expected = Expected[id];
        for (int i=0; i<trainer.Party.Length; i++)
        {
            var member=trainer.Party[i];
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !ValidSourceSpecies(species))
                throw new NotSupportedException("RockTunnel trainer species differs.");
            var moves=expected.Party[i].Moves.TakeWhile(x=>x!="NONE").Select(x=>new MoveId("pret:MOVE_"+x));
            if (trainer.PartyFlags==0 && !PokemonMoveRules.InitialMoves(pokemon,species,member.Level).Select(x=>x.Move).SequenceEqual(moves))
                throw new NotSupportedException("RockTunnel initial natural moves differ from source.");
            if (moves.Any(x=>!pokemon.Moves.TryGetValue(x,out var m) || !ValidSourceMove(m)))
                throw new NotSupportedException("RockTunnel initial move definition differs.");
        }
        // Source custom parties share the original deterministic PID/OT construction.
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(RockTunnelTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit!=SourceCommit || catalog.Profile!=RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count!=30 || catalog.Battles.Count!=30)
            throw new NotSupportedException("Exact thirty-team RockTunnel catalog required.");
        foreach(var (id,e) in Expected)
        {
            if(!catalog.Trainers.TryGetValue(id,out var t) || t is null || t.Id!=id || t.SourceTrainerId!=e.Id ||
                t.SourceTrainerName!=e.Name || !Encode(e.Name,t.EncodedTrainerName) || t.TrainerClass!=e.Class || t.SourceTrainerClass!=e.ClassId ||
                t.TrainerPic!=e.Pic || t.SourceTrainerPic!=e.PicId || t.EncounterMusicGender!=e.Gender || t.BaseMoney!=e.Money ||
                t.DoubleBattle || t.PartyFlags!=e.Flags || t.AiFlags!=e.AiFlags || t.Items.IsDefault ||
                !t.Items.SequenceEqual(Enumerable.Repeat("ITEM_NONE",4)) || t.AiFlagSymbols.IsDefault ||
                !t.AiFlagSymbols.SequenceEqual(e.AiFlags==7?new[]{"AI_SCRIPT_CHECK_BAD_MOVE","AI_SCRIPT_TRY_TO_FAINT","AI_SCRIPT_CHECK_VIABILITY"}:["AI_SCRIPT_CHECK_BAD_MOVE"]) ||
                t.Party.IsDefault || t.Party.Length!=e.Party.Length) throw new NotSupportedException("RockTunnel trainer metadata differs from source.");
            for(int i=0;i<e.Party.Length;i++)
            {
                var a=t.Party[i];var b=e.Party[i];
                if(a is null || a.Species.Value!="pret:SPECIES_"+b.Symbol || a.SourceSpeciesId!=b.Species || a.SpeciesName!=SpeciesName(b.Symbol) ||
                    !Encode(a.SpeciesName,a.EncodedSpeciesName) || a.Level!=b.Level || a.SourceIv!=b.Iv || a.HeldItem!="ITEM_NONE" ||
                    a.CustomMoves.IsDefault || !a.CustomMoves.Select(x=>x.Value).SequenceEqual(e.Flags==1?b.Moves.Select(x=>"pret:MOVE_"+x):[]))
                    throw new NotSupportedException("RockTunnel source party differs.");
            }
            if(!catalog.Battles.TryGetValue(id,out var battle) || battle is null || battle.Trainer!=id)
                throw new NotSupportedException("Missing or misidentified RockTunnel battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !Expected.TryGetValue(b.Trainer,out var e) || b.Mode!=0 || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null || b.Source.Path!=e.Path)
            throw new NotSupportedException("RockTunnel initial battle source differs.");
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
    private static bool ValidSourceSpecies(PokemonSpeciesDefinition s) => Route11BattleScope.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        104 => s.Id.Value == "pret:SPECIES_CUBONE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_GROUND","ABILITY_ROCK_HEAD","ABILITY_LIGHTNING_ROD"),
        108 => s.Id.Value == "pret:SPECIES_LICKITUNG" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_NORMAL","TYPE_NORMAL","ABILITY_OWN_TEMPO","ABILITY_OBLIVIOUS"),
        111 => s.Id.Value == "pret:SPECIES_RHYHORN" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_ROCK","ABILITY_LIGHTNING_ROD","ABILITY_ROCK_HEAD"),
        48 => s.Id.Value == "pret:SPECIES_VENONAT" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_BUG","TYPE_POISON","ABILITY_COMPOUND_EYES","ABILITY_NONE"),
        _ => false
    };
    private static bool ValidSourceMove(PokemonMoveDefinition m)
    {
        if (Route11BattleScope.Supports(m.Id)) return Route11BattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_BONE_CLUB" => (125,20,65,85,"TYPE_GROUND","EFFECT_FLINCH_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_FORESIGHT" => (193,40,0,100,"TYPE_NORMAL","EFFECT_FORESIGHT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_KNOCK_OFF" => (282,20,20,100,"TYPE_DARK","EFFECT_KNOCK_OFF","MOVE_TARGET_SELECTED",100,0),
            "pret:MOVE_ODOR_SLEUTH" => (316,40,0,100,"TYPE_NORMAL","EFFECT_FORESIGHT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_ROCK_BLAST" => (350,10,25,80,"TYPE_ROCK","EFFECT_MULTI_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_STOMP" => (23,20,65,100,"TYPE_NORMAL","EFFECT_FLINCH_MINIMIZE_HIT","MOVE_TARGET_SELECTED",30,0),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_BONE_CLUB" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_FORESIGHT" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_KNOCK_OFF" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ODOR_SLEUTH" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ROCK_BLAST" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_STOMP" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            _ => []
        };
        return expected.Item1 != 0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
