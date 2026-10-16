using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogAvatarTests
{
    private static CampaignQuestLogAction Arrival()=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
    [TestMethod]
    [DataRow(0,false,false,"OBJ_EVENT_GFX_RED_NORMAL")]
    [DataRow(1,false,false,"OBJ_EVENT_GFX_GREEN_NORMAL")]
    [DataRow(0,true,false,"OBJ_EVENT_GFX_RED_BIKE")]
    [DataRow(1,true,false,"OBJ_EVENT_GFX_GREEN_BIKE")]
    [DataRow(0,false,true,"OBJ_EVENT_GFX_RED_SURF")]
    [DataRow(1,false,true,"OBJ_EVENT_GFX_GREEN_SURF")]
    public void InitialAndSavedAppearanceRetainSourceModeAndGender(int gender,bool cycling,bool surfing,string expected)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var state=CampaignSession.CreateCurrent(catalog).State with{PlayerGender=gender,IsCycling=cycling,IsSurfing=surfing};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:Arrival())!;
        Assert.AreEqual(expected,log.Scenes[0].Actors[0].Graphics);
        Assert.AreEqual(expected,CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource).Actors[0].Graphics);
        Assert.AreEqual(gender,CampaignQuestLogAvatar.Gender(expected,1-gender));
        Assert.AreEqual(cycling,CampaignQuestLogAvatar.IsBike(expected));Assert.AreEqual(surfing,CampaignQuestLogAvatar.IsSurf(expected));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
    }
    [TestMethod]
    public void ModeDeltaUsesExistingBudgetAndPureTimelineWithoutReadingSavedMode()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=CampaignSession.CreateCurrent(catalog).State;
        var log=CampaignQuestLogRecorder.Record(null,before,before,new("move"),catalog,suppliedEvent:Arrival())!;
        int budget=CampaignQuestLogRecorder.Budget(log.Scenes[0].Actions,catalog.QuestLogSource);
        var after=before with{IsCycling=true};
        log=CampaignQuestLogRecorder.Record(log,before,after,new("field_bicycle",Text:"pret:ITEM_BICYCLE"),catalog)!;
        Assert.AreEqual("OBJ_EVENT_GFX_RED_BIKE",log.Scenes[0].Actions[^1].Graphics);
        Assert.AreEqual(budget+16,CampaignQuestLogRecorder.Budget(log.Scenes[0].Actions,catalog.QuestLogSource));
        string unchanged=JsonSerializer.Serialize(after);
        var saved=CampaignQuestLogRecorder.SavedScene(before,catalog.QuestLogSource);
        var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource,saved);
        Assert.AreEqual("OBJ_EVENT_GFX_RED_BIKE",playback.Actors.First().Graphics);
        playback.Tick(1d/60);Assert.AreEqual(1d,playback.Frame,.00001);
        playback.Skip();Assert.AreEqual("OBJ_EVENT_GFX_RED_NORMAL",playback.Actors.First().Graphics);
        playback.NextScene();Assert.IsTrue(playback.Finished);Assert.AreEqual(unchanged,JsonSerializer.Serialize(after));
        var complete=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource,saved);
        while(!complete.Finished)complete.Tick(10);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(after));
    }
    [TestMethod]
    [DataRow(FieldSurfSourcePresentation.ShowMon,true,"OBJ_EVENT_GFX_RED_NORMAL")]
    [DataRow(FieldSurfSourcePresentation.MountJump,true,"OBJ_EVENT_GFX_RED_SURF")]
    [DataRow(FieldSurfSourcePresentation.DismountJump,false,"OBJ_EVENT_GFX_RED_SURF")]
    public void SurfAppearanceFollowsVisibleEffectBoundary(string phase,bool surfing,string expected)
    {
        var state=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;var p=state.World.Player;
        state=state with{IsSurfing=surfing,SurfEffect=new(0,1,7,"SQUIRTLE",p,p,0,phase,"test")};
        Assert.AreEqual(expected,CampaignQuestLogAvatar.Graphics(state));
        Assert.AreEqual(surfing?"OBJ_EVENT_GFX_RED_SURF":"OBJ_EVENT_GFX_RED_NORMAL",CampaignQuestLogAvatar.Graphics(state with{SurfEffect=null}));
    }
    [TestMethod]
    public void ModeOverflowClosesWithoutPublishingUnbudgetedGraphics()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:Arrival())!;
        for(int i=0;i<40&&!log.Scenes[0].Closed;i++)
        {
            var next=state with{IsCycling=!state.IsCycling};var prior=log;
            log=CampaignQuestLogRecorder.Record(log,state,next,new("field_bicycle",Text:"pret:ITEM_BICYCLE"),catalog)!;
            if(log.Scenes[0].Closed)Assert.AreEqual(prior.Scenes[0].Actions,log.Scenes[0].Actions);
            state=next;
        }
        Assert.IsTrue(log.Scenes[0].Closed);
        Assert.IsTrue(CampaignQuestLogRecorder.Budget(log.Scenes[0].Actions,catalog.QuestLogSource)<=256);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource));
    }
}
