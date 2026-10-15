using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Trainers;

public sealed record FuchsiaRouteTrainerCatalog(ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,
    ImmutableDictionary<string,CeruleanTrainerBattleDefinition> Battles);

/// <summary>Exact source37 singles plus3 doubles. The doubles remain source records and are rejected before RNG until genuine2v2 exists.</summary>
public static class FuchsiaRouteTrainerPartyFactory
{
    private sealed record Entry(string Trainer,int Mode,uint Flags,string Intro,string Defeat,string Post,string EntryScript);

    private static readonly ImmutableDictionary<string,Entry> Entries=new Dictionary<string,Entry>(StringComparer.Ordinal)
    {
        ["Route12_EventScript_Ned"]=new("pret:TRAINER_FISHERMAN_NED",0,8,"Route12_Text_NedIntro","Route12_Text_NedDefeat","Route12_Text_NedPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Chip"]=new("pret:TRAINER_FISHERMAN_CHIP",0,8,"Route12_Text_ChipIntro","Route12_Text_ChipDefeat","Route12_Text_ChipPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Hank"]=new("pret:TRAINER_FISHERMAN_HANK",0,8,"Route12_Text_HankIntro","Route12_Text_HankDefeat","Route12_Text_HankPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Elliot"]=new("pret:TRAINER_FISHERMAN_ELLIOT",0,8,"Route12_Text_ElliotIntro","Route12_Text_ElliotDefeat","Route12_Text_ElliotPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Luca"]=new("pret:TRAINER_ROCKER_LUCA",0,8,"Route12_Text_LucaIntro","Route12_Text_LucaDefeat","Route12_Text_LucaPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Justin"]=new("pret:TRAINER_CAMPER_JUSTIN",0,8,"Route12_Text_JustinIntro","Route12_Text_JustinDefeat","Route12_Text_JustinPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Andrew"]=new("pret:TRAINER_FISHERMAN_ANDREW",0,8,"Route12_Text_AndrewIntro","Route12_Text_AndrewDefeat","Route12_Text_AndrewPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route12_EventScript_Gia"]=new("pret:TRAINER_YOUNG_COUPLE_GIA_JES",4,9,"Route12_Text_GiaIntro","Route12_Text_GiaDefeat","Route12_Text_GiaPostBattle","EventScript_TryDoDoubleTrainerBattle"),
        ["Route12_EventScript_Jes"]=new("pret:TRAINER_YOUNG_COUPLE_GIA_JES",4,9,"Route12_Text_JesIntro","Route12_Text_JesDefeat","Route12_Text_JesPostBattle","EventScript_TryDoDoubleTrainerBattle"),
        ["Route13_EventScript_Alma"]=new("pret:TRAINER_PICNICKER_ALMA",0,8,"Route13_Text_AlmaIntro","Route13_Text_AlmaDefeat","Route13_Text_AlmaPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Sebastian"]=new("pret:TRAINER_BIRD_KEEPER_SEBASTIAN",0,8,"Route13_Text_SebastianIntro","Route13_Text_SebastianDefeat","Route13_Text_SebastianPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Susie"]=new("pret:TRAINER_PICNICKER_SUSIE",0,8,"Route13_Text_SusieIntro","Route13_Text_SusieDefeat","Route13_Text_SusiePostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Sheila"]=new("pret:TRAINER_BEAUTY_SHEILA",0,8,"Route13_Text_SheilaIntro","Route13_Text_SheilaDefeat","Route13_Text_SheilaPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Lola"]=new("pret:TRAINER_BEAUTY_LOLA",0,8,"Route13_Text_LolaIntro","Route13_Text_LolaDefeat","Route13_Text_LolaPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Valerie"]=new("pret:TRAINER_PICNICKER_VALERIE",0,8,"Route13_Text_ValerieIntro","Route13_Text_ValerieDefeat","Route13_Text_ValeriePostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Gwen"]=new("pret:TRAINER_PICNICKER_GWEN",0,8,"Route13_Text_GwenIntro","Route13_Text_GwenDefeat","Route13_Text_GwenPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Robert"]=new("pret:TRAINER_BIRD_KEEPER_ROBERT",0,8,"Route13_Text_RobertIntro","Route13_Text_RobertDefeat","Route13_Text_RobertPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Perry"]=new("pret:TRAINER_BIRD_KEEPER_PERRY",0,8,"Route13_Text_PerryIntro","Route13_Text_PerryDefeat","Route13_Text_PerryPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route13_EventScript_Jared"]=new("pret:TRAINER_BIKER_JARED",0,8,"Route13_Text_JaredIntro","Route13_Text_JaredDefeat","Route13_Text_JaredPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Gerald"]=new("pret:TRAINER_BIKER_GERALD",0,8,"Route14_Text_GeraldIntro","Route14_Text_GeraldDefeat","Route14_Text_GeraldPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Donald"]=new("pret:TRAINER_BIRD_KEEPER_DONALD",0,8,"Route14_Text_DonaldIntro","Route14_Text_DonaldDefeat","Route14_Text_DonaldPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Beck"]=new("pret:TRAINER_BIRD_KEEPER_BECK",0,8,"Route14_Text_BeckIntro","Route14_Text_BeckDefeat","Route14_Text_BeckPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Marlon"]=new("pret:TRAINER_BIRD_KEEPER_MARLON",0,8,"Route14_Text_MarlonIntro","Route14_Text_MarlonDefeat","Route14_Text_MarlonPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Isaac"]=new("pret:TRAINER_BIKER_ISAAC",0,8,"Route14_Text_IsaacIntro","Route14_Text_IsaacDefeat","Route14_Text_IsaacPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Malik"]=new("pret:TRAINER_BIKER_MALIK",0,8,"Route14_Text_MalikIntro","Route14_Text_MalikDefeat","Route14_Text_MalikPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Mitch"]=new("pret:TRAINER_BIRD_KEEPER_MITCH",0,8,"Route14_Text_MitchIntro","Route14_Text_MitchDefeat","Route14_Text_MitchPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Carter"]=new("pret:TRAINER_BIRD_KEEPER_CARTER",0,8,"Route14_Text_CarterIntro","Route14_Text_CarterDefeat","Route14_Text_CarterPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Lukas"]=new("pret:TRAINER_BIKER_LUKAS",0,8,"Route14_Text_LukasIntro","Route14_Text_LukasDefeat","Route14_Text_LukasPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Benny"]=new("pret:TRAINER_BIRD_KEEPER_BENNY",0,8,"Route14_Text_BennyIntro","Route14_Text_BennyDefeat","Route14_Text_BennyPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route14_EventScript_Jan"]=new("pret:TRAINER_TWINS_KIRI_JAN",4,9,"Route14_Text_JanIntro","Route14_Text_JanDefeat","Route14_Text_JanPostBattle","EventScript_TryDoDoubleTrainerBattle"),
        ["Route14_EventScript_Kiri"]=new("pret:TRAINER_TWINS_KIRI_JAN",4,9,"Route14_Text_KiriIntro","Route14_Text_KiriDefeat","Route14_Text_KiriPostBattle","EventScript_TryDoDoubleTrainerBattle"),
        ["Route15_EventScript_Yazmin"]=new("pret:TRAINER_PICNICKER_YAZMIN",0,8,"Route15_Text_YazminIntro","Route15_Text_YazminDefeat","Route15_Text_YazminPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Edwin"]=new("pret:TRAINER_BIRD_KEEPER_EDWIN",0,8,"Route15_Text_EdwinIntro","Route15_Text_EdwinDefeat","Route15_Text_EdwinPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Chester"]=new("pret:TRAINER_BIRD_KEEPER_CHESTER",0,8,"Route15_Text_ChesterIntro","Route15_Text_ChesterDefeat","Route15_Text_ChesterPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Kindra"]=new("pret:TRAINER_PICNICKER_KINDRA",0,8,"Route15_Text_KindraIntro","Route15_Text_KindraDefeat","Route15_Text_KindraPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Olivia"]=new("pret:TRAINER_BEAUTY_OLIVIA",0,8,"Route15_Text_OliviaIntro","Route15_Text_OliviaDefeat","Route15_Text_OliviaPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Alex"]=new("pret:TRAINER_BIKER_ALEX",0,8,"Route15_Text_AlexIntro","Route15_Text_AlexDefeat","Route15_Text_AlexPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Ernest"]=new("pret:TRAINER_BIKER_ERNEST",0,8,"Route15_Text_ErnestIntro","Route15_Text_ErnestDefeat","Route15_Text_ErnestPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Becky"]=new("pret:TRAINER_PICNICKER_BECKY",0,8,"Route15_Text_BeckyIntro","Route15_Text_BeckyDefeat","Route15_Text_BeckyPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Grace"]=new("pret:TRAINER_BEAUTY_GRACE",0,8,"Route15_Text_GraceIntro","Route15_Text_GraceDefeat","Route15_Text_GracePostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Celia"]=new("pret:TRAINER_PICNICKER_CELIA",0,8,"Route15_Text_CeliaIntro","Route15_Text_CeliaDefeat","Route15_Text_CeliaPostBattle","EventScript_TryDoNormalTrainerBattle"),
        ["Route15_EventScript_Ron"]=new("pret:TRAINER_CRUSH_KIN_RON_MYA",4,9,"Route15_Text_RonIntro","Route15_Text_RonDefeat","Route15_Text_RonPostBattle","EventScript_TryDoDoubleTrainerBattle"),
        ["Route15_EventScript_Mya"]=new("pret:TRAINER_CRUSH_KIN_RON_MYA",4,9,"Route15_Text_MyaIntro","Route15_Text_MyaDefeat","Route15_Text_MyaPostBattle","EventScript_TryDoDoubleTrainerBattle"),
    }.ToImmutableDictionary(StringComparer.Ordinal);
    public static ImmutableArray<TrainerId> SourceTrainers { get; } = [new("pret:TRAINER_BEAUTY_GRACE"), new("pret:TRAINER_BEAUTY_LOLA"), new("pret:TRAINER_BEAUTY_OLIVIA"), new("pret:TRAINER_BEAUTY_SHEILA"), new("pret:TRAINER_BIKER_ALEX"), new("pret:TRAINER_BIKER_ERNEST"), new("pret:TRAINER_BIKER_GERALD"), new("pret:TRAINER_BIKER_ISAAC"), new("pret:TRAINER_BIKER_JARED"), new("pret:TRAINER_BIKER_LUKAS"), new("pret:TRAINER_BIKER_MALIK"), new("pret:TRAINER_BIRD_KEEPER_BECK"), new("pret:TRAINER_BIRD_KEEPER_BENNY"), new("pret:TRAINER_BIRD_KEEPER_CARTER"), new("pret:TRAINER_BIRD_KEEPER_CHESTER"), new("pret:TRAINER_BIRD_KEEPER_DONALD"), new("pret:TRAINER_BIRD_KEEPER_EDWIN"), new("pret:TRAINER_BIRD_KEEPER_MARLON"), new("pret:TRAINER_BIRD_KEEPER_MITCH"), new("pret:TRAINER_BIRD_KEEPER_PERRY"), new("pret:TRAINER_BIRD_KEEPER_ROBERT"), new("pret:TRAINER_BIRD_KEEPER_SEBASTIAN"), new("pret:TRAINER_CAMPER_JUSTIN"), new("pret:TRAINER_CRUSH_KIN_RON_MYA"), new("pret:TRAINER_FISHERMAN_ANDREW"), new("pret:TRAINER_FISHERMAN_CHIP"), new("pret:TRAINER_FISHERMAN_ELLIOT"), new("pret:TRAINER_FISHERMAN_HANK"), new("pret:TRAINER_FISHERMAN_NED"), new("pret:TRAINER_PICNICKER_ALMA"), new("pret:TRAINER_PICNICKER_BECKY"), new("pret:TRAINER_PICNICKER_CELIA"), new("pret:TRAINER_PICNICKER_GWEN"), new("pret:TRAINER_PICNICKER_KINDRA"), new("pret:TRAINER_PICNICKER_SUSIE"), new("pret:TRAINER_PICNICKER_VALERIE"), new("pret:TRAINER_PICNICKER_YAZMIN"), new("pret:TRAINER_ROCKER_LUCA"), new("pret:TRAINER_TWINS_KIRI_JAN"), new("pret:TRAINER_YOUNG_COUPLE_GIA_JES")];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = [new("pret:TRAINER_BEAUTY_GRACE"), new("pret:TRAINER_BEAUTY_LOLA"), new("pret:TRAINER_BEAUTY_OLIVIA"), new("pret:TRAINER_BEAUTY_SHEILA"), new("pret:TRAINER_BIKER_ALEX"), new("pret:TRAINER_BIKER_ERNEST"), new("pret:TRAINER_BIKER_GERALD"), new("pret:TRAINER_BIKER_ISAAC"), new("pret:TRAINER_BIKER_JARED"), new("pret:TRAINER_BIKER_LUKAS"), new("pret:TRAINER_BIKER_MALIK"), new("pret:TRAINER_BIRD_KEEPER_BECK"), new("pret:TRAINER_BIRD_KEEPER_BENNY"), new("pret:TRAINER_BIRD_KEEPER_CARTER"), new("pret:TRAINER_BIRD_KEEPER_CHESTER"), new("pret:TRAINER_BIRD_KEEPER_DONALD"), new("pret:TRAINER_BIRD_KEEPER_EDWIN"), new("pret:TRAINER_BIRD_KEEPER_MARLON"), new("pret:TRAINER_BIRD_KEEPER_MITCH"), new("pret:TRAINER_BIRD_KEEPER_PERRY"), new("pret:TRAINER_BIRD_KEEPER_ROBERT"), new("pret:TRAINER_BIRD_KEEPER_SEBASTIAN"), new("pret:TRAINER_CAMPER_JUSTIN"), new("pret:TRAINER_FISHERMAN_ANDREW"), new("pret:TRAINER_FISHERMAN_CHIP"), new("pret:TRAINER_FISHERMAN_ELLIOT"), new("pret:TRAINER_FISHERMAN_HANK"), new("pret:TRAINER_FISHERMAN_NED"), new("pret:TRAINER_PICNICKER_ALMA"), new("pret:TRAINER_PICNICKER_BECKY"), new("pret:TRAINER_PICNICKER_CELIA"), new("pret:TRAINER_PICNICKER_GWEN"), new("pret:TRAINER_PICNICKER_KINDRA"), new("pret:TRAINER_PICNICKER_SUSIE"), new("pret:TRAINER_PICNICKER_VALERIE"), new("pret:TRAINER_PICNICKER_YAZMIN"), new("pret:TRAINER_ROCKER_LUCA")];
    public static ImmutableArray<TrainerId> DeferredDoubleTrainers { get; } = [new("pret:TRAINER_CRUSH_KIN_RON_MYA"), new("pret:TRAINER_TWINS_KIRI_JAN"), new("pret:TRAINER_YOUNG_COUPLE_GIA_JES")];
    public static void ValidateCatalog(FuchsiaRouteTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    private static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (!Entries.TryGetValue(battle.ScriptEntry, out var entry) || battle.Trainer.Value != entry.Trainer ||
            battle.Mode != entry.Mode || battle.BattleTypeFlags != entry.Flags || battle.IntroText != entry.Intro ||
            battle.DefeatText != entry.Defeat || battle.PostBattleText != entry.Post ||
            battle.EntryScript != entry.EntryScript || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("fuchsia_trainer_battle_binding");
    }
    public static TrainerPartyCreationResult Create(FuchsiaRouteTrainerCatalog trainers,PokemonCatalog pokemon,TrainerId id,RngState rng)
    {
        ValidateCatalog(trainers);
        if(!trainers.Trainers.TryGetValue(id,out var trainer))throw new NotSupportedException("Unknown Fuchsia route trainer.");
        if(trainer.DoubleBattle)throw new NotSupportedException("fuchsia_route_preset_double_requires_2v2");
        PokemonFactory.ValidateCatalog(pokemon);
        var made=TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if((trainer.PartyFlags&1)==0)return made;
        return made with {Party=made.Party.Select((p,i)=>p with {Pokemon=p.Pokemon with {Moves=trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE").Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray()}}).ToImmutableArray()};
    }
}
