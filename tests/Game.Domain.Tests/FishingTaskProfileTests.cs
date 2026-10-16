using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class FishingTaskProfileTests
{
    private static readonly MapId Map = new("map.generated-fishing");
    private static readonly MapId Empty = new("map.empty-fishing");
    private static readonly SpeciesId Fish = new("pret:SPECIES_MAGIKARP");
    private static OldRodEncounterTable Good(byte minimum = 5, byte maximum = 15) => new(Map, 20,
        [new(2, Fish, minimum, maximum, 60), new(3, Fish, minimum, maximum, 20), new(4, Fish, minimum, maximum, 20)]);
    private static FishingTaskProfile Profile(OldRodEncounterTable? table = null) =>
        FishingTaskProfile.ForRod("test-fishing-profile", FishingRod.Good, [table ?? Good()], [Empty]);
    private static OldRodFishingState Ready(FishingTaskProfile profile) => new(Map, true, SourceStep: 10,
        FrameCounter: 1, NumDots: 4, DotsRequired: 4, RoundsPlayed: 1, MinimumRoundsRequired: 1,
        Status: OldRodFishingStatus.EncounterReady, RulesVersion: profile.RulesVersion) { Rod = FishingRod.Good };

    [TestMethod]
    public void SuppliedDefinitionControlsGenerationAndReceiptWithoutStaticMirror()
    {
        var profile = Profile(Good(17, 17));
        var result = profile.GenerateCandidate(Ready(profile), new(0, 4));
        Assert.AreEqual(new OldRodEncounterCandidate(Map, 2, Fish, 17), result.Candidate);
        Assert.AreEqual((ulong)6, result.GlobalRng.Cursor);
        CollectionAssert.AreEqual(new[] { "fishing.slot", "fishing.level" }, result.RandomTrace.Select(t => t.Purpose).ToArray());
        CollectionAssert.AreEqual(new[] { 100, 1 }, result.RandomTrace.Select(t => t.Bound).ToArray());
        var origin = new FishingEncounterOrigin(FishingRod.Good, Map, 2, Fish, 17);
        Assert.IsNotNull(FishingBattleContract.SourceSlot(origin, profile));
        Assert.IsNull(FishingBattleContract.SourceSlot(origin), "Good Rod has no hidden static fallback");
        Assert.IsNull(Profile().SourceSlot(origin));
        Assert.IsNull(profile.SourceSlot(origin with { SlotIndex = 1 }));
        Assert.IsNull(profile.SourceSlot(origin with { Level = 16 }));
        Assert.IsNull(profile.SourceSlot(origin with { RulesVersion = "unknown" }));
    }

    [TestMethod]
    public void EmptyMapCompletesWithoutBiteDrawAndUnsupportedMapCannotStart()
    {
        var profile = Profile(); var rng = new RngState(123, 8);
        var context = new OldRodCastContext(Empty, false, 3, 2, 0x10, 3) { Rod = FishingRod.Good, OwnsGoodRod = true };
        var cast = profile.Start(context, rng);
        Assert.IsFalse(cast.State.HasFishingMons); Assert.AreEqual(rng, cast.GlobalRng);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        for (int i = 0; i < 600 && cast.State.Status == OldRodFishingStatus.Running; i++)
        {
            cast = profile.Advance(cast.State, new(MessageFinished: true, NoCatchAnimationFinished: true), cast.GlobalRng);
            trace.AddRange(cast.RandomTrace);
        }
        Assert.AreEqual(OldRodFishingStatus.NotEvenANibble, cast.State.Status);
        CollectionAssert.AreEqual(new[] { "fishing.minimum-rounds", "fishing.dots" }, trace.Select(t => t.Purpose).ToArray());
        Assert.AreEqual(rng.Cursor + 2, cast.GlobalRng.Cursor);
        Assert.Throws<NotSupportedException>(() => profile.Start(context with { Map = new("map.unloaded") }, rng));
        Assert.Throws<ArgumentException>(() => profile.ValidateState(Ready(profile) with { HasFishingMons = false }));
    }

    [TestMethod]
    public void DefinitionsRejectBadSlotsAndConflictingAdmission()
    {
        var row = Good();
        Assert.Throws<ArgumentException>(() => Profile(row with { RawEncounterRate = 0 }));
        Assert.Throws<ArgumentException>(() => Profile(row with { Slots = row.Slots.SetItem(0, row.Slots[0] with { Index = 0 }) }));
        Assert.Throws<ArgumentException>(() => Profile(row with { Slots = row.Slots.SetItem(0, row.Slots[0] with { Weight = 61 }) }));
        Assert.Throws<ArgumentException>(() => Profile(row with { Slots = row.Slots.SetItem(0, row.Slots[0] with { MinimumLevel = 0 }) }));
        Assert.Throws<ArgumentException>(() => new FishingTaskProfile("test", [new(FishingRod.Good, row), new(FishingRod.Good, row)], []));
        Assert.Throws<ArgumentException>(() => new FishingTaskProfile("test", [new(FishingRod.Good, row)], [new(FishingRod.Good, Map)]));
    }

    [TestMethod]
    public void HeldPairCoverageAndProbabilityAreSharedAndSpeciesDriven()
    {
        var table = Good();
        var profile = FishingTaskProfile.ForRod("test", FishingRod.Good, [table], [],
            [new(Fish, "ITEM_PEARL", "ITEM_BIG_PEARL")]);
        var origin = new FishingEncounterOrigin(FishingRod.Good, Map, 2, Fish, 5);
        Assert.AreEqual("ITEM_NONE", profile.HeldItem(origin, 44));
        Assert.AreEqual("ITEM_PEARL", profile.HeldItem(origin, 45));
        Assert.AreEqual("ITEM_PEARL", profile.HeldItem(origin, 94));
        Assert.AreEqual("ITEM_BIG_PEARL", profile.HeldItem(origin, 95));
        Assert.Throws<ArgumentException>(() => profile.HeldItem(origin, 100));
        Assert.Throws<ArgumentException>(() => Profile().HeldItem(origin, 50));
        Assert.Throws<ArgumentException>(() => FishingTaskProfile.ForRod("test", FishingRod.Good, [table], [], []));
    }

    [TestMethod]
    public void LegacyAdaptersPreserveTheirVersionsAndEmptyMapAdmission()
    {
        var original = OldRodEncounterRules.TaskProfile;
        var current = SevenIslandOldRodEncounterRules.TaskProfile;
        Assert.AreEqual(OldRodFishingRules.RulesVersion, original.RulesVersion);
        Assert.AreEqual(SevenIslandOldRodEncounterRules.RulesVersion, current.RulesVersion);
        foreach (var empty in original.EmptyMaps) Assert.IsTrue(current.IsAdmittedMap(empty.Rod, empty.Map));
        foreach (var table in current.Tables.Values) Assert.IsTrue(current.HasTable(FishingRod.Old, table.Map));
        Assert.IsTrue(SuperRodEncounterRules.TaskProfile.SupportsRod(FishingRod.Super));
        Assert.IsFalse(SuperRodEncounterRules.TaskProfile.SupportsRod(FishingRod.Good));
    }
}
