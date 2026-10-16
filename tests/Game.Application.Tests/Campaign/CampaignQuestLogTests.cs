using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogTests
{
    private static CampaignQuestLog Log(string map="pallet-town")=>new([new(map,
        [new(CampaignQuestLogRecorder.PlayerId,"player",3,4,Direction.South,true)],
        [new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127),
         new(0,4,4,Direction.East,true,CampaignQuestLogEvent.None,16)])]);

    [TestMethod]
    public void ProjectionInterpolatesMovesAndNeverMutatesSavedRecording()
    {
        var log=Log();var encoded=System.Text.Json.JsonSerializer.Serialize(log);
        var playback=new CampaignQuestLogPlayback(log,"LEAF");
        Assert.AreEqual("LEAF arrived at pallet town.",playback.Caption);
        playback.Tick(127d/60);
        Assert.AreEqual(3,playback.MovingFrom!.X);Assert.AreEqual(4,playback.Actors.Single().X);
        playback.Tick(8d/60);Assert.AreEqual(.5,playback.MovementFraction,.001);
        playback.Tick(8d/60);Assert.IsTrue(playback.Finished);
        Assert.AreEqual(encoded,System.Text.Json.JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void NextSceneAndSkipHaveNoCampaignOrRewardExecutionCapability()
    {
        var session=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var before=session.State;var revision=session.Revision;
        var playback=new CampaignQuestLogPlayback(new([Log("first").Scenes[0],Log("second").Scenes[0]]));
        playback.NextScene();Assert.AreEqual("second",playback.Map);Assert.IsFalse(playback.Finished);
        playback.Skip();playback.Tick(200);playback.NextScene();Assert.IsTrue(playback.Finished);
        Assert.AreSame(before,session.State);Assert.AreEqual(revision,session.Revision);
        Assert.AreEqual(before.World.Rng,session.State.World.Rng);Assert.AreEqual(before.Money,session.State.Money);
        Assert.AreEqual(before.World.Player.MapId,session.State.World.Player.MapId);
    }

    [TestMethod]
    public void RecorderRetainsOnlyFourRecentScenesAndStopsMovementAtActionBudget()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;CampaignQuestLog? log=null;
        for(int i=0;i<20;i++)
        {
            var next=state with{World=state.World with{Player=state.World.Player with{MapId=new MapId("scene-"+i)}},Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_WORLD_MAP_VIRIDIAN_CITY",true)}};
            state=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_WORLD_MAP_VIRIDIAN_CITY",false)}};
            log=CampaignQuestLogRecorder.Record(log,state,next,new("move"),catalog);state=next;
        }
        Assert.AreEqual(4,log!.Scenes.Length);Assert.AreEqual("scene-16",log.Scenes[0].Map);
        for(int i=0;i<100;i++)
        {
            var next=state with{World=state.World with{Player=state.World.Player with{X=i}}};
            log=CampaignQuestLogRecorder.Record(log,state,next,new("move"),catalog);state=next;
        }
        Assert.AreEqual(4,log!.Scenes.Length);Assert.IsTrue(CampaignQuestLogRecorder.Budget(log.Scenes[^1].Actions,catalog.QuestLogSource)<=256);
        Assert.IsTrue(CampaignQuestLogRecorder.Budget(log.Scenes[^1].Actions,catalog.QuestLogSource)+8>256);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
    }

    [TestMethod]
    public void NewGameAndClockChangesDoNotInventScenesAndRejectedCommandsDoNotRecord()
    {
        var session=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);Assert.IsNull(session.State.QuestLog);
        var before=session.State;
        var rejected=session.Execute("bad",session.Revision,new("quest_log_reward"));
        Assert.AreNotEqual(CommandStatus.Accepted,rejected.Status);Assert.AreSame(before,session.State);
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,before,new("play_time_tick")));
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,before,new("options_save")));
    }

    [TestMethod]
    public void SerializedRecordingHasNoCampaignSnapshotsOrCaptionsAndRoundTrips()
    {
        var log=Log();var json=System.Text.Json.JsonSerializer.Serialize(log);
        Assert.IsFalse(json.Contains("Rng"));Assert.IsFalse(json.Contains("Inventory"));Assert.IsFalse(json.Contains("Caption"));
        var decoded=System.Text.Json.JsonSerializer.Deserialize<CampaignQuestLog>(json)!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(decoded));
        Assert.AreEqual(json,System.Text.Json.JsonSerializer.Serialize(decoded));
        var playback=new CampaignQuestLogPlayback(decoded);playback.Skip();
        Assert.AreEqual(json,System.Text.Json.JsonSerializer.Serialize(decoded));
    }

    [TestMethod]
    public void SaveRoundTripAndFullPlaybackReturnExactSavedGameplayState()
    {
        var catalog=CurrentCampaignFixture.Catalog;var saved=CurrentCampaignFixture.Checkpoint;
        var source=catalog.QuestLogSource;
        var scene=CampaignQuestLogRecorder.SavedScene(saved.State,source);
        var state=saved.State with{QuestLog=new([scene])};
        var session=CampaignSession.RestoreCurrent(catalog,state,saved.Generation);
        var bytes=SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,catalog.ContentVersion);
        var loaded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,catalog.ContentVersion,validate:session.ValidatePlayerSave);
        var restored=CampaignSession.RestoreCurrent(catalog,loaded.State,loaded.Generation);
        var before=System.Text.Json.JsonSerializer.Serialize(restored.State);
        var playback=new CampaignQuestLogPlayback(restored.State.QuestLog!,restored.State.PlayerName,source,CampaignQuestLogRecorder.SavedScene(restored.State,source));
        playback.NextScene();Assert.IsTrue(playback.SavedGameVisible);
        Assert.AreEqual(restored.State.World.Player.MapId.Value,playback.Map);
        Assert.IsTrue(playback.Caption!.Contains("saved the game"));playback.Tick(3);
        Assert.IsTrue(playback.Finished);Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(restored.State));
        Assert.AreEqual(loaded.Generation,restored.Revision);
    }

    [TestMethod]
    public void FirstVisitCreatesSourceArrivalButOrdinaryMapTransitionsDoNot()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=CampaignSession.CreateCurrent(catalog).State;
        var moved=before with{World=before.World with{Player=before.World.Player with{MapId=new MapId("map.route-1")}}};
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,moved,new("move"),catalog));
        var visited=moved with{Script=moved.Script with{Flags=moved.Script.Flags.SetItem("FLAG_WORLD_MAP_VIRIDIAN_CITY",true)}};
        var recorded=CampaignQuestLogRecorder.Record(null,moved,visited,new("move"),catalog)!;
        Assert.AreEqual(CampaignQuestLogEvent.Arrived,recorded.Scenes.Single().Actions.Single().Event);
        Assert.AreEqual("ArrivedInLocation",recorded.Scenes.Single().Actions.Single().TextKey);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(recorded,catalog.QuestLogSource));
        var disabled=visited with{World=visited.World with{Player=visited.World.Player with{MapId=new MapId("map.trainer-tower-lobby")}}};
        Assert.AreSame(recorded,CampaignQuestLogRecorder.Record(recorded,moved,disabled,new("move"),catalog));
    }

    [TestMethod]
    public void ActorSnapshotsUseActualSourceSlotsAndPreserveSpawnAndVisibilityChanges()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var far=new CampaignActor("far",1,"OBJ_EVENT_GFX_WOMAN_1",30,30,0,Direction.South,"MOVEMENT_TYPE_FACE_DOWN","0","0",true,false,30,30);
        var near=far with{LocalId="near",SourceLocalId=2,X=1,Y=1,Active=false};
        state=state with{Actors=ImmutableSortedDictionary<string,CampaignActor>.Empty.Add("far",far).Add("near",near),ObjectSlots=ImmutableSortedDictionary<int,string>.Empty.Add(0,CampaignQuestLogRecorder.PlayerId).Add(1,"far")};
        var scene=CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource);
        Assert.IsTrue(scene.Actors.Any(a=>a.Id=="far"));Assert.IsFalse(scene.Actors.Any(a=>a.Id=="near"));
        var after=state with{Actors=state.Actors.SetItem("far",far with{Invisible=true}).SetItem("near",near with{Active=true}),ObjectSlots=state.ObjectSlots.Add(2,"near")};
        var recorded=CampaignQuestLogRecorder.Record(new([scene]),state,after,new("wait_done"),catalog)!;
        Assert.IsTrue(recorded.Scenes[0].Actions.Any(a=>a.Spawn?.Id=="near"));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(recorded,catalog.QuestLogSource));
        var playback=new CampaignQuestLogPlayback(recorded,state.PlayerName,catalog.QuestLogSource);
        for(int i=0;i<400&&!playback.Finished;i++)playback.Tick(1d/60);
        Assert.IsTrue(playback.Finished);
        Assert.IsFalse(playback.Actors.Single(a=>a.Id=="far").Visible);
        Assert.IsTrue(playback.Actors.Single(a=>a.Id=="near").Visible);
        Assert.IsFalse(state.Actors["near"].Active);
    }

    [TestMethod]
    public void ChangedGraphicsSurviveLaterMovementWithoutRedundantDeltasAndUnslottedActorsDisappear()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var actor=new CampaignActor("npc",1,"OBJ_EVENT_GFX_WOMAN_1",3,4,0,Direction.South,"MOVEMENT_TYPE_FACE_DOWN","0","0",true,false,3,4);
        state=state with{Actors=ImmutableSortedDictionary<string,CampaignActor>.Empty.Add("npc",actor),ObjectSlots=ImmutableSortedDictionary<int,string>.Empty.Add(0,CampaignQuestLogRecorder.PlayerId).Add(1,"npc")};
        CampaignQuestLog log=new([CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource)]);
        var changed=state with{Actors=state.Actors.SetItem("npc",actor with{GraphicsId="OBJ_EVENT_GFX_MAN"})};
        log=CampaignQuestLogRecorder.Record(log,state,changed,new("wait_done"),catalog)!;
        var moved=changed with{Actors=changed.Actors.SetItem("npc",changed.Actors["npc"] with{X=4})};
        log=CampaignQuestLogRecorder.Record(log,changed,moved,new("wait_done"),catalog)!;
        Assert.IsNull(log.Scenes[0].Actions[^1].Graphics);
        Assert.AreSame(log,CampaignQuestLogRecorder.Record(log,moved,moved,new("wait_done"),catalog));
        var despawned=moved with{ObjectSlots=moved.ObjectSlots!.Remove(1)};
        log=CampaignQuestLogRecorder.Record(log,moved,despawned,new("wait_done"),catalog)!;
        Assert.IsFalse(log.Scenes[0].Actions[^1].Visible);
        var playback=new CampaignQuestLogPlayback(log,state.PlayerName,catalog.QuestLogSource);
        for(int i=0;i<500&&!playback.Finished;i++)playback.Tick(1d/60);
        Assert.IsTrue(playback.Finished);
        var final=playback.Actors.Single(a=>a.Id=="npc");
        Assert.AreEqual("OBJ_EVENT_GFX_MAN",final.Graphics);Assert.AreEqual(4,final.X);Assert.IsFalse(final.Visible);
    }

    [TestMethod]
    public void MovementInterpolationUsesRecordedDuration()
    {
        var scene=Log().Scenes[0] with{Actions=[new(0,4,4,Direction.East,true,CampaignQuestLogEvent.None,32)]};
        var playback=new CampaignQuestLogPlayback(new([scene]));
        playback.Tick(8d/60);Assert.AreEqual(.25,playback.MovementFraction,.001);
        playback.Tick(24d/60);Assert.IsTrue(playback.Finished);
    }

    [TestMethod]
    public void AcceptedLivePartyCommandsRecordOnceAndSaveBoundaryStartsAnotherScene()
    {
        var catalog=CurrentCampaignFixture.Catalog;var saved=CurrentCampaignFixture.Checkpoint;
        var session=CampaignSession.RestoreCurrent(catalog,saved.State with{QuestLog=null},saved.Generation);
        Assert.IsTrue(session.CanReorderFieldParty);
        var revision=session.Revision;var command=new CampaignCommand("party_swap",PartyIndex:1,Choice:0);
        var result=session.Execute("quest-live-swap",revision,command);
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder,session.State.QuestLog!.Scenes.Single().Actions.Single().Event);
        var log=session.State.QuestLog;
        session.Execute("quest-live-swap",revision,command);
        Assert.AreSame(log,session.State.QuestLog);
        var money=session.State.Money;var rng=session.State.World.Rng;
        var bytes=SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,catalog.ContentVersion);
        var decoded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,catalog.ContentVersion,validate:session.ValidatePlayerSave);
        var restored=CampaignSession.RestoreCurrent(catalog,decoded.State,decoded.Generation);
        var snapshot=System.Text.Json.JsonSerializer.Serialize(restored.State);
        var playback=new CampaignQuestLogPlayback(restored.State.QuestLog!,restored.State.PlayerName,catalog.QuestLogSource,CampaignQuestLogRecorder.SavedScene(restored.State,catalog.QuestLogSource));
        playback.Skip();playback.Tick(3);
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(snapshot,System.Text.Json.JsonSerializer.Serialize(restored.State));
        Assert.AreEqual(money,restored.State.Money);Assert.AreEqual(rng,restored.State.World.Rng);
        session.StopQuestLogRecording();
        var second=session.Execute("quest-after-save",session.Revision,command);
        Assert.AreEqual(CommandStatus.Accepted,second.Status,second.Error);
        Assert.AreEqual(2,session.State.QuestLog!.Scenes.Length);
        Assert.IsNull(session.ValidatePlayerSave(session.State));
    }

    [TestMethod]
    public void MalformedAndOverBudgetRecordingsAreRejected()
    {
        Assert.IsNotNull(CampaignQuestLogRecorder.Validate(new([])));
        Assert.IsNotNull(CampaignQuestLogRecorder.Validate(new(Enumerable.Repeat(Log().Scenes[0],5).ToImmutableArray())));
        var scene=Log().Scenes[0];
        Assert.IsNotNull(CampaignQuestLogRecorder.Validate(new([scene with{Actions=[new(18,0,0,Direction.South,true,CampaignQuestLogEvent.None,16)]}])));
        Assert.IsNotNull(CampaignQuestLogRecorder.Validate(new([scene with{Actions=[new(-1,0,0,Direction.South,false,(CampaignQuestLogEvent)999,16)]}])));
        Assert.IsNotNull(CampaignQuestLogRecorder.Validate(new([scene with{Actors=[]}])));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new CampaignQuestLogPlayback(Log()).Tick(double.NaN));
    }
}
