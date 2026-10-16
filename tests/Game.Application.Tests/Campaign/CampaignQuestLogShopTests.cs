using Game.Application;
using Game.Domain.Ids;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogShopTests
{
    [TestMethod]
    public void SourceShopPublishesOnlyOnExitAndKeepsBuySellOrderAndExactSingleItemVariants()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CurrentCampaignFixture.Checkpoint.State;
        var potion=catalog.Inventory.Items.Values.Single(i=>i.SourceItemSymbol=="ITEM_POTION");
        var before=state with{Shop=new("shop",CampaignShopStage.Confirmation,"sell",potion.Id,1)};
        var sold=before with{Shop=before.Shop with{Stage=CampaignShopStage.Message,MessageSymbol="gText_TurnedOverItemsWorthYen"}};
        var first=CampaignQuestLogShopCapture.Observe(null,before,sold,catalog);
        Assert.IsTrue(first.Events.IsEmpty);Assert.AreEqual(1,first.Capture!.Entries.Length);
        var buy=state with{Shop=new("shop",CampaignShopStage.Confirmation,"buy",potion.Id,1)};
        var bought=buy with{Shop=buy.Shop with{Stage=CampaignShopStage.Message,MessageSymbol="gText_HereYouGoThankYou"}};
        var second=CampaignQuestLogShopCapture.Observe(first.Capture,buy,bought,catalog);
        Assert.IsTrue(second.Events.IsEmpty);Assert.AreEqual(2,second.Capture!.Entries.Length);
        var closed=CampaignQuestLogShopCapture.Observe(second.Capture,bought,bought with{Shop=null},catalog);
        Assert.IsNull(closed.Capture);Assert.AreEqual(2,closed.Events.Length);
        Assert.AreEqual("SoldNumOfItem",closed.Events[0].TextKey);Assert.AreEqual("BoughtItem",closed.Events[1].TextKey);
        var soldText=catalog.QuestLogSource.Expand(closed.Events[0].TextKey!,state.PlayerName,closed.Events[0].Arguments);
        Assert.IsTrue(soldText.Contains("just one",StringComparison.OrdinalIgnoreCase));
        Assert.IsNull(state.QuestLog);
        var savedScene=CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource);
        var oldLog=new CampaignQuestLog([savedScene]);
        var resumed=CampaignQuestLogRecorder.RecordEvents(oldLog,state,closed.Events,catalog,recordingOpen:false)!;
        Assert.AreEqual(2,resumed.Scenes.Length);
        Assert.AreSame(savedScene,resumed.Scenes[0]);
        Assert.AreEqual(2,resumed.Scenes[1].Actions.Length);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(resumed,catalog.QuestLogSource));
    }

    [TestMethod]
    public void SourceShopCapsTwoTransientTotalsAndDistinguishesStackSaleFromRepeatedSales()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CurrentCampaignFixture.Checkpoint.State;
        var expensive=catalog.Inventory.Items.Values.Where(i=>i.Price>=3000).First();
        var before=state with{Shop=new("shop",CampaignShopStage.Confirmation,"sell",expensive.Id,999)};
        var after=before with{Shop=before.Shop with{Stage=CampaignShopStage.Message,MessageSymbol="gText_TurnedOverItemsWorthYen"}};
        var first=CampaignQuestLogShopCapture.Observe(null,before,after,catalog);
        var single=CampaignQuestLogShopCapture.Observe(first.Capture,after,after with{Shop=null},catalog);
        Assert.AreEqual("SoldNumOfItem",single.Events.Single().TextKey);
        Assert.IsTrue(catalog.QuestLogSource.Expand(single.Events[0].TextKey!,state.PlayerName,single.Events[0].Arguments).Contains("999"));
        var repeated=CampaignQuestLogShopCapture.Observe(first.Capture,before,after,catalog);
        Assert.AreEqual(1,repeated.Capture!.Entries.Length);
        Assert.AreEqual(999,repeated.Capture.Entries[0].Quantity);Assert.AreEqual(999999,repeated.Capture.Entries[0].Money);
        var closed=CampaignQuestLogShopCapture.Observe(repeated.Capture,after,after with{Shop=null},catalog);
        Assert.AreEqual("SoldItemsIncludingItem",closed.Events.Single().TextKey);
        Assert.IsTrue(catalog.QuestLogSource.Expand(closed.Events[0].TextKey!,state.PlayerName,closed.Events[0].Arguments).Contains("999999"));
    }

    [TestMethod]
    public void CancelledOrFailedPurchaseProducesNoEvent()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CurrentCampaignFixture.Checkpoint.State;
        var before=state with{Shop=new("shop",CampaignShopStage.Confirmation,"buy",new ItemId("pret:ITEM_POTION"),1)};
        var failed=before with{Shop=before.Shop with{Stage=CampaignShopStage.Message,MessageSymbol="gText_YouDontHaveMoney"}};
        var capture=CampaignQuestLogShopCapture.Observe(null,before,failed,catalog);
        Assert.IsTrue(capture.Capture!.Entries.IsEmpty);
        var closed=CampaignQuestLogShopCapture.Observe(capture.Capture,failed,failed with{Shop=null},catalog);
        Assert.IsTrue(closed.Events.IsEmpty);Assert.IsNull(closed.Capture);
    }
}
