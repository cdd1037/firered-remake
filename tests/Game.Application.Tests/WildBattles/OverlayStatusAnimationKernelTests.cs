using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class OverlayStatusAnimationKernelTests
{
    [TestMethod, DataRow(184), DataRow(137), DataRow(114)]
    public void NaturalStatusRootsUseActualKernelResultsAtEverySlotAndBothSinglesSides(int id)
    {
        var definition = Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(Spec(id).Effect, definition.Effect);
        Assert.AreEqual(0, definition.Power);
        Assert.AreEqual(id == 114 ? "MOVE_TARGET_USER" : "MOVE_TARGET_SELECTED", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in id == 114 ? new[] { actor } : single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Resolve(Frame(Bodies(id, actor), single), actor, target);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());
            if (id == 137) Assert.AreEqual(WildBattleStatus.Paralysis, result.State.Slots[target].Body!.Status);
            if (id == 184) Assert.AreEqual(4, result.State.Slots[target].Body!.SpeedStage);
            Check(result, id, single);
        }
    }

    [TestMethod, DataRow(184), DataRow(137)]
    public void PrimaryStatusMissProtectAndSubstituteNeverAcquireARootOrWave(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (string branch in new[] { "miss", "protect", "substitute" })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            if (branch == "miss") bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            if (branch == "protect") bodies[target] = bodies[target] with { Protected = true };
            if (branch == "substitute") bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var frame = Frame(bodies, single);
            var result = branch == "miss" ? Find(frame, actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any() || r.Events.OfType<SourceMoveFailedEvent>().Any(f => f.Reason == "accuracy"), $"{Spec(id).Name} miss")
                : Resolve(frame, actor, target);
            Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
            Check(result, id, single, false);
        }
    }

    [TestMethod, DataRow("limber"), DataRow("existing-status"), DataRow("ghost"), DataRow("safeguard")]
    public void GlareAuthenticatesParalysisRatherThanAnyStatusReceipt(string branch)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(137, actor);
            bodies[target] = branch switch {
                "limber" => Mon("PERSIAN", "SCRATCH"),
                "ghost" => Mon("GASTLY", "LICK"),
                "existing-status" => bodies[target] with { Status = WildBattleStatus.Burn },
                _ => bodies[target]
            };
            if (branch == "limber") Assert.AreEqual("ABILITY_LIMBER", WildBattleEffective.Ability(bodies[target]));
            var frame = Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(target & 1,
                frame.Sides[target & 1] with { SafeguardTurnsRemaining = 5 }) };
            var result = Resolve(frame, actor, target);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(s => s.Reason == "move"));
            if (branch == "ghost") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "type-immunity"));
            Check(result, 137, single, false);
        }
    }

    [TestMethod]
    public void GlareParalyzesGroundAndElectricTypesUnderPinnedGenerationThreeRules()
    {
        foreach (string species in new[] { "DIGLETT", "PIKACHU" }) foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(137); bodies[1] = Mon(species, species == "DIGLETT" ? "SCRATCH" : "THUNDER_SHOCK");
            var result = Resolve(Frame(bodies, single));
            Assert.AreEqual(WildBattleStatus.Paralysis, result.Events.OfType<SourceStatusChangedEvent>().Single().After);
            Check(result, 137, single);
        }
    }

    [TestMethod, DataRow(0), DataRow(1), DataRow(2)]
    public void ScaryFaceCapAndClampingUseRequestedTwoStageSpeedWave(int speed)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(184, actor); bodies[target] = bodies[target] with { SpeedStage = speed };
            var result = Resolve(Frame(bodies, single), actor, target);
            Assert.AreEqual(0, result.State.Slots[target].Body!.SpeedStage);
            Assert.AreEqual(speed == 0 ? 1 : 0, result.Events.OfType<SourceStatAtMinimumEvent>().Count());
            Check(result, 184, single, speed > 0);
        }
    }

    [TestMethod, DataRow("clear-body"), DataRow("white-smoke"), DataRow("mist")]
    public void ScaryFacePreventionDoesNotForgeTheDedicatedRoot(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(184);
            if (branch != "mist") bodies[1] = Mon(branch == "clear-body" ? "TENTACOOL" : "TORKOAL", branch == "clear-body" ? "ACID" : "EMBER");
            var frame = Frame(bodies, single);
            if (branch == "mist") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { MistTurnsRemaining = 5 }) };
            var result = Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any()); Check(result, 184, single, false);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void HazeNeutralAndMixedResetsOwnOneMarkerRootAndNoIndividualStatWaves(bool changed)
    {
        // BattleScript_EffectHaze (battle_scripts_1.s:564) does attackanimation,
        // normalisebuffs and one STATCHANGESGONE message, with no generic stat wave.
        WildBattleStat[] order = [WildBattleStat.Attack, WildBattleStat.Defense, WildBattleStat.Speed,
            WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense, WildBattleStat.Accuracy, WildBattleStat.Evasion];
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(114, actor);
            if (changed) for (int slot = 0; slot < bodies.Length; slot++) bodies[slot] = bodies[slot] with {
                AttackStage = 12, DefenseStage = 0, SpeedStage = 6, SpecialAttackStageDelta = 1,
                SpecialDefenseStageDelta = -1, AccuracyStage = 6, EvasionStage = 12,
                Protected = slot != actor, CalledVolatile = new() { SubstituteHp = 1 }
            };
            var frame = Frame(bodies, single); var result = Resolve(frame, actor, actor);
            var events = result.Events; var pp = events.OfType<SourcePpCommandEvent>().Single();
            var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "haze");
            Assert.AreEqual(actor, marker.Side.Value); Assert.AreEqual(0, marker.Before); Assert.AreEqual(0, marker.After);
            Assert.AreEqual("all-stat-stages-reset", marker.Reason); Assert.AreEqual(pp.Turn, marker.Turn);
            var rows = events.OfType<SourceStatChangedEvent>().ToArray();
            Assert.AreEqual(changed ? (single ? 10 : 20) : 0, rows.Length);
            Assert.AreEqual(events.IndexOf(pp) + rows.Length + 1, events.IndexOf(marker));
            Assert.AreEqual(pp.Sequence + rows.Length + 1, marker.Sequence);
            int previous = -1;
            foreach (var row in rows)
            {
                Assert.AreEqual(actor, row.Actor.Value); Assert.IsTrue(row.Before is >= 0 and <= 12 && row.Before != 6);
                Assert.AreEqual(6, row.After); int key = row.Target.Value * 7 + Array.IndexOf(order, row.Stat);
                Assert.IsTrue(key > previous, "Changed-only rows retain source battler/stat iteration order."); previous = key;
                Assert.IsTrue(pp.Sequence < row.Sequence && row.Sequence < marker.Sequence);
            }
            foreach (var slot in result.State.Slots.Where(s => s.Body is not null))
            {
                var body = slot.Body!;
                CollectionAssert.AreEqual(new[] { 6, 6, 6, 0, 0, 6, 6 }, new[] { body.AttackStage, body.DefenseStage,
                    body.SpeedStage, body.SpecialAttackStageDelta, body.SpecialDefenseStageDelta, body.AccuracyStage, body.EvasionStage });
                Assert.AreEqual(frame.Slots[slot.Slot.Value].Body!.CalledVolatile, body.CalledVolatile);
            }
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Check(result, 114, single);
        }
    }

    [TestMethod]
    public void HazeRequiresExactUniqueMarkerAndSourceOrderedResetRowsAfterOwnedPp()
    {
        // The typed source envelope rejects impossible slots before projection.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BattlerSlot(4));
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(114); bodies[0] = bodies[0] with { AttackStage = 12, DefenseStage = 0 };
            bodies[1] = bodies[1] with { SpeedStage = 1 };
            var events = Resolve(Frame(bodies, single), 0, 0).Events;
            var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "haze"); int at = events.IndexOf(marker);
            foreach (var invalid in new[] { marker with { Side = new(1) }, marker with { Volatile = "psych-up" },
                marker with { Before = 1 }, marker with { After = 1 }, marker with { Reason = "move" },
                marker with { Turn = marker.Turn + 1 }, marker with { Sequence = marker.Sequence - 1 } })
                Deny(events.SetItem(at, invalid), single, "Forged Haze marker");
            Deny(events.RemoveAt(at), single, "Missing Haze marker");
            Deny(events.Insert(at + 1, marker with { Sequence = marker.Sequence + 1 }), single, "Duplicate Haze marker");
            var rows = events.OfType<SourceStatChangedEvent>().ToArray(); int first = events.IndexOf(rows[0]), second = events.IndexOf(rows[1]);
            foreach (var invalid in new[] { rows[0] with { Actor = new(1) },
                rows[0] with { Before = 6 }, rows[0] with { Before = 13 }, rows[0] with { After = 5 },
                rows[0] with { Stat = (WildBattleStat)99 }, rows[0] with { Turn = rows[0].Turn + 1 } })
                Deny(events.SetItem(first, invalid), single, "Invalid Haze reset");
            if (single)
            {
                // Mutate the exported two-slot envelope: slot 2 is representable as a
                // source BattlerSlot, but cannot belong to this singles invocation.
                var battle = Single(events, events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single()));
                var row = (WildBattleStatChangedEvent)battle.Events[first];
                battle = battle with { Events = battle.Events.SetItem(first, row with { Target = (WildBattleSide)2 }) };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch,
                    "A Haze reset outside the singles layout must not authorize its root.");
            }
            Deny(events.SetItem(first, rows[1] with { Sequence = rows[0].Sequence })
                .SetItem(second, rows[0] with { Sequence = rows[1].Sequence }), single, "Wrong source stat order");
            Deny(events.SetItem(second, rows[0] with { Sequence = rows[1].Sequence }), single, "Duplicate battler/stat reset");
            var late = rows[^1]; int last = events.IndexOf(late);
            Deny(events.SetItem(last, marker with { Sequence = late.Sequence })
                .SetItem(at, late with { Sequence = marker.Sequence }), single, "Reset cannot follow completion marker");
        }
    }

    [TestMethod]
    public void GlareStatusResultMustMatchSelectedTargetAndExactPrimaryTransition()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Resolve(Frame(Bodies(137), single)).Events;
            var changed = events.OfType<SourceStatusChangedEvent>().Single(); int at = events.IndexOf(changed);
            foreach (var invalid in new[] { changed with { Side = new(0) }, changed with { Before = WildBattleStatus.Burn },
                changed with { After = WildBattleStatus.Poison }, changed with { After = WildBattleStatus.Paralysis | WildBattleStatus.Burn },
                changed with { Reason = "ability" }, changed with { Turn = changed.Turn + 1 } })
                Deny(events.SetItem(at, invalid), single, "Wrong Glare primary status receipt");
            Deny(events.RemoveAt(at), single, "Missing Glare result");
            Deny(events.Insert(at + 1, changed with { Sequence = changed.Sequence + 1 }), single, "Duplicate Glare result");
        }
    }
}
