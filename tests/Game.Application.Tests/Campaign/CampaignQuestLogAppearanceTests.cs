using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogAppearanceTests
{
    private static CampaignQuestLogAction Arrival()=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
    [TestMethod]
    [DataRow(false,false,false)]
    [DataRow(false,false,true)]
    [DataRow(false,true,false)]
    [DataRow(false,true,true)]
    [DataRow(true,false,false)]
    [DataRow(true,false,true)]
    [DataRow(true,true,false)]
    [DataRow(true,true,true)]
    public void LabelsCannotAdmitUnmountedScenesOrTransitionProjections(bool transition,bool emptyTiles,bool zeroFlash)
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        const string unmounted="map.label-only-test";
        Assert.IsFalse(catalog.Maps.ContainsKey(new(unmounted)));
        var source=catalog.QuestLogSource with{MapNames=catalog.QuestLogSource.MapNames.SetItem(unmounted,"LABEL ONLY")};
        Assert.AreEqual("LABEL ONLY",source.MapName(unmounted));
        var actor=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"OBJ_EVENT_GFX_RED_NORMAL",6,8,Direction.South,true);
        var input=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Input,1);
        ImmutableArray<FieldMetatileEdit> tiles=emptyTiles?[]:default;int? flash=zeroFlash?0:null;
        var scene=new CampaignQuestLogScene(transition?state.World.Player.MapId.Value:unmounted,[actor],[input]){Tiles=tiles,FlashLevel=flash};
        if(transition)scene=scene with{Actions=[input,new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,1)
            {StartFrame=1,Transition=new(unmounted,[actor],null,tiles){FlashLevel=flash}}]};
        var log=new CampaignQuestLog([scene]);
        // Without a catalog this API deliberately validates structure and labels only.
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,source));
        Assert.AreEqual("save_quest_log_map_invalid",CampaignQuestLogRecorder.Validate(log,source,catalog));
        Assert.AreEqual("save_quest_log_map_invalid",CampaignQuestLogRecorder.Validate(log,null,catalog));
        var valid=transition?scene with{Actions=scene.Actions.SetItem(1,scene.Actions[1] with
            {Transition=scene.Actions[1].Transition! with{Map=state.World.Player.MapId.Value,Tiles=[]}})}:scene with{Map=state.World.Player.MapId.Value};
        var roundtrip=JsonSerializer.Deserialize<CampaignQuestLog>(JsonSerializer.Serialize(new CampaignQuestLog([valid])))!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(roundtrip,source,catalog));
    }
    [TestMethod]
    public void CurrentSaveAndRestoreRejectLabeledUnmountedRecordingWithoutLoadingACheckpoint()
    {
        var catalog=CurrentCampaignFixture.Catalog;var session=CampaignSession.CreateCurrent(catalog);var state=session.State;
        var unmounted=catalog.QuestLogSource.MapNames.Keys.First(id=>!catalog.Maps.ContainsKey(new(id))&&!catalog.QuestLogSource.DisabledMaps.Contains(id));
        var scene=CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource) with{Map=unmounted,Layout=null,Tiles=[],FlashLevel=0};
        var forged=state with{QuestLog=new([scene])};
        Assert.IsNull(CampaignQuestLogRecorder.Validate(forged.QuestLog,catalog.QuestLogSource));
        Assert.AreEqual("save_quest_log_map_invalid",session.ValidatePlayerSave(forged));
        StringAssert.Contains(Assert.Throws<InvalidDataException>(()=>CampaignSession.RestoreCurrent(catalog,forged,0)).Message,"save_quest_log_map_invalid");
        var valid=state with{QuestLog=new([scene with{Map=state.World.Player.MapId.Value}])};
        Assert.IsNull(session.ValidatePlayerSave(valid));
        Assert.IsNull(CampaignSession.RestoreCurrent(catalog,valid,0).ValidatePlayerSave(valid));
    }
    [TestMethod]
    public void RecordingFormatHasOneTimelineAndRejectsMissingVersionClearly()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:Arrival())!;
        string json=JsonSerializer.Serialize(log);var parsed=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();parsed.Remove("Version");
        var old=JsonSerializer.Deserialize<CampaignQuestLog>(parsed.ToJsonString())!;
        Assert.AreEqual(0,old.Version);Assert.AreEqual("save_quest_log_version_unsupported",CampaignQuestLogRecorder.Validate(old,catalog.QuestLogSource));
        Assert.AreEqual(2,JsonSerializer.Deserialize<CampaignQuestLog>(json)!.Version);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(null,catalog.QuestLogSource));
    }
    [TestMethod]
    public void SparseAppearanceCapturesInitialTilesAndAppliesRemovalWithoutMutatingSavedField()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var tile=new FieldMetatileEdit(2,2,1,0,3);
        state=state with{MapEdits=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty.Add("2,2",tile)};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:Arrival())!;
        Assert.AreEqual(tile,log.Scenes[0].Tiles.Single());
        var after=state with{MapEdits=null};
        log=CampaignQuestLogRecorder.Record(log,state,after,new("ack"),catalog)!;
        Assert.AreEqual(-1,log.Scenes[0].Actions[^1].Tile!.SourceMetatileId);
        var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource);
        Assert.IsTrue(playback.Tiles.IsEmpty);Assert.AreEqual(tile,state.MapEdits!["2,2"]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
    }
    [TestMethod]
    public void OversizedInitialProjectionAndDeltaStopWithoutPartiallyRecordedRoom()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:Arrival())!;
        var edits=Enumerable.Range(0,CampaignQuestLogRecorder.AppearanceTileLimit+1).ToImmutableSortedDictionary(i=>i.ToString(),i=>new FieldMetatileEdit(i%20,i/20,1,0,3));
        var oversized=state with{MapEdits=edits};
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,oversized,oversized,new("move"),catalog,suppliedEvent:Arrival()));
        var closed=CampaignQuestLogRecorder.Record(log,state,oversized,new("ack"),catalog)!;
        Assert.IsTrue(closed.Scenes[0].Closed);Assert.AreEqual(log.Scenes[0].Actions,closed.Scenes[0].Actions);
        Assert.IsTrue(closed.Scenes[0].Tiles.IsDefaultOrEmpty);
    }
    [TestMethod]
    public void UnknownLayoutIsRejectedAgainstCatalogBeforePresentation()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var log=new CampaignQuestLog([CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource) with{Layout="LAYOUT_NOT_REAL"}]);
        Assert.AreEqual("save_quest_log_layout_invalid",CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
    }
    [TestMethod]
    public void ConnectedBorderAppearanceIsBoundedAndSurvivesPurePlayback()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var maps=catalog.ConnectedFieldMaps!;
        var candidate=maps.Values.SelectMany(map=>Enumerable.Range(0,map.Width).Select(x=>(Map:map.Id,X:x,Y:-1)))
            .First(cell=>ConnectedFieldCoordinates.Resolve(maps,cell.Map,cell.X,cell.Y) is not null&&catalog.QuestLogSource.MapNames.ContainsKey(cell.Map.Value));
        var scene=CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource) with
        {
            Map=candidate.Map.Value,Layout=null,
            Tiles=[new(candidate.X,candidate.Y,1,0,3)],Actions=[Arrival()]
        };
        var log=new CampaignQuestLog([scene]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
        var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource);
        Assert.AreEqual(-1,playback.Tiles.Single().Y);
        playback.Skip();Assert.AreEqual(-1,log.Scenes[0].Tiles.Single().Y);
        Assert.AreEqual("save_quest_log_tiles_invalid",CampaignQuestLogRecorder.Validate(new([scene with{Tiles=[new(candidate.X,-8,1,0,3)]}]),catalog.QuestLogSource,catalog));
    }

}
