using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class SafariFishingTests
{
    private static readonly MapId Map = new("map.safari-zone-center");
    private static readonly SpeciesId Fish = new("pret:SPECIES_MAGIKARP");
    private static SafariFishingEncounterCatalog Catalog(string common = "ITEM_NONE", string rare = "ITEM_NONE") => new([
        new(FishingRod.Old, new(Map, 20, [new(0, Fish, 5, 5, 70), new(1, Fish, 5, 5, 30)])),
        new(FishingRod.Good, new(Map, 20, [new(2, Fish, 5, 15, 60), new(3, Fish, 5, 15, 20), new(4, Fish, 5, 15, 20)])),
        new(FishingRod.Super, new(Map, 20, [new(5, Fish, 15, 25, 40), new(6, Fish, 20, 30, 40), new(7, Fish, 15, 25, 15), new(8, Fish, 15, 35, 4), new(9, Fish, 25, 35, 1)]))
    ], [new(Fish, common, rare)]);
    private static OldRodCastContext Context(FishingRod rod) => new(Map, rod == FishingRod.Old, 3, 2, 0x10, 3)
        { Rod = rod, OwnsGoodRod = rod == FishingRod.Good, OwnsSuperRod = rod == FishingRod.Super };

    [TestMethod]
    [DataRow(FishingRod.Old)]
    [DataRow(FishingRod.Good)]
    [DataRow(FishingRod.Super)]
    public void SameFrameTaskPreservesDrawOrderAndNeverDrawsOnRejectedCast(FishingRod rod)
    {
        var catalog = Catalog(); var rng = new RngState(123);
        var rejected = catalog.Start(Context(rod) with { PlayerElevation = 1 }, rng);
        Assert.AreEqual(OldRodFishingStatus.Rejected, rejected.State.Status);
        Assert.AreEqual(rng, rejected.GlobalRng); Assert.IsTrue(rejected.RandomTrace.IsEmpty);
        var cancelled = catalog.Start(Context(rod), rng, true);
        Assert.AreEqual(OldRodFishingStatus.Cancelled, cancelled.State.Status);
        Assert.AreEqual(rng, cancelled.GlobalRng);
        var state = catalog.Start(Context(rod), rng);
        Assert.AreEqual(1, state.State.SourceStep); Assert.AreEqual(rng, state.GlobalRng);
        var first = catalog.Advance(state.State, new(), state.GlobalRng);
        Assert.AreEqual(1, first.RandomTrace.Length);
        Assert.AreEqual("fishing.minimum-rounds", first.RandomTrace[0].Purpose);
        Assert.AreEqual(rng.Cursor + 1, first.GlobalRng.Cursor);
        for (int i = 0; i < 60; i++) first = catalog.Advance(first.State, new(), first.GlobalRng);
        Assert.AreEqual(3, first.State.SourceStep);
        Assert.AreEqual(rng.Cursor + 1, first.GlobalRng.Cursor);
        first = catalog.Advance(first.State, new(), first.GlobalRng);
        Assert.AreEqual("fishing.dots", first.RandomTrace.Single().Purpose);
    }

    [TestMethod]
    public void GeneratedDefinitionsRejectMissingRodAndMalformedSlots()
    {
        var catalog = Catalog();
        var rows = catalog.Tables.Select(p => new SafariFishingTable(p.Key.Rod, p.Value)).ToImmutableArray();
        Assert.Throws<ArgumentException>(() => new SafariFishingEncounterCatalog(rows.RemoveAt(0), [new(Fish, "ITEM_NONE", "ITEM_NONE")]));
        var bad = rows.SetItem(0, rows[0] with { Table = rows[0].Table with { Slots = rows[0].Table.Slots.SetItem(0, rows[0].Table.Slots[0] with { Weight = 99 }) } });
        Assert.Throws<ArgumentException>(() => new SafariFishingEncounterCatalog(bad, [new(Fish, "ITEM_NONE", "ITEM_NONE")]));
        Assert.Throws<ArgumentException>(() => new SafariFishingEncounterCatalog(rows, [new(new("pret:SPECIES_PIKACHU"), "ITEM_NONE", "ITEM_NONE")]));
    }

    [TestMethod]
    public void SourceSlotPreservesRodMapIndexSpeciesAndLevel()
    {
        var catalog = Catalog(); var origin = new FishingEncounterOrigin(FishingRod.Super, Map, 9, Fish, 25);
        Assert.IsNotNull(catalog.SourceSlot(origin));
        Assert.IsNull(catalog.SourceSlot(origin with { Rod = FishingRod.Old }));
        Assert.IsNull(catalog.SourceSlot(origin with { Level = 24 }));
        Assert.IsNull(catalog.SourceSlot(origin with { Map = new("map.fuchsia-city") }));
        Assert.IsNull(catalog.SourceSlot(origin with { Species = new("pret:SPECIES_PIKACHU") }));
    }

    [TestMethod]
    public void HeldItemDrawIsUnconditionalAndUsesSourceRareBoundary()
    {
        var origin = new FishingEncounterOrigin(FishingRod.Super, Map, 9, Fish, 25);
        foreach (int expected in new[] { 44, 45, 94, 95, 99 })
        {
            uint seed = Enumerable.Range(0, 100000).Select(i => (uint)i).First(i => Gen3Rng.Next(new(i), "oracle", 100).Trace.Result == expected);
            var rng = new RngState(seed); var catalog = Catalog("ITEM_PEARL", "ITEM_DRAGON_FANG");
            var held = catalog.AssignHeldItem(origin, rng);
            Assert.AreEqual(expected < 45 ? "ITEM_NONE" : expected < 95 ? "ITEM_PEARL" : "ITEM_DRAGON_FANG", held.HeldItem);
            Assert.AreEqual(rng.Cursor + 1, held.GlobalRng.Cursor);
            Assert.AreEqual("ITEM_NONE", Catalog().AssignHeldItem(origin, rng).HeldItem);
            Assert.AreEqual(rng.Cursor + 1, Catalog().AssignHeldItem(origin, rng).GlobalRng.Cursor);
        }
    }
}
