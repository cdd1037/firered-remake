using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogTransitionObservationTests
{
    // Controlled location/recording setup only. The warp, entry scripts, accepted
    // receipt and recording below are produced by the public campaign command.
    private static CampaignSession AtGate(bool record=true)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var session=CampaignSession.CreateCurrent(catalog);
        var state=session.State;
        var id=new MapId("map.route-15-west-entrance-1f");
        Assert.AreEqual(0x62,catalog.Maps[id].SourceBehaviorAt(11,6));
        state=state with
        {
            World=state.World with{Player=new(id,11,6,3,Direction.East)},
            Actors=ImmutableSortedDictionary<string,CampaignActor>.Empty,ObjectSlots=null,
            Script=state.Script with{Completed=true,Locked=false,Wait=null,Stack=[]},
            EntryPrograms=[],ArrivalFacing=null,Dialogue=null,Motions=[],PresentationTicks=0,
            PresentationEffects=[],MapLayout=null,MapEdits=null,PlayerVisible=true
        };
        if(record)state=state with{QuestLog=new([CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource)])};
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,catalog.ContentVersion,session.Revision));
        typeof(CampaignSession).GetField("_questLogRecordingOpen",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,record);
        return session;
    }
    private static FieldTransitionReceipt Warp(CampaignSession session)
    {
        var result=session.Execute("transition-test:"+session.Revision,session.Revision,new("move",Direction.East));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        var receipt=result.State.FieldTransition;
        Assert.IsNotNull(receipt);Assert.AreEqual(FieldTransitionCause.PlainWarp,receipt.Cause);
        Assert.AreEqual("map.route-15",receipt.To.MapId.Value);
        Assert.IsTrue(session.CanPresentFieldTransition(receipt));
        return receipt;
    }
    private static CampaignQuestLogAction Marker(CampaignState state)=>state.QuestLog!.Scenes[^1].Actions.Last(a=>a.Transition is not null);

    [TestMethod]
    public void AcceptedWarpIsUnknownUntilObservedAndSaveProjectionDoesNotMutateState()
    {
        var session=AtGate();var receipt=Warp(session);var state=session.State;long revision=session.Revision;
        Assert.IsNull(Marker(state).TransitionTiming);Assert.AreEqual(1,Marker(state).Frames);
        string original=JsonSerializer.Serialize(state);
        Assert.IsFalse(original.Contains("FieldTransition"));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,50,74));
        Assert.AreSame(state,session.State);Assert.AreEqual(revision,session.Revision);
        Assert.AreEqual(original,JsonSerializer.Serialize(session.State));
        var prepared=session.PrepareFieldSave();
        Assert.AreEqual(new CampaignQuestLogTransitionTiming(FieldTransitionCause.PlainWarp,50,74),Marker(prepared).TransitionTiming);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(prepared.QuestLog,CurrentCampaignFixture.Catalog.QuestLogSource,CurrentCampaignFixture.Catalog));
        Assert.AreEqual(JsonSerializer.Serialize(prepared),JsonSerializer.Serialize(session.PrepareFieldSave()));
        Assert.AreSame(state,session.State);
    }

    [TestMethod]
    public void CompletionIdentityAndPhaseContractRejectForgedStaleOrConflictingObservations()
    {
        var session=AtGate();var receipt=Warp(session);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with{},24,48));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,23,47));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,24,49));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,24,48));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,24,48));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,25,49));
        var committed=session.Execute("flush",session.Revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,committed.Status,committed.Error);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,24,48));
        Assert.AreEqual(48,Marker(session.State).Frames);
    }

    [TestMethod]
    public void RejectedCommandsDoNotPublishCompletionAndClockCommitReconcilesOnce()
    {
        var session=AtGate();var receipt=Warp(session);var before=session.State;long revision=session.Revision;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,60,84));
        var rejected=session.Execute("bad",revision,new("not_a_command"));
        Assert.AreEqual(CommandStatus.Rejected,rejected.Status);Assert.AreSame(before,session.State);
        var result=session.Execute("time",revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);Assert.AreEqual(revision+1,session.Revision);
        Assert.AreEqual(JsonSerializer.Serialize(before with{QuestLog=null}),JsonSerializer.Serialize(result.State with{QuestLog=null,PlayTime=before.PlayTime}));
        Assert.AreEqual(84,Marker(result.State).Frames);
        var second=session.Execute("time-2",session.Revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,second.Status);Assert.AreEqual(84,Marker(second.State).Frames);
    }

    [TestMethod]
    public void HarmlessClockCommitBeforeCompletionKeepsReceiptButHeadlessActionInvalidatesIt()
    {
        var session=AtGate();var receipt=Warp(session);
        Assert.AreEqual(CommandStatus.Accepted,session.Execute("time",session.Revision,new("play_time_tick",Choice:1)).Status);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,24,48));
        var other=AtGate();var stale=Warp(other);
        Assert.AreEqual(CommandStatus.Accepted,other.Execute("turn",other.Revision,new("turn",Direction.North)).Status);
        Assert.IsFalse(other.CompleteFieldTransitionPresentation(stale,24,48));
        Assert.IsNull(Marker(other.State).TransitionTiming);
    }

    [TestMethod]
    public void DurationOverflowClosesUnknownMarkerInsteadOfTruncatingAudioWait()
    {
        var session=AtGate();var receipt=Warp(session);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,250,274));
        var prepared=session.PrepareFieldSave();Assert.IsTrue(prepared.QuestLog!.Scenes[^1].Closed);
        Assert.IsNull(Marker(prepared).TransitionTiming);Assert.AreEqual(1,Marker(prepared).Frames);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(prepared.QuestLog,CurrentCampaignFixture.Catalog.QuestLogSource));
    }

    [TestMethod]
    public void StoppingAfterSuccessfulSaveKeepsCompletedProjectionForNextAcceptedCommand()
    {
        var session=AtGate();var receipt=Warp(session);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,24,48));
        var prepared=session.PrepareFieldSave();session.StopQuestLogRecording();
        var result=session.Execute("time",session.Revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,result.Status);
        Assert.AreEqual(JsonSerializer.Serialize(prepared.QuestLog),JsonSerializer.Serialize(result.State.QuestLog));
    }

    [TestMethod]
    public void RecordingOffStillExposesLiveCauseWithoutInventingQuestTiming()
    {
        var session=AtGate(false);var receipt=Warp(session);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,24,48));
        Assert.IsNull(session.State.QuestLog);
    }

    [TestMethod]
    public void PlainWarpCapabilityRejectsDifferentSectionsWithoutRestrictingConnectedEdges()
    {
        var session=AtGate();var receipt=Warp(session);var catalog=CurrentCampaignFixture.Catalog;
        Assert.AreEqual(catalog.MapDefinition(receipt.From.MapId).RegionMapSection,catalog.MapDefinition(receipt.To.MapId).RegionMapSection);
        var from=receipt.From with{MapId=new("map.route-14")};
        Assert.AreNotEqual(catalog.MapDefinition(from.MapId).RegionMapSection,catalog.MapDefinition(receipt.To.MapId).RegionMapSection);
        var crossSection=receipt with{From=from};
        void SetReceipt(FieldTransitionReceipt accepted)=>typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(session.State with{FieldTransition=accepted},session.RulesVersion,catalog.ContentVersion,session.Revision));
        SetReceipt(crossSection);Assert.IsFalse(session.CanPresentFieldTransition(crossSection));
        var connected=crossSection with{Cause=FieldTransitionCause.ConnectedEdge};
        SetReceipt(connected);Assert.IsTrue(session.CanPresentFieldTransition(connected));
    }
    [TestMethod]
    public void CompletedDurationAndSubsequentIdleTickAreCountedOnlyOnce()
    {
        var session=AtGate();var receipt=Warp(session);int start=Marker(session.State).StartFrame;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,24,48));
        session.ObserveQuestLogFieldFrame(100);session.ObserveQuestLogFieldFrame(100);
        var result=session.Execute("turn",session.Revision,new("turn",Direction.North));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        Assert.AreEqual(start+49,result.State.QuestLog!.Scenes[^1].Actions[^1].StartFrame);
        Assert.AreEqual(48,Marker(result.State).Frames);
    }

    [TestMethod]
    public void RealizedTimingAllocationOverflowClosesWithoutPartialMetadata()
    {
        var session=AtGate();var state=session.State;var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        var initial=state.QuestLog!.Scenes[^1];
        var log=new CampaignQuestLog([initial with{Actions=[new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.UsedItem,127){TextKey="UsedItemOnMonAtThisLocation",Arguments=["POTION","RATTATA"]}]}]);
        // Leave room for the provisional eight-byte map marker, but not its
        // separate eight-byte observed phase receipt.
        while(CampaignQuestLogRecorder.Budget(log.Scenes[^1].Actions,source)+16<=256&&
            CampaignQuestLogRecorder.LogicalCommandCount(log.Scenes[^1].Actions)+2<=QuestLogSource.EventCursorCount)
        {
            var next=CampaignQuestLogRecorder.RecordFieldInput(log,0,source,true)!;
            Assert.IsFalse(next.Scenes[^1].Closed);log=next;
        }
        state=state with{QuestLog=log};
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,CurrentCampaignFixture.Catalog.ContentVersion,session.Revision));
        var receipt=Warp(session);Assert.IsNull(Marker(session.State).TransitionTiming);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,24,48));
        var saved=session.PrepareFieldSave();Assert.IsTrue(saved.QuestLog!.Scenes[^1].Closed);
        Assert.IsNull(Marker(saved).TransitionTiming);Assert.AreEqual(1,Marker(saved).Frames);
        Assert.IsTrue(CampaignQuestLogRecorder.Budget(saved.QuestLog.Scenes[^1].Actions,source)<=256);
        var committed=session.Execute("overflow-time",session.Revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,committed.Status,committed.Error);
        Assert.IsTrue(committed.State.QuestLog!.Scenes[^1].Closed);
        var clock=(CampaignQuestLogFieldClock)typeof(CampaignSession).GetField("_questLogFieldClock",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
        int stoppedFrame=clock.Frame;session.ObserveQuestLogFieldFrame(200);
        Assert.AreEqual(stoppedFrame,clock.Frame,"A closed recording must stop observing field time.");
        var turn=session.Execute("overflow-turn",session.Revision,new("turn",Direction.North));
        Assert.AreEqual(CommandStatus.Accepted,turn.Status,turn.Error);
        Assert.AreEqual(JsonSerializer.Serialize(saved.QuestLog),JsonSerializer.Serialize(turn.State.QuestLog));
    }

}
