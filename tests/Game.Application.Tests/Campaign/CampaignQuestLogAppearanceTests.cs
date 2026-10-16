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
