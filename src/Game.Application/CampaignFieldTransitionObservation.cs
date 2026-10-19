using System.Text.Json.Serialization;
using Game.Domain.World;

namespace Game.Application;

// Absent on older records and headless transitions. These are observed presentation
// callbacks, not a guessed music duration or an executable gameplay continuation.
public sealed record CampaignQuestLogDoorAnchor(int X,int Y);
public sealed record CampaignQuestLogTransitionTiming(FieldTransitionCause Cause,int SwitchFrame,int EndFrame)
{
    // Departure cause and arrival branch are independent. Missing fields retain the
    // original ordinary timing shape; an unobserved historical marker stays unknown.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public FieldTransitionArrival Arrival{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignQuestLogDoorAnchor? DepartureDoor{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignQuestLogDoorAnchor? ArrivalDoor{get;init;}

    internal bool HasValidPhases()
    {
        if(SwitchFrame<0||EndFrame<=SwitchFrame)return false;
        if(Cause==FieldTransitionCause.ConnectedEdge)
            return Arrival==FieldTransitionArrival.Plain&&SwitchFrame==0&&EndFrame==1;
        if(Cause is not (FieldTransitionCause.PlainWarp or FieldTransitionCause.AnimatedDoor or FieldTransitionCause.NonAnimatedDoor)||
            Arrival is not (FieldTransitionArrival.Plain or FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor))return false;
        int minimum=FieldDoorPresentation.DepartureFrames(Cause);
        int arrival=FieldDoorPresentation.ArrivalFrames(Arrival);
        return SwitchFrame>=minimum&&EndFrame-SwitchFrame==arrival;
    }
}

public static partial class CampaignQuestLogRecorder
{
    private static int TransitionTimingBytes(CampaignQuestLogTransitionTiming? timing)=>timing is null?0:
        8+(timing.Arrival==FieldTransitionArrival.Plain?0:1)+(timing.DepartureDoor is null?0:4)+(timing.ArrivalDoor is null?0:4);

    private static string? ValidateTransitionTiming(CampaignQuestLogAction action,CampaignQuestLogActor player)
    {
        if(action.TransitionTiming is not {} timing)return null;
        if(!timing.HasValidPhases()||timing.EndFrame>255||action.Frames!=timing.EndFrame||
            action.StartFrame>ushort.MaxValue-timing.EndFrame)return "save_quest_log_transition_timing_invalid";
        static bool Anchor(CampaignQuestLogDoorAnchor? door,CampaignQuestLogActor actor,Direction facing)=>
            door is {X:>=0 and <1024,Y:>=0 and <1024}&&door.X==actor.X&&door.Y==actor.Y-1&&actor.Facing==facing;
        bool validDeparture=timing.Cause switch
        {
            FieldTransitionCause.AnimatedDoor=>Anchor(timing.DepartureDoor,player,Direction.North),
            FieldTransitionCause.NonAnimatedDoor=>timing.DepartureDoor is {X:>=0 and <1024,Y:>=0 and <1024} step&&
                Math.Abs(step.X-player.X)+Math.Abs(step.Y-player.Y)==1,
            _=>timing.DepartureDoor is null
        };
        if(!validDeparture)return "save_quest_log_transition_timing_invalid";
        if(timing.Arrival is FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor)
        {
            if(action.Transition is not {} projection||projection.Actors.IsDefault||
                projection.Actors.FirstOrDefault(a=>a?.Id==PlayerId) is not {} destination||
                !Anchor(timing.ArrivalDoor,destination,Direction.South))return "save_quest_log_transition_timing_invalid";
        }
        else if(timing.ArrivalDoor is not null)return "save_quest_log_transition_timing_invalid";
        return null;
    }
}

public sealed partial class CampaignSession
{
    private sealed record TransitionObservation(FieldTransitionReceipt Receipt,CampaignQuestLogScene Scene,
        CampaignQuestLogAction Marker,int MarkerIndex,CampaignQuestLogTransitionTiming? Timing=null);
    private TransitionObservation? _fieldTransitionObservation;

    /// <summary>The admitted shared presentation capability, never movement legality.</summary>
    public bool CanPresentFieldTransition(FieldTransitionReceipt receipt)=>
        ReferenceEquals(State.FieldTransition,receipt)&&!WalkingFieldTransition(receipt)&&SupportsFieldTransition(State,receipt);

    public bool CanPresentFieldTransition(CampaignState origin,FieldTransitionReceipt receipt)=>
        ReferenceEquals(State.FieldTransition,receipt)&&SupportsFieldTransitionOrigin(origin,receipt)&&SupportsFieldTransition(State,receipt);

