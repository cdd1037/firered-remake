using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Trainers;
namespace Game.Application;

public sealed partial class CampaignSession
{
    public int WhiteOutMoneyLoss=>WhiteOutLoss(State);
    private int WhiteOutLoss(CampaignState state)=>ViridianGymWhiteoutRules.Loss(state.Money,state.Party.Max(p=>p.Pokemon.Level),Enumerable.Range(1,8).Count(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET")));
    private bool IsVictoryRoadTrainer(TrainerId id)=>_content.VictoryRoadTrainerParties?.Trainers.TryGetValue(id,out var trainer)==true&&!trainer.DoubleBattle;
    private bool IsLeagueRivalTrainer(TrainerId id)=>_content.LeagueRivalTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsFirstSeviiTrainer(TrainerId id)=>_content.FirstSeviiTrainerParties?.Trainers.TryGetValue(id,out var trainer)==true&&!trainer.DoubleBattle;
    private bool IsCinnabarTrainer(TrainerId id)=>_content.CinnabarTrainerParties?.Trainers.TryGetValue(id,out var trainer)==true&&!trainer.DoubleBattle;
    private bool IsSilphRival(TrainerId id)=>TrainerRegisteredOnMap(id, "map.silph-co-7f")&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_LATE";
    private bool IsFuchsiaRouteTrainer(TrainerId id)=>_content.FuchsiaRouteTrainers?.Trainers.TryGetValue(id,out var t)==true&&!t.DoubleBattle;
    private bool IsPokemonTowerRival(TrainerId id)=>TrainerRegisteredOnMap(id, "map.pokemon-tower-2f")&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_LATE";
    private bool IsSsAnneRival(TrainerId id)=>TrainerRegisteredOnMap(id, "map.ssanne-2f-corridor")&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_LATE";
    private bool IsCeruleanRival(TrainerId id)=>TrainerRegisteredOnMap(id, "map.cerulean-city")&&OrdinaryTrainer(id).TrainerClass=="TRAINER_CLASS_RIVAL_EARLY";
    private bool IsRoute22Trainer(TrainerId id)=>_content.Route22Trainers?.Trainers.ContainsKey(id)==true;
    private bool TrainerRegisteredOnMap(TrainerId trainer, string map) =>
        TrainerOccurrences.ByTrainer.TryGetValue(trainer, out var occurrences) &&
        occurrences.Any(occurrence => occurrence.Map.Value == map);
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
    private IEnumerable<ForestTrainerBattleDefinition> OrdinaryTrainerScripts => TrainerOccurrences.Primary.Values.Select(occurrence => occurrence.Source.Definition);
    private IEnumerable<TrainerId> OrdinaryTrainerIds => OrdinaryTrainerBindings.Keys;
    private string OrdinaryTrainerClass(TrainerId id) => _content.TrainerClassDisplayName(OrdinaryTrainer(id).TrainerClass);
    private int OrdinaryWonFlagCount(CampaignState state)=>state.BattledTrainers.Count(x=>OrdinaryTrainerIds.Contains(new TrainerId(x)));
}
