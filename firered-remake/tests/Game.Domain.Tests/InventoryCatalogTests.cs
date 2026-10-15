using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
using System.Collections.Immutable;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class InventoryCatalogTests
{
    private static readonly InventoryLimits Limits = new(2, 2, 999);
    private static readonly QuestItemDefinition Potion = Item("POTION", "POCKET_ITEMS");
    private static readonly QuestItemDefinition Key = Item("KEY", "POCKET_KEY_ITEMS");
    private static readonly QuestItemDefinition Ball = Item("BALL", "POCKET_POKE_BALLS");
    private static QuestItemDefinition Item(string symbol, string pocket) => new(new("item:" + symbol), symbol,
        1, symbol, 100, pocket == "POCKET_KEY_ITEMS" ? 1 : 0, 0, pocket, "ITEM_TYPE_BAG_MENU",
        "HOLD_EFFECT_NONE", 0, "NULL", 0, "NULL", 0, pocket != "POCKET_KEY_ITEMS", pocket != "POCKET_KEY_ITEMS", pocket != "POCKET_KEY_ITEMS");
    private static InventoryCatalog Catalog() => new([Potion, Key, Ball], Limits, new(2, 2, 999), 58, [Key.Id]);
    private static InventoryState State() => new([new(Potion.Id, 2)], [], Limits)
        { KeyItems = [new(Key.Id, 1)], Balls = [new(Ball.Id, 3)] };

    [TestMethod]
    public void ResolvesSemanticDefinitionsWithoutObjectIdentityProof()
    {
        var catalog = new InventoryCatalog([Potion with {}, Key with {}, Ball with {}], Limits, new(2, 2, 999), 58, [Key.Id]);
        Assert.IsNull(catalog.Validate(State()));
        Assert.IsNull(catalog.Validate(State() with { PcItems = [new(Ball.Id, 2)] }));
    }

    [TestMethod]
    public void RejectsUnknownItemsWrongPocketsAndDuplicateStacks()
    {
        var catalog = Catalog();
        Assert.AreEqual("inventory_item_unsupported", catalog.Validate(State() with { BagItems = [new(new ItemId("item:UNKNOWN"), 1)] }));
        Assert.AreEqual("inventory_item_pocket", catalog.Validate(State() with { BagItems = [new(Ball.Id, 1)] }));
        Assert.AreEqual("inventory_item_pocket", catalog.Validate(State() with { PcItems = [new(Key.Id, 1)] }));
        Assert.AreEqual("inventory_duplicate_stack", catalog.Validate(State() with { BagItems = [new(Potion.Id, 1), new(Potion.Id, 2)] }));
    }

    [TestMethod]
    public void RejectsInvalidQuantitiesLimitsAndPocketCapacity()
    {
        var catalog = Catalog();
        foreach (int quantity in new[] {0, -1, 1000})
            Assert.AreEqual("inventory_stack_quantity", catalog.Validate(State() with { BagItems = [new(Potion.Id, quantity)] }));
        Assert.AreEqual("inventory_stack_quantity", catalog.Validate(State() with { KeyItems = [new(Key.Id, 2)] }));
        Assert.AreEqual("inventory_limits", catalog.Validate(State() with { Limits = new(3, 2, 999) }));
        Assert.AreEqual("inventory_pocket_capacity", catalog.Validate(State() with { BagItems = [new(Potion.Id, 1), new(Potion.Id, 1), new(Potion.Id, 1)] }));
    }

    [TestMethod]
    public void RejectsConflictingDefinitionInsteadOfChoosingOne()
        => Assert.ThrowsExactly<ArgumentException>(() => new InventoryCatalog([Potion, Potion with { Price = 101 }], Limits, new(2, 2, 999), 58, []));

    [TestMethod]
    public void ContainersAreRequiredForMachineAndBerryPockets()
    {
        var berry = Item("BERRY", "POCKET_BERRY_POUCH");
        var pouch = Item("BERRY_POUCH", "POCKET_KEY_ITEMS") with { Id = SsAnneItemRules.BerryPouchId };
        var machine = Item("TM01", "POCKET_TM_CASE");
        var tmCase = Item("TM_CASE", "POCKET_KEY_ITEMS") with { Id = MachineRules.TmCaseId };
        var catalog = new InventoryCatalog([berry, pouch, machine, tmCase], Limits, new(2, 2, 999), 58, []);
        var state = new InventoryState([], [], Limits) { Berries = [new(berry.Id, 1)], Machines = [new(machine.Id, 1)],
            KeyItems = [new(pouch.Id, 1), new(tmCase.Id, 1)] };
        Assert.IsNull(catalog.Validate(state));
        Assert.AreEqual("inventory_berry_pouch_missing", catalog.Validate(state with { KeyItems = [new(tmCase.Id, 1)] }));
        Assert.AreEqual("inventory_berry_pouch_quantity", catalog.Validate(state with { KeyItems = [new(pouch.Id, 2), new(tmCase.Id, 1)] }));
        Assert.AreEqual("inventory_tm_case_missing", catalog.Validate(state with { KeyItems = [new(pouch.Id, 1)] }));
        Assert.AreEqual("inventory_tm_case_quantity", catalog.Validate(state with { KeyItems = [new(pouch.Id, 1), new(tmCase.Id, 2)] }));
    }

    [TestMethod]
    public void EscapeRopeConsumesOnlyItsStackAndRejectsInvalidContextsAtomically()
    {
        var rope = Item("ESCAPE_ROPE", "POCKET_ITEMS") with { Id = EscapeRopeRules.Item, FieldUseFunction = "ItemUseOutOfBattle_EscapeRope" };
        var items = new InventoryCatalog([rope, Potion, Key], Limits, new(2, 2, 999), 58, [Key.Id]);
        var exit = new EscapeRopeDestination(new("map.outside"), 2, 3);
        var policy = new EscapeRopePolicy(ImmutableDictionary<MapId, ImmutableArray<EscapeRopeDestination>>.Empty.Add(new("map.cave"), [exit]), []);
        var before = new InventoryState([new(rope.Id, 1), new(Potion.Id, 5)], [new(Potion.Id, 7)], Limits) { KeyItems = [new(Key.Id, 1)] };
        var result = EscapeRopeRules.Use(items, policy, before, new("map.cave"), exit, PotionUseContext.Field);
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, result.ItemsConsumed);
        CollectionAssert.AreEqual(new[] { new InventoryStack(Potion.Id, 5) }, result.Inventory.BagItems.ToArray());
        Assert.AreEqual(before.PcItems, result.Inventory.PcItems);
        Assert.AreEqual(before.KeyItems, result.Inventory.KeyItems);
        Assert.AreEqual(new TilePosition(exit.MapId, 2, 3, 3, Direction.South), result.Arrival);
        foreach (var failure in new[] {
            EscapeRopeRules.Use(items, policy, before, new("map.outside"), exit, PotionUseContext.Field),
            EscapeRopeRules.Use(items, policy, before, new("map.cave"), null, PotionUseContext.Field),
            EscapeRopeRules.Use(items, policy, before, new("map.cave"), exit, PotionUseContext.Battle) })
        {
            Assert.IsFalse(failure.Succeeded);
            Assert.AreSame(before, failure.Inventory);
            Assert.AreEqual(0, failure.ItemsConsumed);
        }
    }
}
