using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogDoorTransitionTests
{
    // Only the stable starting place and story context are controlled. Both door
    // receipts, entry scripts and recording markers come from public commands.
    private static CampaignSession AtPalletDoor(bool outside=true,bool record=true)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var session=CampaignSession.CreateCurrent(catalog);var state=session.State;
        var id=new MapId(outside?"map.pallet-town":"map.rivals-house");
        int x=outside?15:4,y=8;
        Assert.AreEqual(outside?0x69:0x65,catalog.Maps[id].SourceBehaviorAt(x,outside?y-1:y));
        state=state with
        {
            World=state.World with{Player=new(id,x,y,catalog.Maps[id].GetTile(x,y)!.Elevation,outside?Direction.North:Direction.South)},
            Actors=ImmutableSortedDictionary<string,CampaignActor>.Empty,ObjectSlots=null,
            Script=state.Script with{Completed=true,Locked=false,Wait=null,Stack=[],
                Variables=state.Script.Variables.SetItem("VAR_MAP_SCENE_PALLET_TOWN_OAK",1)
                    .SetItem("VAR_MAP_SCENE_PALLET_TOWN_SIGN_LADY",2)},
            EntryPrograms=[],ArrivalFacing=null,Dialogue=null,Motions=[],PresentationTicks=0,
            PresentationEffects=[],MapLayout=null,MapEdits=null,PlayerVisible=true,
            IsCycling=false,IsSurfing=false,FieldWeather=null
        };
        if(record)state=state with{QuestLog=new([CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource)])};
        SetState(session,state);
        typeof(CampaignSession).GetField("_questLogRecordingOpen",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,record);
        return session;
    }
    private static void SetState(CampaignSession session,CampaignState state)=>
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,CurrentCampaignFixture.Catalog.ContentVersion,session.Revision));
    private static FieldTransitionReceipt Warp(CampaignSession session,bool outside)
    {
        var before=session.State;
        var result=session.Execute("door:"+session.Revision,session.Revision,new("move",outside?Direction.North:Direction.South));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        var receipt=result.State.FieldTransition;Assert.IsNotNull(receipt);
        Assert.AreEqual(outside?FieldTransitionCause.AnimatedDoor:FieldTransitionCause.PlainWarp,receipt.Cause);
        Assert.AreEqual(outside?FieldTransitionArrival.Plain:FieldTransitionArrival.AnimatedDoor,receipt.Arrival);
        Assert.AreEqual(outside?"map.rivals-house":"map.pallet-town",receipt.To.MapId.Value);
        Assert.IsTrue(session.CanPresentFieldTransition(before,receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt),"An animated branch requires its actual origin snapshot.");
        Assert.IsTrue(result.State.Script.Completed);Assert.IsTrue(result.State.EntryPrograms.IsEmpty);
        Assert.IsNull(result.State.Dialogue);Assert.IsTrue(result.State.Motions.IsEmpty);
        return receipt;
    }
    private static CampaignQuestLogAction Marker(CampaignState state)=>state.QuestLog!.Scenes[^1].Actions.Last(a=>a.Transition is not null);
    private static int Departure(FieldTransitionReceipt receipt)=>receipt.Cause==FieldTransitionCause.AnimatedDoor?
        FieldDoorPresentation.MinimumDepartureFrames:FieldWarpPresentation.MinimumDepartureFrames;
    private static int Arrival(FieldTransitionReceipt receipt)=>receipt.Arrival==FieldTransitionArrival.AnimatedDoor?
        FieldDoorPresentation.AnimatedArrivalFrames:FieldWarpPresentation.PlainArrivalFrames;

    [TestMethod]
    public void PublicPalletDoorRoundTripPreservesCommittedPositionAndRecordsIndependentBranches()
    {
        var session=AtPalletDoor();var initial=session.State.World;var enter=Warp(session,true);
        var state=session.State;long revision=session.Revision;string unchanged=JsonSerializer.Serialize(state);
        Assert.AreEqual((4,8),(enter.To.X,enter.To.Y));Assert.AreEqual(enter.To,state.World.Player);
        Assert.IsNull(Marker(state).TransitionTiming);
        int change=Departure(enter)+7,end=change+Arrival(enter);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(enter,change,end));
        var saved=session.PrepareFieldSave();var entered=Marker(saved).TransitionTiming!;
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(15,7),entered.DepartureDoor);
        Assert.IsNull(entered.ArrivalDoor);Assert.AreEqual(FieldTransitionArrival.Plain,entered.Arrival);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(session.State));Assert.AreSame(state,session.State);Assert.AreEqual(revision,session.Revision);
        Assert.AreEqual(initial.Rng,session.State.World.Rng);Assert.AreEqual(initial.StepCount,session.State.World.StepCount);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog,CurrentCampaignFixture.Catalog.QuestLogSource,CurrentCampaignFixture.Catalog));

        // Arrival occupies the real south-arrow exit. Turn through the command
        // boundary, then return without relocating the player or inventing a receipt.
        var turn=session.Execute("door-turn",session.Revision,new("turn",Direction.South));
        Assert.AreEqual(CommandStatus.Accepted,turn.Status,turn.Error);
        var leave=Warp(session,false);Assert.AreEqual((15,7),(leave.ArrivalAnchor.X,leave.ArrivalAnchor.Y));
        Assert.AreEqual((15,8),(leave.To.X,leave.To.Y));
        change=Departure(leave);end=change+Arrival(leave);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(leave,change,end));
        saved=session.PrepareFieldSave();var exited=Marker(saved).TransitionTiming!;
        Assert.IsNull(exited.DepartureDoor);Assert.AreEqual(new CampaignQuestLogDoorAnchor(15,7),exited.ArrivalDoor);
        Assert.AreEqual(FieldTransitionArrival.AnimatedDoor,exited.Arrival);
        Assert.AreEqual(leave.To,session.State.World.Player);Assert.AreEqual(initial.Rng,session.State.World.Rng);
        Assert.AreEqual(initial.StepCount,session.State.World.StepCount);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog,CurrentCampaignFixture.Catalog.QuestLogSource,CurrentCampaignFixture.Catalog));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DoorObservationRetainsReceiptIdentityAndAcceptedTransactionReconciliation(bool outside)
    {
        var session=AtPalletDoor(outside);var receipt=Warp(session,outside);var state=session.State;
        int change=Departure(receipt),end=change+Arrival(receipt);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with{},change,end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,change-1,end-1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,change,end+1));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,change,end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,change,end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,change+1,end+1));
        Assert.AreEqual(CommandStatus.Rejected,session.Execute("door-invalid",session.Revision,new("not_a_command")).Status);
        Assert.AreSame(state,session.State);Assert.IsNull(Marker(session.State).TransitionTiming);
        var result=session.Execute("door-time",session.Revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);Assert.AreEqual(end,Marker(result.State).Frames);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,change,end));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(result.State.QuestLog,CurrentCampaignFixture.Catalog.QuestLogSource,CurrentCampaignFixture.Catalog));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DoorReplayKeepsObservedMapBoundaryAndUsesTheLivePhaseSampler(bool outside)
    {
        var session=AtPalletDoor(outside);var receipt=Warp(session,outside);
        int change=Departure(receipt)+11,end=change+Arrival(receipt);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,change,end));
        var log=session.PrepareFieldSave().QuestLog!;string unchanged=JsonSerializer.Serialize(log);
        var marker=log.Scenes[^1].Actions.Last(a=>a.Transition is not null);var timing=marker.TransitionTiming!;
        var playback=new CampaignQuestLogPlayback(log);
        playback.Tick(marker.StartFrame/60d);
        var doorDeparture=new FieldDoorPresentation.Departure();var plainDeparture=new FieldWarpPresentation.Departure();
        var doorArrival=new FieldDoorPresentation.AnimatedArrival();var plainArrival=new FieldWarpPresentation.PlainArrival();
        for(int frame=0;frame<end;frame++)
        {
            Assert.AreEqual(timing,playback.ActiveTransitionTiming);
            Assert.AreEqual(frame,playback.TransitionElapsedFrames,.00001);
            Assert.AreEqual(frame<change?receipt.From.MapId.Value:receipt.To.MapId.Value,playback.Map);
            var sample=frame<change
                ?outside?doorDeparture.Sample:FieldDoorPresentation.FromPlain(plainDeparture.Sample)
                :outside?FieldDoorPresentation.FromPlain(plainArrival.Sample):doorArrival.Sample;
            var replay=FieldDoorPresentation.SampleRecorded(timing.Cause,
                timing.Arrival,timing.SwitchFrame,(int)playback.TransitionElapsedFrames);
            Assert.AreEqual(sample,replay);
            if(frame<change)
            {
                bool ready=frame+1>=change-1;
                if(outside)doorDeparture.Advance(ready);else plainDeparture.Advance(ready);
            }
            else if(outside)plainArrival.Advance();else doorArrival.Advance();
            playback.Tick(1d/60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);Assert.AreEqual(receipt.To.MapId.Value,playback.Map);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UnobservedDoorRecordingStaysUnknownAndRecordingOffDoesNotInventEvidence(bool outside)
    {
        var session=AtPalletDoor(outside);var receipt=Warp(session,outside);
        var log=session.PrepareFieldSave().QuestLog!;Assert.IsNull(Marker(session.State).TransitionTiming);
        var playback=new CampaignQuestLogPlayback(log);playback.Tick(Marker(session.State).StartFrame/60d);
        Assert.AreEqual(receipt.To.MapId.Value,playback.Map);Assert.IsNull(playback.ActiveTransitionTiming);
        var headless=AtPalletDoor(outside,false);var other=Warp(headless,outside);
        Assert.IsFalse(headless.CompleteFieldTransitionPresentation(other,Departure(other),Departure(other)+Arrival(other)));
        Assert.IsNull(headless.State.QuestLog);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void LongDoorMusicWaitClosesUnknownRecordingWithoutTruncation(bool outside)
    {
        var session=AtPalletDoor(outside);var receipt=Warp(session,outside);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,250,250+Arrival(receipt)));
        var saved=session.PrepareFieldSave();Assert.IsTrue(saved.QuestLog!.Scenes[^1].Closed);
        Assert.IsNull(Marker(saved).TransitionTiming);Assert.AreEqual(1,Marker(saved).Frames);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog,CurrentCampaignFixture.Catalog.QuestLogSource,CurrentCampaignFixture.Catalog));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void OptionalDoorEvidenceOverflowRetainsTheWholeUnknownMarker(bool outside)
    {
        var session=AtPalletDoor(outside);var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        var log=session.State.QuestLog!;
        while(CampaignQuestLogRecorder.Budget(log.Scenes[^1].Actions,source)+16<=QuestLogSource.ScriptBytes&&
            CampaignQuestLogRecorder.LogicalCommandCount(log.Scenes[^1].Actions)+2<=QuestLogSource.EventCursorCount)
        {
            log=CampaignQuestLogRecorder.RecordFieldInput(log,0,source,true)!;
            Assert.IsFalse(log.Scenes[^1].Closed);
        }
        SetState(session,session.State with{QuestLog=log});var receipt=Warp(session,outside);
        Assert.IsNull(Marker(session.State).TransitionTiming);
        int change=Departure(receipt),end=change+Arrival(receipt);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt,change,end));
        var saved=session.PrepareFieldSave();Assert.IsTrue(saved.QuestLog!.Scenes[^1].Closed);
        Assert.IsNull(Marker(saved).TransitionTiming);Assert.AreEqual(1,Marker(saved).Frames);
        Assert.IsTrue(CampaignQuestLogRecorder.Budget(saved.QuestLog.Scenes[^1].Actions,source)<=QuestLogSource.ScriptBytes);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog,source,CurrentCampaignFixture.Catalog));
        var commit=session.Execute("door-budget-commit",session.Revision,new("play_time_tick",Choice:1));
        Assert.AreEqual(CommandStatus.Accepted,commit.Status,commit.Error);
        Assert.AreEqual(JsonSerializer.Serialize(saved.QuestLog),JsonSerializer.Serialize(commit.State.QuestLog));
    }

    [TestMethod]
    public void DoorCapabilityRejectsUnprovenOriginsOtherArrivalBranchesAndCrossSectionPreview()
    {
        var session=AtPalletDoor();var origin=session.State;var receipt=Warp(session,true);
        Assert.IsFalse(session.CanPresentFieldTransition(origin with{IsCycling=true},receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with{IsSurfing=true},receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with{World=origin.World with{Player=origin.World.Player with{X=14}}},receipt));
        var after=session.State;
        foreach(var invalid in new[]{receipt with{Departure=FieldTransitionDeparture.StepOntoWarp},
            receipt with{Arrival=FieldTransitionArrival.NonAnimatedDoor},receipt with{Arrival=FieldTransitionArrival.Stair},
            receipt with{Cause=FieldTransitionCause.StairWarp},receipt with{Cause=FieldTransitionCause.Unknown},
            receipt with{From=receipt.From with{Facing=Direction.East}}})
        {
            SetState(session,after with{FieldTransition=invalid});
            Assert.IsFalse(session.CanPresentFieldTransition(origin with{World=origin.World with{Player=invalid.From}},invalid));
        }
        var cross=receipt with{From=receipt.From with{MapId=new("map.viridian-city")}};
        SetState(session,after with{FieldTransition=cross});
        Assert.IsFalse(session.CanPresentFieldTransition(origin with{World=origin.World with{Player=cross.From}},cross));
    }

    [TestMethod]
    public void DestinationWalkingModeCannotHideAnUnsupportedCyclingOrigin()
    {
        var session=AtPalletDoor();SetState(session,session.State with{IsCycling=true});var origin=session.State;
        var result=session.Execute("cycling-door",session.Revision,new("move",Direction.North));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);var receipt=result.State.FieldTransition;Assert.IsNotNull(receipt);
        Assert.IsFalse(result.State.IsCycling,"Entering this indoor map clears cycling in authoritative gameplay.");
        Assert.IsFalse(session.CanPresentFieldTransition(origin,receipt));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,FieldDoorPresentation.MinimumDepartureFrames,
            FieldDoorPresentation.MinimumDepartureFrames+FieldWarpPresentation.PlainArrivalFrames));
        Assert.IsNull(Marker(session.State).TransitionTiming);
    }
}
