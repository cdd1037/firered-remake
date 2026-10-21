using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class WaterSportAnimationKernelTests
{
    private static string Name(int id) => id == 346 ? "WATER_SPORT" : "MUD_SPORT";
    private static string Sport(int id) => id == 346 ? "water-sport" : "mud-sport";
    private static MoveId Move(int id) => Kernel.Id(Name(id));
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
        bodies[actor] = Kernel.Mon(id == 346 ? "PSYDUCK" : "GEODUDE", Name(id), 100);
        return bodies;
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single, SourceBattleScene scene = SourceBattleScene.On) =>
        single ? AnimationDispatchFixture.AtAttackText(Kernel.Single(events, index), true, scene)
            : AnimationDispatchFixture.AtAttackText(Kernel.Double(events, index), scene);
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) => events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();

    [TestMethod, DataRow(346), DataRow(300)]
    public void NaturalSportLearnersOwnAdjacentStateBeforeAnimationAtEverySlot(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual("EFFECT_" + Name(id), definition.Effect);
        Assert.AreEqual(0, definition.Power); Assert.AreEqual("MOVE_TARGET_USER", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0, Protected = true, CalledVolatile = new() { SubstituteHp = 1 } };
            var frame = Kernel.Frame(bodies, single); var result = Kernel.Resolve(frame, actor, actor);
            var used = result.Events.OfType<SourceMoveUsedEvent>().Single(); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            var marker = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == Sport(id));
            Assert.AreEqual(pp.Sequence + 1, marker.Sequence); Assert.AreEqual(used.Actor, marker.Side);
            Assert.AreEqual(0, marker.Before); Assert.AreEqual(1, marker.After); Assert.AreEqual("move", marker.Reason);
            Assert.IsTrue(id == 346 ? result.State.Slots[actor].Body!.WaterSport : result.State.Slots[actor].Body!.MudSport);
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            string before = JsonSerializer.Serialize(result); int roots = 0;
            for (int i = 0; i < result.Events.Length; i++)
            {
                var decision = Project(result.Events, i, single); Assert.AreEqual(decision, Project(result.Events, i, single));
                Assert.IsNull(Project(result.Events, i, single, SourceBattleScene.Off).Dispatch);
                Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Kernel.Single(result.Events, i), true, SourceBattleScene.On).Dispatch
                    : AnimationDispatchFixture.AtResult(Kernel.Double(result.Events, i), SourceBattleScene.On).Dispatch);
                if (result.Events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
                Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Actor, cue.Target);
                Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(marker.Sequence, cue.ResultSequence);
                Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
                Assert.IsFalse(cue.HitsSubstitute); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage);
                if (single)
                {
                    Assert.IsNull(AnimationDispatchFixture.AtAttackText(Kernel.Single(result.Events, i), false, SourceBattleScene.On).Dispatch);
                    var exportedOnly = Kernel.Single(result.Events, i);
                    Assert.AreEqual(cue, AnimationDispatchFixture.AtAttackText(exportedOnly, true, SourceBattleScene.On).Dispatch);
                }
            }
            Assert.AreEqual(1, roots); Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(346), DataRow(300)]
    public void ExistingSportSpendsPpAndFailsWithoutASecondAnimation(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor); bodies[actor] = id == 346 ? bodies[actor] with { WaterSport = true } : bodies[actor] with { MudSport = true };
            var frame = Kernel.Frame(bodies, single); var result = Kernel.Resolve(frame, actor, actor);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "already-" + Sport(id)));
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == Sport(id)));
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); Assert.AreEqual(pp.Before - 1, pp.After);
            Assert.AreEqual(frame.Rng, result.State.Rng); Kernel.Deny(result.Events, single, "Existing sport cannot replay setup.");
        }
    }

    [TestMethod, DataRow(346), DataRow(300)]
    public void SportRequiresOwnedImmediateUniqueMarkerAndMatchingMoveMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Kernel.Resolve(Kernel.Frame(Bodies(id), single), 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == Sport(id));
            int at = events.IndexOf(marker), usedAt = events.IndexOf(used), ppAt = events.IndexOf(pp);
            void Deny(ImmutableArray<SourceBattleEvent> altered) => Kernel.Deny(altered, single, "Sport receipt ownership failed.");
            Deny(events.RemoveAt(at)); Deny(Number(events.Insert(at + 1, marker)));
            foreach (var wrong in new[] { marker with { Side = new(1) }, marker with { Before = 1 }, marker with { After = 0 },
                marker with { After = 5 }, marker with { Volatile = Sport(id == 346 ? 300 : 346) }, marker with { Reason = "switch" },
                marker with { Turn = marker.Turn + 1 }, marker with { Sequence = marker.Sequence + 1 } }) Deny(events.SetItem(at, wrong));
            Deny(Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, marker.Turn, used.Actor, "unrelated", 0, 1, "move"))));
            Deny(events.SetItem(ppAt, marker with { Sequence = pp.Sequence }).SetItem(at, pp with { Sequence = marker.Sequence }));
            Deny(events.SetItem(ppAt, pp with { PpOwner = new(1) })); Deny(events.SetItem(ppAt, pp with { Actor = new(1) }));
            Deny(events.SetItem(usedAt, used with { Target = new(1) }).SetItem(ppAt, pp with { Target = new(1) }));
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = "MOVE_TARGET_SELECTED" },
                definition with { Effect = "EFFECT_MIST" }, definition with { SourceMoveId = id == 346 ? 300 : 346 } })
            {
                var metadata = definitions.ToDictionary(p => p.Key, p => p.Value); metadata[Move(id)] = wrong;
                var decision = single ? AnimationDispatchFixture.AtAttackText(Kernel.Single(events, usedAt), true, SourceBattleScene.On, metadata)
                    : AnimationDispatchFixture.AtAttackText(Kernel.Double(events, usedAt), SourceBattleScene.On, metadata);
                Assert.IsNull(decision.Dispatch);
            }
        }
    }

    [TestMethod, DataRow(346), DataRow(300)]
    public void GenuineMoveEndCuresAndWhiteHerbRemainIndependentOfSportSetup(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool herb in new[] { false, true })
        {
            var frame = Kernel.Frame(Bodies(id), single);
            frame = ChargeCopyAnimationKernelFixture.Body(frame, 1, body => herb
                ? body with { HeldItem = "ITEM_WHITE_HERB", AttackStage = 5 }
                : body with { HeldItem = "ITEM_LUM_BERRY", Status = WildBattleStatus.Burn, ConfusionTurnsRemaining = 3 });
            var events = Kernel.Resolve(frame, 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var item = events.OfType<SourceHeldItemConsumedEvent>().Single();
            var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == Sport(id));
            Assert.AreEqual(herb ? "ITEM_WHITE_HERB" : "ITEM_LUM_BERRY", item.Item); Assert.AreEqual(new BattlerSlot(1), item.Side);
            Assert.IsTrue(item.Sequence > marker.Sequence);
            var decision = Project(events, events.IndexOf(used), single); Assert.IsNotNull(decision.Dispatch, decision.Reason);
            Assert.AreEqual(marker.Sequence, decision.Dispatch.ResultSequence);
            Kernel.Deny(Number(events.Remove(item)), single, "A cure group without its consumed item cannot authorize the sport tail.");
            Kernel.Deny(events.SetItem(events.IndexOf(item), item with { Side = new(0) }), single, "Foreign item ownership cannot authorize the sport tail.");
        }
    }

    [TestMethod, DataRow(346), DataRow(300)]
    public void ForeignFailureHpAndUninterpretedOutcomesCannotHideAfterSportSetup(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Kernel.Resolve(Kernel.Frame(Bodies(id), single), 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single();
            var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == Sport(id));
            int at = events.IndexOf(marker) + 1;
            SourceBattleEvent[] extras = [
                new SourceMoveFailedEvent(0, used.Turn, used.Actor, used.Target, used.Move, "protected"),
                new SourceMoveFailedEvent(0, used.Turn, new(1), new(0), Kernel.Id("TACKLE"), "missed"),
                new SourceHpChangedEvent(0, used.Turn, used.Actor, new(1), used.Move, 100, 90, 10, false, 100),
                new SourceHpChangedEvent(0, used.Turn, used.Actor, used.Actor, used.Move, 100, 90, 10, false, 100),
                new SourceRootsChangedEvent(0, used.Turn, used.Actor, true, "ingrain"),
                new SourceMtMoonVolatileEvent(0, used.Turn, used.Actor, "unrelated", 0, 1, "move"),
                new SourcePayDayEvent(0, used.Turn, new(1), 0, 100) ];
            foreach (var extra in extras) Kernel.Deny(Number(events.Insert(at, extra)), single, "Unowned outcome in the sport tail.");
        }
    }

    [TestMethod]
    public void SplashKeepsItsDistinctIntentionalNothingHappenedBoundary()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(346); bodies[0] = Kernel.Mon("MAGIKARP", "SPLASH", 100);
            var events = Kernel.Resolve(Kernel.Frame(bodies, single), 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single();
            var result = events.OfType<SourceMoveFailedEvent>().Single(e => e.Reason == "nothing-happened");
            var decision = Project(events, events.IndexOf(used), single);
            Assert.IsNotNull(decision.Dispatch, decision.Reason); Assert.AreEqual(150, decision.Dispatch.SourceMoveId);
            Assert.AreEqual(result.Sequence, decision.Dispatch.ResultSequence);
        }
    }
}
