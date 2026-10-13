using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Trainers;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private int HighestSupportedBadge=>_content.ViridianGymEnabled?8:_content.CinnabarEnabled?7:_content.SaffronGymEnabled?6:_content.FuchsiaGymEnabled?5:_content.CeladonGymEnabled?4:_content.VermilionGymEnabled?3:_content.CeruleanEnabled?2:_content.PewterEnabled?1:0;
    private bool HasUnimplementedBadge(CampaignState state)=>Enumerable.Range(HighestSupportedBadge+1,8-HighestSupportedBadge)
        .Any(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"));
    public int WhiteOutMoneyLoss=>WhiteOutLoss(State);
    private int WhiteOutLoss(CampaignState state)=>_content.ViridianGymEnabled?ViridianGymWhiteoutRules.Loss(state.Money,state.Party.Max(p=>p.Pokemon.Level),Enumerable.Range(1,8).Count(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))):Math.Min(state.Money,state.Party.Max(p=>p.Pokemon.Level)*(_content.CinnabarEnabled?4*new[]{2,4,6,9,12,16,20,25}[Enumerable.Range(1,7).Count(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))]:_content.SaffronGymEnabled?4*new[]{2,4,6,9,12,16,20}[Enumerable.Range(1,6).Count(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))]:_content.FuchsiaGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET")?64:_content.CeladonGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE04_GET")?48:_content.VermilionGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET")?36:_content.CeruleanEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET")?24:_content.PewterEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET")?16:8));
    private bool IsVictoryRoadTrainer(TrainerId id)=>_content.VictoryRoadEnabled&&_content.VictoryRoadTrainerParties?.Trainers.TryGetValue(id,out var trainer)==true&&!trainer.DoubleBattle;
    private bool IsLeagueRivalTrainer(TrainerId id)=>_content.LeagueRivalEnabled&&_content.LeagueRivalTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsViridianGymTrainer(TrainerId id)=>_content.ViridianGymEnabled&&_content.ViridianGymTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsFirstSeviiTrainer(TrainerId id)=>_content.FirstSeviiEnabled&&_content.FirstSeviiTrainerParties?.Trainers.TryGetValue(id,out var trainer)==true&&!trainer.DoubleBattle;
    private bool IsCinnabarTrainer(TrainerId id)=>_content.CinnabarTrainerParties?.Trainers.TryGetValue(id,out var trainer)==true&&!trainer.DoubleBattle;
    private bool IsSaffronGymTrainer(TrainerId id)=>_content.SaffronGymTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsSilphTrainer(TrainerId id)=>_content.SilphTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsSilphRival(TrainerId id)=>IsSilphTrainer(id)&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_LATE";
    private bool IsFuchsiaGymTrainer(TrainerId id)=>_content.FuchsiaGymTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsFuchsiaRouteTrainer(TrainerId id)=>_content.FuchsiaRouteTrainers?.Trainers.TryGetValue(id,out var t)==true&&!t.DoubleBattle;
    private bool IsPokemonTowerTrainer(TrainerId id)=>_content.PokemonTowerTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsRocketHideoutTrainer(TrainerId id)=>_content.RocketHideoutTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsCeladonGymTrainer(TrainerId id)=>_content.CeladonGymTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsCeladonApproachTrainer(TrainerId id)=>_content.CeladonApproachTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsRockTunnelTrainer(TrainerId id)=>_content.RockTunnelTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsRoute11Trainer(TrainerId id)=>_content.Route11Trainers?.Trainers.ContainsKey(id)==true;
    private bool IsVermilionGymTrainer(TrainerId id)=>_content.VermilionGymTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsSsAnneTrainer(TrainerId id)=>_content.SsAnneTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsPokemonTowerRival(TrainerId id)=>IsPokemonTowerTrainer(id)&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_LATE";
    private bool IsSsAnneRival(TrainerId id)=>IsSsAnneTrainer(id)&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_LATE";
    private bool IsVermilionTrainer(TrainerId id)=>_content.VermilionTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsBillTrainer(TrainerId id)=>_content.BillTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsCeruleanTrainer(TrainerId id)=>_content.CeruleanTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsCeruleanRival(TrainerId id)=>IsCeruleanTrainer(id)&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_EARLY";
    private bool IsMtMoonTrainer(TrainerId id)=>_content.MtMoonTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsPewterTrainer(TrainerId id)=>_content.PewterTrainers?.Trainers.ContainsKey(id)==true;
    private bool IsRoute3Trainer(TrainerId id)=>_content.Route3Trainers?.Trainers.ContainsKey(id)==true;
    private bool IsRoute22Trainer(TrainerId id)=>_content.Route22Trainers?.Trainers.ContainsKey(id)==true;
    private TrainerDefinition OrdinaryTrainer(TrainerId id) => OrdinaryBinding(id).Definition;
    // PlayTrainerEncounterMusic masks F_TRAINER_FEMALE; the remaining source music
    // category selects one of three songs, independent of the sprite's gender.
    private string OrdinaryEncounterMusic(TrainerId id)=>(OrdinaryTrainer(id).EncounterMusicGender & 0x7f) switch
    {
        1 or 2 or 9=>"MUS_ENCOUNTER_GIRL",
        0 or 4 or 5 or 8 or 10 or 11 or 12 or 13=>"MUS_ENCOUNTER_BOY",
        _=>"MUS_ENCOUNTER_ROCKET"
    };
    private ForestTrainerBattleDefinition OrdinaryTrainerScript(TrainerId id) => OrdinaryTrainerScript(id, null);
    private ForestTrainerBattleDefinition OrdinaryTrainerScript(TrainerId id, string? script, int? sourceLine = null) =>
        BoundTrainerScript(OrdinaryBinding(id), script, sourceLine);
    private IEnumerable<ForestTrainerBattleDefinition> OrdinaryTrainerScripts => OrdinaryTrainerIds.Select(OrdinaryTrainerScript);
    private IEnumerable<TrainerId> OrdinaryTrainerIds => OrdinaryTrainerBindings.Keys;
    private string OrdinaryTrainerClass(TrainerId id)=>OrdinaryTrainer(id).TrainerClass switch
    {
        "TRAINER_CLASS_ELITE_FOUR"=>"ELITE FOUR","TRAINER_CLASS_CHAMPION"=>"CHAMPION",
        "TRAINER_CLASS_CRUSH_GIRL"=>"CRUSH GIRL","TRAINER_CLASS_PKMN_RANGER"=>"POKéMON RANGER",
        "TRAINER_CLASS_BLACK_BELT"=>"BLACK BELT","TRAINER_CLASS_RUIN_MANIAC"=>"RUIN MANIAC",
        "TRAINER_CLASS_CUE_BALL"=>"CUE BALL","TRAINER_CLASS_TUBER"=>"TUBER","TRAINER_CLASS_AROMA_LADY"=>"AROMA LADY",
        "TRAINER_CLASS_BURGLAR"=>"BURGLAR","TRAINER_CLASS_SWIMMER_F"=>"SWIMMER",
        "TRAINER_CLASS_SCIENTIST"=>"SCIENTIST","TRAINER_CLASS_PSYCHIC"=>"PSYCHIC","TRAINER_CLASS_JUGGLER"=>"JUGGLER","TRAINER_CLASS_TAMER"=>"TAMER",
        "TRAINER_CLASS_CHANNELER"=>"CHANNELER","TRAINER_CLASS_BOSS"=>"BOSS","TRAINER_CLASS_POKEMANIAC"=>"POKéMANIAC","TRAINER_CLASS_GAMER"=>"GAMER","TRAINER_CLASS_ENGINEER"=>"ENGINEER","TRAINER_CLASS_SAILOR"=>"SAILOR","TRAINER_CLASS_FISHERMAN"=>"FISHERMAN","TRAINER_CLASS_GENTLEMAN"=>"GENTLEMAN","TRAINER_CLASS_RIVAL_LATE"=>"RIVAL",
        "TRAINER_CLASS_PICNICKER"=>"PICNICKER","TRAINER_CLASS_SWIMMER_M"=>"SWIMMER",
        "TRAINER_CLASS_SUPER_NERD"=>"SUPER NERD","TRAINER_CLASS_HIKER"=>"HIKER","TRAINER_CLASS_TEAM_ROCKET"=>"TEAM ROCKET",
        "TRAINER_CLASS_YOUNGSTER"=>"YOUNGSTER","TRAINER_CLASS_LASS"=>"LASS","TRAINER_CLASS_RIVAL_EARLY"=>"RIVAL","TRAINER_CLASS_BUG_CATCHER"=>"BUG CATCHER","TRAINER_CLASS_CAMPER"=>"CAMPER","TRAINER_CLASS_LEADER"=>"LEADER",
        "TRAINER_CLASS_BIRD_KEEPER"=>"BIRD KEEPER","TRAINER_CLASS_ROCKER"=>"ROCKER","TRAINER_CLASS_BIKER"=>"BIKER","TRAINER_CLASS_BEAUTY"=>"BEAUTY","TRAINER_CLASS_COOLTRAINER"=>"COOLTRAINER",
        _=>throw new InvalidDataException("ordinary_trainer_class_unsupported")
    };
    private bool SeenByOrdinaryTrainer(CampaignState state,int species)=>_content.ForestTrainersEnabled&&(state.ForestTrainerHistory??[]).Any(h=>
        OrdinaryTrainerIds.Contains(new TrainerId(h.TrainerId))&&h.OpponentsSent.Any(n=>n>=0&&n<OrdinaryTrainer(new TrainerId(h.TrainerId)).Party.Length&&OrdinaryTrainer(new TrainerId(h.TrainerId)).Party[n].SourceSpeciesId==species));
    private int OrdinaryWonFlagCount(CampaignState state)=>state.BattledTrainers.Count(x=>OrdinaryTrainerIds.Contains(new TrainerId(x)));
}