    private static bool WalkingFieldTransition(FieldTransitionReceipt receipt)=>
        receipt.Cause is FieldTransitionCause.AnimatedDoor or FieldTransitionCause.NonAnimatedDoor||
        receipt.Arrival is FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor;

    private static bool SupportsFieldTransitionOrigin(CampaignState origin,FieldTransitionReceipt receipt)=>
        receipt.From==origin.World.Player&&origin.FieldWeather is null&&
        (!WalkingFieldTransition(receipt)||!origin.IsCycling&&!origin.IsSurfing&&IsStableFieldForParty(origin));

    private bool SupportsFieldTransition(CampaignState state,FieldTransitionReceipt receipt)
    {
        if(receipt.To!=state.World.Player||!IsStableFieldForParty(state)||state.FieldWeather is not null||
            receipt.Departure!=(receipt.Cause==FieldTransitionCause.NonAnimatedDoor?FieldTransitionDeparture.StepOntoWarp:FieldTransitionDeparture.Immediate)||
            receipt.Arrival is not (FieldTransitionArrival.Plain or FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor)||
            receipt.Cause is not (FieldTransitionCause.ConnectedEdge or FieldTransitionCause.PlainWarp or FieldTransitionCause.AnimatedDoor or FieldTransitionCause.NonAnimatedDoor))return false;
        if(WalkingFieldTransition(receipt)&&(state.IsCycling||state.IsSurfing))return false;
        if(receipt.Cause==FieldTransitionCause.NonAnimatedDoor)
        {
            if(receipt.DepartureStep is not {X:>=0 and <1024,Y:>=0 and <1024} step||step.MapId!=receipt.From.MapId)return false;
            var delta=step.Facing switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.West=>(-1,0),Direction.East=>(1,0),_=>(0,0)};
            if(delta==(0,0)||step.X!=receipt.From.X+delta.Item1||step.Y!=receipt.From.Y+delta.Item2)return false;
        }
        else if(receipt.DepartureStep is not null)return false;
        if(receipt.Cause==FieldTransitionCause.ConnectedEdge&&receipt.Arrival!=FieldTransitionArrival.Plain||
            receipt.Cause==FieldTransitionCause.AnimatedDoor&&(receipt.From.Facing!=Direction.North||receipt.From.Y<1)||
            receipt.Arrival is (FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor)&&
                (receipt.ArrivalAnchor.MapId!=receipt.To.MapId||receipt.ArrivalAnchor.X!=receipt.To.X||
                 receipt.ArrivalAnchor.Y+1!=receipt.To.Y||receipt.To.Facing!=Direction.South))return false;
        var from=_content.MapDefinition(receipt.From.MapId);
        var to=_content.MapDefinition(receipt.To.MapId);
        // Both source preview paths are skipped within the same named section.
        // Cross-section preview behavior needs its own presentation contract.
        if(receipt.Cause!=FieldTransitionCause.ConnectedEdge&&
            (string.IsNullOrEmpty(from.RegionMapSection)||from.RegionMapSection!=to.RegionMapSection))return false;
        static bool OrdinaryMap(FieldMapDefinition map)=>
            (map.MapType is "MAP_TYPE_TOWN" or "MAP_TYPE_CITY" or "MAP_TYPE_ROUTE" or "MAP_TYPE_INDOOR")&&
            (map.Weather is "WEATHER_NONE" or "WEATHER_SUNNY")&&!map.RequiresFlash;
        return OrdinaryMap(from)&&OrdinaryMap(to);
    }

    // Called once by the owner of a completed live transition. The receipt instance
    // and provisional marker/caption suffix authenticate this observation across harmless time
    // commits. No campaign snapshot, generation, RNG or gameplay state is changed.
    public bool CompleteFieldTransitionPresentation(FieldTransitionReceipt receipt,int switchFrame,int endFrame)
    {
        if(_fieldTransitionObservation is not {} pending||!ReferenceEquals(receipt,pending.Receipt)||
            !ObservationMatches(State,pending))return false;
        var timing=new CampaignQuestLogTransitionTiming(receipt.Cause,switchFrame,endFrame)
        {
            Arrival=receipt.Arrival,
            DepartureDoor=receipt.Cause==FieldTransitionCause.AnimatedDoor?new(receipt.From.X,receipt.From.Y-1):
                receipt.DepartureStep is {} step?new(step.X,step.Y):null,
            ArrivalDoor=receipt.Arrival is FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor?new(receipt.ArrivalAnchor.X,receipt.ArrivalAnchor.Y):null
        };
        if(!timing.HasValidPhases())return false;
        if(pending.Timing is not null)return pending.Timing==timing;
        _fieldTransitionObservation=pending with{Timing=timing};
        _questLogFieldClock.AdvanceTo((int)Math.Min(CampaignQuestLogFieldClock.FrameLimit,(long)pending.Marker.StartFrame+endFrame));
        if(endFrame>255||pending.Marker.StartFrame>ushort.MaxValue-endFrame)_questLogRecordingOpen=false;
        return true;
    }

    private static bool ObservationMatches(CampaignState state,TransitionObservation pending)=>
        state.World.Player==pending.Receipt.To&&state.QuestLog is {} log&&!log.Scenes.IsEmpty&&
        ReferenceEquals(log.Scenes[^1],pending.Scene)&&!pending.Scene.Actions.IsEmpty&&
        ReferenceEquals(pending.Scene.Actions[pending.MarkerIndex],pending.Marker);

    // Pure projection: accepted commands publish it through the existing transaction;
    // PrepareFieldSave also sees a completed receipt before another command occurs.
    private CampaignState ReconcileFieldTransitionObservation(CampaignState state)
    {
        if(_fieldTransitionObservation is not {Timing:{} timing} pending||!ObservationMatches(state,pending))return state;
        var log=state.QuestLog!;
        var marker=pending.Marker with{Frames=timing.EndFrame,TransitionTiming=timing};
        var actions=pending.Scene.Actions.SetItem(pending.MarkerIndex,marker);
        long shift=(long)timing.EndFrame-pending.Marker.Frames;
        bool captionFits=true;
        for(int i=pending.MarkerIndex+1;i<actions.Length;i++)
        {
            long start=(long)actions[i].StartFrame+shift;
            if(start>ushort.MaxValue){captionFits=false;break;}
            actions=actions.SetItem(i,actions[i] with{StartFrame=(int)start});
        }
        var scene=pending.Scene with{Actions=actions};
        var source=(_content as ICampaignQuestLogCatalog)?.QuestLogSource;
        bool fits=captionFits&&timing.EndFrame<=255&&pending.Marker.StartFrame<=ushort.MaxValue-timing.EndFrame&&
            CampaignQuestLogRecorder.FitsActions(scene.Actions,source)&&CampaignQuestLogRecorder.VisualMemoryBudget(scene)<=CampaignQuestLogRecorder.VisualMemoryLimit;
        var updated=fits?new CampaignQuestLog(log.Scenes.SetItem(log.Scenes.Length-1,scene)):CampaignQuestLogRecorder.Close(log);
        return state with{QuestLog=updated};
    }

    private void CaptureFieldTransitionObservation(CampaignState before,CampaignState after,CampaignCommand command)
    {
        // A clock commit does not replace the live owner or its provisional marker.
        if(command.Kind=="play_time_tick"&&_fieldTransitionObservation is {Timing:null} active&&ObservationMatches(after,active))return;
        _fieldTransitionObservation=null;
        if(after.FieldTransition is not {} receipt||!SupportsFieldTransitionOrigin(before,receipt)||!SupportsFieldTransition(after,receipt)||
            after.QuestLog is not {} log||log.Scenes.IsEmpty)return;
        var scene=log.Scenes[^1];
        if(scene.Closed||scene.Actions.IsEmpty)return;
        int index=scene.Actions.Length-1;
        if(scene.Actions[index].Transition is null)index--;
        if(index<0||scene.Actions[index] is not {Transition:{} projection,TransitionTiming:null} marker||
            projection.Map!=receipt.To.MapId.Value||before.QuestLog?.Scenes.LastOrDefault()?.Actions.Contains(marker)==true)return;
        if(index!=scene.Actions.Length-1)
        {
            // OnTransition can record the first-arrival caption in the same accepted
            // command. Source overworld playback cannot display it while warp field
            // controls are locked. Authenticate the producer payload, then retain its
            // offset after the provisional marker when observed duration is known.
            var caption=scene.Actions[^1];
            var arrival=(_content as ICampaignQuestLogCatalog)?.QuestLogSource is {} source?
                CampaignQuestLogRecorder.FirstArrival(before,after,source):null;
            if(arrival is null||caption.StartFrame!=marker.StartFrame+marker.Frames||caption.Arguments.IsDefault||
                !caption.Arguments.SequenceEqual(arrival.Arguments)||
                (caption with{StartFrame=arrival.StartFrame,Arguments=arrival.Arguments})!=arrival)return;
        }
        // Input, scripted motion, other semantic tails and historical records do not
        // acquire timing from this bounded observation.
        _fieldTransitionObservation=new(receipt,scene,marker,index);
    }

    private void DiscardUnfinishedFieldTransitionObservation()
    { if(_fieldTransitionObservation?.Timing is null)_fieldTransitionObservation=null; }
}
