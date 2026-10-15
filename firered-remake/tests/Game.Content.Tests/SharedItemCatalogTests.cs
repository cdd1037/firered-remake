using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedItemCatalogTests
{
    [TestMethod]
    public void InventoryMachineAndBattleViewsResolveTheSameBusinessItem()
    {
        var machines = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var items = machines.Items;
        var potion = items.Definition(new("pret:ITEM_POTION"));
        var early = PretEarlyShop.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/early-shop/early-shop.json")), items);
        var opening = PretOpeningItems.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/opening-items/opening-items.json")), items);
        var battle = SourceBattleItemCatalogLoader.LoadAuthenticated(TestAssets.Root, items);
        Assert.AreSame(potion, early.Catalog.Items.Single(i => i.Id == potion.Id).Item);
        Assert.AreSame(potion, opening.Potion.Item);
        Assert.AreSame(potion, battle.Items[potion.Id].Item);
        Assert.AreSame(items.Definition(new("pret:ITEM_TM39")), machines.Definition(new("pret:ITEM_TM39")).Machine.Item);
        Assert.AreEqual(20, opening.Potion.HealHp);
        Assert.AreEqual(300, potion.Price);
        Assert.IsTrue(potion.CanGive && potion.CanSell && potion.CanToss);
        Assert.AreEqual(1, items.Definition(new("pret:ITEM_GOOD_ROD")).SecondaryId);
    }

    [TestMethod]
    public void SelectionDoesNotAdmitOtherKnownItemsOrUnsupportedMachines()
    {
        var items = SharedItemCatalog.LoadProject(TestAssets.Root);
        var selected = items.Select([new("pret:ITEM_POTION"), new("pret:ITEM_POTION")]);
        Assert.AreEqual(1, selected.Count);
        Assert.IsFalse(selected.ContainsKey(new("pret:ITEM_MASTER_BALL")));
        Assert.IsNotNull(items.Definition(new("pret:ITEM_HM08")));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedMachineCatalog.LoadProject(TestAssets.Root).Definition(new("pret:ITEM_HM08")));
        Assert.ThrowsExactly<InvalidDataException>(() => items.Definition(new("pret:ITEM_NONE")));
    }

    [TestMethod]
    public void TypedReferencesRejectUnknownIdsAndExpandedBusinessObjects()
    {
        var items = SharedItemCatalog.LoadProject(TestAssets.Root);
        var options = items.CreateJsonOptions();
        var item = JsonSerializer.Deserialize<QuestItemDefinition>("\"pret:ITEM_TOWN_MAP\"", options)!;
        Assert.AreSame(items.Definition(item.Id), item);
        Assert.ThrowsExactly<InvalidDataException>(() => JsonSerializer.Deserialize<QuestItemDefinition>("\"pret:ITEM_MISSING\"", options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<QuestItemDefinition>(JsonSerializer.Serialize(item), options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<QuestItemDefinition>("{\"item\":\"pret:ITEM_TOWN_MAP\",\"source\":{}}", options));
    }
}
