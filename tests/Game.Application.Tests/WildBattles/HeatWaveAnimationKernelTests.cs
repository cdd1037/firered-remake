using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class HeatWaveAnimationKernelTests
{
    private static int TargetOf(SourceBattleEvent row) => row switch {
        SourceHpChangedEvent hp => hp.Target.Value, SourceSubstituteDamagedEvent substitute => substitute.Target.Value,
        _ => throw new ArgumentException("Expected committed damage receipt.", nameof(row)) };

    [TestMethod]
    public void HeatWaveUsesOneRootForBothOrderedTargetPassesAtEveryActorAndBothSinglesSides()
    {
        var definition = Mechanics.Catalog.Moves[Move(257)]; Assert.AreEqual("EFFECT_BURN_HIT", definition.Effect);
        Assert.AreEqual("MOVE_TARGET_BOTH", definition.Target); Assert.AreEqual(10, definition.SecondaryEffectChance);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 3 })
        {
            var result = Ordered(Frame(Bodies(257, actor), single), actor, target);
            var hits = Hits(result.Events); var commands = result.Events.OfType<SourcePpCommandEvent>().ToArray();
            CollectionAssert.AreEqual(single ? new[] { target } : new[] { target, target ^ 2 }, hits.Select(TargetOf).ToArray());
            CollectionAssert.AreEqual(hits.Select(TargetOf).ToArray(), commands.Select(p => p.Target.Value).ToArray());
            Assert.AreEqual(single ? 1 : 2, commands.Length);
            if (!single) { Assert.AreEqual(commands[0].After, commands[1].Before); Assert.AreEqual(commands[1].Before, commands[1].After); }
            Assert.IsTrue(hits.All(h => commands.Single(p => p.Target.Value == TargetOf(h)).Sequence < h.Sequence));
            Check(result, 257, single, hits[0]);
        }
    }

    [TestMethod, DataRow("miss"), DataRow("protect"), DataRow("flash-fire")]
    public void APreventedFirstTargetLeavesTheSecondAuthenticPassAsTheSingleVisualOwner(string branch)
    {
        for (int actor = 0; actor < 4; actor++) foreach (int target in new[] { actor ^ 1, actor ^ 3 })
        {
            var bodies = Bodies(257, actor);
            if (branch == "miss")
            {
                bodies[actor] = bodies[actor] with { AccuracyStage = 6 };
                bodies[target] = bodies[target] with { EvasionStage = 12 };
            }
            else if (branch == "protect") bodies[target] = bodies[target] with { Protected = true };
            else bodies[target] = Mon("VULPIX", "EMBER", 100);
            var frame = Frame(bodies);
            var result = branch == "miss" ? Find(frame, actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(e => e.Target.Value == target) &&
                    Hits(r.Events).Any(h => TargetOf(h) == (target ^ 2)), "Heat Wave first miss and second hit") : Ordered(frame, actor, target);
            if (branch == "protect") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Target.Value == target && e.Reason == "protected"));
            if (branch == "flash-fire") Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Target.Value == target && e.Ability == "ABILITY_FLASH_FIRE"));
            var owner = Hits(result.Events).Single(h => TargetOf(h) == (target ^ 2));
            Check(result, 257, false, owner);
        }
    }

    [TestMethod, DataRow("substitute"), DataRow("ko"), DataRow("endure")]
    public void FirstSubstituteKoOrZeroLossEndureRemainsEligibleBeforeThePartnerHit(string branch)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(257, actor);
            bodies[target] = branch == "substitute" ? bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } }
                : bodies[target] with { Enduring = branch == "endure", Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var result = Ordered(Frame(bodies), actor, target); var hits = Hits(result.Events);
            CollectionAssert.AreEqual(new[] { target, target ^ 2 }, hits.Select(TargetOf).ToArray());
            if (branch == "substitute") Assert.AreEqual(0, result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
            else
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single(h => h.Target.Value == target);
                Assert.AreEqual(branch == "ko" ? 0 : 1, hp.HpAfter);
                if (branch == "ko") Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == target));
                else { Assert.AreEqual(1, hp.HpBefore); Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Any(e => e.Target.Value == target && e.Endured && e.DamageAfter == 0)); }
            }
            Check(result, 257, false, hits[0]);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("flash-fire"), DataRow("miss")]
    public void AllPreventedTargetsNeverCreateAnyHeatWaveRoot(string prevention)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(257);
            foreach (int target in single ? new[] { 1 } : new[] { 1, 3 })
                bodies[target] = prevention switch { "protect" => bodies[target] with { Protected = true },
                    "flash-fire" => Mon("VULPIX", "EMBER", 100), _ => bodies[target] with { EvasionStage = 12 } };
            if (prevention == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Frame(bodies, single);
            var result = prevention == "miss" ? Find(frame, 0, 1,
                r => r.Events.OfType<SourceMoveMissedEvent>().Count() == (single ? 1 : 2), "Heat Wave all miss") : Ordered(frame);
            Check(result, 257, single, null);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void SecondaryBurnChanceNeverOwnsOrGatesTheHeatWaveRoot(bool burns)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Find(Frame(Bodies(257), single), 0, 1,
                r => r.Events.OfType<SourceStatusChangedEvent>().Any(e => e.After == WildBattleStatus.Burn) == burns, "Heat Wave burn=" + burns);
            var hits = Hits(result.Events); Check(result, 257, single, hits[0]);
            foreach (var status in result.Events.OfType<SourceStatusChangedEvent>().Where(e => e.After == WildBattleStatus.Burn))
                Assert.IsTrue(hits.Any(h => TargetOf(h) == status.Side.Value && h.Sequence < status.Sequence));
        }
    }

    [TestMethod, DataRow("water-veil"), DataRow("fire-type"), DataRow("safeguard"), DataRow("existing-status")]
    public void APreventedBurnStillLeavesTheCommittedHeatWaveHitEligible(string prevention)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(257);
            foreach (int target in single ? new[] { 1 } : new[] { 1, 3 }) bodies[target] = prevention switch {
                "water-veil" => Mon("WAILORD", "SPLASH", 100), "fire-type" => Mon("MAGMAR", "EMBER", 100),
                "existing-status" => bodies[target] with { Status = WildBattleStatus.Paralysis }, _ => bodies[target] };
            var frame = Frame(bodies, single);
            if (prevention == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = Find(frame, 0, 1, r => r.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 10),
                "Heat Wave qualifying but prevented burn");
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(e => e.After == WildBattleStatus.Burn));
            Check(result, 257, single, Hits(result.Events)[0]);
        }
    }

    [TestMethod]
    public void SpreadPressureCommandsSpendOnceAndMalformedRevisitsFailClosed()
    {
        var bodies = Bodies(257); bodies[1] = Mon("ARTICUNO", "MIST", 100); bodies[3] = Mon("DUSCLOPS", "LEER", 100);
        var result = Ordered(Frame(bodies)); var events = result.Events; var pp = events.OfType<SourcePpCommandEvent>().ToArray();
        Assert.AreEqual(2, pp.Length); Assert.AreEqual(3, pp[0].CommandCost); Assert.AreEqual(3, pp[1].CommandCost);
        Assert.AreEqual(pp[0].Before - 3, pp[0].After); Assert.AreEqual(pp[0].After, pp[1].Before); Assert.AreEqual(pp[1].Before, pp[1].After);
        Check(result, 257, false, Hits(events)[0]); int at = events.IndexOf(pp[1]);
        foreach (var invalid in new[] { pp[1] with { PpOwner = new(1) }, pp[1] with { Target = new(1) },
            pp[1] with { Before = pp[0].Before }, pp[1] with { After = pp[1].After - 1 }, pp[1] with { ChosenMove = Id("METRONOME") } })
            Deny(events.SetItem(at, invalid), false);
        Deny(events.RemoveAt(at), false);
        var first = Hits(events)[0]; int hitAt = events.IndexOf(first);
        Deny(events.Insert(hitAt + 1, first with { Sequence = first.Sequence + 1 }), false);
    }
}
