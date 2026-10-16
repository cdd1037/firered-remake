using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class OrdinaryMartCatalogTests
{
    private static readonly StoryInstruction Counter = new(StoryOp.Special, "pokemart", ["CurrentStock"],
        new("data/maps/ArbitraryRoom/scripts.inc", "ArbitraryClerk", 9));
    private static readonly FieldActorDefinition Clerk = new("actor.counter", 7, "OBJ_EVENT_GFX_CLERK", 2, 3, 0,
        "MOVEMENT_TYPE_FACE_RIGHT", "ArbitraryClerk", "0");
    private static readonly FieldMapDefinition Map = new(new("map.arbitrary-room"), "MAP_ARBITRARY_ROOM",
        [Clerk], [], [], new(null, null, null), "MUS_POKE_CENTER");
    private static readonly EarlyShopCatalog Stock = new(new(30, 50, 999), new(30, 16, 999), 99, 999999,
        [], [new("CurrentStock", [new("pret:ITEM_POTION")])]);
    private sealed class Scripts(params StoryInstruction[] instructions) : IStoryCatalog
    {
        public StoryProgram? FindProgram(string id) => id == Clerk.ScriptId ? new(id, [..instructions]) : null;
        public bool TryConstant(string symbol, out int value) { value = 0; return false; }
    }

    [TestMethod]
    public void LoadedActorAndAdmittedInstructionBindWithoutMapOrLabelNamingRules()
    {
        var catalog = OrdinaryMartCatalog.Bind(new Scripts(Counter), [Map], Stock);
        var bound = catalog.ByActor[(Map.Id, Clerk.LocalId)];
        Assert.AreSame(Clerk, bound.Actor);
        Assert.AreSame(Stock.Shops[0], bound.Stock);
        Assert.AreSame(Counter, bound.Instruction);
    }

    [TestMethod]
    public void MissingMapActorProgramOrStockDoesNotBecomeAnOrdinaryShop()
    {
        Assert.IsEmpty(OrdinaryMartCatalog.Bind(new Scripts(Counter), [], Stock).ByActor);
        Assert.IsEmpty(OrdinaryMartCatalog.Bind(new Scripts(Counter), [Map with { Actors = [] }], Stock).ByActor);
        Assert.IsEmpty(OrdinaryMartCatalog.Bind(new Scripts(Counter), [Map with { Actors = [Clerk with { IsClone = true }] }], Stock).ByActor);
        Assert.IsEmpty(OrdinaryMartCatalog.Bind(new Scripts(), [Map], Stock).ByActor);
        Assert.IsEmpty(OrdinaryMartCatalog.Bind(new Scripts(Counter), [Map], Stock with { Shops = [] }).ByActor);
    }

    [TestMethod]
    public void AmbiguousCounterAndMismatchedExecutableContextFailComposition()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => OrdinaryMartCatalog.Bind(new Scripts(Counter, Counter), [Map], Stock));
        Assert.ThrowsExactly<InvalidDataException>(() => OrdinaryMartCatalog.Bind(new Scripts(Counter with {
            Source = Counter.Source with { Symbol = "AnotherActor" } }), [Map], Stock));
        Assert.ThrowsExactly<InvalidDataException>(() => OrdinaryMartCatalog.Bind(new Scripts(Counter with {
            Op = StoryOp.Unsupported }), [Map], Stock));
    }
}
