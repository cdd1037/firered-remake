using Game.Domain.World;

namespace Game.Application;

// field_specials.c: map-load stage4 first selects an entrance for the NEW map,
// then checks its retained outside destination. Only the two original script fields
// persist; the emitted caption and played-slots marker are live recorder data.
public static class CampaignQuestLogDepartureCapture
{
    public const string EntranceVariable="VAR_QL_ENTRANCE",PendingFlag="FLAG_SYS_QL_DEPARTED";
    public static (CampaignState State,CampaignQuestLogAction? Event,bool ClearPlayedSlots) Observe(
        CampaignState before,CampaignState after,QuestLogSource source,bool playedSlots=false)
    {
        if(before.World.Player.MapId==after.World.Player.MapId)return(after,null,false);
        var script=after.Script;string map=after.World.Player.MapId.Value;
        int entrance=script.Variables.GetValueOrDefault(EntranceVariable);
        bool pending=script.Flags.GetValueOrDefault(PendingFlag);
        var entered=source.Departures.FirstOrDefault(d=>d.Inside==map);
        var retained=source.Departures.FirstOrDefault(d=>d.LocationId==entrance);
        if(entered is not null&&!(retained?.LocationSymbol=="QL_LOCATION_ROCKET_HIDEOUT"&&entered.LocationSymbol=="QL_LOCATION_GAME_CORNER"))
        {entrance=entered.LocationId;pending=true;retained=entered;}
        QuestLogDeparture? departed=null;
        if(pending&&retained is not null)
        {
            if(retained.LocationSymbol is "QL_LOCATION_VIRIDIAN_FOREST_1" or "QL_LOCATION_LEAGUE_GATE_1")
                departed=source.Departures.FirstOrDefault(d=>d.LocationId>=entrance&&d.LocationId<=entrance+1&&d.Outside==map);
            else if(retained.Outside==map)
            {
                departed=retained;
                // Source PlayerGetDestCoords includes the seven-cell map border.
                bool alternate=retained.LocationSymbol switch
                {
                    "QL_LOCATION_ROCK_TUNNEL_1"=>after.World.Player.X!=15-7||after.World.Player.Y!=26-7,
                    "QL_LOCATION_SEAFOAM_ISLANDS_1"=>after.World.Player.X!=67-7||after.World.Player.Y!=15-7,
                    _=>false
                };
                if(alternate)departed=source.Departures.Single(d=>d.LocationId==entrance+1);
            }
        }
        if(departed is not null)
        {
            pending=false;
            if(retained!.LocationSymbol=="QL_LOCATION_ROCKET_HIDEOUT")
            {entrance=source.Departures.Single(d=>d.LocationSymbol=="QL_LOCATION_GAME_CORNER").LocationId;pending=true;}
        }
        // Preserve identity on maps which do not change the original latch.
        if(entrance!=script.Variables.GetValueOrDefault(EntranceVariable))script=script with{Variables=script.Variables.SetItem(EntranceVariable,entrance)};
        if(pending!=script.Flags.GetValueOrDefault(PendingFlag))script=script with{Flags=script.Flags.SetItem(PendingFlag,pending)};
        var state=ReferenceEquals(script,after.Script)?after:after with{Script=script};
        if(departed is null||source.DisabledMaps.Contains(map)||departed.Kind=="GAME_CORNER"&&!playedSlots)return(state,null,false);
        string key=departed.Kind=="GYM"&&departed.BadgeFlag is {} badge&&state.Script.Flags.GetValueOrDefault(badge)?"DepartedGym":departed.TextKey;
        return(state,CampaignQuestLogRecorder.Event(CampaignQuestLogEvent.Departed,key,departed.RegionName,departed.Place) with{LocationId=departed.LocationId},true);
    }
}


// Original sLastDepartedLocation: one live scalar, retained across saves. Continue
// and skip both read the first event of the final scene before resuming play.
public sealed class CampaignQuestLogStartGate
{
    private int? _lastDeparture;
    // The original wild-event pointer survives action-buffer overflow, but not
    // explicit finish/cut, save/restore, another event or a disabled-map boundary.
    public bool WildRecordRetained{get;private set;}
    public void EndRecording()=>WildRecordRetained=false;
    public void NoteEvent(CampaignQuestLogAction action)=>WildRecordRetained=action.Event==CampaignQuestLogEvent.WildWon;
    public CampaignQuestLogStartGate(CampaignQuestLog? restored=null)
    {
        var first=restored?.Scenes.LastOrDefault()?.Actions.FirstOrDefault(CampaignQuestLogRecorder.IsCaption);
        _lastDeparture=first?.Event==CampaignQuestLogEvent.Departed?first.LocationId:null;
    }
    public bool Admit(CampaignQuestLogAction first)
    {
        if(first.Event!=CampaignQuestLogEvent.Departed){_lastDeparture=null;return true;}
        if(first.LocationId is not {} location)return true;
        if(_lastDeparture==location)return false;
        _lastDeparture=location;return true;
    }
}
