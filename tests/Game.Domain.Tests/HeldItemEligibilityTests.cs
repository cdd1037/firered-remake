using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class HeldItemEligibilityTests
{
    private static QuestItemDefinition Item(string symbol, string pocket = "POCKET_ITEMS", int importance = 0) =>
        new(new("pret:" + symbol), symbol, 1, symbol, 100, importance, 0, pocket, "ITEM_TYPE_BAG_MENU",
            "HOLD_EFFECT_NONE", 0, "NULL", 0, "NULL", 0, importance == 0, true, importance == 0);

    private static EarlyShopItemDefinition Ordinary(QuestItemDefinition item) =>
        new(item, item.Name, 0, 0, [], EarlyShopUseSupport.NoFieldUse);

    private static EarlyShopCatalog Shop() => new(new(42, 50, 999), new(30, 13, 999), 99, 999999,
        [Ordinary(Item("ITEM_POTION")), Ordinary(Item("ITEM_ORAN_BERRY", "POCKET_BERRY_POUCH")),
            Ordinary(Item("ITEM_POKE_BALL", "POCKET_POKE_BALLS")), Ordinary(Item("ITEM_BICYCLE", "POCKET_KEY_ITEMS", 1)),
            Ordinary(Item("ITEM_ENIGMA_BERRY", "POCKET_BERRY_POUCH")), Ordinary(Item("ITEM_ORANGE_MAIL"))], []);

    private static MachineDefinition Machine(string symbol, int importance) =>
        new(new(Item(symbol, "POCKET_TM_CASE", importance), symbol), new("pret:MOVE_TACKLE"), 33, 35,
            [new(new("pret:SPECIES_BULBASAUR"), 1, true)]);

    private static MachineCatalog Machines() => new(new(Item("ITEM_TM_CASE", "POCKET_KEY_ITEMS", 1), "TM CASE"),
        [Machine("ITEM_TM01", 0), Machine("ITEM_HM01", 1)], 58, 30, 999);

    [TestMethod]
    public void BoundSetPreservesPocketImportanceMailAndEnigmaRules()
    {
        var shop = Shop();
        var machines = Machines();
        var bound = HeldItemTransferRules.BuildHoldableItems(shop, machines);
        Assert.IsTrue(bound.SetEquals(new[] { "ITEM_POTION", "ITEM_ORAN_BERRY", "ITEM_POKE_BALL", "ITEM_TM01" }
            .Select(symbol => new ItemId("pret:" + symbol))));
        foreach (var id in shop.Items.Select(item => item.Id).Concat(machines.Machines.Select(machine => machine.Machine.Item.Id))
                     .Append(new("pret:ITEM_UNKNOWN")))
            Assert.AreEqual(HeldItemTransferRules.CanHold(shop, id, machines), bound.Contains(id), id.Value);
    }

    [TestMethod]
    public void OptionalMachinesAndIndependentCatalogsDoNotBroadenAdmission()
    {
        var shop = Shop();
        var original = HeldItemTransferRules.BuildHoldableItems(shop);
        var potion = new ItemId("pret:ITEM_POTION");
        var reduced = shop with { Items = shop.Items.RemoveAt(0) };
        Assert.IsTrue(original.Contains(potion));
        Assert.IsFalse(original.Contains(new("pret:ITEM_TM01")));
        Assert.IsFalse(HeldItemTransferRules.BuildHoldableItems(reduced).Contains(potion));
        Assert.IsTrue(original.Contains(potion));
    }

    [TestMethod]
    public void MachineDefinitionKeepsPrecedenceOverOrdinaryItem()
    {
        var shop = Shop();
        var machines = Machines() with { Machines = [Machine("ITEM_POTION", 1)] };
        var potion = new ItemId("pret:ITEM_POTION");
        Assert.IsFalse(HeldItemTransferRules.CanHold(shop, potion, machines));
        Assert.IsFalse(HeldItemTransferRules.BuildHoldableItems(shop, machines).Contains(potion));
    }

    [TestMethod]
    public void InvalidShopDefinitionsStillFailAtBothBoundaries()
    {
        var shop = Shop();
        var invalid = shop with { Items = shop.Items.Add(shop.Items[0]) };
        Assert.ThrowsExactly<NotSupportedException>(() => HeldItemTransferRules.BuildHoldableItems(invalid, Machines()));
        Assert.ThrowsExactly<NotSupportedException>(() => HeldItemTransferRules.CanHold(invalid, new("pret:ITEM_POTION"), Machines()));
        Assert.ThrowsExactly<ArgumentNullException>(() => HeldItemTransferRules.BuildHoldableItems(null!));
    }

    [TestMethod]
    public void InvalidMachineDefinitionsStillFailAtBothBoundaries()
    {
        var machine = Machine("ITEM_TM01", 0);
        var invalid = Machines() with { Machines = [machine with { Compatibility = machine.Compatibility.Add(machine.Compatibility[0]) }] };
        Assert.ThrowsExactly<ArgumentException>(() => HeldItemTransferRules.BuildHoldableItems(Shop(), invalid));
        Assert.ThrowsExactly<ArgumentException>(() => HeldItemTransferRules.CanHold(Shop(), new("pret:ITEM_POTION"), invalid));
        Assert.ThrowsExactly<ArgumentException>(() => HeldItemTransferRules.BuildHoldableItems(Shop(), Machines() with { MachineSlots = 0 }));
    }
}
