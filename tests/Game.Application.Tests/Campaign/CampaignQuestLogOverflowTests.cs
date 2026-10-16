using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogOverflowTests
{
    private static CampaignQuestLogAction Wild()=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.WildWon,127)
        {TextKey="DefeatedWildMon",Arguments=["ROUTE 1","PIDGEY","1","PIDGEY","0","RED"]};
    private static (CampaignState State,CampaignQuestLog Log,CampaignQuestLogStartGate Gate) Full(CampaignQuestLogAction first)
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        state=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_SYS_GAME_CLEAR",true)}};
        var gate=new CampaignQuestLogStartGate();
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,recordingOpen:false,suppliedEvent:first,startGate:gate)!;
        for(int i=0;i<100;i++)
        {
            var next=state with{World=state.World with{Player=state.World.Player with{X=state.World.Player.X+1}}};
            log=CampaignQuestLogRecorder.Record(log,state,next,new("move"),catalog,recordingOpen:!log.Scenes[^1].Closed,startGate:gate)!;state=next;
        }
        Assert.IsTrue(log.Scenes[^1].Closed);Assert.IsTrue(CampaignQuestLogRecorder.FitsActions(log.Scenes[^1].Actions,catalog.QuestLogSource));
        return(state,log,gate);
    }
    [TestMethod]
    public void MovementOverflowStopsRecordingButPreservesOriginalWildAggregatePointer()
    {
        var (state,log,gate)=Full(Wild());var before=System.Text.Json.JsonSerializer.Serialize(state);
        Assert.IsTrue(gate.WildRecordRetained);
        var next=CampaignQuestLogRecorder.Record(log,state,state,new("battle_ack"),CurrentCampaignFixture.Catalog,recordingOpen:false,suppliedEvent:Wild(),startGate:gate)!;
        Assert.AreEqual(1,next.Scenes.Length);Assert.IsTrue(next.Scenes[0].Closed);Assert.AreEqual("2",next.Scenes[0].Actions[0].Arguments[2]);
        Assert.AreEqual(log.Scenes[0].Actions.Length,next.Scenes[0].Actions.Length);Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(state));
    }
    [TestMethod]
    public void ExplicitSaveEndAndContinueDoNotRetainTheOverflowWildPointer()
    {
        var (state,log,gate)=Full(Wild());gate.EndRecording();
        foreach(var reset in new[]{gate,new CampaignQuestLogStartGate(log)})
        {
            Assert.IsFalse(reset.WildRecordRetained);
            var next=CampaignQuestLogRecorder.Record(log,state,state,new("battle_ack"),CurrentCampaignFixture.Catalog,recordingOpen:false,suppliedEvent:Wild(),startGate:reset)!;
            Assert.AreEqual(2,next.Scenes.Length);Assert.AreEqual("1",next.Scenes[0].Actions[0].Arguments[2]);Assert.AreEqual("1",next.Scenes[1].Actions[0].Arguments[2]);
        }
    }
    [TestMethod]
    public void DuplicateDepartureAfterMovementOverflowUsesStoppedSceneGate()
    {
        var departure=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Departed,127){TextKey="RestedAtHome",Arguments=[],LocationId=0};
        var (state,log,gate)=Full(departure);
        Assert.AreSame(log,CampaignQuestLogRecorder.Record(log,state,state,new("move"),CurrentCampaignFixture.Catalog,recordingOpen:false,suppliedEvent:departure,startGate:gate));
    }
    [TestMethod]
    public void InputOverflowPreservesWildPointerButDisabledMapBoundaryClearsIt()
    {
        var (state,log,gate)=Full(Wild());
        var open=log with{Scenes=log.Scenes.SetItem(0,log.Scenes[0] with{Closed=false})};
        var full=CampaignQuestLogRecorder.RecordFieldInput(open,100,CurrentCampaignFixture.Catalog.QuestLogSource,true)!;
        Assert.IsTrue(full.Scenes[0].Closed);Assert.IsTrue(gate.WildRecordRetained);
        var after=state with{World=state.World with{Player=state.World.Player with{MapId=new(CurrentCampaignFixture.Catalog.QuestLogSource.DisabledMaps[0])}}};
        CampaignQuestLogRecorder.Record(full,state,after,new("move"),CurrentCampaignFixture.Catalog,recordingOpen:false,startGate:gate);
        Assert.IsFalse(gate.WildRecordRetained);
    }
    [TestMethod]
    public void CaptionAllocationFinishClearsWildPointerWhenNextEventCannotStart()
    {
        var (state,log,gate)=Full(Wild());
        var active=log with{Scenes=log.Scenes.SetItem(0,log.Scenes[0] with{Closed=false})};
        var move=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.FieldMove,127){TextKey="UsedCut",Arguments=["PIDGEY"]};
        var result=CampaignQuestLogRecorder.Record(active,state,state,new("field_cut"),CurrentCampaignFixture.Catalog,recordingOpen:true,suppliedEvent:move,startGate:gate)!;
        Assert.AreEqual(1,result.Scenes.Length);Assert.IsTrue(result.Scenes[0].Closed);Assert.IsFalse(gate.WildRecordRetained);
    }

    [TestMethod]
    public void RetainedOverflowWildPointerCrossesOrdinaryMapWithoutRecordingAnotherProjection()
    {
        var (state,log,gate)=Full(Wild());var catalog=CurrentCampaignFixture.Catalog;
        var after=state with{World=state.World with{Player=state.World.Player with{MapId=new("map.route-2")}},
            MapEdits=System.Collections.Immutable.ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty.Add("1,1",new(1,1,1,0,3))};
        var crossed=CampaignQuestLogRecorder.Record(log,state,after,new("move"),catalog,recordingOpen:false,startGate:gate)!;
        Assert.AreSame(log,crossed);Assert.IsTrue(gate.WildRecordRetained);
        string original=System.Text.Json.JsonSerializer.Serialize(after);
        var result=CampaignQuestLogRecorder.Record(crossed,after,after,new("battle_ack"),catalog,recordingOpen:false,
            suppliedEvent:Wild() with{Arguments=Wild().Arguments.SetItem(0,"ROUTE 2")},startGate:gate)!;
        Assert.AreEqual(1,result.Scenes.Length);Assert.IsTrue(result.Scenes[0].Closed);
        Assert.AreEqual(log.Scenes[0].Map,result.Scenes[0].Map);Assert.AreEqual(log.Scenes[0].Actions.Length,result.Scenes[0].Actions.Length);
        Assert.AreEqual("ROUTE 2",result.Scenes[0].Actions[0].Arguments[0]);Assert.AreEqual("2",result.Scenes[0].Actions[0].Arguments[2]);
        Assert.IsFalse(result.Scenes[0].Actions.Any(a=>a.Transition is not null||a.Tile is not null));
        Assert.AreEqual(original,System.Text.Json.JsonSerializer.Serialize(after));
    }
    [TestMethod]
    public void NewSemanticEventAfterOrdinaryCrossingEndsTheRetainedWildAggregate()
    {
        var (state,log,gate)=Full(Wild());var catalog=CurrentCampaignFixture.Catalog;
        var after=state with{World=state.World with{Player=state.World.Player with{MapId=new("map.route-2")}}};
        var arrival=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["ROUTE 2"]};
        var next=CampaignQuestLogRecorder.Record(log,state,after,new("move"),catalog,recordingOpen:false,suppliedEvent:arrival,startGate:gate)!;
        Assert.IsFalse(gate.WildRecordRetained);Assert.AreEqual(2,next.Scenes.Length);
        next=CampaignQuestLogRecorder.Record(next,after,after,new("battle_ack"),catalog,recordingOpen:true,suppliedEvent:Wild(),startGate:gate)!;
        Assert.AreEqual("1",next.Scenes[0].Actions[0].Arguments[2]);
        Assert.AreEqual("1",next.Scenes[1].Actions.Single(a=>a.Event==CampaignQuestLogEvent.WildWon).Arguments[2]);
    }

}
