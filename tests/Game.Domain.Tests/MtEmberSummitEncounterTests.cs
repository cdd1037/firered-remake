using Game.Domain.Encounters;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class MtEmberSummitEncounterTests
{
    [TestMethod]
    public void RegisteredSummitLandTablesUseOrdinaryTerrainAndExactOriginSlots()
    {
        foreach (var table in MtEmberSummitEncounterTables.Land)
        {
            EarlyLandEncounterRules.ValidateTable(table);
            var definition = OrdinaryLandEncounterCatalogTests.Bind(table, 8);
            var state = new EarlyLandEncounterState(new(0), 8, StepsSinceLastEncounter: 21,
                RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
            var result = EarlyLandEncounterRules.Check(state, definition,
                new(table.Map, EncounterCheckTrigger.WalkingStepCompleted, 8, EncounterTileType.Land),
                new(RulesVersion: state.RulesVersion), new(0, 19));
            Assert.AreEqual(EncounterCheckOutcome.CandidateReady, result.Outcome);
            var candidate = result.Candidate!;
            var slot = SourceTableWildBattleContract.SourceSlot(new(table.Map, SourceTableEncounterKind.Land, candidate.Slot))!;
            Assert.AreEqual(candidate.Species, slot.Species);
            Assert.IsTrue(candidate.Level >= slot.MinimumLevel && candidate.Level <= slot.MaximumLevel);
            Assert.IsNull(EarlyLandEncounterRules.CompleteCandidate(result.State, table, definition).Pending);
            Assert.IsNull(SourceTableWildBattleContract.SourceSlot(new(table.Map, SourceTableEncounterKind.Land, 12)));
        }
    }

    [TestMethod]
    public void SourceReceiptTablesRetainExactSlotsWhenBoundToOrdinaryTerrain()
    {
        foreach (var table in SourceTableEncounterRegistry.Land)
        {
            Assert.AreSame(table, OrdinaryLandEncounterCatalogTests.Bind(table, 8).Table);
            Assert.Throws<ArgumentException>(() => OrdinaryLandEncounterCatalogTests.Bind(
                table with { RawEncounterRate = table.RawEncounterRate + 1 }, 8));
            Assert.Throws<ArgumentException>(() => OrdinaryLandEncounterCatalogTests.Bind(
                table with { Slots = table.Slots.SetItem(0, table.Slots[0] with { MinimumLevel = 1 }) }, 8));
        }
    }
}
