using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Application.FieldItems;

[TestClass, TestCategory("Game")]
public sealed class HeldItemAdmissionTests
{
    [TestMethod]
    public void BoundEligibilityMatchesRawRulesForEveryAdmittedItemAndMachine()
    {
        var content = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(content);
        var shop = content.EarlyShop!;
        var bound = HeldItemTransferRules.BuildHoldableItems(shop, content.Machines);
        var items = shop.Items.Select(item => item.Id)
            .Concat(content.Machines.Machines.Select(machine => machine.Machine.Item.Id))
            .Concat(content.Inventory.Items.Keys)
            .Concat(new[] { "ITEM_NOT_REAL", "ITEM_BERRY_JUICE", "ITEM_MOOMOO_MILK", "ITEM_BLUE_SHARD",
                "ITEM_GREEN_SHARD", "ITEM_HM08", "ITEM_ENIGMA_BERRY", "ITEM_ORANGE_MAIL" }
                .Select(symbol => new ItemId("pret:" + symbol))).Distinct().ToArray();
        Assert.IsTrue(items.Length > 0);
        foreach (var item in items)
        {
            bool ordinary = HeldItemTransferRules.CanHold(shop, item, content.Machines);
            Assert.AreEqual(ordinary, bound.Contains(item), item.Value);
            Assert.AreEqual(session.CanGiveMail(item) || ordinary, session.CanGiveHeldItem(item), item.Value);
        }
        Assert.IsFalse(bound.Contains(new("pret:ITEM_ORANGE_MAIL")));
        Assert.IsTrue(session.CanGiveMail(new("pret:ITEM_ORANGE_MAIL")));
        Assert.IsTrue(session.CanGiveHeldItem(new("pret:ITEM_ORANGE_MAIL")));
    }

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
