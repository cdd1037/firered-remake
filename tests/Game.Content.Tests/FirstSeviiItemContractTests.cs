using Game.Content;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class FirstSeviiItemContractTests
{
    [TestMethod]
    public void CatalogChecksMembershipRatherThanHistoricalTableSizes()
    {
        var catalog = PretFirstSeviiRules.LoadItems(TestAssets.Root);
        (catalog with { Shops = catalog.Shops.RemoveAt(0), Pickups = catalog.Pickups.RemoveAt(0) }).Validate();
        Assert.ThrowsExactly<NotSupportedException>(() =>
            (catalog with { Items = catalog.Items.Add(catalog.Items[0]) }).Validate());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            (catalog with { Shops = [catalog.Shops[0] with { Items = [new ItemId("missing")] }] }).Validate());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            (catalog with { RenewableStepThreshold = 1499 }).Validate());
    }
}
