using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogMapTransitionTests
{
    private static CampaignState At(CampaignState state,string map)=>state with
    {World=state.World with{Player=new(new(map),6,8,3,Direction.North)},Actors=ImmutableSortedDictionary<string,CampaignActor>.Empty,ObjectSlots=null,MapLayout=null,MapEdits=null,Motions=[]};
    private static CampaignQuestLogAction Arrival()=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
    private static CampaignQuestLog Begin(CampaignState state)=>CampaignQuestLogRecorder.Record(null,state,state,new("move"),CurrentCampaignFixture.Catalog,suppliedEvent:Arrival())!;
    [TestMethod]
    public void QuietOrdinaryMapChangesAndReturnStayInOneSceneWithCaptionContinuity()
    {
        var catalog=CurrentCampaignFixture.Catalog;var first=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");
        var second=At(first,"map.route-1");var log=Begin(first);
        log=CampaignQuestLogRecorder.Record(log,first,second,new("move",Direction.North),catalog,recordingFrame:10)!;
        log=CampaignQuestLogRecorder.Record(log,second,first,new("move",Direction.South),catalog,recordingFrame:20)!;
        Assert.AreEqual(1,log.Scenes.Length);Assert.AreEqual(2,log.Scenes[0].Actions.Count(a=>a.Transition is not null));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
        log=JsonSerializer.Deserialize<CampaignQuestLog>(JsonSerializer.Serialize(log))!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
        string original=JsonSerializer.Serialize(first);var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource,CampaignQuestLogRecorder.SavedScene(first,catalog.QuestLogSource));
        string? caption=playback.Caption;Assert.AreEqual("map.pallet-town",playback.Map);
        playback.Tick(10d/60);Assert.AreEqual("map.route-1",playback.Map);Assert.AreEqual(caption,playback.Caption);
        Assert.IsNull(playback.Movement(CampaignQuestLogRecorder.PlayerId));
        playback.Tick(10d/60);Assert.AreEqual("map.pallet-town",playback.Map);Assert.AreEqual(caption,playback.Caption);
        while(!playback.Finished)playback.Tick(10);
        Assert.AreEqual(original,JsonSerializer.Serialize(first));
    }
    [TestMethod]
    public void DestinationMovementUsesDestinationActorSlotsAndSavedSkipUsesActualMap()
    {
        var catalog=CurrentCampaignFixture.Catalog;var first=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");var second=At(first,"map.route-1") with{IsCycling=true};
        var log=CampaignQuestLogRecorder.Record(Begin(first),first,second,new("move"),catalog,recordingFrame:10)!;
        var moved=second with{World=second.World with{Player=second.World.Player with{X=7}}};
        log=CampaignQuestLogRecorder.Record(log,second,moved,new("move",Direction.East),catalog,recordingFrame:20)!;
        var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource,CampaignQuestLogRecorder.SavedScene(first,catalog.QuestLogSource));
        playback.Tick(23d/60);Assert.AreEqual("map.route-1",playback.Map);Assert.AreEqual("OBJ_EVENT_GFX_RED_BIKE",playback.Actors.Single().Graphics);
        Assert.AreEqual(.5,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.001);
        playback.Skip();Assert.AreEqual("map.pallet-town",playback.Map);Assert.AreEqual("OBJ_EVENT_GFX_RED_NORMAL",playback.Actors.Single().Graphics);
    }
    [TestMethod]
    public void DisabledDestinationCutsWithoutTransitionOrChangedActions()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");
        var log=Begin(before);var after=At(before,"map.trainer-tower-1f");
        Assert.IsTrue(catalog.QuestLogSource.DisabledMaps.Contains(after.World.Player.MapId.Value));
        var result=CampaignQuestLogRecorder.Record(log,before,after,new("move"),catalog)!;
        Assert.IsTrue(result.Scenes[0].Closed);Assert.AreEqual(log.Scenes[0].Actions,result.Scenes[0].Actions);
    }
    [TestMethod]
    public void TotalVisualBudgetClosesBeforeSecondLargeProjection()
    {
        var catalog=CurrentCampaignFixture.Catalog;var first=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");
        var edits=Enumerable.Range(0,200).ToImmutableSortedDictionary(i=>i.ToString(),i=>new FieldMetatileEdit(i%20,i/20,1,0,3));
        var second=At(first,"map.route-1") with{MapEdits=edits};
        var log=CampaignQuestLogRecorder.Record(Begin(first),first,second,new("move"),catalog)!;
        Assert.IsFalse(log.Scenes[0].Closed);Assert.IsTrue(CampaignQuestLogRecorder.VisualMemoryBudget(log.Scenes[0])<=CampaignQuestLogRecorder.VisualMemoryLimit);
        var third=At(second,"map.pallet-town") with{MapEdits=edits};
        var result=CampaignQuestLogRecorder.Record(log,second,third,new("move"),catalog)!;
        Assert.IsTrue(result.Scenes[0].Closed);Assert.AreEqual(log.Scenes[0].Actions,result.Scenes[0].Actions);
        Assert.AreEqual(1,result.Scenes[0].Actions.Count(a=>a.Transition is not null));
    }
    [TestMethod]
    public void ScriptCapacityClosesBeforeTransitionWithoutExtraScene()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");var log=Begin(before);
        while(!log.Scenes[0].Closed)
        {
            var next=CampaignQuestLogRecorder.RecordFieldInput(log,0,catalog.QuestLogSource,true)!;
            if(next.Scenes[0].Closed)break;log=next;
        }
        var result=CampaignQuestLogRecorder.Record(log,before,At(before,"map.route-1"),new("move"),catalog)!;
        Assert.IsTrue(result.Scenes[0].Closed);Assert.AreEqual(log.Scenes[0].Actions,result.Scenes[0].Actions);Assert.AreEqual(1,result.Scenes.Length);
    }
    [TestMethod]
    [DataRow("unknown")]
    [DataRow("disabled")]
    [DataRow("actor")]
    public void MalformedTransitionProjectionIsRejected(string kind)
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");
        var log=CampaignQuestLogRecorder.Record(Begin(state),state,At(state,"map.route-1"),new("move"),catalog)!;
        var scene=log.Scenes[0];int index=Array.FindIndex(scene.Actions.ToArray(),a=>a.Transition is not null);var marker=scene.Actions[index];
        marker=kind switch
        {"unknown"=>marker with{Transition=marker.Transition! with{Map="map.unknown"}},"disabled"=>marker with{Transition=marker.Transition! with{Map="map.trainer-tower-1f"}},_=>marker with{Transition=marker.Transition! with{Actors=[]}}};
        var malformed=new CampaignQuestLog([scene with{Actions=scene.Actions.SetItem(index,marker)}]);
        Assert.IsNotNull(CampaignQuestLogRecorder.Validate(malformed,catalog.QuestLogSource,catalog));
    }
    [TestMethod]
    public void DestinationSlotAndTransitionTimeCannotReferToPriorProjection()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");
        var log=CampaignQuestLogRecorder.Record(Begin(before),before,At(before,"map.route-1"),new("move"),catalog,recordingFrame:10)!;
        var scene=log.Scenes[0];
        var badSlot=new CampaignQuestLogAction(1,7,8,Direction.East,true,CampaignQuestLogEvent.None,16){StartFrame=20};
        Assert.AreEqual("save_quest_log_actor_invalid",CampaignQuestLogRecorder.Validate(new([scene with{Actions=scene.Actions.Add(badSlot)}]),catalog.QuestLogSource));
        var badTime=badSlot with{ActorIndex=0,StartFrame=9};
        Assert.AreEqual("save_quest_log_transition_time_invalid",CampaignQuestLogRecorder.Validate(new([scene with{Actions=scene.Actions.Add(badTime)}]),catalog.QuestLogSource));
    }

    [TestMethod]
    public void VisualAllocationAcceptsExactLimitAndRejectsOneAdditionalProjection()
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var actors=Enumerable.Range(0,16).Select(i=>new CampaignQuestLogActor(i==0?CampaignQuestLogRecorder.PlayerId:"actor"+i,"OBJ_EVENT_GFX_MAN",i,1,Direction.South,true)).ToImmutableArray();
        var tiles=Enumerable.Range(0,210).Select(i=>new FieldMetatileEdit(i%15,i/15,1,0,3)).ToImmutableArray();
        var scene=new CampaignQuestLogScene("map.pallet-town",actors,[Arrival()]){Layout="LAYOUT_PALLET_TOWN",Tiles=tiles};
        Assert.AreEqual(1636,CampaignQuestLogRecorder.VisualMemoryBudget(scene));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([scene]),catalog.QuestLogSource));
        var transition=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,1)
        {StartFrame=1,Transition=new("map.route-1",[actors[0]],null,[])};
        var extra=scene with{Actions=scene.Actions.Add(transition)};
        Assert.AreEqual(1664,CampaignQuestLogRecorder.VisualMemoryBudget(extra));
        Assert.AreEqual("save_quest_log_visual_budget_invalid",CampaignQuestLogRecorder.Validate(new([extra]),catalog.QuestLogSource));
    }

    [TestMethod]
    public void NewMapModalMarkerUsesSegmentFloorWhileMalformedSaveStillFails()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=At(CampaignSession.CreateCurrent(catalog).State,"map.pallet-town");
        var log=CampaignQuestLogRecorder.Record(Begin(state),state,At(state,"map.route-1"),new("move"),catalog,recordingFrame:16)!;
        var recorded=CampaignQuestLogRecorder.RecordFieldInput(log,0,catalog.QuestLogSource,true)!;
        Assert.AreEqual(16,recorded.Scenes[0].Actions[^1].StartFrame);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(recorded,catalog.QuestLogSource,catalog));
        var bad=recorded.Scenes[0] with{Actions=recorded.Scenes[0].Actions.SetItem(recorded.Scenes[0].Actions.Length-1,recorded.Scenes[0].Actions[^1] with{StartFrame=0})};
        Assert.AreEqual("save_quest_log_transition_time_invalid",CampaignQuestLogRecorder.Validate(new([bad]),catalog.QuestLogSource,catalog));
    }

}
