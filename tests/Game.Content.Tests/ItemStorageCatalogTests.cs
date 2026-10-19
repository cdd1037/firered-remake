using System.Collections.Immutable;
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
        foreach (var limits in new[] {
            catalog.Limits with { KeyItemSlots = 31 },
            catalog.Limits with { BallSlots = 14 },
            catalog.Limits with { MaximumStack = 1000 } })
            Assert.ThrowsExactly<NotSupportedException>(() => QuestItemRules.ValidateCatalog(catalog with { Limits = limits }));
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
    [DataRow(QuestItemRules.ParcelSymbol)]
    [DataRow(QuestItemRules.PokeBallSymbol)]
    [DataRow(QuestItemRules.TeachyTvSymbol)]
    [DataRow(QuestItemRules.TownMapSymbol)]
    public void QuestItemRulesUseStableIdentityAndRetainEveryCapability(string symbol)
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var catalog = new QuestItemsCatalog(QuestItemRules.SourceLimits,
            new[] { QuestItemRules.ParcelId, QuestItemRules.PokeBallId, QuestItemRules.TeachyTvId, QuestItemRules.TownMapId }
                .Select(id => shared.Definition(id) with { Name = "Same display name" }).ToImmutableArray());
        QuestItemRules.ValidateCatalog(catalog);
        var item = catalog.Items.Single(value => value.SourceItemSymbol == symbol);
        var received = QuestItemRules.Add(catalog, new([], [], OpeningInventoryRules.SourceLimits), symbol);
        Assert.IsTrue(received.Succeeded);
        Assert.AreEqual(1, QuestItemRules.Quantity(catalog, received.State, symbol));
        var pocket = item.Pocket == "POCKET_KEY_ITEMS" ? received.State.KeyItems : received.State.Balls;
        Assert.AreEqual(item.Id, pocket!.Value.Single().Item);

        foreach (var name in new string?[] { null, "", " \t\n" })
            Reject(item with { Name = name! });
        foreach (var invalid in new[] {
            item with { Id = new("pret:ITEM_POTION") },
            item with { SourceItemSymbol = "ITEM_POTION" },
            item with { SourceItemId = item.SourceItemId + 1 },
            item with { Price = item.Price + 1 },
            item with { Importance = item.Importance + 1 },
            item with { Registrability = 1 - item.Registrability },
            item with { Pocket = "POCKET_ITEMS" },
            item with { Type = "ITEM_TYPE_PARTY_MENU" },
            item with { HoldEffect = "HOLD_EFFECT_LEFTOVERS" },
            item with { HoldEffectParam = 1 },
            item with { FieldUseFunction = "FieldUseFunc_Medicine" },
            item with { BattleUsage = item.BattleUsage + 1 },
            item with { BattleUseFunction = "BattleUseFunc_Medicine" },
            item with { SecondaryId = item.SecondaryId + 1 },
            item with { CanToss = !item.CanToss },
            item with { CanSell = !item.CanSell },
            item with { CanGive = !item.CanGive } })
            Reject(invalid);

        void Reject(QuestItemDefinition invalid) => Assert.ThrowsExactly<NotSupportedException>(() =>
            QuestItemRules.ValidateCatalog(catalog with { Items = catalog.Items.Replace(item, invalid) }));
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
