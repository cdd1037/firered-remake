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
public sealed class EncoreAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("ENCORE");
    private static SourceBattleFrame Initial(bool single, int actor = 0, int? historyTarget = null)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("CLEFAIRY", "ENCORE") with { SpeedStage = 12, AccuracyStage = 12 };
        var frame = Kernel.Frame(bodies, single);
        return historyTarget is {} target ? Envelope.Resolve(frame, target, target).State : frame;
    }
    private static void Check(ImmutableArray<SourceBattleEvent> rows, bool single)
    {
        string before = JsonSerializer.Serialize(rows); var used = rows.OfType<SourceMoveUsedEvent>().Single();
        var attack = rows.OfType<SourceAttackStringEvent>().Single(); var pp = rows.OfType<SourcePpCommandEvent>().Single();
        var result = rows.OfType<SourceEncoreChangedEvent>().Single(); Assert.IsTrue(AnimationDispatchFixture.Supports(227, Move.Value));
        Assert.AreEqual("EFFECT_ENCORE", Kernel.Mechanics.Catalog.Moves[Move].Effect); Assert.AreEqual(Move, pp.ChosenMove); Assert.AreEqual(Move, pp.CurrentMove);
        Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.AreEqual(used.Target, result.Side); Assert.AreEqual(0, result.Before); Assert.IsTrue(result.After is >= 3 and <= 6);
        Assert.AreEqual(0, result.Slot); Assert.AreEqual("move", result.Reason); Assert.AreEqual(pp.Sequence + 1, result.Sequence);
        int roots = 0;
        for (int i = 0; i < rows.Length; i++) foreach (bool atResult in new[] { false, true })
        {
            var decision = F.Project(rows, i, single, atResult); Assert.AreEqual(decision, F.Project(rows, i, single, atResult));
            Assert.IsNull(F.Project(rows, i, single, atResult, SourceBattleScene.Off).Dispatch); Assert.IsNull(F.Wave(rows, i, single));
            if (rows[i] != used || atResult) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
            Assert.AreEqual(227, cue.SourceMoveId); Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence); Assert.AreEqual(result.Sequence, cue.ResultSequence);
            Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target); Assert.IsFalse(cue.HitsSubstitute);
            Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveTurn);
        }
        Assert.AreEqual(1, roots); Assert.AreEqual(before, JsonSerializer.Serialize(rows));
        if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(F.Single(rows, rows.IndexOf(used)), false, SourceBattleScene.On).Dispatch);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ActualPriorMoveOwnsEncoreAcrossAllPositionsAndSelectedAlliesEvenBehindSubstitute(bool substitute)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : Enumerable.Range(0, 4).Where(t => t != actor).ToArray())
        {
            var frame = Initial(single, actor, target);
            if (substitute) frame = F.Body(frame, target, b => b with { CalledVolatile = new() { SubstituteHp = 1 } });
            var result = F.Ordered(frame, actor, target); string before = JsonSerializer.Serialize(result); Check(result.Events, single);
            Assert.AreEqual(Kernel.Id("SPLASH"), result.State.Slots[target].Body!.EncoredMove); Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(3), DataRow(4), DataRow(5), DataRow(6)]
    public void AllFourActualCommandDurationsShareTheSuccessBoundary(int duration)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = F.Find(Initial(single, historyTarget: 1), 0, 1,
                r => r.Events.OfType<SourceEncoreChangedEvent>().Any(e => e.After == duration), "Encore duration=" + duration);
            Check(result.Events, single);
        }
    }

    [TestMethod, DataRow("no-history"), DataRow("already"), DataRow("no-pp"), DataRow("forbidden"), DataRow("protect"), DataRow("miss")]
    public void FailedEncoreNeverAnimatesFromPpOrExistingFinalEncoreState(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, historyTarget: branch is "no-history" or "forbidden" ? null : 1);
            if (branch == "already") frame = Envelope.Resolve(frame).State;
            if (branch == "no-pp") frame = F.Body(frame, 1, b => b with { Pokemon = b.Pokemon with { Moves = [b.Pokemon.Moves[0] with { CurrentPp = 0 }] } });
            if (branch == "forbidden")
            {
                frame = F.Body(frame, 1, _ => Kernel.Mon("CLEFAIRY", "ENCORE") with { SpeedStage = 0 });
                frame = Envelope.Resolve(frame, 1, 0).State;
                Assert.AreEqual(Move, frame.Slots[1].Body!.LastUsedMove);
            }
            if (branch == "protect") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "miss") { frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 }); frame = F.Body(frame, 1, b => b with { EvasionStage = 12 }); }
            // The exhausted donor is not a selectable ordinary turn action.
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Encore accuracy miss") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceEncoreChangedEvent>().Any(e => e.Reason == "move"));
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count()); F.Deny(result.Events, single);
        }
    }

    [TestMethod]
    public void RealPressureLastPpAndLaterCountdownRemainSeparateReceipts()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 1, _ => Kernel.Mon("DUSCLOPS", "LEER", 100) with { SpeedStage = 0 });
            frame = Envelope.Resolve(frame, 1, 0).State;
            frame = F.Body(frame, 0, b => b with { Pokemon = b.Pokemon with { Moves = [b.Pokemon.Moves[0] with { CurrentPp = 1 }] } });
            var result = F.Ordered(frame, 0, 1); Check(result.Events, single);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); Assert.AreEqual(2, pp.CommandCost); Assert.AreEqual(0, pp.After);
            bool sawCountdown = false; int steps = 0;
            while (!result.CompletedCleanTurn)
            {
                Assert.IsTrue(++steps <= 32); Assert.IsNotNull(result.Continuation);
                result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value); Assert.IsTrue(result.Succeeded, result.Fault);
                foreach (var countdown in result.Events.OfType<SourceEncoreChangedEvent>())
                { Assert.AreEqual("countdown", countdown.Reason); Assert.AreEqual(countdown.Before - 1, countdown.After); sawCountdown = true; }
                CallerPreludeAnimationKernelFixture.DenyLeaf(result.Events, single, 227);
            }
            Assert.IsTrue(sawCountdown);
        }
    }

    [TestMethod, DataRow("missing"), DataRow("duplicate"), DataRow("owner"), DataRow("slot"), DataRow("before"), DataRow("after"), DataRow("countdown"), DataRow("pp")]
    public void OnlyTheUniqueCommandOwnedEncoreSuccessCanAuthenticateTheRoot(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single, historyTarget: 1), 0, 1).Events; var marker = rows.OfType<SourceEncoreChangedEvent>().Single();
            var pp = rows.OfType<SourcePpCommandEvent>().Single(); int at = rows.IndexOf(marker);
            var changed = mutation switch {
                "missing" => Numbering.Number(rows.Remove(marker)), "duplicate" => Numbering.Number(rows.Insert(at + 1, marker)),
                "owner" => rows.SetItem(at, marker with { Side = new(0) }), "slot" => rows.SetItem(at, marker with { Slot = 4 }),
                "before" => rows.SetItem(at, marker with { Before = 1 }), "after" => rows.SetItem(at, marker with { After = 7 }),
                "countdown" => rows.SetItem(at, marker with { Before = marker.After + 1, Reason = "countdown" }),
                _ => rows.SetItem(rows.IndexOf(pp), pp with { PpOwner = new(1) }) };
            F.Deny(changed, single);
        }
    }

    [TestMethod]
    public void ActualCalledEncoreCannotReplayTheDirectRootAndRequiresCurrentMetadata()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single); foreach (int target in single ? new[] { 1 } : new[] { 1, 3 }) frame = Envelope.Resolve(frame, target, target).State;
            frame = F.Body(frame, 0, _ => Kernel.Mon("CLEFAIRY", "METRONOME") with { SpeedStage = 12, AccuracyStage = 12 });
            var called = Envelope.Resolve(frame with { Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(227)) });
            Assert.AreEqual(Move, called.Events.OfType<SourceCallerSelectionEvent>().Single().Result); Assert.AreEqual(1, called.Events.OfType<SourceEncoreChangedEvent>().Count());
            CallerPreludeAnimationKernelFixture.DenyLeaf(called.Events, single, 227);
            var rows = F.Ordered(Initial(single, historyTarget: 1), 0, 1).Events; int at = rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());
            var definitions = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, definitions.Remove(Move), definitions.SetItem(Move, definitions[Move] with { SourceMoveId = 1 }),
                definitions.SetItem(Move, definitions[Move] with { Power = 1 }), definitions.SetItem(Move, definitions[Move] with { Target = "MOVE_TARGET_USER" }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }
}
