using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Trainers;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private int HighestSupportedBadge=>_content.SaffronGymEnabled?6:_content.FuchsiaGymEnabled?5:_content.CeladonGymEnabled?4:_content.VermilionGymEnabled?3:_content.CeruleanEnabled?2:_content.PewterEnabled?1:0;
    private bool HasUnimplementedBadge(CampaignState state)=>Enumerable.Range(HighestSupportedBadge+1,8-HighestSupportedBadge)
        .Any(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"));
    public int WhiteOutMoneyLoss=>WhiteOutLoss(State);
    private int WhiteOutLoss(CampaignState state)=>Math.Min(state.Money,state.Party.Max(p=>p.Pokemon.Level)*(_content.SaffronGymEnabled?4*new[]{2,4,6,9,12,16,20}[Enumerable.Range(1,6).Count(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))]:_content.FuchsiaGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET")?64:_content.CeladonGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE04_GET")?48:_content.VermilionGymEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET")?36:_content.CeruleanEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET")?24:_content.PewterEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET")?16:8));
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
    private TrainerDefinition OrdinaryTrainer(TrainerId id)=>IsSaffronGymTrainer(id)?_content.SaffronGymTrainerParties!.Trainers[id]:IsSilphTrainer(id)?_content.SilphTrainerParties!.Trainers[id]:IsFuchsiaGymTrainer(id)?_content.FuchsiaGymTrainerParties!.Trainers[id]:IsFuchsiaRouteTrainer(id)?_content.FuchsiaRouteTrainers!.Trainers[id]:IsPokemonTowerTrainer(id)?_content.PokemonTowerTrainers!.Trainers[id]:IsRocketHideoutTrainer(id)?_content.RocketHideoutTrainers!.Trainers[id]:IsCeladonGymTrainer(id)?_content.CeladonGymTrainers!.Trainers[id]:IsCeladonApproachTrainer(id)?_content.CeladonApproachTrainers!.Trainers[id]:IsRockTunnelTrainer(id)?_content.RockTunnelTrainers!.Trainers[id]:IsRoute11Trainer(id)?_content.Route11Trainers!.Trainers[id]:IsVermilionGymTrainer(id)?_content.VermilionGymTrainers!.Trainers[id]:IsSsAnneTrainer(id)?_content.SsAnneTrainers!.Trainers[id]:IsVermilionTrainer(id)?_content.VermilionTrainers!.Trainers[id]:IsBillTrainer(id)?_content.BillTrainers!.Trainers[id]:IsCeruleanTrainer(id)?_content.CeruleanTrainers!.Trainers[id]:IsMtMoonTrainer(id)?_content.MtMoonTrainers!.Trainers[id]:IsRoute3Trainer(id)?_content.Route3Trainers!.Trainers[id]:IsRoute22Trainer(id)?_content.Route22Trainers!.Trainers[id]:IsPewterTrainer(id)?_content.PewterTrainers!.Trainers[id]:ForestTrainers.Trainers[id];
    // PlayTrainerEncounterMusic masks F_TRAINER_FEMALE; the remaining source music
    // category selects one of three songs, independent of the sprite's gender.
    private string OrdinaryEncounterMusic(TrainerId id)=>(OrdinaryTrainer(id).EncounterMusicGender & 0x7f) switch
    {
        1 or 2 or 9=>"MUS_ENCOUNTER_GIRL",
        0 or 4 or 5 or 8 or 10 or 11 or 12 or 13=>"MUS_ENCOUNTER_BOY",
        _=>"MUS_ENCOUNTER_ROCKET"
    };
    private ForestTrainerBattleDefinition OrdinaryTrainerScript(TrainerId id)
    {
        if(IsSaffronGymTrainer(id)||IsSilphTrainer(id)||IsFuchsiaGymTrainer(id)||IsFuchsiaRouteTrainer(id)||IsPokemonTowerTrainer(id)||IsRocketHideoutTrainer(id)||IsCeladonGymTrainer(id)||IsCeladonApproachTrainer(id)||IsRockTunnelTrainer(id)||IsRoute11Trainer(id)||IsVermilionGymTrainer(id)||IsSsAnneTrainer(id)||IsVermilionTrainer(id)||IsBillTrainer(id)||IsCeruleanTrainer(id))
        {
            var c=IsSaffronGymTrainer(id)?_content.SaffronGymTrainerParties!.Battles[id]:IsSilphTrainer(id)?_content.SilphTrainerParties!.Battles[id]:IsFuchsiaGymTrainer(id)?_content.FuchsiaGymTrainerParties!.Battles[id]:IsFuchsiaRouteTrainer(id)?_content.FuchsiaRouteTrainers!.Battles.Values.Single(b=>b.Trainer==id):IsPokemonTowerTrainer(id)?_content.PokemonTowerTrainers!.Battles[id]:IsRocketHideoutTrainer(id)?_content.RocketHideoutTrainers!.Battles[id]:IsCeladonGymTrainer(id)?_content.CeladonGymTrainers!.Battles[id]:IsCeladonApproachTrainer(id)?_content.CeladonApproachTrainers!.Battles[id]:IsRockTunnelTrainer(id)?_content.RockTunnelTrainers!.Battles[id]:IsRoute11Trainer(id)?_content.Route11Trainers!.Battles[id]:IsVermilionGymTrainer(id)?_content.VermilionGymTrainers!.Battles[id]:IsSsAnneTrainer(id)?_content.SsAnneTrainers!.Battles[id]:IsVermilionTrainer(id)?_content.VermilionTrainers!.Battles[id]:IsBillTrainer(id)?_content.BillTrainers!.Battles[id]:_content.CeruleanTrainers!.Battles[id];
            return new(c.Trainer,c.Mode,c.BattleTypeFlags,c.ScriptEntry,c.IntroText,c.DefeatText,c.PostBattleText,c.EntryScript,c.Callback,c.Source);
        }
        if(IsMtMoonTrainer(id))return _content.MtMoonTrainers!.Battles[id];
        if(IsRoute3Trainer(id))return _content.Route3Trainers!.Battles[id];
        if(IsRoute22Trainer(id))
        {
            var r=_content.Route22Trainers!.Battles[id];
            return new(r.Trainer,r.Mode,r.BattleTypeFlags,r.ScriptEntry,"Route22_Text_EarlyRivalIntro",r.DefeatText,"Route22_Text_EarlyRivalPostBattle",r.EntryScript,r.Callback,r.Source);
        }
        if(!IsPewterTrainer(id))return ForestTrainers.Battles[id];
        var d=_content.PewterTrainers!.Battles[id];
        return new(d.Trainer,d.Mode,d.BattleTypeFlags,d.ScriptEntry,d.IntroText,d.DefeatText,d.PostBattleText,d.EntryScript,d.Callback,d.Source);
    }
    private IEnumerable<ForestTrainerBattleDefinition> OrdinaryTrainerScripts=>ForestTrainers.Battles.Values.Concat(
        _content.PewterTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.Route22Trainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.Route3Trainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.MtMoonTrainers?.Battles.Values??[]).Concat(_content.CeruleanTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.BillTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.VermilionTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.SsAnneTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.VermilionGymTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.Route11Trainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.RockTunnelTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.CeladonApproachTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.CeladonGymTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.RocketHideoutTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.PokemonTowerTrainers?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.FuchsiaRouteEnabled?FuchsiaRouteTrainerPartyFactory.SupportedTrainers.Select(OrdinaryTrainerScript):[]).Concat(_content.FuchsiaGymTrainerParties?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.SilphTrainerParties?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).Concat(_content.SaffronGymTrainerParties?.Battles.Keys.Select(OrdinaryTrainerScript)??[]).DistinctBy(x=>x.Trainer);
    private IEnumerable<TrainerId> OrdinaryTrainerIds=>ForestTrainers.Trainers.Keys.Concat(_content.PewterTrainers?.Trainers.Keys??[]).Concat(_content.Route22Trainers?.Trainers.Keys??[]).Concat(_content.Route3Trainers?.Trainers.Keys??[]).Concat(_content.MtMoonTrainers?.Trainers.Keys??[]).Concat(_content.CeruleanTrainers?.Trainers.Keys??[]).Concat(_content.BillTrainers?.Trainers.Keys??[]).Concat(_content.VermilionTrainers?.Trainers.Keys??[]).Concat(_content.SsAnneTrainers?.Trainers.Keys??[]).Concat(_content.VermilionGymTrainers?.Trainers.Keys??[]).Concat(_content.Route11Trainers?.Trainers.Keys??[]).Concat(_content.RockTunnelTrainers?.Trainers.Keys??[]).Concat(_content.CeladonApproachTrainers?.Trainers.Keys??[]).Concat(_content.CeladonGymTrainers?.Trainers.Keys??[]).Concat(_content.RocketHideoutTrainers?.Trainers.Keys??[]).Concat(_content.PokemonTowerTrainers?.Trainers.Keys??[]).Concat(_content.FuchsiaRouteEnabled?FuchsiaRouteTrainerPartyFactory.SupportedTrainers:[]).Concat(_content.FuchsiaGymTrainerParties?.Trainers.Keys??[]).Concat(_content.SilphTrainerParties?.Trainers.Keys??[]).Concat(_content.SaffronGymTrainerParties?.Trainers.Keys??[]).Distinct();
    private string OrdinaryTrainerClass(TrainerId id)=>OrdinaryTrainer(id).TrainerClass switch
    {
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
