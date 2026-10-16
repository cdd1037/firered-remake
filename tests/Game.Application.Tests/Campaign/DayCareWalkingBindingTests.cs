using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class DayCareWalkingBindingTests
{
    [TestMethod]
    public void WalkingReusesBoundBreedingRowsAndPreservesCounterAndRngSemantics()
    {
        var c = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(c);
        var baseline = CurrentCampaignFixture.Checkpoint.State with { Party = [], Route5DayCare = null,
            FourIslandDayCare = FourIslandDayCareState.Empty };
        var walk = typeof(CampaignSession).GetMethod("ApplyDayCareWalkingStep", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<CampaignState, CampaignState>>(session);
        var species = c.BreedingMetadata!.Species.ToImmutableDictionary(p => p.Key, p => p.Value.Breeding);
        var actual = baseline;
        var expected = FourIslandDayCareState.Empty;
        var rng = baseline.World.Rng;
        for (int i = 0; i < 512; i++)
        {
            var step = FourIslandDayCareRules.AdvanceEligibleStep(expected, species, rng);
            expected = step.State;
            rng = step.Rng;
            actual = walk(actual);
            Assert.AreEqual(expected, actual.FourIslandDayCare);
            Assert.AreEqual(rng, actual.World.Rng);
        }
        // Warm the call before measuring managed allocations. The budget allows state/result
        // records and hatch-cycle work, but not a full species dictionary on every step.
        for (int i = 0; i < 256; i++) actual = walk(actual);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 512; i++) actual = walk(actual);
        long bytesPerStep = (GC.GetAllocatedBytesForCurrentThread() - before) / 512;
        Assert.IsTrue(bytesPerStep < 4096, $"Empty day-care walking allocated {bytesPerStep} bytes/step.");
        Assert.AreEqual(baseline.World.Rng, actual.World.Rng);
    }
}
