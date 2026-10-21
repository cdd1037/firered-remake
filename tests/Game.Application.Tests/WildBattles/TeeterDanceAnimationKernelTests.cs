using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.SupportStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class TeeterDanceAnimationKernelTests
{
    [TestMethod]
    public void NaturalTeeterDanceUsesFirstNumericOtherTargetAtStatusBoundaryAcrossEverySlotAndLayout()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int selected in Enumerable.Range(0, single ? 2 : 4).Where(s => s != actor))
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(298, actor), single), actor, selected);
            var successes = Confusions(result.Events); Assert.AreEqual(single ? 1 : 3, successes.Length);
            CollectionAssert.AreEqual(Enumerable.Range(0, single ? 2 : 4).Where(s => s != actor).ToArray(), successes.Select(s => s.Side.Value).ToArray());
            foreach (var success in successes) { Assert.AreEqual(0, success.Before); Assert.IsTrue(success.After is >= 2 and <= 5); }
            Assert.AreEqual(selected, result.Events.OfType<SourceMoveUsedEvent>().Single().Target.Value);
            Check(result, 298, single, successes[0]);
        }
    }

    [TestMethod]
    public void TeeterDancePublicTurnActionAlsoOwnsOneRootAndOnlyItsFirstSuccessCursor()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(298, actor), single), actor, actor ^ 1);
            var successes = Confusions(result.Events); Assert.IsTrue(successes.Length > 0);
            Check(result, 298, single, successes[0]);
            foreach (var subsequent in successes.Skip(1)) Assert.IsNull(Result(result.Events, result.Events.IndexOf(subsequent), single).Dispatch);
        }
    }

    [TestMethod, DataRow("own-tempo"), DataRow("substitute"), DataRow("already-confused"), DataRow("protect"), DataRow("safeguard"), DataRow("accuracy")]
    public void EarlierTargetFailureDoesNotSuppressLaterNumericSuccessOrReorderItsMessage(string branch)
    {
        // Actor 3 visits 0,1,2. Initial target 2 deliberately differs from first success 1.
        var bodies = Bodies(298, 3);
        bodies[0] = branch switch {
            "own-tempo" => Kernel.Mon("SPINDA", "TACKLE") with { SpeedStage = 0 },
            "substitute" => bodies[0] with { CalledVolatile = new() { SubstituteHp = 1 } },
            "already-confused" => bodies[0] with { ConfusionTurnsRemaining = 3 },
            "protect" => bodies[0] with { Protected = true },
            "accuracy" => bodies[0] with { EvasionStage = 12 }, _ => bodies[0] };
        if (branch == "accuracy")
        {
            bodies[3] = bodies[3] with { AccuracyStage = 0 };
            bodies[1] = bodies[1] with { EvasionStage = 0 }; bodies[2] = bodies[2] with { EvasionStage = 0 };
        }
        var frame = Kernel.Frame(bodies);
        if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(0, frame.Sides[0] with { SafeguardTurnsRemaining = 5 }) };
        var result = branch == "accuracy" ? Envelope.Find(frame, 3, 2,
            r => r.Events.OfType<SourceMoveMissedEvent>().Any(e => e.Target.Value == 0) && Confusions(r.Events).FirstOrDefault()?.Side.Value == 1,
            "Teeter Dance first-target accuracy failure") : Envelope.Resolve(frame, 3, 2);
        var first = Confusions(result.Events).First(); Assert.AreEqual(1, first.Side.Value);
        SourceBattleEvent blocked = branch == "own-tempo" ? result.Events.OfType<SourceAbilityPreventedEvent>().Single(e => e.Target.Value == 0)
            : branch == "accuracy" ? result.Events.OfType<SourceMoveMissedEvent>().Single(e => e.Target.Value == 0)
            : result.Events.OfType<SourceMoveFailedEvent>().Single(e => e.Target.Value == 0);
        Assert.IsTrue(blocked.Sequence < first.Sequence); Assert.IsNull(Result(result.Events, result.Events.IndexOf(blocked), false).Dispatch);
        Assert.IsFalse(Confusions(result.Events).Any(e => e.Side.Value == 0)); Check(result, 298, false, first);
    }

    [TestMethod, DataRow("own-tempo"), DataRow("substitute"), DataRow("already-confused"), DataRow("protect"), DataRow("safeguard")]
    public void AllOtherTargetsPreventedMeansNoStatusCueInEitherLayout(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(298);
            for (int target = 1; target < (single ? 2 : 4); target++) bodies[target] = branch switch {
                "own-tempo" => Kernel.Mon("SPINDA", "TACKLE"),
                "substitute" => bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "already-confused" => bodies[target] with { ConfusionTurnsRemaining = 3 },
                "protect" => bodies[target] with { Protected = true }, _ => bodies[target] };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.Select(s => s with { SafeguardTurnsRemaining = 5 }).ToImmutableArray() };
            var result = Envelope.Resolve(frame); Assert.AreEqual(0, Confusions(result.Events).Length); Check(result, 298, single, null);
        }
    }

    [TestMethod]
    public void TeeterDancePressureCountsEveryOtherBattlerButStillOwnsOnlyOnePpCommand()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        for (int count = 0; count < (single ? 2 : 4); count++)
        {
            var bodies = Bodies(298, actor); var targets = Enumerable.Range(0, single ? 2 : 4).Where(s => s != actor).ToArray();
            foreach (int target in targets.Take(count)) bodies[target] = Pressure();
            var result = Envelope.Resolve(Kernel.Frame(bodies, single), actor, actor ^ 1);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); Assert.AreEqual(1 + count, pp.CommandCost);
            Assert.AreEqual(Math.Max(0, pp.Before - count - 1), pp.After); Check(result, 298, single, Confusions(result.Events)[0]);
        }
    }

    [TestMethod]
    public void LaterConfusionBerryCureRetainsEarlierCommittedFirstSuccess()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(298); bodies[1] = bodies[1] with { HeldItem = "ITEM_PERSIM_BERRY" };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var first = Confusions(result.Events)[0];
            var clear = result.Events.OfType<SourceConfusionChangedEvent>().Single(e => e.Side.Value == 1 && e.Before > 0 && e.After == 0);
            Assert.IsTrue(first.Sequence < clear.Sequence); Assert.AreEqual(0, result.State.Get(new(1)).Body!.ConfusionTurnsRemaining);
            Check(result, 298, single, first); Assert.IsNull(Result(result.Events, result.Events.IndexOf(clear), single).Dispatch);
        }
    }

    [TestMethod]
    public void TeeterDanceAuthenticatesWholeNumericSuccessSetAndRejectsForeignDuplicateAndMalformedReceipts()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(298), single)).Events;
            var first = Confusions(events)[0]; int fi = events.IndexOf(first); var pp = events.OfType<SourcePpCommandEvent>().Single();
            RejectCommonEnvelopeMutations(events, single);
            foreach (var wrong in new[] { first with { Side = new(0) }, first with { Before = 1 }, first with { After = 1 },
                first with { After = 6 }, first with { Turn = first.Turn + 1 }, first with { Sequence = pp.Sequence } }) Deny(events.SetItem(fi, wrong), single);
            // Removing every actual success leaves no receipt that can own the root.
            Deny(events.Where(e => e is not SourceConfusionChangedEvent { Reason: "teeter-dance" }).ToImmutableArray(), single);
            Deny(Number(events.Insert(fi + 1, first)), single);
            var foreign = new SourceMoveFailedEvent(0, first.Turn, new(0), first.Side, Move(298), "safeguard");
            Deny(Number(events.Insert(fi, foreign)), single);
            if (!single)
            {
                var second = Confusions(events)[1]; int si = events.IndexOf(second);
                Deny(events.SetItem(fi, first with { Side = second.Side }).SetItem(si, second with { Side = first.Side }), false);
                Deny(events.SetItem(si, second with { Side = first.Side }), false);
            }
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int ui = events.IndexOf(used);
            Deny(events.SetItem(events.IndexOf(pp), pp with { CommandCost = single ? 3 : 5, After = pp.Before - (single ? 3 : 5) })
                .SetItem(ui, used with { PpAfter = pp.Before - (single ? 3 : 5) }), single);
        }
    }

    [TestMethod]
    public void SinglesStatusProjectionUsesExportedConfusionEnvelopeAndRejectsWrongReasonWithoutFinalState()
    {
        var result = Envelope.Resolve(Kernel.Frame(Bodies(298), true)); var first = Confusions(result.Events)[0]; int fi = result.Events.IndexOf(first);
        var single = Envelope.Single(result.Events, fi);
        var projected = AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On);
        Assert.IsNotNull(projected.Dispatch); Assert.AreEqual(first.Sequence, projected.Dispatch.ResultSequence);
        var exported = (SourceConfusionChangedEvent)single.Events[fi]; Assert.AreEqual("teeter-dance", exported.Reason);
        foreach (string reason in new[] { "move", "countdown", "berry" })
            Assert.IsNull(AnimationDispatchFixture.AtResult(single with { Events = single.Events.SetItem(fi, exported with { Reason = reason }) }, true, SourceBattleScene.On).Dispatch);
    }

    [TestMethod]
    public void ActualMetronomeTeeterDanceOwnsOnlyItsFirstStatusBoundary()
    {
        uint? seed = null; for (uint s = 0; s < 65536; s++) if (WildBattleSourceCallers.Metronome(new(s)).SourceMoveId == 298) { seed = s; break; }
        Assert.IsNotNull(seed);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(298); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 50) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(seed.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(298)));
            Assert.IsTrue(Confusions(result.Events).Length > 0); var outcome = Confusions(result.Events)[0];
            CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), 298, outcome,
                Game.Application.CampaignMoveAnimationBoundary.BeforeStatusResult, outcome.Side);
        }
    }
}
