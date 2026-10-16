using Game.Content;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class ItemStorageCatalogTests
{
    [TestMethod]
    public void QuestItemsRequireActualDefinitionsAndPreservePocketLimits()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var catalog = PretQuestItems.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/items/quest/quest-items.json")), shared);
        var townMap = shared.Definition(QuestItemRules.TownMapId);
        var teachyTv = shared.Definition(QuestItemRules.TeachyTvId);
        // Either supported key can be added without walking historical stage profiles.
        QuestItemRules.ValidateCatalog(catalog with { Items = catalog.Items.Add(townMap) });
        QuestItemRules.ValidateCatalog(catalog with { Items = catalog.Items.Add(teachyTv) });
        QuestItemRules.ValidateCatalog(catalog with { Items = catalog.Items.Add(townMap).Add(teachyTv) });
        Assert.ThrowsExactly<NotSupportedException>(() => QuestItemRules.ValidateCatalog(
            catalog with { Items = catalog.Items.RemoveAt(0) }));
        Assert.ThrowsExactly<NotSupportedException>(() => QuestItemRules.ValidateCatalog(
            catalog with { Items = catalog.Items.Add(catalog.Items[0]) }));
        Assert.ThrowsExactly<NotSupportedException>(() => QuestItemRules.ValidateCatalog(
            catalog with { Limits = catalog.Limits with { MaximumStack = 1000 } }));
        Assert.ThrowsExactly<NotSupportedException>(() => QuestItemRules.ValidateCatalog(
            catalog with { Items = catalog.Items.Add(townMap with { Registrability = 0 }) }));
        var state = new InventoryState([], [], OpeningInventoryRules.SourceLimits);
        var received = QuestItemRules.Add(catalog, state, QuestItemRules.PokeBallSymbol, 999);
        Assert.IsTrue(received.Succeeded);
        Assert.AreEqual(999, QuestItemRules.Quantity(catalog, received.State, QuestItemRules.PokeBallSymbol));
        Assert.AreEqual("inventory_stack_full", QuestItemRules.Add(catalog, received.State,
            QuestItemRules.PokeBallSymbol).Error);
        Assert.AreEqual("inventory_item_unsupported", QuestItemRules.Add(catalog, state,
            QuestItemRules.TownMapSymbol).Error);
    }

    [TestMethod]
    public void OpeningItemsKeepInitialInventoryAndPotionEffects()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var catalog = PretOpeningItems.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/items/opening/opening-items.json")), shared);
        var state = OpeningInventoryRules.NewGame(catalog);
        Assert.IsTrue(state.BagItems.IsEmpty);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(state.PcItems, OpeningInventoryRules.PotionId));
        var withdrawn = OpeningInventoryRules.Withdraw(catalog, state, OpeningInventoryRules.PotionId, 1);
        Assert.IsTrue(withdrawn.Succeeded);
        Assert.IsTrue(withdrawn.State.PcItems.IsEmpty);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(withdrawn.State.BagItems, OpeningInventoryRules.PotionId));
        Assert.ThrowsExactly<NotSupportedException>(() => OpeningInventoryRules.ValidateCatalog(
            catalog with { Potion = catalog.Potion with { HealHp = 21 } }));
        Assert.ThrowsExactly<NotSupportedException>(() => OpeningInventoryRules.ValidateCatalog(
            catalog with { InitialPcItems = [] }));
        Assert.ThrowsExactly<NotSupportedException>(() => OpeningInventoryRules.ValidateCatalog(
            catalog with { Limits = catalog.Limits with { BagItemSlots = 43 } }));
    }
}
