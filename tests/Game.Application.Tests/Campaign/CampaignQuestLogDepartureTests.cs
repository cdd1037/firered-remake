using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogDepartureTests
{
    private static CampaignState At(CampaignState state,string map,int x=6,int y=8)=>state with{World=state.World with{Player=state.World.Player with{MapId=new(map),X=x,Y=y}}};
    private static CampaignState Fresh()=>CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
    private static QuestLogSource Source=>CurrentCampaignFixture.Catalog.QuestLogSource;
    private static (CampaignState State,CampaignQuestLogAction? Event,bool ClearPlayedSlots) Enter(CampaignState state,string map,bool played=false,int x=6,int y=8)=>CampaignQuestLogDepartureCapture.Observe(state,At(state,map,x,y),Source,played);
    [TestMethod]
    public void GameCornerDepartureRequiresPlayedReelsAndUsesOriginalCaption()
    {
        var pair=Source.Departures.Single(d=>d.Kind=="GAME_CORNER");
        var before=Enter(Fresh(),pair.Inside).State;
        Assert.IsNull(Enter(before,pair.Outside).Event);
        var exit=Enter(before,pair.Outside,true);
        Assert.AreEqual("PlayedGamesAtGameCorner",exit.Event!.TextKey);Assert.IsTrue(exit.ClearPlayedSlots);
        Assert.AreEqual("Played a lot of games at the ROCKET\nGAME CORNER in CELADON CITY.",Source.Expand(exit.Event.TextKey!,"RED",exit.Event.Arguments));
        Assert.AreEqual(before.World.Rng,exit.State.World.Rng);
        Assert.IsFalse(exit.State.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
    }
    [TestMethod]
    public void EntranceIsSelectedOnNewMapAndSurvivesNestedRoomAndSaveRoundTrip()
    {
        var entered=Enter(Fresh(),"map.players-house-1f");Assert.IsNull(entered.Event);
        var nested=Enter(entered.State,"map.players-house-2f");
        Assert.IsTrue(nested.State.Script.Flags[CampaignQuestLogDepartureCapture.PendingFlag]);
        var json=System.Text.Json.JsonSerializer.Serialize(nested.State);
        var restored=System.Text.Json.JsonSerializer.Deserialize<CampaignState>(json)!;
        var exit=Enter(restored,"map.pallet-town");
        Assert.AreEqual("RestedAtHome",exit.Event!.TextKey);
        Assert.IsFalse(exit.State.Script.Flags[CampaignQuestLogDepartureCapture.PendingFlag]);
        Assert.AreSame(exit.State,CampaignQuestLogDepartureCapture.Observe(exit.State,exit.State,Source).State);
    }
    [TestMethod]
    public void RocketHideoutRetainsItsEntranceThroughGameCornerThenRearmsGameCorner()
    {
        var entered=Enter(Fresh(),"map.rocket-hideout-b1f").State;
        var nested=Enter(entered,"map.rocket-hideout-b2f").State;
        var exit=Enter(nested,"map.celadon-city-game-corner",true);
        var rocket=Source.Departures.Single(d=>d.LocationSymbol=="QL_LOCATION_ROCKET_HIDEOUT");
        var corner=Source.Departures.Single(d=>d.Kind=="GAME_CORNER");
        Assert.AreEqual(rocket.TextKey,exit.Event!.TextKey);Assert.IsTrue(exit.ClearPlayedSlots);
        Assert.AreEqual(corner.LocationId,exit.State.Script.Variables[CampaignQuestLogDepartureCapture.EntranceVariable]);
        Assert.IsTrue(exit.State.Script.Flags[CampaignQuestLogDepartureCapture.PendingFlag]);
        Assert.IsNull(Enter(exit.State,corner.Outside).Event);
    }
    [TestMethod]
    public void GymCaptionUsesTheOriginalBadgeCityOrder()
    {
        foreach(var gym in Source.Departures.Where(d=>d.Kind=="GYM"))
        {
            var entered=Enter(Fresh(),gym.Inside).State;
            Assert.AreEqual("GymWasFullOfToughTrainers",Enter(entered,gym.Outside).Event!.TextKey);
            var won=entered with{Script=entered.Script with{Flags=entered.Script.Flags.SetItem(gym.BadgeFlag!,true)}};
            Assert.AreEqual("DepartedGym",Enter(won,gym.Outside).Event!.TextKey);
        }
    }
    [TestMethod]
    public void ForestAlternateExitUsesRouteTwoAndTravelResetSuppressesStaleDeparture()
    {
        var forest=Source.Departures.Single(d=>d.LocationSymbol=="QL_LOCATION_VIRIDIAN_FOREST_1");
        var north=Source.Departures.Single(d=>d.LocationSymbol=="QL_LOCATION_VIRIDIAN_FOREST_2");
        var entered=Enter(Fresh(),forest.Inside).State;
        var exit=Enter(entered,north.Outside);Assert.AreEqual("ROUTE 2",exit.Event!.Arguments[0]);
        var reset=entered with{Script=entered.Script with{Flags=entered.Script.Flags.SetItem(CampaignQuestLogDepartureCapture.PendingFlag,false),Variables=entered.Script.Variables.SetItem(CampaignQuestLogDepartureCapture.EntranceVariable,0)}};
        Assert.IsNull(Enter(reset,north.Outside).Event);
    }
    [TestMethod]
    public void SameCommandDepartureAndFirstArrivalRetainBothOrderedSourceCaptions()
    {
        var gym=Source.Departures.Single(d=>d.LocationSymbol=="QL_LOCATION_PEWTER_GYM");
        var before=Enter(Fresh(),gym.Inside).State;
        var after=At(before,gym.Outside) with{Script=before.Script with{Flags=before.Script.Flags.SetItem("FLAG_WORLD_MAP_PEWTER_CITY",true)}};
        var departure=CampaignQuestLogDepartureCapture.Observe(before,after,Source);
        var arrival=CampaignQuestLogRecorder.FirstArrival(before,departure.State,Source)!;
        var log=CampaignQuestLogRecorder.RecordEvents(null,departure.State,[departure.Event!,arrival],CurrentCampaignFixture.Catalog,false)!;
        Assert.AreEqual(2,log.Scenes.Single().Actions.Length);
        CollectionAssert.AreEqual(new[]{CampaignQuestLogEvent.Departed,CampaignQuestLogEvent.Arrived},log.Scenes[0].Actions.Select(a=>a.Event).ToArray());
        Assert.IsNull(CampaignQuestLogRecorder.FirstArrival(departure.State,departure.State,Source));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,Source,CurrentCampaignFixture.Catalog));
    }

}
