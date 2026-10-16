using System.Collections.Immutable;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogWildTests
{
    private static CampaignQuestLogAction Wild(int defeated,int caught,string species="PIDGEY")=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.WildWon,127)
    {TextKey=defeated>0?"DefeatedWildMon":"CaughtWildMon",Arguments=["ROUTE 1",species,defeated.ToString(),species,caught.ToString(),"RED"]};

    [TestMethod]
    [DataRow(0,1,0,1,"CaughtWildMons")]
    [DataRow(1,0,1,0,"DefeatedWildMons")]
    [DataRow(1,0,0,1,"DefeatedWildMonAndCaughtWildMon")]
    [DataRow(1,0,0,2,"DefeatedWildMonAndCaughtWildMons")]
    [DataRow(2,0,0,1,"DefeatedWildMonsAndCaughtWildMon")]
    [DataRow(2,0,0,2,"DefeatedWildMonsAndCaughtWildMons")]
    public void UsesOriginalCountVariantAndUpdatesLastSpecies(int d,int c,int nextD,int nextC,string key)
    {
        var movement=new CampaignQuestLogAction(0,3,4,Direction.East,true,CampaignQuestLogEvent.None,16);
        ImmutableArray<CampaignQuestLogAction> original=[Wild(d,c),movement];
        var updated=CampaignQuestLogWildCapture.Merge(original,Wild(nextD,nextC,"RATTATA"));
        Assert.AreEqual(2,updated.Length);Assert.AreSame(movement,updated[1]);
        Assert.AreEqual(key,updated[0].TextKey);Assert.AreEqual((d+nextD).ToString(),updated[0].Arguments[2]);Assert.AreEqual((c+nextC).ToString(),updated[0].Arguments[4]);
        Assert.AreEqual(nextD>0?"RATTATA":"PIDGEY",updated[0].Arguments[1]);Assert.AreEqual(nextC>0?"RATTATA":"PIDGEY",updated[0].Arguments[3]);
        Assert.AreEqual(d.ToString(),original[0].Arguments[2]);
        Assert.IsFalse(string.IsNullOrWhiteSpace(CurrentCampaignFixture.Catalog.QuestLogSource.Expand(key,"RED",updated[0].Arguments)));
    }

    [TestMethod]
    public void CountsSaturateAndInterveningSemanticEventEndsAggregation()
    {
        ImmutableArray<CampaignQuestLogAction> original=[Wild(255,254)];
        var updated=CampaignQuestLogWildCapture.Merge(original,Wild(1,1));
        Assert.AreEqual("255",updated[0].Arguments[2]);Assert.AreEqual("255",updated[0].Arguments[4]);
        var stopped=original.Add(new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.PokemonCenter,127));
        Assert.AreEqual(stopped,CampaignQuestLogWildCapture.Merge(stopped,Wild(1,0)));
    }

    [TestMethod]
    public void FullMovementBufferUpdatesExistingWildRecordButSaveBoundaryDoesNot()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var actions=ImmutableArray.Create(Wild(1,0));
        var movement=new CampaignQuestLogAction(0,3,4,Direction.East,true,CampaignQuestLogEvent.None,16);
        while(CampaignQuestLogRecorder.Budget(actions.Add(movement),catalog.QuestLogSource)<=256)actions=actions.Add(movement);
        var scene=new CampaignQuestLogScene(state.World.Player.MapId.Value,[new(CampaignQuestLogRecorder.PlayerId,"player",3,4,Direction.South,true)],actions);
        var log=new CampaignQuestLog([scene]);
        var updated=CampaignQuestLogRecorder.Record(log,state,state,new("battle_ack"),catalog,recordingOpen:true,suppliedEvent:Wild(0,1));
        Assert.AreEqual(1,updated!.Scenes.Length);Assert.AreEqual(actions.Length,updated.Scenes[0].Actions.Length);
        Assert.AreEqual("DefeatedWildMonAndCaughtWildMon",updated.Scenes[0].Actions[0].TextKey);
        var saved=CampaignQuestLogRecorder.Record(log,state,state,new("battle_ack"),catalog,recordingOpen:false,suppliedEvent:Wild(0,1));
        Assert.AreSame(log,saved); // Fresh game's pre-Champion wild event cannot start a new scene.
        Assert.AreEqual("0",log.Scenes[0].Actions[0].Arguments[4]);
    }
}
