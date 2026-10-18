using Game.Domain.World;

namespace Game.Application;

// Absent on older records and headless transitions. These are observed presentation
// callbacks, not a guessed music duration or an executable gameplay continuation.
public sealed record CampaignQuestLogTransitionTiming(FieldTransitionCause Cause,int SwitchFrame,int EndFrame);

public static partial class CampaignQuestLogRecorder
{
    private static string? ValidateTransitionTiming(CampaignQuestLogAction action)
    {
        if(action.TransitionTiming is not {} timing)return null;
        return timing.Cause is not (FieldTransitionCause.ConnectedEdge or FieldTransitionCause.PlainWarp)||
            timing.EndFrame is <1 or >255||action.Frames!=timing.EndFrame||
            timing.SwitchFrame<0||timing.SwitchFrame>=timing.EndFrame||
            (timing.Cause==FieldTransitionCause.ConnectedEdge?timing.SwitchFrame!=0||timing.EndFrame!=1:
                timing.SwitchFrame<FieldWarpPresentation.MinimumDepartureFrames||timing.EndFrame-timing.SwitchFrame!=FieldWarpPresentation.PlainArrivalFrames)||
            action.StartFrame>ushort.MaxValue-timing.EndFrame
                ?"save_quest_log_transition_timing_invalid":null;
    }
}

public sealed partial class CampaignSession
{
    private sealed record TransitionObservation(FieldTransitionReceipt Receipt,CampaignQuestLogScene Scene,
        CampaignQuestLogAction Marker,CampaignQuestLogTransitionTiming? Timing=null);
    private TransitionObservation? _fieldTransitionObservation;

    /// <summary>The admitted shared presentation capability, never movement legality.</summary>
    public bool CanPresentFieldTransition(FieldTransitionReceipt receipt)=>
        ReferenceEquals(State.FieldTransition,receipt)&&CanPresentFieldTransition(State,receipt);

    private bool CanPresentFieldTransition(CampaignState state,FieldTransitionReceipt receipt)
    {
        if(receipt.To!=state.World.Player||!IsStableFieldForParty(state)||state.FieldWeather is not null||
            receipt.Departure!=FieldTransitionDeparture.Immediate||receipt.Arrival!=FieldTransitionArrival.Plain||
            receipt.Cause is not (FieldTransitionCause.ConnectedEdge or FieldTransitionCause.PlainWarp))return false;
        var from=_content.MapDefinition(receipt.From.MapId);
        var to=_content.MapDefinition(receipt.To.MapId);
        // Both source preview paths are skipped within the same named section.
        // Cross-section preview behavior needs its own presentation contract.
        if(receipt.Cause==FieldTransitionCause.PlainWarp&&
            (string.IsNullOrEmpty(from.RegionMapSection)||from.RegionMapSection!=to.RegionMapSection))return false;
        static bool OrdinaryMap(FieldMapDefinition map)=>
            (map.MapType is "MAP_TYPE_TOWN" or "MAP_TYPE_CITY" or "MAP_TYPE_ROUTE" or "MAP_TYPE_INDOOR")&&
            (map.Weather is "WEATHER_NONE" or "WEATHER_SUNNY")&&!map.RequiresFlash;
        return OrdinaryMap(from)&&OrdinaryMap(to);
    }

    // Called once by the owner of a completed live transition. The receipt instance
    // and last provisional marker authenticate this observation across harmless time
    // commits. No campaign snapshot, generation, RNG or gameplay state is changed.
    public bool CompleteFieldTransitionPresentation(FieldTransitionReceipt receipt,int switchFrame,int endFrame)
    {
        if(_fieldTransitionObservation is not {} pending||!ReferenceEquals(receipt,pending.Receipt)||
            !ObservationMatches(State,pending)||switchFrame<0||endFrame<=switchFrame||
            (receipt.Cause==FieldTransitionCause.ConnectedEdge?switchFrame!=0||endFrame!=1:
                switchFrame<FieldWarpPresentation.MinimumDepartureFrames||endFrame-switchFrame!=FieldWarpPresentation.PlainArrivalFrames))return false;
        var timing=new CampaignQuestLogTransitionTiming(receipt.Cause,switchFrame,endFrame);
        if(pending.Timing is not null)return pending.Timing==timing;
        _fieldTransitionObservation=pending with{Timing=timing};
        _questLogFieldClock.AdvanceTo((int)Math.Min(CampaignQuestLogFieldClock.FrameLimit,(long)pending.Marker.StartFrame+endFrame));
        if(endFrame>255||pending.Marker.StartFrame>ushort.MaxValue-endFrame)_questLogRecordingOpen=false;
        return true;
    }

    private static bool ObservationMatches(CampaignState state,TransitionObservation pending)=>
        state.World.Player==pending.Receipt.To&&state.QuestLog is {} log&&!log.Scenes.IsEmpty&&
        ReferenceEquals(log.Scenes[^1],pending.Scene)&&!pending.Scene.Actions.IsEmpty&&
        ReferenceEquals(pending.Scene.Actions[^1],pending.Marker);

    // Pure projection: accepted commands publish it through the existing transaction;
    // PrepareFieldSave also sees a completed receipt before another command occurs.
    private CampaignState ReconcileFieldTransitionObservation(CampaignState state)
    {
        if(_fieldTransitionObservation is not {Timing:{} timing} pending||!ObservationMatches(state,pending))return state;
        var log=state.QuestLog!;
        var marker=pending.Marker with{Frames=timing.EndFrame,TransitionTiming=timing};
        var scene=pending.Scene with{Actions=pending.Scene.Actions.SetItem(pending.Scene.Actions.Length-1,marker)};
        var source=(_content as ICampaignQuestLogCatalog)?.QuestLogSource;
        bool fits=timing.EndFrame<=255&&pending.Marker.StartFrame<=ushort.MaxValue-timing.EndFrame&&
            CampaignQuestLogRecorder.FitsActions(scene.Actions,source)&&CampaignQuestLogRecorder.VisualMemoryBudget(scene)<=CampaignQuestLogRecorder.VisualMemoryLimit;
        var updated=fits?new CampaignQuestLog(log.Scenes.SetItem(log.Scenes.Length-1,scene)):CampaignQuestLogRecorder.Close(log);
        return state with{QuestLog=updated};
    }

    private void CaptureFieldTransitionObservation(CampaignState before,CampaignState after,CampaignCommand command)
    {
        // A clock commit does not replace the live owner or its provisional marker.
        if(command.Kind=="play_time_tick"&&_fieldTransitionObservation is {Timing:null} active&&ObservationMatches(after,active))return;
        _fieldTransitionObservation=null;
        if(before.FieldWeather is not null||after.FieldTransition is not {} receipt||!CanPresentFieldTransition(after,receipt)||
            receipt.From!=before.World.Player||after.QuestLog is not {} log||log.Scenes.IsEmpty)return;
        var scene=log.Scenes[^1];
        if(scene.Closed||scene.Actions.IsEmpty||scene.Actions[^1] is not {Transition:{} projection,TransitionTiming:null} marker||
            projection.Map!=receipt.To.MapId.Value||before.QuestLog?.Scenes.LastOrDefault()?.Actions.Contains(marker)==true)return;
        // A script/caption/input tail needs a separate source timing contract. This
        // bounded slice only finalizes a transition that is the transaction's last row.
        _fieldTransitionObservation=new(receipt,scene,marker);
    }

    private void DiscardUnfinishedFieldTransitionObservation()
    { if(_fieldTransitionObservation?.Timing is null)_fieldTransitionObservation=null; }
}
