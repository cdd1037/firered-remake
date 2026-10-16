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
        var copy = table with { Slots = table.Slots.ToImmutableArray() };
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
    [TestMethod]
    public void SharedCatalogAcceptsNewDefinitionsAndIdempotentOverlapButRejectsConflicts()
    {
        var first = CinnabarWaterEncounterCatalog.SourceAdditions[0];
        var source = WaterEncounterCatalog.Authenticate([first]);
        var edited = first with { Map = new("map.additional-surf"), RawEncounterRate = 17 };
        var added = source.Add([edited]);
        Assert.AreSame(edited, added.Tables[edited.Map]);
        Assert.AreEqual(2, added.Tables.Count);
        var repeated = added.Add([first with { Slots = first.Slots.ToImmutableArray() }]);
        Assert.AreSame(first, repeated.Tables[first.Map]);
        Assert.Throws<ArgumentException>(() => added.Add([first with { RawEncounterRate = 17 }]));
        Assert.Throws<ArgumentException>(() => source.Add([edited, edited]));
        Assert.Throws<ArgumentException>(() => source.Add([edited with { RawEncounterRate = 0 }]));
        Assert.Throws<ArgumentException>(() => source.Add([edited with { Slots = edited.Slots.RemoveAt(4) }]));
        Assert.Throws<ArgumentException>(() => source.Add([edited with {
            Slots = edited.Slots.SetItem(0, edited.Slots[0] with { MinimumLevel = 101, MaximumLevel = 101 }) }]));
    }

    [TestMethod]
    public void BareWalkingProfilesRejectUnverifiedAbilityAndItemNames()
    {
        EarlyLandEncounterRules.ValidateProfile(new(LeadAbility: "ABILITY_STENCH", LeadHeldItem: "ITEM_CLEANSE_TAG"));
        Assert.Throws<NotSupportedException>(() => EarlyLandEncounterRules.ValidateProfile(new(LeadAbility: "ABILITY_NOT_REAL")));
        Assert.Throws<NotSupportedException>(() => EarlyLandEncounterRules.ValidateProfile(new(LeadHeldItem: "ITEM_NOT_REAL")));
    }
}
