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
        EarlyLandEncounterRules.ValidateTable(table with { RawEncounterRate = 1 });
        EarlyLandEncounterRules.ValidateTable(table with { RawEncounterRate = 255 });
        var safari = SafariLandEncounterTables.All[0];
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateTable(safari with { RawEncounterRate = safari.RawEncounterRate + 1 }));
        Assert.Throws<NotSupportedException>(() => EarlyLandEncounterRules.ValidateTable(table with { Map = new("map.unregistered") }));
        EarlyLandEncounterRules.ValidateTable(table with { RawEncounterRate = 22,
            Slots = table.Slots.SetItem(0, table.Slots[0] with { Species = new("pret:SPECIES_PIDGEY"), MinimumLevel = 5, MaximumLevel = 30 }) });
        Action<EarlyLandEncounterTable>[] invalid = [
            t => EarlyLandEncounterRules.ValidateTable(t with { RawEncounterRate = 0 }),
            t => EarlyLandEncounterRules.ValidateTable(t with { RawEncounterRate = 256 }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = default }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.RemoveAt(0) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.Add(t.Slots[0]) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, null!) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, t.Slots[0] with { Index = 1 }) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, t.Slots[0] with { Weight = 19 }) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, t.Slots[0] with { Species = default }) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, t.Slots[0] with { MinimumLevel = 0 }) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, t.Slots[0] with { MaximumLevel = 101 }) }),
            t => EarlyLandEncounterRules.ValidateTable(t with { Slots = t.Slots.SetItem(0, t.Slots[0] with { MinimumLevel = 4 }) })
        ];
        foreach (var reject in invalid) Assert.Throws<ArgumentException>(() => reject(table));
        CollectionAssert.AreEqual(new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 },
            Enumerable.Range(0, 100).GroupBy(EarlyLandEncounterRules.ChooseSlot).Select(g => g.Count()).ToArray());
    }

    [TestMethod]
    public void SuppliedTableControlsPendingAndKeepsIndependentRngVector()
    {
        var table = Route11LandEncounterTables.Route11 with { RawEncounterRate = 255,
            Slots = Route11LandEncounterTables.Route11.Slots.Select(s => s with {
                Species = new("pret:SPECIES_PIDGEY"), MinimumLevel = 5, MaximumLevel = 30 }).ToImmutableArray() };
        var state = new EarlyLandEncounterState(new(0), EarlyLandEncounterRules.TallGrassBehavior,
            RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var check = new EarlyLandEncounterCheck(table.Map, EncounterCheckTrigger.TurnInPlaceCompleted,
            EarlyLandEncounterRules.TallGrassBehavior, EncounterTileType.Land);
        var profile = new EarlyLandEncounterProfile(RulesVersion: state.RulesVersion);
        var result = EarlyLandEncounterRules.Check(state, table, check, profile, new(0, 19));
        // Independent Gen III LCG: global draws 0 then 59774; 59774 % 26 == 0.
        Assert.AreEqual(0, result.Candidate!.Slot);
        Assert.AreEqual((byte)5, result.Candidate.Level);
        Assert.AreEqual(new RngState(3917380458u, 21), result.GlobalRng);
        CollectionAssert.AreEqual(new[] { "encounter.rate", "encounter.land-slot", "encounter.level" },
            result.RandomTrace.Select(t => t.Draw.Purpose).ToArray());
        var original = EarlyLandEncounterRules.Check(state with { EncounterRateBuff = ushort.MaxValue, StepsSinceLastEncounter = 21 },
            Route11LandEncounterTables.Route11, check, profile, new(0, 19));
        Assert.AreEqual(new EarlyLandEncounterCandidate(table.Map, 0, new("pret:SPECIES_EKANS"), 14), original.Candidate);
        Assert.AreEqual(result.GlobalRng, original.GlobalRng);
        CollectionAssert.AreEqual(result.RandomTrace.Select(t => t.Draw.Purpose).ToArray(), original.RandomTrace.Select(t => t.Draw.Purpose).ToArray());
        EarlyLandEncounterRules.ValidateState(result.State);
        Assert.IsNull(EarlyLandEncounterRules.CompleteCandidate(result.State, table).Pending);
        Assert.Throws<InvalidOperationException>(() => EarlyLandEncounterRules.Check(result.State, table, check, profile, result.GlobalRng));
        Assert.Throws<InvalidOperationException>(() => EarlyLandEncounterRules.ResetRateModifiers(result.State));
        Assert.Throws<InvalidOperationException>(() => EarlyLandEncounterRules.CompleteCandidate(state, table));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, table, check with { Map = new("map.route-22") }, profile, new(0)));
        foreach (var candidate in new[] {
            result.Candidate with { Map = new("map.route-22") }, result.Candidate with { Slot = 12 },
            result.Candidate with { Species = new("pret:SPECIES_RATTATA") }, result.Candidate with { Level = 31 } })
            Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateState(result.State with { Pending = new(candidate, 2) }, table));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateState(result.State with { Pending = result.State.Pending! with { MetatileBehavior = 0 } }, table));
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
