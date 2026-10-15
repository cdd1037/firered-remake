using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Randomness;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class EncounterDefinitionTests
{
    [TestMethod]
    public void LandTableAdmissionKeepsMapRateSpeciesLevelAndProbabilityConstraints()
    {
        var table = EarlyLandEncounterRules.Route22Table;
        EarlyLandEncounterRules.ValidateTable(new(table.Map, table.RawEncounterRate, table.Slots.ToImmutableArray()));
        Assert.Throws<NotSupportedException>(() => EarlyLandEncounterRules.ValidateTable(table with { Map = new("map.unregistered") }));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateTable(table with { RawEncounterRate = 22 }));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateTable(table with
        {
            Slots = table.Slots.SetItem(0, table.Slots[0] with { Species = new("pret:SPECIES_MEW") })
        }));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateTable(table with
        {
            Slots = table.Slots.SetItem(0, table.Slots[0] with { MinimumLevel = 4 })
        }));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateTable(table with
        {
            Slots = table.Slots.SetItem(0, table.Slots[0] with { Weight = 19 })
        }));
        CollectionAssert.AreEqual(new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 },
            Enumerable.Range(0, 100).GroupBy(EarlyLandEncounterRules.ChooseSlot).Select(g => g.Count()).ToArray());
    }

    [TestMethod]
    public void RodsKeepDistinctSlotRangesAndWeights()
    {
        CollectionAssert.AreEqual(new[] { 70, 30 }, Distribution(OldRodEncounterRules.ChooseSlot));
        CollectionAssert.AreEqual(new[] { 60, 20, 20 }, Distribution(GoodRodEncounterRules.ChooseSourceSlot));
        CollectionAssert.AreEqual(new[] { 40, 40, 15, 4, 1 }, Distribution(SuperRodEncounterRules.ChooseSourceSlot));
        Assert.AreEqual(0, OldRodEncounterRules.ChooseSlot(0));
        Assert.AreEqual(1, OldRodEncounterRules.ChooseSlot(99));
        Assert.AreEqual(2, GoodRodEncounterRules.ChooseSourceSlot(0));
        Assert.AreEqual(4, GoodRodEncounterRules.ChooseSourceSlot(99));
        Assert.AreEqual(5, SuperRodEncounterRules.ChooseSourceSlot(0));
        Assert.AreEqual(9, SuperRodEncounterRules.ChooseSourceSlot(99));
        var old = OldRodEncounterRules.Tables[0];
        OldRodEncounterRules.ValidateTable(new(old.Map, old.RawEncounterRate, old.Slots.ToImmutableArray()));
        Assert.Throws<ArgumentException>(() => OldRodEncounterRules.ValidateTable(old with { Map = new("map.not-admitted") }));
        Assert.Throws<ArgumentException>(() => GoodRodEncounterRules.ValidateTable(old));
        Assert.Throws<ArgumentException>(() => SuperRodEncounterRules.ValidateTable(old));
        Assert.Throws<ArgumentException>(() => OldRodEncounterRules.ValidateCandidate(new(old.Map, 2, old.Slots[0].Species, 5)));
        Assert.Throws<ArgumentException>(() => OldRodEncounterRules.ValidateCandidate(new(old.Map, 0, old.Slots[0].Species, 11)));
        foreach (var choose in new Func<int, int>[] { OldRodEncounterRules.ChooseSlot, GoodRodEncounterRules.ChooseSourceSlot, SuperRodEncounterRules.ChooseSourceSlot })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => choose(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => choose(100));
        }
    }

    // Independently calculated from the Gen III LCG (0x41c64e6d * seed + 0x6073),
    // upper 16-bit slot modulo 100 and inclusive Pallet Town level modulo 6.
    [TestMethod]
    [DataRow(0u, 0, (byte)7, 3917380458u)]
    [DataRow(1u, 0, (byte)6, 2887849427u)]
    [DataRow(uint.MaxValue, 1, (byte)10, 651944193u)]
    [DataRow(0x12345678u, 0, (byte)5, 2229936802u)]
    public void OldRodCandidateKeepsTwoDrawOrderAndExactRng(uint seed, int slot, byte level, uint finalSeed)
    {
        var table = OldRodEncounterRules.Tables.Single(t => t.Map.Value == "map.pallet-town");
        var ready = new OldRodFishingState(table.Map, true, SourceStep: 10, FrameCounter: 1,
            NumDots: 4, DotsRequired: 4, RoundsPlayed: 1, MinimumRoundsRequired: 1,
            Status: OldRodFishingStatus.EncounterReady);
        var result = OldRodEncounterRules.GenerateCandidate(table, ready, new(seed, 19));
        Assert.AreEqual(new OldRodEncounterCandidate(table.Map, slot, new("pret:SPECIES_MAGIKARP"), level), result.Candidate);
        Assert.AreEqual(new RngState(finalSeed, 21), result.GlobalRng);
        CollectionAssert.AreEqual(new[] { "fishing.slot", "fishing.level" }, result.RandomTrace.Select(t => t.Purpose).ToArray());
        CollectionAssert.AreEqual(new[] { 100, 6 }, result.RandomTrace.Select(t => t.Bound).ToArray());
        Assert.Throws<InvalidOperationException>(() => OldRodEncounterRules.GenerateCandidate(table,
            new(table.Map, true), new(seed)));
        Assert.Throws<NotSupportedException>(() => OldRodEncounterRules.GenerateCandidate(table,
            ready with { Rod = FishingRod.Good }, new(seed)));
    }

    private static int[] Distribution(Func<int, int> choose) =>
        Enumerable.Range(0, 100).GroupBy(choose).Select(g => g.Count()).ToArray();
}
