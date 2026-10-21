using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Previous = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.FieldRootAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class FieldRootAnimationKernelTests
{
    [TestMethod, DataRow(219), DataRow(275), DataRow(139), DataRow(308)]
    public void NaturalLearnersAndCapeBrinkTutorOwnOneRootAtEverySlotAndBothSinglesSides(int id)
    {
        var definition = Mechanics.Catalog.Moves[Move(id)]; Assert.AreEqual(id, definition.SourceMoveId);
        Assert.AreEqual(Spec(id).Effect, definition.Effect);
        Assert.AreEqual(id is 219 or 275 ? "MOVE_TARGET_USER" : "MOVE_TARGET_SELECTED", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int requested in id is 219 or 275 ? new[] { actor } : single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Previous.Resolve(Previous.Frame(Bodies(id, actor), single), actor, requested);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());
            SourceBattleEvent owner = id switch {
                219 => result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "safeguard"),
                275 => result.Events.OfType<SourceRootsChangedEvent>().Single(),
                139 => result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Side.Value == requested && e.Reason == "move"),
                _ => Previous.Hits(result.Events).Single()
            };
            Assert.AreEqual(id is 219 or 275 ? actor : requested, result.Events.OfType<SourceMoveUsedEvent>().Single().Target.Value);
            if (id == 219) Assert.AreEqual(5, result.State.Sides[actor & 1].SafeguardTurnsRemaining);
            if (id == 275) Assert.IsTrue(result.State.Slots[actor].Body!.Rooted);
            if (id == 139) Assert.AreEqual(WildBattleStatus.Poison, result.State.Slots[requested].Body!.Status);
            Check(result, id, single, owner);
        }
    }

    [TestMethod, DataRow(219), DataRow(275)]
    public void UserSettingsIgnoreAccuracyProtectAndSubstituteAndCommitImmediatelyAfterOwnedPp(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with {
                AccuracyStage = 0, Protected = true, CalledVolatile = new() { SubstituteHp = 1 } };
            var frame = Previous.Frame(bodies, single); var result = Previous.Resolve(frame, actor, actor);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            SourceBattleEvent marker = id == 219 ? result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "safeguard")
                : result.Events.OfType<SourceRootsChangedEvent>().Single();
            Assert.AreEqual(result.Events.IndexOf(pp) + 1, result.Events.IndexOf(marker));
            Assert.AreEqual(pp.Sequence + 1, marker.Sequence); Assert.AreEqual(pp.Turn, marker.Turn);
            if (marker is SourceMtMoonVolatileEvent side)
            { Assert.AreEqual(actor, side.Side.Value); Assert.AreEqual(0, side.Before); Assert.AreEqual(5, side.After); Assert.AreEqual("move", side.Reason); }
            if (marker is SourceRootsChangedEvent roots)
            { Assert.AreEqual(actor, roots.Side.Value); Assert.IsTrue(roots.Rooted); Assert.AreEqual("ingrain", roots.Reason); }
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Check(result, id, single, marker);
        }
    }

    [TestMethod, DataRow(219), DataRow(275)]
    public void ReapplicationSpendsPpWithoutRefreshingOrAnimatingTheExistingSetting(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor); if (id == 275) bodies[actor] = bodies[actor] with { Rooted = true };
            var frame = Previous.Frame(bodies, single);
            if (id == 219) frame = frame with { Sides = frame.Sides.SetItem(actor & 1, frame.Sides[actor & 1] with { SafeguardTurnsRemaining = 3 }) };
            var result = Previous.Resolve(frame, actor, actor);
            Assert.AreEqual(id == 219 ? "already-safeguard" : "already-rooted", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.IsFalse(result.Events.OfType<SourceRootsChangedEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "safeguard"));
            Assert.AreEqual(frame.Sides, result.State.Sides); Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Assert.AreEqual(frame.Slots[actor].Body!.Rooted, result.State.Slots[actor].Body!.Rooted);
            Check(result, id, single, null);
        }
    }

    [TestMethod, DataRow(219), DataRow(275)]
    public void SelfSettingRequiresOwnedPpUniqueImmediateMarkerAndMatchingMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Previous.Resolve(Previous.Frame(Bodies(id), single), 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            SourceBattleEvent marker = id == 219 ? events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "safeguard")
                : events.OfType<SourceRootsChangedEvent>().Single();
            int at = events.IndexOf(marker), ppAt = events.IndexOf(pp), usedAt = events.IndexOf(used);
            Deny(events.RemoveAt(at), single);
            Deny(Number(events.Insert(at + 1, marker)), single);
            Deny(events.SetItem(at, marker with { Turn = marker.Turn + 1 }), single);
            Deny(events.SetItem(at, marker with { Sequence = marker.Sequence + 1 }), single);
            Deny(events.SetItem(ppAt, marker with { Sequence = pp.Sequence }).SetItem(at, pp with { Sequence = marker.Sequence }), single);
            Deny(Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, marker.Turn, used.Actor, "unrelated", 0, 1, "move"))), single);
            Deny(events.SetItem(ppAt, pp with { PpOwner = new(1) }), single);
            Deny(events.SetItem(ppAt, pp with { Actor = new(1) }), single);
            Deny(events.SetItem(ppAt, pp with { PpSlot = 1 }), single);
            Deny(events.SetItem(usedAt, used with { Target = new(1) }).SetItem(ppAt, pp with { Target = new(1) }), single);
            if (marker is SourceMtMoonVolatileEvent side)
                foreach (var wrong in new[] { side with { Side = new(1) }, side with { Before = 1 }, side with { After = 4 },
                    side with { After = 6 }, side with { Volatile = "mist" }, side with { Reason = "countdown" } }) Deny(events.SetItem(at, wrong), single);
            if (marker is SourceRootsChangedEvent roots)
            {
                foreach (var wrong in new[] { roots with { Side = new(1) }, roots with { Rooted = false }, roots with { Reason = "move" },
                    roots with { Reason = "switch" } }) Deny(events.SetItem(at, wrong), single);
                Deny(events.SetItem(at, new SourceIngrainHealEvent(roots.Sequence, roots.Turn, roots.Side, 10, 11, 1)), single);
                Deny(Number(events.Insert(at + 1, roots with { Rooted = false })), single);
                if (single)
                {
                    var battle = Previous.Single(events, usedAt); var exported = (SourceRootsChangedEvent)battle.Events[at];
                    battle = battle with { Events = battle.Events.SetItem(at, exported with { Rooted = false }) };
                    Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch,
                        "The canonical receipt is authoritative for the single envelope.");
                    battle = Previous.Single(events, usedAt);
                    Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch,
                        "Singles dispatch must consume SourceRootsChangedEvent itself.");
                }
            }
            var definitions = Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = "MOVE_TARGET_SELECTED" },
                definition with { Effect = "EFFECT_HIT", Power = 1, Target = "MOVE_TARGET_SELECTED" } })
            {
                var metadata = definitions.SetItem(Move(id), wrong);
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Previous.Single(events, usedAt), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Previous.Double(events, usedAt), SourceBattleScene.On, metadata).Dispatch);
            }
        }
    }

    [TestMethod, DataRow(219), DataRow(275)]
    public void GenuineCountdownAndIngrainHealingNeverReplayTheSetupRoot(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); var actor = bodies[0];
            bodies[0] = actor with { Pokemon = actor.Pokemon with { CurrentHp = actor.Pokemon.Stats.MaxHp - 20 } };
            var result = Previous.Ordered(Previous.Frame(bodies, single), 0, 0);
            int setupCount = 0, residualCount = 0;
            for (int step = 0; ; step++)
            {
                Assert.IsTrue(step < 32); Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
                string snapshot = JsonSerializer.Serialize(result);
                for (int i = 0; i < result.Events.Length; i++)
                {
                    var cue = Project(result.Events, i, single).Dispatch;
                    if (cue?.SourceMoveId == id) setupCount++;
                    bool residual = id == 275 ? result.Events[i] is SourceIngrainHealEvent
                        : result.Events[i] is SourceMtMoonVolatileEvent { Volatile: "safeguard", Reason: "countdown" };
                    if (!residual) continue;
                    residualCount++; Assert.IsNull(cue);
                    Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Previous.Single(result.Events, i), true, SourceBattleScene.On).Dispatch
                        : AnimationDispatchFixture.AtResult(Previous.Double(result.Events, i), SourceBattleScene.On).Dispatch);
                }
                Assert.AreEqual(snapshot, JsonSerializer.Serialize(result));
                if (result.CompletedCleanTurn) break;
                Assert.IsNotNull(result.Continuation);
                result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
            }
            Assert.AreEqual(1, setupCount); Assert.AreEqual(1, residualCount);
            if (id == 219) Assert.AreEqual(4, result.State.Sides[0].SafeguardTurnsRemaining);
            else Assert.IsTrue(result.State.Slots[0].Body!.Pokemon.CurrentHp > bodies[0].Pokemon.CurrentHp);
        }
    }

    [TestMethod, DataRow("immunity"), DataRow("poison"), DataRow("steel"), DataRow("existing-status"),
        DataRow("protect"), DataRow("substitute"), DataRow("safeguard"), DataRow("miss")]
    public void PoisonGasUsesActualPrimaryPoisonPreventionRatherThanAnyStatusChange(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(139);
            bodies[1] = branch switch {
                "immunity" => Previous.Mon("SNORLAX", "DEFENSE_CURL", 100),
                "poison" => Previous.Mon("TENTACOOL", "ACID"),
                "steel" => Previous.Mon("MAGNEMITE", "THUNDER_SHOCK"),
                "existing-status" => bodies[1] with { Status = WildBattleStatus.Burn },
                "protect" => bodies[1] with { Protected = true },
                "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } }, _ => bodies[1]
            };
            if (branch == "immunity") Assert.AreEqual("ABILITY_IMMUNITY", WildBattleEffective.Ability(bodies[1]));
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Previous.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = branch == "miss" ? Previous.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any() ||
                r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy"), "Poison Gas miss") : Previous.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Reason == "move")); Check(result, 139, single, null);
        }
    }

    [TestMethod]
    public void PoisonGasRejectsMissingDuplicateWrongTargetAndWrongPrimaryStatusReceipts()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Previous.Resolve(Previous.Frame(Bodies(139), single)).Events;
            var status = events.OfType<SourceStatusChangedEvent>().Single(); int at = events.IndexOf(status);
            foreach (var wrong in new[] { status with { Side = new(0) }, status with { Before = WildBattleStatus.Burn },
                status with { After = WildBattleSourceFieldEffects.ToxicPoison }, status with { After = WildBattleStatus.Paralysis },
                status with { Reason = "ability" }, status with { Turn = status.Turn + 1 } }) Deny(events.SetItem(at, wrong), single);
            Deny(events.RemoveAt(at), single); Deny(Number(events.Insert(at + 1, status)), single);
        }
    }

    [TestMethod, DataRow("substitute"), DataRow("protect"), DataRow("water-absorb"), DataRow("miss")]
    public void HydroCannonUsesCommittedOrdinaryHitAndSubstituteReceipts(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(308);
            if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "water-absorb")
            {
                bodies[1] = Previous.Mon("POLIWAG", "BUBBLE"); Assert.AreEqual("ABILITY_WATER_ABSORB", WildBattleEffective.Ability(bodies[1]));
                bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = bodies[1].Pokemon.Stats.MaxHp - 10 } };
            }
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Previous.Frame(bodies, single);
            var result = branch == "miss" ? Previous.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Hydro Cannon miss")
                : Previous.Resolve(frame);
            var hits = Previous.Hits(result.Events);
            Assert.AreEqual(branch == "substitute" ? 1 : 0, hits.Length);
            if (branch == "substitute") Assert.IsInstanceOfType<SourceSubstituteDamagedEvent>(hits.Single());
            Check(result, 308, single, branch == "substitute" ? hits.Single() : null);
        }
    }
}
