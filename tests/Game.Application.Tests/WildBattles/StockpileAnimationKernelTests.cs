using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class StockpileAnimationKernelTests
{
    private static string Name(int id) => id switch {
        254 => "STOCKPILE", 256 => "SWALLOW", _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    private static MoveId Move(int id) => Kernel.Id(Name(id));

    // Gulpin34 and Swalot40 naturally learn both moves. Counts and HP are
    // controlled integration inputs, not claims about a replayed campaign route.
    private static WildBattleCombatant[] Bodies(int id, int actor, int count, bool fullHp = false)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var body = Kernel.Mon(actor % 2 == 0 ? "GULPIN" : "SWALOT", Name(id), actor % 2 == 0 ? 34 : 40);
        bodies[actor] = body with {
            AccuracyStage = 12, SpeedStage = 12, CalledVolatile = new() { StockpileCount = count },
            Pokemon = body.Pokemon with { CurrentHp = fullHp || id == 254 ? body.Pokemon.Stats.MaxHp : 1 }
        };
        return bodies;
    }

    private static SourceMtMoonVolatileEvent Marker(ImmutableArray<SourceBattleEvent> events) =>
        events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "stockpile");

    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) =>
        Envelope.Deny(events, single, "Only a direct owned stockpile result may authorize Stockpile or Swallow.");

    private static void Check(ImmutableArray<SourceBattleEvent> events, object committed, int id, bool single,
        SourceBattleEvent? owner, int count)
    {
        string before = JsonSerializer.Serialize(committed);
        Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value), "Positive tests require the generated source script and matching label.");
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Actor, used.Target);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(used.Actor, pp.Target);
        Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        int roots = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Kernel.Project(events, i, single);
            Assert.AreEqual(decision, Kernel.Project(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Envelope.Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Envelope.Double(events, i), SourceBattleScene.On).Dispatch,
                "Swallow retains ordinary attack-text ownership, even at the positive recovery cursor.");
            Assert.IsNull(Shared.Wave(events, i, single), "Gen III Stockpile has no Defense or Special Defense stat wave.");
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
            }
            if (owner is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Actor, cue.Target);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.IsFalse(cue.HitsSubstitute); Assert.AreEqual(id == 256 ? (byte?)count : null, cue.MoveTurn);
            Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.FuryCutterCounter); Assert.IsNull(cue.RolloutCounter);
        }
        Assert.AreEqual(owner is null ? 0 : 1, roots);
        Assert.AreEqual(before, JsonSerializer.Serialize(committed), "Repeated projection preserves state, RNG, trace and committed receipts.");
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void NaturalLearnersOwnEverySuccessfulCounterBranchAtAllSourceSlots(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(id == 254 ? "EFFECT_STOCKPILE" : "EFFECT_SWALLOW", definition.Effect);
        Assert.AreEqual(0, definition.Power); Assert.AreEqual("MOVE_TARGET_USER", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int count in id == 254 ? new[] { 0, 1, 2 } : new[] { 1, 2, 3 })
        {
            var frame = Kernel.Frame(Bodies(id, actor, count), single); var result = Envelope.Resolve(frame, actor, actor);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());
            var marker = Marker(result.Events); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(actor, marker.Side.Value); Assert.AreEqual(count, marker.Before); Assert.AreEqual(id == 254 ? count + 1 : 0, marker.After);
            Assert.AreEqual(id == 254 ? "move" : "swallow", marker.Reason); Assert.AreEqual(pp.Sequence + 1, marker.Sequence);
            Assert.AreEqual(result.Events.IndexOf(pp) + 1, result.Events.IndexOf(marker));
            Assert.AreEqual(id == 254 ? count + 1 : 0, result.State.Slots[actor].Body!.CalledVolatile?.StockpileCount ?? 0);
            SourceBattleEvent owner = marker;
            if (id == 256)
            {
                var healed = result.Events.OfType<SourceRecoveredEvent>().Single(); owner = healed;
                int maximum = frame.Slots[actor].Body!.Pokemon.Stats.MaxHp;
                Assert.AreEqual(actor, healed.Side.Value); Assert.AreEqual(Move(id), healed.Move);
                Assert.AreEqual(marker.Sequence + 1, healed.Sequence); Assert.AreEqual(result.Events.IndexOf(marker) + 1, result.Events.IndexOf(healed));
                Assert.AreEqual(Math.Min(maximum - 1, Math.Max(1, maximum / (1 << (3 - count)))), healed.Recovered);
                Assert.AreEqual(1, healed.HpBefore); Assert.AreEqual(healed.HpBefore + healed.Recovered, healed.HpAfter);
                Assert.AreEqual(healed.HpAfter, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            }
            else Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Check(result.Events, result, id, single, owner, count);
        }
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void PublicTurnStartPreservesTheSameAttackTextOwnerAtEverySourceSlot(int id)
    {
        int count = id == 254 ? 2 : 3;
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id, actor, count), single), actor, actor);
            SourceBattleEvent owner = id == 254 ? Marker(result.Events) : result.Events.OfType<SourceRecoveredEvent>().Single();
            Check(result.Events, result, id, single, owner, count);
        }
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void SelfDirectedStockpileEffectsBypassSubstituteProtectAndAccuracy(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor, 2); bodies[actor] = bodies[actor] with {
                AccuracyStage = 0, Protected = true, CalledVolatile = new() { StockpileCount = 2, SubstituteHp = 1 } };
            var frame = Kernel.Frame(bodies, single); var result = Envelope.Resolve(frame, actor, actor);
            Assert.AreEqual(1, result.State.Slots[actor].Body!.CalledVolatile!.SubstituteHp);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any()); Assert.IsFalse(result.Events.OfType<SourceSubstituteDamagedEvent>().Any());
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            SourceBattleEvent owner = id == 254 ? Marker(result.Events) : result.Events.OfType<SourceRecoveredEvent>().Single();
            Check(result.Events, result, id, single, owner, 2);
        }
    }

    [TestMethod, DataRow(254, 3, true, "stockpile-full"), DataRow(256, 0, false, "stockpile-empty"),
        DataRow(256, 0, true, "stockpile-empty"), DataRow(256, 1, true, "already-full-hp"),
        DataRow(256, 2, true, "already-full-hp"), DataRow(256, 3, true, "already-full-hp")]
    public void MaximumEmptyAndFullHpFailuresSpendPpButNeverAnimate(int id, int count, bool fullHp, string reason)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = Kernel.Frame(Bodies(id, actor, count, fullHp), single); var result = Envelope.Resolve(frame, actor, actor);
            Assert.AreEqual(reason, result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any());
            Assert.IsTrue(result.Events.OfType<SourcePpCommandEvent>().Single().After < result.Events.OfType<SourcePpCommandEvent>().Single().Before);
            if (id == 256 && count > 0)
            {
                var consumed = Marker(result.Events); Assert.AreEqual(count, consumed.Before); Assert.AreEqual(0, consumed.After);
                Assert.AreEqual("swallow", consumed.Reason); Assert.AreEqual(actor, consumed.Side.Value);
            }
            else Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "stockpile"));
            Assert.AreEqual(id == 254 ? 3 : 0, result.State.Slots[actor].Body!.CalledVolatile?.StockpileCount ?? 0);
            Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.CurrentHp, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Check(result.Events, result, id, single, null, count);
        }
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void ActualTauntCancellerPreservesTheCounterAndCannotAuthorizeEitherRoot(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor, 2); bodies[actor] = bodies[actor] with { CalledVolatile = new() { StockpileCount = 2, TauntTurnsRemaining = 2 } };
            var frame = Kernel.Frame(bodies, single); var result = Envelope.Resolve(frame, actor, actor);
            Assert.AreEqual("taunted", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.IsFalse(result.Events.OfType<SourcePpCommandEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "stockpile"));
            Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any()); Assert.AreEqual(2, result.State.Slots[actor].Body!.CalledVolatile!.StockpileCount);
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            string before = JsonSerializer.Serialize(result); Deny(result.Events, single); Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void ActualMetronomeStockpileAndSwallowOwnTheirAttackTextRoots(int id)
    {
        Assert.IsTrue(WildBattleSourceCallers.IsDirectMetronomeChoice(id));
        uint seed = CallerPreludeAnimationKernelFixture.MetronomeSeed(id);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor, 2); var caller = Kernel.Mon("TOGETIC", "METRONOME", 29);
            bodies[actor] = caller with { Pokemon = caller.Pokemon with { CurrentHp = 1 }, CalledVolatile = new() { StockpileCount = 2 } };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(seed) }, actor, actor ^ 1);
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Actor.Value == actor && e.Caller == Kernel.Id("METRONOME") && e.Result == Move(id)));
            var marker = Marker(result.Events); Assert.AreEqual(actor, marker.Side.Value); Assert.AreEqual(2, marker.Before);
            Assert.AreEqual(id == 254 ? 3 : 0, marker.After);
            if (id == 256) Assert.IsTrue(result.Events.OfType<SourceRecoveredEvent>().Any(e => e.Move == Move(id) && e.Recovered > 0));
            string before = JsonSerializer.Serialize(result); var cue = CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id,
                id == 256 ? result.Events.OfType<SourceRecoveredEvent>().Single() : marker);
            Assert.AreEqual(id == 256 ? (byte?)2 : null, cue.MoveTurn);
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void DirectRootsRejectMissingDuplicateForeignReorderedAndUnownedCounterReceipts(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(id, 0, 2), single), 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
            var pp = events.OfType<SourcePpCommandEvent>().Single(); var marker = Marker(events);
            int ui = events.IndexOf(used), ai = events.IndexOf(attack), pi = events.IndexOf(pp), mi = events.IndexOf(marker);
            Deny(events.RemoveAt(mi), single); Deny(events.RemoveAt(pi), single); Deny(events.RemoveAt(ai), single);
            Deny(Shared.Number(events.Insert(mi + 1, marker)), single); Deny(Shared.Number(events.Insert(pi + 1, pp)), single);
            Deny(events.SetItem(mi, marker with { Sequence = marker.Sequence + 1 }), single);
            Deny(events.SetItem(mi, marker with { Turn = marker.Turn + 1 }), single);
            Deny(events.SetItem(pi, marker with { Sequence = pp.Sequence }).SetItem(mi, pp with { Sequence = marker.Sequence }), single);
            Deny(Shared.Number(events.Insert(mi, new SourceMtMoonVolatileEvent(0, used.Turn, used.Actor, "unrelated", 0, 1, "move"))), single);
            foreach (var wrong in new[] { marker with { Side = new(1) }, marker with { Volatile = "charge" },
                marker with { Reason = "countdown" }, marker with { Before = -1 }, marker with { Before = 4 },
                marker with { After = -1 }, marker with { After = 4 }, marker with { After = marker.Before },
                marker with { Reason = id == 254 ? "swallow" : "move" } }) Deny(events.SetItem(mi, wrong), single);
            if (id == 254) Deny(events.SetItem(mi, marker with { Before = 3, After = 4 }), single);
            else Deny(events.SetItem(mi, marker with { Before = 0 }), single);
            foreach (var wrong in new[] { pp with { Actor = new(1) }, pp with { PpOwner = new(1) }, pp with { PpSlot = 1 },
                pp with { Target = new(1) }, pp with { CurrentMove = Kernel.Id("POUND") }, pp with { ChosenMove = Kernel.Id("METRONOME") },
                pp with { Before = pp.Before + 1 }, pp with { After = pp.Before }, pp with { CommandCost = 0 } }) Deny(events.SetItem(pi, wrong), single);
            Deny(events.SetItem(ui, used with { Target = new(1) }).SetItem(pi, pp with { Target = new(1) }), single);
            Deny(events.SetItem(ai, attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Deny(events.SetItem(ui, used with { PpAfter = used.PpBefore }).SetItem(pi, pp with { After = pp.Before }), single);
        }
    }

    [TestMethod]
    public void SwallowRequiresUniqueAdjacentPositiveLivingSelfRecoveryAfterConsumption()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(256, 0, 2), single), 0, 0).Events;
            var marker = Marker(events); var healed = events.OfType<SourceRecoveredEvent>().Single();
            int mi = events.IndexOf(marker), hi = events.IndexOf(healed);
            Deny(events.RemoveAt(hi), single); Deny(Shared.Number(events.Insert(hi + 1, healed)), single);
            Deny(events.SetItem(mi, healed with { Sequence = marker.Sequence }).SetItem(hi, marker with { Sequence = healed.Sequence }), single);
            Deny(Shared.Number(events.Insert(hi, new SourceMtMoonVolatileEvent(0, healed.Turn, healed.Side, "unrelated", 0, 1, "move"))), single);
            foreach (var wrong in new[] { healed with { Side = new(1) }, healed with { Move = Kernel.Id("RECOVER") },
                healed with { Turn = healed.Turn + 1 }, healed with { Sequence = healed.Sequence + 1 },
                healed with { HpBefore = 0 }, healed with { HpAfter = healed.HpBefore },
                healed with { HpAfter = healed.HpBefore - 1 }, healed with { Recovered = 0 },
                healed with { Recovered = healed.Recovered + 1 } }) Deny(events.SetItem(hi, wrong), single);
        }
    }

    [TestMethod, DataRow(254), DataRow(256)]
    public void MetadataGeneratedScriptSupportAndExportedSinglesReceiptsRemainAuthoritative(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(id, 0, 2), single), 0, 0).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int ui = events.IndexOf(used);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = "MOVE_TARGET_SELECTED" },
                definition with { Effect = id == 254 ? "EFFECT_SWALLOW" : "EFFECT_STOCKPILE" }, definition with { SourceMoveId = id == 254 ? 256 : 254 } })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, null).Dispatch
                : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, null).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions.Remove(Move(id))).Dispatch
                : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions.Remove(Move(id))).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
            if (!single) continue;
            var battle = Envelope.Single(events, ui);
            var detached = AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch;
            Assert.IsNotNull(detached, "Singles must consume the canonical receipts directly.");
            Assert.AreEqual(id == 256 ? (byte?)2 : null, detached.MoveTurn);
            int mi = events.IndexOf(Marker(events)); var marker = (SourceMtMoonVolatileEvent)battle.Events[mi];
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(mi, marker with { After = marker.Before }) }, true, SourceBattleScene.On).Dispatch);
            if (id == 256)
            {
                int hi = events.IndexOf(events.OfType<SourceRecoveredEvent>().Single()); var healed = (SourceRecoveredEvent)battle.Events[hi];
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(hi, healed with { Recovered = 0 }) }, true, SourceBattleScene.On).Dispatch);
            }
        }
    }
}
