using System.Collections.Immutable;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogCursorTests
{
    private static CampaignQuestLogAction Payload(int value)=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.PartyOrder,95){TextKey="SwitchedMons",Arguments=[value.ToString(),"OTHER"]};
    private static CampaignQuestLogAction Input()=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Input,1);
    [TestMethod]
    public void InitialFacingAndZeroInputReserveSixteenBytesWithoutDuplicatingPose()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        Assert.AreEqual(18,CampaignQuestLogRecorder.Budget([],source));
        Assert.AreEqual(26,CampaignQuestLogRecorder.Budget([Input()],source));
    }
    [TestMethod]
    public void SameEventAndCursorRetainsLatestFiveDifferentPayloads()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        ImmutableArray<CampaignQuestLogAction> actions=[];
        for(int i=0;i<7;i++)Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(i),source,out actions));
        Assert.AreEqual(5,actions.Length);Assert.AreEqual("2",actions[0].Arguments[0]);Assert.AreEqual("6",actions[^1].Arguments[0]);
        Assert.IsFalse(actions[0].Repeat);Assert.IsTrue(actions.Skip(1).All(a=>a.Repeat));
        Assert.AreEqual(18+source.EventSizes[3]+4*(source.EventSizes[3]-4),CampaignQuestLogRecorder.Budget(actions,source));
    }
    [TestMethod]
    public void NonvisualInputAndMovementSeparateGroupsWithoutComparingTextOrTime()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        foreach(var boundary in new[]{Input(),new CampaignQuestLogAction(0,1,1,Direction.South,true,CampaignQuestLogEvent.None,16)})
        {
            ImmutableArray<CampaignQuestLogAction> actions=[Payload(1),boundary];
            Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(1),source,out actions));
            Assert.IsFalse(actions[^1].Repeat);
        }
        ImmutableArray<CampaignQuestLogAction> delayed=[Payload(1)];
        Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(delayed,Payload(2) with{StartFrame=100},source,out delayed));
        Assert.IsTrue(delayed[^1].Repeat);Assert.AreEqual(0,delayed[^1].StartFrame);
    }
    [TestMethod]
    public void SixthPayloadStillChecksAdditionalCapacityBeforeReplacingOldest()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource with{EventSizes=CurrentCampaignFixture.Catalog.QuestLogSource.EventSizes.SetItem(3,40)};
        ImmutableArray<CampaignQuestLogAction> actions=[Input(),Input(),Input()];
        for(int i=0;i<5;i++)Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(i),source,out actions));
        Assert.AreEqual(226,CampaignQuestLogRecorder.Budget(actions,source));
        var original=actions;Assert.IsFalse(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(6),source,out actions));
        Assert.AreEqual(original,actions);
    }
    [TestMethod]
    public void PokemonCenterSuppressesOnlySameCursorDuplicate()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        var center=Payload(0) with{Event=CampaignQuestLogEvent.PokemonCenter};
        ImmutableArray<CampaignQuestLogAction> actions=[center];
        Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,center,source,out actions));Assert.AreEqual(1,actions.Length);
        actions=actions.Add(Input());Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,center,source,out actions));Assert.AreEqual(3,actions.Length);
    }
    [TestMethod]
    public void NonvisualInputPlaybackNeverMovesActorOrMutatesSavedRecording()
    {
        var scene=new CampaignQuestLogScene("map.pallet-town",[new(CampaignQuestLogRecorder.PlayerId,"player",6,8,Direction.North,true)],[Input() with{StartFrame=120}]);
        var log=new CampaignQuestLog([scene]);Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        var before=System.Text.Json.JsonSerializer.Serialize(log);var playback=new CampaignQuestLogPlayback(log);
        playback.Tick(1);Assert.AreEqual(6,playback.Actors.Single().X);Assert.IsFalse(playback.Finished);
        playback.Tick(3);Assert.IsTrue(playback.Finished);Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(log));
    }
    [TestMethod]
    public void StartMenuUsesAcceptedFieldBoundaryAndSetsOriginalFlagWithoutRewards()
    {
        var saved=CurrentCampaignFixture.Checkpoint;var catalog=CurrentCampaignFixture.Catalog;
        var session=CampaignSession.RestoreCurrent(catalog,saved.State,saved.Generation);var before=session.State;
        var result=session.Execute("quest-start",session.Revision,new("field_start_menu"));
        Assert.AreEqual(CommandStatus.Accepted,result.Status);Assert.IsTrue(result.State.Script.Flags.GetValueOrDefault("FLAG_OPENED_START_MENU"));
        Assert.AreEqual(before.World.Rng,result.State.World.Rng);Assert.AreEqual(before.Money,result.State.Money);Assert.AreEqual(before.World.Player,result.State.World.Player);
    }
    [TestMethod]
    public void FieldInputIsBoundedAndNeverStartsARecordingByItself()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        Assert.IsNull(CampaignQuestLogRecorder.RecordFieldInput(null,100,source,true));
        var scene=new CampaignQuestLogScene("map.pallet-town",[new(CampaignQuestLogRecorder.PlayerId,"player",6,8,Direction.North,true)],[Payload(1)]);
        var log=new CampaignQuestLog([scene]);Assert.AreSame(log,CampaignQuestLogRecorder.RecordFieldInput(log,100,source,false));
        log=CampaignQuestLogRecorder.RecordFieldInput(log,100,source,true)!;
        Assert.AreEqual(CampaignQuestLogEvent.Input,log.Scenes[0].Actions[^1].Event);Assert.AreEqual(100,log.Scenes[0].Actions[^1].StartFrame);
        for(int i=0;i<100;i++)log=CampaignQuestLogRecorder.RecordFieldInput(log,100+i,source,true)!;
        Assert.IsTrue(log.Scenes[0].Closed);Assert.IsTrue(CampaignQuestLogRecorder.Budget(log.Scenes[0].Actions,source)<=256);
    }

    [TestMethod]
    public void MoreThanThirtyTwoPayloadsFitWhenRepeatedGroupsShareCommandPointers()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        ImmutableArray<CampaignQuestLogAction> actions=[];
        for(int group=0;group<7;group++)for(int payload=0;payload<5;payload++)
            Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(payload) with{Event=group%2==0?CampaignQuestLogEvent.PartyOrder:CampaignQuestLogEvent.GaveHeldItem},source,out actions));
        Assert.AreEqual(35,actions.Length);Assert.AreEqual(10,CampaignQuestLogRecorder.LogicalCommandCount(actions));
        Assert.IsTrue(CampaignQuestLogRecorder.FitsActions(actions,source));
    }
    [TestMethod]
    public void ThirtyTwoLogicalCommandsIncludeInitialPairAndSceneEnd()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        ImmutableArray<CampaignQuestLogAction> actions=[];
        for(int i=0;i<29;i++)Assert.IsTrue(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(i) with{Event=i%2==0?CampaignQuestLogEvent.PokemonCenter:CampaignQuestLogEvent.Arrived},source,out actions));
        Assert.AreEqual(32,CampaignQuestLogRecorder.LogicalCommandCount(actions));Assert.IsTrue(CampaignQuestLogRecorder.Budget(actions,source)<256);
        var before=actions;Assert.IsFalse(CampaignQuestLogRecorder.TryAppendEvent(actions,Payload(30) with{Event=CampaignQuestLogEvent.Arrived},source,out actions));Assert.AreEqual(before,actions);
    }
    [TestMethod]
    public void DepartureGateOnlySuppressesStoppedStartsAndUsesStableLocationIdentity()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var departure=Payload(1) with{Event=CampaignQuestLogEvent.Departed,LocationId=1};var gate=new CampaignQuestLogStartGate();
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,recordingOpen:false,suppliedEvent:departure,startGate:gate)!;
        Assert.AreSame(log,CampaignQuestLogRecorder.Record(log,state,state,new("move"),catalog,recordingOpen:false,suppliedEvent:departure with{Arguments=["DIFFERENT TEXT"]},startGate:gate));
        var active=CampaignQuestLogRecorder.Record(log,state,state,new("move"),catalog,recordingOpen:true,suppliedEvent:departure,startGate:gate)!;
        Assert.AreEqual(2,active.Scenes[0].Actions.Length);
        var other=CampaignQuestLogRecorder.Record(active,state,state,new("move"),catalog,recordingOpen:false,suppliedEvent:Payload(3) with{Event=CampaignQuestLogEvent.Arrived},startGate:gate)!;
        var next=CampaignQuestLogRecorder.Record(other,state,state,new("move"),catalog,recordingOpen:false,suppliedEvent:departure,startGate:gate)!;
        Assert.AreEqual(3,next.Scenes.Length);
    }
    [TestMethod]
    public void ContinueAndSkipReconstructDepartureGateFromFirstEventOfFinalScene()
    {
        var departure=Payload(1) with{Event=CampaignQuestLogEvent.Departed,LocationId=4};
        var scene=new CampaignQuestLogScene("map.pallet-town",[new(CampaignQuestLogRecorder.PlayerId,"player",6,8,Direction.North,true)],[departure,Payload(2) with{Event=CampaignQuestLogEvent.Arrived}]);
        var log=new CampaignQuestLog([scene]);var original=System.Text.Json.JsonSerializer.Serialize(log);
        foreach(bool skip in new[]{false,true})
        {
            var playback=new CampaignQuestLogPlayback(log);if(skip)playback.Skip();else playback.Tick(20);
            Assert.IsTrue(playback.Finished);var gate=new CampaignQuestLogStartGate(log);
            Assert.IsFalse(gate.Admit(departure));Assert.IsTrue(gate.Admit(departure with{LocationId=5}));
            Assert.AreEqual(original,System.Text.Json.JsonSerializer.Serialize(log));
        }
    }

}
