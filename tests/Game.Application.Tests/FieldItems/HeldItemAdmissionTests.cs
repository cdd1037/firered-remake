using Game.Application;

namespace FireRed.Tests.Application.FieldItems;

[TestClass, TestCategory("Game")]
public sealed class HeldItemAdmissionTests
{
    [TestMethod]
    [DataRow("ITEM_NONE")]
    [DataRow("ITEM_ORAN_BERRY")]
    [DataRow("ITEM_LUCKY_EGG")]
    [DataRow("ITEM_THICK_CLUB")]
    [DataRow("ITEM_KINGS_ROCK")]
    [DataRow("ITEM_HEART_SCALE")]
    [DataRow("ITEM_METAL_POWDER")]
    [DataRow("ITEM_TM10")]
    [DataRow("ITEM_SPELON_BERRY")]
    [DataRow("ITEM_PAMTRE_BERRY")]
    [DataRow("ITEM_WATMEL_BERRY")]
    [DataRow("ITEM_DURIN_BERRY")]
    [DataRow("ITEM_BELUE_BERRY")]
    public void CurrentDefinitionsAdmitHeldItemsWithoutRegionalProofTables(string item)
    {
        var content = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(content);
        var state = CurrentCampaignFixture.Checkpoint.State;
        int index = Enumerable.Range(0, state.Party.Length)
            .First(i => !state.Party[i].IsEgg && state.Party[i].Mail == 255);
        var changed = state with { Party = state.Party.SetItem(index, state.Party[index] with { HeldItem = item }) };
        Assert.IsNull(session.ValidatePlayerSave(changed), item);
    }

    [TestMethod]
    [DataRow("ITEM_NOT_REAL")]
    [DataRow("ITEM_BERRY_JUICE")]
    [DataRow("ITEM_MOOMOO_MILK")]
    [DataRow("ITEM_BLUE_SHARD")]
    [DataRow("ITEM_GREEN_SHARD")]
    [DataRow("ITEM_HM01")]
    [DataRow("ITEM_BICYCLE")]
    public void UnknownAndImportantItemsAreRejected(string item)
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        Assert.IsFalse(session.CanGiveHeldItem(new("pret:" + item)));
        var state = CurrentCampaignFixture.Checkpoint.State;
        int index = Enumerable.Range(0, state.Party.Length)
            .First(i => !state.Party[i].IsEgg && state.Party[i].Mail == 255);
        var changed = state with { Party = state.Party.SetItem(index, state.Party[index] with { HeldItem = item }) };
        Assert.IsNotNull(session.ValidatePlayerSave(changed), item);
    }
}
