using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Numbering = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SpiteAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("SPITE");
    private static SourceBattleFrame Initial(bool single, int actor = 0, int? target = null, bool prime = true)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("GASTLY", "SPITE") with { SpeedStage = 12, AccuracyStage = 12 };
        var frame = Kernel.Frame(bodies, single); int victim = target ?? (actor ^ 1);
        return prime ? Envelope.Resolve(frame, victim, victim).State : frame;
    }
    private static SourceBattleFrame Remaining(SourceBattleFrame frame, int slot, int pp) => F.Body(frame, slot,
        b => b with { Pokemon = b.Pokemon with { Moves = b.Pokemon.Moves.SetItem(0, b.Pokemon.Moves[0] with { CurrentPp = pp }) } });
    private static SourceKernelResult DirectSlot(SourceBattleFrame frame, int actor, int target, int slot)
    {
        var result = SourceBattleKernel.ResolveAction(Kernel.Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, slot, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State)); return result;
    }
    private static void Check(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        string before = JsonSerializer.Serialize(events); var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        var result = events.OfType<SourceSpiteEvent>().Single(); Assert.IsTrue(AnimationDispatchFixture.Supports(180, Move.Value));
        Assert.AreEqual("EFFECT_SPITE", Kernel.Mechanics.Catalog.Moves[Move].Effect);
        Assert.AreEqual(Move, used.Move); Assert.AreEqual(Move, attack.ChosenMove); Assert.AreEqual(Move, attack.CurrentMove);
        Assert.AreEqual(Move, pp.ChosenMove); Assert.AreEqual(Move, pp.CurrentMove); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.AreEqual(used.Actor, result.Actor); Assert.AreEqual(used.Target, result.Target);
        Assert.AreEqual(pp.Sequence + 1, result.Sequence); Assert.IsTrue(result.PpBefore > 1);
        Assert.IsTrue(result.RolledReduction is >= 2 and <= 5); Assert.AreEqual(Math.Max(0, result.PpBefore - result.RolledReduction), result.PpAfter);
        int roots = 0;
        for (int i = 0; i < events.Length; i++) foreach (bool atResult in new[] { false, true })
        {
            var decision = F.Project(events, i, single, atResult); Assert.AreEqual(decision, F.Project(events, i, single, atResult));
            Assert.IsNull(F.Project(events, i, single, atResult, SourceBattleScene.Off).Dispatch); Assert.IsNull(F.Wave(events, i, single));
            if (events[i] != used || atResult) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
            Assert.AreEqual(180, cue.SourceMoveId); Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence); Assert.AreEqual(result.Sequence, cue.ResultSequence);
            Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target); Assert.IsFalse(cue.HitsSubstitute);
            Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveTurn);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(1, roots); Assert.AreEqual(before, JsonSerializer.Serialize(events));
        Assert.AreEqual(before, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(before)));
        if (single)
        {
            var exported = events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(); string json = JsonSerializer.Serialize(exported);
            Assert.AreEqual(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<WildBattleEvent>>(json)));
            Assert.AreEqual(0, F.Single(events, events.IndexOf(used)).SourceEvents.Length, "Exercise legacy normalization without canonical receipts.");
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(F.Single(events, events.IndexOf(used)), false, SourceBattleScene.On).Dispatch);
            var legacy = exported.OfType<WildBattleSpiteEvent>().Single(); Assert.AreEqual(result.Move, legacy.Move); Assert.AreEqual(result.Slot, legacy.Slot);
            Assert.AreEqual(result.RolledReduction, legacy.RolledReduction); Assert.AreEqual(result.PpAfter, legacy.PpAfter);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ActualLastMoveReductionOwnsOneRootAcrossEveryPositionSelectedAllyAndSubstitute(bool substitute)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : Enumerable.Range(0, 4).Where(t => t != actor).ToArray())
        {
            var frame = Initial(single, actor, target);
            if (substitute) frame = F.Body(frame, target, b => b with { CalledVolatile = new() { SubstituteHp = 1 } });
            var result = F.Ordered(frame, actor, target); string before = JsonSerializer.Serialize(result); Check(result.Events, single);
            var reduced = result.Events.OfType<SourceSpiteEvent>().Single(); Assert.AreEqual(Kernel.Id("SPLASH"), reduced.Move);
            Assert.AreNotEqual(Move, reduced.Move); Assert.AreEqual(39, reduced.PpBefore);
            Assert.AreEqual(reduced.PpAfter, result.State.Slots[target].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual(reduced.PpAfter, result.State.Parties[target & 1].Members[target / 2].Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(2), DataRow(3), DataRow(4), DataRow(5)]
    public void AllFourActualReductionsRetainTheRollAndClampAtZero(int roll)
    {
        foreach (bool single in new[] { false, true }) foreach (int available in new[] { 2, 3, 8 })
        {
            var frame = Remaining(Initial(single), 1, available);
            var result = Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceSpiteEvent>().Any(e => e.RolledReduction == roll), $"Spite roll {roll}, PP {available}");
            Check(result.Events, single); var reduced = result.Events.OfType<SourceSpiteEvent>().Single();
            Assert.AreEqual(available, reduced.PpBefore); Assert.AreEqual(Math.Max(0, available - roll), reduced.PpAfter);
        }
    }

    [TestMethod, DataRow("no-history"), DataRow("zero-pp"), DataRow("one-pp"), DataRow("no-longer-learned"), DataRow("protect"), DataRow("miss")]
    public void FailedSpiteCannotAnimateFromPpOrAFinalStateWithReducedPp(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: branch != "no-history");
            if (branch is "zero-pp" or "one-pp") frame = Remaining(frame, 1, branch == "one-pp" ? 1 : 0);
            if (branch == "no-longer-learned") frame = F.Body(frame, 1, b => b with { Pokemon = b.Pokemon with { Moves = Kernel.Mon("WAILORD", "WATER_GUN", 100).Pokemon.Moves } });
            if (branch == "protect") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "miss") { frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 }); frame = F.Body(frame, 1, b => b with { EvasionStage = 12 }); }
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy"), "Spite miss") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceSpiteEvent>().Any()); Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any()); F.Deny(result.Events, single);
        }
    }

    [TestMethod]
    public void VictimLearnedSlotIsIndependentOfSpitesPaymentSlotAndPressureCanSpendTheLastPp()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false); var target = Kernel.Mon("DUSCLOPS", "LEER", 100) with { SpeedStage = 0 };
            frame = F.Body(frame, 1, _ => target with { Pokemon = target.Pokemon with { Moves = target.Pokemon.Moves.Insert(0, Kernel.Mon("DUSCLOPS", "ASTONISH", 100).Pokemon.Moves[0]) } });
            frame = DirectSlot(frame, 1, 0, 1).State; frame = Remaining(frame, 0, 1);
            var result = Envelope.Resolve(frame); Check(result.Events, single);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); var reduced = result.Events.OfType<SourceSpiteEvent>().Single();
            Assert.AreEqual(0, pp.PpSlot); Assert.AreEqual(1, reduced.Slot); Assert.AreEqual(Kernel.Id("LEER"), reduced.Move);
            Assert.AreEqual(2, pp.CommandCost); Assert.AreEqual(0, pp.After);
        }
    }

    [TestMethod, DataRow("SEEDOT", "BIDE", "called-bide"), DataRow("WHISMUR", "UPROAR", "called-uproar"),
        DataRow("MANKEY", "THRASH", "called-rampage"), DataRow("VOLTORB", "ROLLOUT", "called-rollout"), DataRow("SCYTHER", "FURY_CUTTER", "fury-cutter")]
    public void EmptyingAnActuallyStartedMultiTurnMoveAuthenticatesOnlyItsOrderedCancellation(string species, string move, string marker)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false);
            frame = F.Body(frame, 0, _ => Kernel.Mon("WAILORD", "SPLASH", 100));
            frame = F.Body(frame, 1, _ => Kernel.Mon(species, move) with { AccuracyStage = 12, SpeedStage = 0 });
            frame = Remaining(frame, 1, 3); frame = Envelope.Resolve(frame, 1, move == "BIDE" ? 1 : 0).State;
            Assert.AreEqual(Kernel.Id(move), frame.Slots[1].Body!.LastUsedMove); Assert.AreEqual(2, frame.Slots[1].Body!.Pokemon.Moves[0].CurrentPp);
            frame = F.Body(frame, 0, _ => Kernel.Mon("GASTLY", "SPITE") with { AccuracyStage = 12, SpeedStage = 12 });
            var result = Envelope.Resolve(frame); Check(result.Events, single);
            var reduced = result.Events.OfType<SourceSpiteEvent>().Single(); Assert.AreEqual(0, reduced.PpAfter);
            var cancelled = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Reason == "spite-empty-pp");
            Assert.AreEqual(marker, cancelled.Volatile); Assert.AreEqual(new BattlerSlot(1), cancelled.Side); Assert.AreEqual(0, cancelled.After);
            Assert.AreEqual(reduced.Sequence + 1, cancelled.Sequence);
            foreach (var forged in new[] { cancelled with { Side = new(0) }, cancelled with { Before = 0 }, cancelled with { After = 1 },
                cancelled with { Reason = "move" }, cancelled with { Volatile = "called-recharge" } })
                F.Deny(result.Events.SetItem(result.Events.IndexOf(cancelled), forged), single);
            F.Deny(Numbering.Number(result.Events.Insert(result.Events.IndexOf(cancelled) + 1, cancelled)), single);
            F.Deny(Numbering.Number(result.Events.Remove(cancelled).Insert(result.Events.IndexOf(reduced), cancelled)), single);
        }
    }

    [TestMethod]
    public void EmptyPpCancelsAnActuallyRestartedSkullBashWithoutUsingFinalChargeState()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false);
            frame = F.Body(frame, 0, _ => Kernel.Mon("WAILORD", "SPLASH", 100));
            frame = F.Body(frame, 1, _ => Kernel.Mon("SQUIRTLE", "SKULL_BASH") with { AccuracyStage = 12, AttackStage = 0 });
            frame = Remaining(frame, 1, 4);
            frame = Envelope.Resolve(frame, 1, 0).State;
            Assert.AreEqual(WildBattleCalledLockKind.Charge, frame.Slots[1].Registers.Turn!.Lock!.Kind);
            frame = Envelope.Resolve(frame, 1, 0).State;
            Assert.AreEqual(Kernel.Id("SKULL_BASH"), frame.Slots[1].Body!.LastUsedMove);
            Assert.IsNull(frame.Slots[1].Registers.Turn?.Lock);
            frame = Envelope.Resolve(frame, 1, 0).State;
            Assert.AreEqual(WildBattleCalledLockKind.Charge, frame.Slots[1].Registers.Turn!.Lock!.Kind);
            Assert.AreEqual(2, frame.Slots[1].Body!.Pokemon.Moves[0].CurrentPp);
            frame = F.Body(frame, 0, _ => Kernel.Mon("GASTLY", "SPITE") with { AccuracyStage = 12 });
            var result = Envelope.Resolve(frame); Check(result.Events, single);
            var cancelled = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Reason == "spite-empty-pp");
            Assert.AreEqual("called-charge", cancelled.Volatile); Assert.AreEqual(1, cancelled.Before); Assert.AreEqual(0, cancelled.After);
            Assert.IsNull(result.State.Slots[1].Registers.Turn?.Lock);
            F.Deny(result.Events.SetItem(result.Events.IndexOf(cancelled), cancelled with { Before = 2 }), single);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ActualTransformAndMimicVictimsKeepTransientPpOutOfTheirPersistentParty(bool transform)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false);
            frame = F.Body(frame, 0, _ => Kernel.Mon("WAILORD", "SPLASH", 100));
            frame = F.Body(frame, 1, _ => Kernel.Mon(transform ? "DITTO" : "JIGGLYPUFF", transform ? "TRANSFORM" : "MIMIC"));
            frame = Envelope.Resolve(frame, 0, 0).State; frame = Envelope.Resolve(frame, 1, 0).State;
            Assert.IsTrue(transform ? frame.Slots[1].Body!.Transform is not null : frame.Slots[1].Body!.CopyMoves is not null);
            frame = Envelope.Resolve(frame, 1, 1).State; Assert.AreEqual(Kernel.Id("SPLASH"), frame.Slots[1].Body!.LastUsedMove);
            frame = F.Body(frame, 0, _ => Kernel.Mon("GASTLY", "SPITE") with { AccuracyStage = 12 });
            string persistent = JsonSerializer.Serialize(frame.Parties[1].Members[0].Pokemon.Moves);
            var result = Envelope.Resolve(frame); Check(result.Events, single);
            Assert.AreEqual(persistent, JsonSerializer.Serialize(result.State.Parties[1].Members[0].Pokemon.Moves));
            Assert.IsFalse(result.Events.OfType<SourceCalledPartyChangedEvent>().Any(e => e.SourceSlot == new BattlerSlot(1)));
        }
    }

    [TestMethod]
    public void RealOtherSlotImmunityAndCureBerryTailsStayOwnedAndOrdered()
    {
        var frame = Initial(false);
        frame = F.Body(frame, 2, b => b with { Status = WildBattleStatus.Burn });
        frame = F.Body(frame, 3, _ => Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) with { Status = WildBattleStatus.Paralysis, HeldItem = "ITEM_CHERI_BERRY" });
        var result = Envelope.Resolve(frame); Check(result.Events, false);
        var ability = result.Events.OfType<SourceAbilityActivatedEvent>().Single(e => e.Side.Value == 2);
        var consumed = result.Events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Side.Value == 3);
        Assert.IsTrue(ability.Sequence < consumed.Sequence);
        F.Deny(Numbering.Number(result.Events.Remove(ability)), false); F.Deny(Numbering.Number(result.Events.Remove(consumed)), false);
        foreach (var sync in result.Events.OfType<SourceCalledPartyChangedEvent>())
        {
            F.Deny(result.Events.SetItem(result.Events.IndexOf(sync), sync with { SourceSlot = new(sync.SourceSlot!.Value.Value ^ 2) }), false);
            F.Deny(result.Events.SetItem(result.Events.IndexOf(sync), sync with { After = sync.After with { Pokemon = sync.After.Pokemon with { Experience = sync.After.Pokemon.Experience + 1 } } }), false);
        }
        var syncs = result.Events.OfType<SourceCalledPartyChangedEvent>().ToArray();
        F.Deny(Numbering.Number(result.Events.SetItem(result.Events.IndexOf(syncs[0]), syncs[1]).SetItem(result.Events.IndexOf(syncs[1]), syncs[0])), false);
    }

    [TestMethod, DataRow("move"), DataRow("slot"), DataRow("before"), DataRow("after"), DataRow("actor"), DataRow("target"),
        DataRow("roll-low"), DataRow("roll-high"), DataRow("missing"), DataRow("duplicate"), DataRow("sequence"), DataRow("turn"),
        DataRow("owner"), DataRow("payment"), DataRow("unknown-tail"), DataRow("failure-tail"), DataRow("wrong-turn-tail")]
    public void OnlyTheUniqueOwnedReductionAndItsRealTailCanAuthenticateSpite(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single), 0, 1).Events; var result = rows.OfType<SourceSpiteEvent>().Single(); int at = rows.IndexOf(result);
            var pp = rows.OfType<SourcePpCommandEvent>().Single();
            var changed = mutation switch {
                "move" => rows.SetItem(at, result with { Move = Move }), "slot" => rows.SetItem(at, result with { Slot = 4 }),
                "before" => rows.SetItem(at, result with { PpBefore = 1 }), "after" => rows.SetItem(at, result with { PpAfter = result.PpAfter + 1 }),
                "actor" => rows.SetItem(at, result with { Actor = result.Target }), "target" => rows.SetItem(at, result with { Target = result.Actor }),
                "roll-low" => rows.SetItem(at, result with { RolledReduction = 1 }), "roll-high" => rows.SetItem(at, result with { RolledReduction = 6 }),
                "missing" => Numbering.Number(rows.RemoveAt(at)), "duplicate" => Numbering.Number(rows.Insert(at + 1, result)),
                "sequence" => rows.SetItem(at, result with { Sequence = pp.Sequence }), "turn" => rows.SetItem(at, result with { Turn = result.Turn + 1 }),
                "owner" => rows.SetItem(rows.IndexOf(pp), pp with { PpOwner = result.Target }),
                "payment" => rows.SetItem(rows.IndexOf(pp), pp with { After = pp.Before }),
                "unknown-tail" => Numbering.Number(rows.Insert(at + 1, new SourceMtMoonVolatileEvent(0, result.Turn, result.Target, "spite-tail", 0, 1, "move"))),
                "failure-tail" => Numbering.Number(rows.Insert(at + 1, new SourceMoveFailedEvent(0, result.Turn, result.Actor, result.Target, Move, "accuracy"))),
                _ => rows.SetItem(rows.Length - 1, rows[^1] with { Turn = result.Turn + 1 }) };
            F.Deny(changed, single);
        }
    }

    [TestMethod, DataRow("pp"), DataRow("move"), DataRow("slot"), DataRow("source"), DataRow("transient"), DataRow("mask"), DataRow("duplicate"), DataRow("status")]
    public void VictimPartyWritesMustContainExactlyTheOwnedPersistentReduction(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single), 0, 1).Events;
            var sync = rows.OfType<SourceCalledPartyChangedEvent>().Single(e => e.SourceSlot == new BattlerSlot(1)); int at = rows.IndexOf(sync);
            var changed = mutation switch {
                "pp" => rows.SetItem(at, sync with { After = sync.After with { Pokemon = sync.After.Pokemon with { Moves = sync.After.Pokemon.Moves.SetItem(0, sync.After.Pokemon.Moves[0] with { CurrentPp = sync.After.Pokemon.Moves[0].CurrentPp + 1 }) } } }),
                "move" => rows.SetItem(at, sync with { After = sync.After with { Pokemon = sync.After.Pokemon with { Moves = sync.After.Pokemon.Moves.SetItem(0, sync.After.Pokemon.Moves[0] with { Move = Move }) } } }),
                "slot" => rows.SetItem(at, sync with { SourceSlot = new(0) }), "source" => rows.SetItem(at, sync with { SourceSlot = null }),
                "transient" => rows.SetItem(at, sync with { TransientMoveSlots = 1 }), "mask" => rows.SetItem(at, sync with { TransientMoveSlots = 16 }),
                "duplicate" => Numbering.Number(rows.Insert(at + 1, sync)),
                _ => rows.SetItem(at, sync with { After = sync.After with { Status = WildBattleStatus.Poison } }) };
            F.Deny(changed, single);
        }
    }

    [TestMethod]
    public void ActualCalledSpiteUsesTheSameReceiptAndCurrentCatalogAuthority()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single); if (!single) frame = Envelope.Resolve(frame, 3, 3).State;
            frame = F.Body(frame, 0, _ => Kernel.Mon("CLEFAIRY", "METRONOME") with { AccuracyStage = 12, SpeedStage = 12 });
            var called = Envelope.Resolve(frame with { Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(180)) });
            Assert.AreEqual(Move, called.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            CalledLeafAnimationKernelFixture.Check(called.Events, called, single, Kernel.Id("METRONOME"), 180, called.Events.OfType<SourceSpiteEvent>().Single());
            var rows = F.Ordered(Initial(single), 0, 1).Events; int at = rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());
            var definitions = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, definitions.Remove(Move), definitions.SetItem(Move, definitions[Move] with { SourceMoveId = 1 }),
                definitions.SetItem(Move, definitions[Move] with { Power = 1 }), definitions.SetItem(Move, definitions[Move] with { Target = "MOVE_TARGET_USER" }), definitions.Remove(Kernel.Id("SPLASH")) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }
}
