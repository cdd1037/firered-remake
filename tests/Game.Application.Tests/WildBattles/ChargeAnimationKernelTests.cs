using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ChargeAnimationKernelTests
{
    [TestMethod, DataRow(13), DataRow(130)]
    public void PublicSetupOwnsItsPpAndChargeResultWithoutAnAttackPageAtEveryPosition(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Initial(id, single, actor); string before = JsonSerializer.Serialize(frame);
            var result = F.Ordered(frame, actor, actor ^ 1); var events = result.Events;
            var pp = events.OfType<SourcePpCommandEvent>().Single();
            var charge = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            Assert.AreEqual("move", charge.Reason); Assert.AreEqual(0, charge.Before); Assert.AreEqual(1, charge.After);
            Assert.AreEqual(pp.Sequence + 1, charge.Sequence); Assert.AreEqual(new BattlerSlot(actor), charge.Side);
            Assert.IsFalse(events.Any(e => e is SourceAttackStringEvent or SourceMoveUsedEvent));
            Assert.AreEqual(1, pp.CommandCost); Assert.AreEqual(pp.Before - 1, pp.After);
            F.Check(events, id, single, charge, charge, 0);
            // The authored animation already ran before any later Defense result.
            var prefix = events.Take(events.IndexOf(charge) + 1).ToImmutableArray(); F.Check(prefix, id, single, charge, charge, 0);
            if (id == 130)
            {
                var defense = events.OfType<SourceStatChangedEvent>().Single(); Assert.AreEqual(WildBattleStat.Defense, defense.Stat);
                Assert.AreEqual(charge.Side, defense.Actor); Assert.AreEqual(charge.Side, defense.Target);
                Assert.AreEqual(6, defense.Before); Assert.AreEqual(7, defense.After); Assert.IsTrue(defense.Sequence > charge.Sequence);
            }
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }
    [TestMethod, DataRow(6), DataRow(11), DataRow(12)]
    public void SkullBashChargePrecedesItsIndependentDefenseResultAndCap(int stage)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(F.Initial(130, single), 0, b => b with { DefenseStage = stage });
            var result = F.Ordered(frame, 0, 1); var charge = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            F.Check(result.Events, 130, single, charge, charge, 0);
            Assert.AreEqual(Math.Min(12, stage + 1), result.State.Slots[0].Body!.DefenseStage);
            Assert.AreEqual(stage == 12 ? 1 : 0, result.Events.OfType<SourceStatAtMaximumEvent>().Count());
            for (int i = 0; i < result.Events.Length; i++)
            {
                var wave = F.Wave(result.Events, i, single);
                if (result.Events[i] is not SourceStatChangedEvent) { Assert.IsNull(wave); continue; }
                Assert.IsNotNull(wave); Assert.AreEqual(16, wave.AnimationArgument); Assert.IsNull(wave.AttackSequence);
                Assert.AreEqual(130, wave.SourceMoveId); Assert.AreEqual(new BattlerSlot(0), wave.Affected);
                Assert.AreEqual(result.Events.OfType<SourcePpCommandEvent>().Single().Sequence, wave.PpSequence);
            }
        }
    }
    [TestMethod, DataRow(13, false), DataRow(13, true), DataRow(130, false), DataRow(130, true)]
    public void CompletedPublicTurnReleaseUsesItsMarkerUnchangedPpAndAuthoredTurn(int id, bool substitute)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Charged(id, single, actor);
            if (substitute) foreach (int target in Enumerable.Range(0, frame.BattlerCount).Where(s => (s & 1) != (actor & 1)))
                frame = F.Body(frame, target, b => b with { CalledVolatile = new() { SubstituteHp = 100 } });
            string before = JsonSerializer.Serialize(frame); int priorPp = frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;
            var result = F.Ordered(frame, actor, actor ^ 1); var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
            var release = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            Assert.AreEqual("released", release.Reason); Assert.AreEqual(1, release.Before); Assert.AreEqual(0, release.After);
            var attack = result.Events.OfType<SourceAttackStringEvent>().Single(); Assert.AreEqual(release.Sequence + 1, attack.Sequence);
            var commands = result.Events.OfType<SourcePpCommandEvent>().ToArray();
            Assert.AreEqual(!single && id == 13 ? 2 : 1, commands.Length);
            foreach (var pp in commands) { Assert.AreEqual(priorPp, pp.Before); Assert.AreEqual(priorPp, pp.After); Assert.AreEqual(1, pp.CommandCost); }
            Assert.AreEqual(priorPp, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.IsNull(result.State.Slots[actor].Registers.Turn?.Lock);
            F.Check(result.Events, id, single, used, Envelope.Hits(result.Events).First(), 1);
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }
    [TestMethod, DataRow(13), DataRow(130)]
    public void PressureKeepsNominalCommandCostAndFinalPpStillReleasesAtZero(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = F.Bodies(id); bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { Moves = [bodies[0].Pokemon.Moves[0] with { CurrentPp = 1 }] } };
            foreach (int slot in Enumerable.Range(0, single ? 2 : 4).Where(s => (s & 1) == 1))
                bodies[slot] = Kernel.Mon("ARTICUNO", "MIST", 100) with { SpeedStage = 0 };
            var setup = F.Ordered(Kernel.Frame(bodies, single), 0, 1); var pp = setup.Events.OfType<SourcePpCommandEvent>().Single();
            int cost = id == 13 && !single ? 3 : 2;
            Assert.AreEqual(cost, pp.CommandCost); Assert.AreEqual(1, pp.Before); Assert.AreEqual(0, pp.After);
            var marker = setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge"); F.Check(setup.Events, id, single, marker, marker, 0);
            var next = F.CompleteTurn(setup); Assert.AreEqual(0, next.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
            var release = F.Ordered(next, 0, 1);
            foreach (var command in release.Events.OfType<SourcePpCommandEvent>())
            { Assert.AreEqual(cost, command.CommandCost); Assert.AreEqual(0, command.Before); Assert.AreEqual(0, command.After); }
            F.Check(release.Events, id, single, release.Events.OfType<SourceMoveUsedEvent>().Single(), Envelope.Hits(release.Events).First(), 1);
        }
    }
    [TestMethod, DataRow(13), DataRow(130)]
    public void FailedReleaseCannotBorrowSetupOrUnchangedPpToAnimate(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "protected", "immune", "miss", "sleep" })
        {
            var frame = F.Charged(id, single);
            if (branch == "sleep") frame = F.Body(frame, 0, b => b with { Status = 3 });
            if (branch == "miss") frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 });
            foreach (int target in Enumerable.Range(0, frame.BattlerCount).Where(s => (s & 1) == 1))
                frame = F.Body(frame, target, b => branch switch {
                    "protected" => b with { Protected = true }, "immune" => Kernel.Mon("GASTLY", "NIGHT_SHADE", 100) with { SpeedStage = 0 },
                    "miss" => b with { EvasionStage = 12 }, _ => b });
            var result = branch == "miss" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Count() == (id == 13 && !single ? 2 : 1), $"{id}-charge-release-miss") : F.Ordered(frame, 0, 1);
            F.Deny(result.Events, single);
            if (branch == "immune") Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            if (branch == "sleep") Assert.IsFalse(result.Events.Any(e => e is SourceAttackStringEvent or SourceMoveUsedEvent or SourcePpCommandEvent));
        }
    }
    [TestMethod]
    public void RazorWindLaterSuccessfulTargetOwnsTheRootAfterFirstTargetImmunity()
    {
        var frame = F.Body(F.Charged(13, false), 1, _ => Kernel.Mon("GASTLY", "NIGHT_SHADE", 100) with { SpeedStage = 0 });
        var result = F.Ordered(frame, 0, 1); var hit = result.Events.OfType<SourceHpChangedEvent>().Single(h => h.Target.Value == 3);
        Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Target.Value == 1 && e.Effectiveness == WildBattleEffectiveness.NoEffect));
        F.Check(result.Events, 13, false, result.Events.OfType<SourceMoveUsedEvent>().Single(), hit, 1);
    }
    [TestMethod, DataRow("owner"), DataRow("chosen"), DataRow("unchanged"), DataRow("cost"), DataRow("slot"), DataRow("side"), DataRow("before"), DataRow("after"), DataRow("turn"), DataRow("reason"), DataRow("duplicate"), DataRow("missing-pp"), DataRow("sequence")]
    public void SetupRequiresItsOwnUniqueAdjacentPpAndZeroToOneMarker(string mutation)
    {
        foreach (bool single in new[] { false, true }) foreach (int id in new[] { 13, 130 })
        {
            var events = F.Ordered(F.Initial(id, single), 0, 1).Events;
            var pp = events.OfType<SourcePpCommandEvent>().Single(); var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            int p = events.IndexOf(pp), m = events.IndexOf(marker);
            events = mutation switch {
                "owner" => events.SetItem(p, pp with { PpOwner = new(1) }), "chosen" => events.SetItem(p, pp with { ChosenMove = Kernel.Id("METRONOME") }),
                "unchanged" => events.SetItem(p, pp with { After = pp.Before }), "cost" => events.SetItem(p, pp with { CommandCost = 0 }),
                "slot" => events.SetItem(p, pp with { PpSlot = -1 }), "side" => events.SetItem(m, marker with { Side = new(1) }),
                "before" => events.SetItem(m, marker with { Before = 1 }), "after" => events.SetItem(m, marker with { After = 0 }),
                "turn" => events.SetItem(m, marker with { Turn = marker.Turn + 1 }), "reason" => events.SetItem(m, marker with { Reason = "released" }),
                "duplicate" => Shared.Number(events.Insert(m + 1, marker)), "missing-pp" => events.RemoveAt(p),
                "sequence" => events.SetItem(m, marker with { Sequence = pp.Sequence }), _ => throw new InvalidOperationException() };
            F.Deny(events, single);
        }
    }
    [TestMethod, DataRow("missing"), DataRow("duplicate"), DataRow("side"), DataRow("turn"), DataRow("before"), DataRow("after"), DataRow("reason"), DataRow("owner"), DataRow("deducted"), DataRow("cost"), DataRow("sequence")]
    public void ReleasePpExceptionRequiresTheExactUniqueReleasedPrefix(string mutation)
    {
        foreach (bool single in new[] { false, true }) foreach (int id in new[] { 13, 130 })
        {
            var events = F.Ordered(F.Charged(id, single), 0, 1).Events;
            var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge"); var pp = events.OfType<SourcePpCommandEvent>().First();
            int m = events.IndexOf(marker), p = events.IndexOf(pp);
            events = mutation switch {
                "missing" => events.RemoveAt(m), "duplicate" => Shared.Number(events.Insert(m, marker)),
                "side" => events.SetItem(m, marker with { Side = new(1) }), "turn" => events.SetItem(m, marker with { Turn = marker.Turn + 1 }),
                "before" => events.SetItem(m, marker with { Before = 0 }), "after" => events.SetItem(m, marker with { After = 1 }),
                "reason" => events.SetItem(m, marker with { Reason = "move" }), "owner" => events.SetItem(p, pp with { PpOwner = new(1) }),
                "deducted" => events.SetItem(p, pp with { After = pp.Before - 1 }), "cost" => events.SetItem(p, pp with { CommandCost = 0 }),
                "sequence" => events.SetItem(m, marker with { Sequence = marker.Sequence + 1 }), _ => throw new InvalidOperationException() };
            F.Deny(events, single);
        }
    }
    [TestMethod]
    public void TwoActualChargingActorsKeepSeparateOwnersInOnePublicTurnBatch()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = F.Bodies(13); bodies[1] = Kernel.Mon("SQUIRTLE", "SKULL_BASH") with { SpeedStage = 11, AccuracyStage = 12 };
            var first = F.Ordered(Kernel.Frame(bodies, single), 0, 1); Assert.IsNotNull(first.Continuation);
            var second = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, first.State, first.Continuation.Value);
            Assert.IsTrue(second.Succeeded, second.Fault);
            var events = first.Events.AddRange(second.Events); var markers = events.OfType<SourceMtMoonVolatileEvent>().Where(e => e.Volatile == "called-charge").ToArray();
            Assert.AreEqual(2, markers.Length); Assert.AreEqual(new BattlerSlot(1), markers[1].Side);
            foreach (var marker in markers)
            {
                int i = events.IndexOf(marker); var cue = F.Project(events, i, single, true).Dispatch; Assert.IsNotNull(cue);
                Assert.AreEqual(marker.Side, cue.Actor); Assert.AreEqual(marker.Sequence, cue.Sequence);
                Assert.AreEqual(marker.Side.Value == 0 ? 13 : 130, cue.SourceMoveId);
                Assert.AreEqual(((SourcePpCommandEvent)events[i - 1]).Sequence, cue.PpSequence);
            }
        }
    }

    [TestMethod, DataRow(101), DataRow(13), DataRow(130)]
    public void EarlierHitOrReleaseKeepsItsOwnerBeforeTheNextActorsHeaderlessChargeSetup(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = id == 101 ? F.Initial(id, single) : F.Charged(id, single);
            // A level-100 natural Squirtle survives the first hit and acts after
            // the fast tested actor, before the low-speed Splash participants.
            frame = F.Body(frame, 1, _ => Kernel.Mon("SQUIRTLE", "SKULL_BASH", 100) with { SpeedStage = 6, AccuracyStage = 12 });
            var first = F.Ordered(frame, 0, 1); Assert.IsTrue(first.State.Slots[1].Body!.Pokemon.CurrentHp > 0);
            Assert.IsNotNull(first.Continuation);
            var second = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, first.State, first.Continuation.Value);
            Assert.IsTrue(second.Succeeded, second.Fault);
            var charge = second.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge" && e.Reason == "move");
            Assert.AreEqual(new BattlerSlot(1), charge.Side);
            Assert.IsFalse(second.Events.Any(e => e is SourceAttackStringEvent or SourceMoveUsedEvent));
            var events = first.Events.AddRange(second.Events); var used = first.Events.OfType<SourceMoveUsedEvent>().Single();
            var owner = F.Project(events, events.IndexOf(used), single, false).Dispatch;
            Assert.IsNotNull(owner); Assert.AreEqual(id, owner.SourceMoveId); Assert.AreEqual(used.Sequence, owner.Sequence);
            Assert.AreEqual(id == 101 ? null : (byte?)1, owner.MoveTurn); Assert.AreEqual(new BattlerSlot(0), owner.Actor);
            Assert.IsTrue(first.Events.OfType<SourcePpCommandEvent>().Any(e => e.Sequence == owner.PpSequence));
            Assert.IsTrue(Envelope.Hits(first.Events).Any(e => e.Sequence == owner.ResultSequence));
            var setup = F.Project(events, events.IndexOf(charge), single, true).Dispatch;
            Assert.IsNotNull(setup); Assert.AreEqual(130, setup.SourceMoveId); Assert.AreEqual(new BattlerSlot(1), setup.Actor);
            Assert.AreEqual(charge.Sequence, setup.Sequence); Assert.AreEqual((byte?)0, setup.MoveTurn);
            Assert.AreEqual(second.Events.OfType<SourcePpCommandEvent>().Single().Sequence, setup.PpSequence);
        }
    }
    [TestMethod]
    public void RazorWindSetupCannotBorrowAnAlliedTargetPpCommand()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var events = F.Ordered(F.Initial(13, false, actor), actor, actor ^ 1).Events;
            var pp = events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreNotEqual(pp.Actor.Side, pp.Target.Side);
            F.Deny(events.SetItem(events.IndexOf(pp), pp with { Target = pp.Actor.Partner }), false);
        }
    }
    [TestMethod, DataRow(13, "after-pp"), DataRow(13, "after-hit"), DataRow(130, "after-pp"), DataRow(130, "after-hit")]
    public void ReleaseRejectsAnExtraOneToZeroMarkerInsideItsOwnResultSuffix(int id, string position)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = F.Ordered(F.Charged(id, single), 0, 1).Events;
            var release = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge" && e.Before == 1 && e.After == 0 && e.Reason == "released");
            // Preserve every authentic spread-pass receipt. Only insert a second
            // impossible 1-to-0 release, never reject a source 0-to-0 command.
            SourceBattleEvent anchor = position == "after-pp" ? events.OfType<SourcePpCommandEvent>().First() : Envelope.Hits(events).First();
            F.Deny(Shared.Number(events.Insert(events.IndexOf(anchor) + 1, release)), single);
        }
    }
    [TestMethod, DataRow(13), DataRow(130)]
    public void ChargeSetupRejectsAnExtraSameActorPpCommandAfterItsMarker(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = F.Ordered(F.Initial(id, single), 0, 1).Events;
            var pp = events.OfType<SourcePpCommandEvent>().Single();
            var charge = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            var extra = pp with { Before = pp.After, After = Math.Max(0, pp.After - pp.CommandCost) };
            F.Deny(Shared.Number(events.Insert(events.IndexOf(charge) + 1, extra)), single);
        }
    }
}
