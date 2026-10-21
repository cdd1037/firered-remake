using System.Text.Json;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Legal = FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Producer/receipt-identity regression only. No animation-resource availability or root admission
// is asserted for these existing dynamic effects, particularly Low Kick and Eruption.
[TestClass, TestCategory("Game")]
public sealed class PreparedPowerReceiptRegressionTests
{
    private static int? Power(SourceBattleEvent e) => e switch {
        SourceHpChangedEvent h => h.PreparedBasePower, SourceSubstituteDamagedEvent s => s.PreparedBasePower,
        _ => throw new InvalidOperationException() };
    private static int WeightPower(int hectograms) => hectograms < 100 ? 20 : hectograms < 250 ? 40 : hectograms < 500 ? 60 :
        hectograms < 1000 ? 80 : hectograms < 2000 ? 100 : 120;
    private static void Check(WildBattleCombatant actor, WildBattleCombatant target, bool single, bool substitute, int expected)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
        bodies[0] = actor with { AccuracyStage = 12 }; bodies[1] = target;
        if (substitute) foreach (int slot in new[] { 1, 3 }) bodies[slot] = bodies[slot] with { CalledVolatile = new() { SubstituteHp = 100 } };
        var frame = Kernel.Frame(bodies, single); string before = JsonSerializer.Serialize(frame);
        var result = Envelope.Resolve(frame); var hits = Envelope.Hits(result.Events);
        bool spread = Kernel.Mechanics.Catalog.Moves[actor.Pokemon.Moves[0].Move].Target == "MOVE_TARGET_BOTH";
        int passes = !single && spread ? 2 : 1; Assert.AreEqual(passes, hits.Length);
        foreach (var hit in hits)
        {
            Assert.AreEqual(expected, Power(hit)); Assert.AreEqual(substitute, hit is SourceSubstituteDamagedEvent);
            if (single)
            {
                var exported = OverlayHitAnimationKernelFixture.Single([hit], 0).Events[0];
                Assert.AreEqual(Power(hit), exported switch { SourceHpChangedEvent h => h.PreparedBasePower,
                    SourceSubstituteDamagedEvent s => s.PreparedBasePower, _ => throw new InvalidOperationException() });
            }
        }
        // Pinned EffectHit order remains accuracy, critical, random damage, zero-chance
        // secondary per successful pass. Preparing/reading power adds no RNG draw.
        string[] suffixes = [".accuracy", ".critical", ".damage", ".secondary-effect"];
        int[] bounds = [100, 16, 16, 100]; Assert.AreEqual(passes * 4, result.Trace.Length);
        uint seed = frame.Rng.Seed; ulong cursor = frame.Rng.Cursor;
        for (int i = 0; i < result.Trace.Length; i++)
        {
            var trace = result.Trace[i]; Assert.IsTrue(trace.Purpose.EndsWith(suffixes[i % 4], StringComparison.Ordinal));
            Assert.AreEqual(bounds[i % 4], trace.Bound); Assert.AreEqual(seed, trace.Before);
            seed = unchecked(seed * 0x41C64E6Du + 0x6073u); ushort raw = (ushort)(seed >> 16);
            Assert.AreEqual(++cursor, trace.Cursor); Assert.AreEqual(seed, trace.After); Assert.AreEqual(raw, trace.Raw);
            Assert.AreEqual(raw % trace.Bound, trace.Result);
        }
        Assert.AreEqual(seed, result.State.Rng.Seed); Assert.AreEqual(cursor, result.State.Rng.Cursor);
        Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        Assert.AreEqual(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Envelope.Resolve(frame)), "The detached receipt field preserves repeatability.");
    }

    [TestMethod, DataRow(216), DataRow(218)]
    public void ReturnAndFrustrationExportTheActualEffectiveOverrideAtFriendshipBoundaries(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        foreach (int friendship in new[] { 0, 1, 2, 3, 30, 60, 61, 91, 92, 200, 201, 254, 255 })
        {
            var actor = Legal.MachineMon("RATICATE", id == 216 ? "TM27" : "TM21", id == 216 ? "RETURN" : "FRUSTRATION");
            actor = actor with { Pokemon = actor.Pokemon with { Friendship = friendship } };
            // Source command can yield zero; CalculateBaseDamage then uses metadata power1.
            int expected = Math.Max(1, 10 * (id == 216 ? friendship : 255 - friendship) / 25);
            Check(actor, Kernel.Mon("WAILORD", "SPLASH", 100), single, substitute, expected);
        }
    }

    [TestMethod]
    public void LowKickExportsAllSixSourceWeightBucketsWithoutChangingItsDrawOrder()
    {
        var targets = new[] { ("SQUIRTLE", "TACKLE"), ("GEODUDE", "TACKLE"), ("SEAKING", "PECK"),
            ("MACHOKE", "LOW_KICK"), ("GRAVELER", "TACKLE"), ("WAILORD", "SPLASH") };
        var observed = new HashSet<int>();
        foreach (var (species, move) in targets)
        {
            var target = Kernel.Mon(species, move, 100);
            int power = WeightPower(Kernel.Mechanics.Catalog.MechanicsData!.SpeciesMechanics[target.Pokemon.Species].WeightHectograms); observed.Add(power);
            foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
                Check(Kernel.Mon("MANKEY", "LOW_KICK"), target, single, substitute, power);
        }
        CollectionAssert.AreEquivalent(new[] { 20, 40, 60, 80, 100, 120 }, observed.ToArray());
    }

    [TestMethod]
    public void EruptionExportsFrozenHpScaledOverrideForBothRealSpreadPasses()
    {
        var natural = Kernel.Mon("CAMERUPT", "ERUPTION"); int maximum = natural.Pokemon.Stats.MaxHp;
        foreach (int hp in new[] { 1, maximum / 3, maximum / 2, maximum })
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var actor = natural with { Pokemon = natural.Pokemon with { CurrentHp = hp } };
            Check(actor, Kernel.Mon("WAILORD", "SPLASH", 100), single, substitute, Math.Max(1, hp * 150 / maximum));
        }
    }
}
