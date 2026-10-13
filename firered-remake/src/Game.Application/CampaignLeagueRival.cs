using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The original scene-3 Route22 encounter. The source caller owns approach,
/// post-battle speech, removal and scene 4; ordinary trainer combat owns all results.</summary>
public sealed partial class CampaignSession
{
    private static readonly MapId LeagueRivalMap=new("map.route-22");
    private static TrainerId LeagueRivalForStarter(int starter)=>new(starter switch
    {
        2=>"pret:TRAINER_RIVAL_ROUTE22_LATE_SQUIRTLE",
        1=>"pret:TRAINER_RIVAL_ROUTE22_LATE_BULBASAUR",
        0=>"pret:TRAINER_RIVAL_ROUTE22_LATE_CHARMANDER",
        _=>throw new InvalidDataException("league_rival_starter_invalid")
    });

    private static void ValidateLeagueRivalCatalog(ICampaignCatalog content)
    {
        if(!content.LeagueRivalEnabled)
        {
            if(content.LeagueRivalTrainerParties is not null)throw new InvalidDataException("league_rival_profile_missing");
            return;
        }
        if(!content.ViridianGymEnabled||content.LeagueRivalTrainerParties is null||content.CinnabarOwnership is null||
            content.CinnabarOpponents is null||!ReferenceEquals(content.CinnabarOwnership,content.CinnabarOpponents.Owned)||
            content.CinnabarOwnership.Catalog.Species.Count!=115||content.Maps.Count!=(content.FiveIslandEnabled?254:content.SixIslandEnabled?249:content.FourIslandEnabled?237:content.SeviiRubyEnabled?229:content.PokemonLeagueEnabled?218:content.VictoryRoadEnabled?211:207))
            throw new InvalidDataException("league_rival_source_profile_missing");
        LeagueRivalTrainerPartyFactory.ValidateCatalog(content.LeagueRivalTrainerParties);
        var map=content.MapDefinition(LeagueRivalMap);
        var actor=map.Actors.SingleOrDefault(a=>a.LocalId==Route22RivalActor);
        if(actor is null||actor.ScriptId!="0x0"||actor.GraphicsId!="OBJ_EVENT_GFX_BLUE"||
            actor.X!=25||actor.Y!=4||actor.Elevation!=3||actor.HideFlag!="FLAG_HIDE_ROUTE_22_RIVAL"||
            actor.TrainerType!="TRAINER_TYPE_NONE")throw new InvalidDataException("league_rival_source_actor");
        FieldCoordTrigger[] expected=[
            new(33,4,3,"VAR_MAP_SCENE_ROUTE22",3,"Route22_EventScript_LateRivalTriggerTop"),
            new(33,5,3,"VAR_MAP_SCENE_ROUTE22",3,"Route22_EventScript_LateRivalTriggerMid"),
            new(33,6,0,"VAR_MAP_SCENE_ROUTE22",3,"Route22_EventScript_LateRivalTriggerBottom")];
        if(!map.CoordTriggers.Where(t=>t.Variable=="VAR_MAP_SCENE_ROUTE22"&&t.Expected==3).SequenceEqual(expected))
            throw new InvalidDataException("league_rival_source_triggers");
        foreach(var battle in content.LeagueRivalTrainerParties.Battles.Values)
        {
            var program=content.FindProgram(battle.ScriptEntry)??throw new InvalidDataException("league_rival_source_program");
            var call=program.Instructions.SingleOrDefault(i=>i.Op==StoryOp.TrainerBattle);
            if(call is null||call.Opcode!="trainerbattle_no_intro"||call.Source.Path!=battle.Source.Path||
                call.Source.Symbol!=battle.ScriptEntry||call.Source.Line!=battle.Source.StartLine+1||
                !call.Args.SequenceEqual(new[]{battle.Trainer.Value[5..],battle.DefeatText}))
                throw new InvalidDataException("league_rival_source_battle");
            _=content.TextPages(battle.IntroText);_=content.TextPages(battle.DefeatText);_=content.TextPages(battle.PostBattleText);
        }
    }

    private void RequireLeagueRivalOccurrence(CampaignState state,TrainerId trainer,ForestTrainerBattleDefinition definition,bool noIntro)
    {
        var player=state.World.Player;
        int row=state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");
        if(!noIntro||definition.Mode!=3||definition.BattleTypeFlags!=8||player.MapId!=LeagueRivalMap||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE22")!=3||
            !state.Script.Flags.GetValueOrDefault("FLAG_BADGE08_GET")||!ViridianGymVictory(state)||
            trainer!=LeagueRivalForStarter(state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON"))||
            state.BattledTrainers.Contains(trainer.Value)||LeagueRivalVictory(state)||
            row is <0 or >2||player.X!=33||player.Y!=row+4||row<2&&player.Elevation!=3||
            state.LastTalked!=Route22RivalActor||!state.Actors.TryGetValue(Route22RivalActor,out var actor)||!actor.Active||
            actor.ScriptId!="0x0"||state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=actor.SourceLocalId||
            actor.X!=(row==2?33:32)||actor.Y!=(row==0?4:5))
            throw new InvalidDataException("league_rival_source_occurrence");
        // Top/mid preserve the player's entry facing. The bottom source movement
        // itself turns the player upward; no invented trigger direction is required.
    }

    private bool LeagueRivalVictory(CampaignState state)=>_content.LeagueRivalEnabled&&
        (state.ForestTrainerHistory??[]).Any(h=>h is not null&&h.TrainerId is not null&&
            IsLeagueRivalTrainer(new(h.TrainerId))&&h.Outcome==WildBattleOutcome.Won);

    private bool LeagueRivalHistoryOriginMatches(CampaignForestTrainerHistory history)=>
        _content.LeagueRivalTrainerParties?.Battles.TryGetValue(new(history.TrainerId),out var battle)==true&&
        history.SourceScriptEntry==battle.ScriptEntry&&history.SourceScriptLine==battle.Source.StartLine+1;

    private string? ValidateLeagueRivalProgress(CampaignState state)
    {
        bool giovanniWon=false,lateWon=false;
        foreach(var history in state.ForestTrainerHistory??[])
        {
            if(history is null||history.TrainerId is null)continue; // General history validator rejects malformed entries.
            if(history.TrainerId==ViridianGymTrainerPartyFactory.Giovanni.Value&&history.Outcome==WildBattleOutcome.Won)giovanniWon=true;
            if(!LeagueRivalTrainerPartyFactory.SupportedTrainers.Any(t=>t.Value==history.TrainerId))continue;
            int starter=state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON");
            if(!_content.LeagueRivalEnabled||!giovanniWon||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE08_GET")||lateWon||
                starter is <0 or >2||history.TrainerId!=LeagueRivalForStarter(starter).Value||
                !LeagueRivalHistoryOriginMatches(history))return "save_league_rival_history_invalid";
            lateWon=history.Outcome==WildBattleOutcome.Won;
        }
        foreach(var trainer in LeagueRivalTrainerPartyFactory.SupportedTrainers)
            if(state.BattledTrainers.Contains(trainer.Value)&&(!_content.LeagueRivalEnabled||!lateWon||
                state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON") is <0 or >2||
                trainer!=LeagueRivalForStarter(state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON"))))
                return "save_league_rival_flag_invalid";
        // General trainer validation proves opponent/EXP/prize bounds, the unique
        // winner and its fought flag. Route22 validation ties that winner to scene 4.
        return null;
    }
}
