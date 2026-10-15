using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class WaterEncounterAdmissionTests
{
    [TestMethod]
    public void MapAdmissionAcceptsEquivalentValuesAndRejectsChangedProbabilityOrSpecies()
    {
        var table = CinnabarWaterEncounterCatalog.SourceAdditions[0];
        var tables = ImmutableDictionary<MapId, WaterEncounterTable>.Empty.Add(table.Map, table);
        var copy = table with { Slots = table.Slots.ToImmutableArray(), SourcePath = "irrelevant-build-path" };
        WaterEncounterCatalog.ValidateAdmission(tables, copy);
        Assert.IsTrue(CinnabarWaterEncounterCatalog.MatchesDefinition(copy));
        Assert.Throws<ArgumentException>(() => WaterEncounterCatalog.ValidateAdmission(tables, table with { RawEncounterRate = table.RawEncounterRate + 1 }));
        Assert.Throws<ArgumentException>(() => WaterEncounterCatalog.ValidateAdmission(tables,
            table with { Slots = table.Slots.SetItem(0, table.Slots[0] with { Species = new("pret:SPECIES_MEW") }) }));
        Assert.Throws<ArgumentException>(() => WaterEncounterCatalog.ValidateAdmission(tables, table with { Map = new("map.not-admitted") }));
    }

    [TestMethod]
    public void FiveSlotProbabilityAndPendingBoundsRemainExact()
    {
        var table = CinnabarWaterEncounterCatalog.SourceAdditions[0];
        Assert.IsTrue(WaterEncounterCatalog.IsValidDefinition(table));
        Assert.IsFalse(WaterEncounterCatalog.IsValidDefinition(table with { Slots = table.Slots.SetItem(0, table.Slots[0] with { Weight = 59 }) }));
        var observed = Enumerable.Range(0, 100).GroupBy(WaterEncounterRules.ChooseSlot).Select(g => g.Count()).ToArray();
        CollectionAssert.AreEqual(new[] { 60, 30, 5, 4, 1 }, observed);
        var slot = table.Slots[0];
        var state = new WaterEncounterState(new(1), Pending: new(new(table.Map, 0, slot.Species, slot.MinimumLevel), 0));
        WaterEncounterRules.ValidateState(state, table);
        Assert.Throws<ArgumentException>(() => WaterEncounterRules.ValidateState(state with
        {
            Pending = new(new(table.Map, 0, slot.Species, checked((byte)(slot.MaximumLevel + 1))), 0)
        }, table));
    }
}
