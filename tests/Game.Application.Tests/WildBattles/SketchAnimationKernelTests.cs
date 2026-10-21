using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SketchAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("SKETCH");
    private static SourceBattleFrame Initial(bool single, int actor = 0, int? selected = null, bool prime = true)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("SMEARGLE", "SKETCH") with { SpeedStage = 12 };
        var frame = Kernel.Frame(bodies, single); int target = selected ?? (actor ^ 1);
        return prime ? Envelope.Resolve(frame, target, target).State : frame;
    }
    private static void Check(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        string before = JsonSerializer.Serialize(events); var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        var copy = events.OfType<SourceMoveCopiedEvent>().Single();
        Assert.IsTrue(AnimationDispatchFixture.Supports(166, Move.Value)); Assert.IsTrue(copy.Permanent);
        Assert.AreEqual(Move, copy.Move); Assert.AreEqual(used.Slot, copy.MoveSlot); Assert.AreEqual(pp.Sequence + 1, copy.Sequence);
        Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(Move, pp.ChosenMove); Assert.AreEqual(Move, pp.CurrentMove);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After); int count = 0;
        for (int i = 0; i < events.Length; i++) foreach (bool result in new[] { false, true })
        {
            var decision = F.Project(events, i, single, result); Assert.AreEqual(decision, F.Project(events, i, single, result));
            Assert.IsNull(F.Project(events, i, single, result, SourceBattleScene.Off).Dispatch);
            if (events[i] != used || result) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(166, cue.SourceMoveId); Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence); Assert.AreEqual(copy.Sequence, cue.ResultSequence);
            Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target); Assert.IsNull(cue.MoveTurn);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(1, count); Assert.AreEqual(before, JsonSerializer.Serialize(events));
        Assert.AreEqual(before, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(before)));
        if (single)
        {
            var exported = events; string json = JsonSerializer.Serialize(exported);
            Assert.AreEqual(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(json)));
            Assert.AreEqual(events, F.Single(events, events.IndexOf(used)).Events);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(F.Single(events, events.IndexOf(used)), false, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void NaturalSketchUsesActualLastPrintedHistoryAcrossAllSlotsAndAllSelectedTargets()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : Enumerable.Range(0, 4).Where(t => t != actor).ToArray())
        {
            var frame = Initial(single, actor, target); var result = F.Ordered(frame, actor, target); Check(result.Events, single);
            var copy = result.Events.OfType<SourceMoveCopiedEvent>().Single(); Assert.AreEqual(Kernel.Id("SPLASH"), copy.CopiedMove);
            Assert.AreEqual(40, copy.Replacement.CurrentPp); Assert.AreEqual(40, copy.Replacement.MaximumPp);
            Assert.AreEqual(copy.Replacement, result.State.Slots[actor].Body!.Pokemon.Moves[0]);
            Assert.AreEqual(copy.Replacement, result.State.Parties[actor & 1].Members[actor / 2].Pokemon.Moves[0]);
            Assert.IsNull(result.State.Slots[actor].Body!.CopyMoves);
        }
    }

    [TestMethod, DataRow(0), DataRow(1), DataRow(2), DataRow(3)]
    public void PermanentCopyFillsBasePpAndRetainsPpUpsRatherThanUsingMimicsFivePp(int ups)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 0, b => b with { Pokemon = b.Pokemon with {
                Moves = [b.Pokemon.Moves[0] with { PpUps = ups }] } });
            var result = F.Ordered(frame, 0, 1); var copy = result.Events.OfType<SourceMoveCopiedEvent>().Single(); Check(result.Events, single);
            Assert.AreEqual(ups, copy.Replacement.PpUps); Assert.AreEqual(40, copy.Replacement.CurrentPp);
            Assert.AreEqual(40 + 8 * ups, copy.Replacement.MaximumPp);
        }
    }

    [TestMethod, DataRow("MIMIC"), DataRow("METRONOME")]
    public void SketchAllowsSourcePermittedMimicAndMetronomeFromActualDonorCommands(string name)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false);
            frame = F.Body(frame, 1, _ => Kernel.Mon(name == "MIMIC" ? "JIGGLYPUFF" : "TOGETIC", name) with { SpeedStage = 0 });
            if (name == "MIMIC") frame = F.Body(frame, 0, b => b with { CalledVolatile = new() { SubstituteHp = 100 } });
            else frame = frame with { Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(150)) };
            var donor = Envelope.Resolve(frame, 1, 0);
            Assert.AreEqual(Kernel.Id(name), donor.State.Slots[1].Registers.History!.LastPrintedMove);
            if (name == "METRONOME") Assert.AreEqual(Kernel.Id("SPLASH"), donor.State.Slots[1].Registers.History!.LastResultingMove);
            else Assert.IsTrue(donor.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "substitute"));
            var result = F.Ordered(donor.State, 0, 1); Check(result.Events, single);
            Assert.AreEqual(Kernel.Id(name), result.Events.OfType<SourceMoveCopiedEvent>().Single().CopiedMove);
        }
    }

    [TestMethod, DataRow("no-history"), DataRow("substitute"), DataRow("unavailable")]
    public void FailedSketchCannotAnimateFromPpOrFinalMoveState(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: branch != "no-history");
            if (branch == "substitute") frame = F.Body(frame, 1, b => b with { CalledVolatile = new() { SubstituteHp = 40 } });
            if (branch == "unavailable")
            {
                frame = F.Body(frame, 1, _ => Kernel.Mon("SMEARGLE", "SKETCH") with { SpeedStage = 0 });
                frame = Envelope.Resolve(frame, 1, 0).State;
                Assert.AreEqual(Kernel.Id("UNAVAILABLE"), frame.Slots[1].Registers.History!.LastPrintedMove);
            }
            // The unavailable donor spent Sketch's only PP. Resolve the tested
            // actor directly rather than selecting that exhausted donor again.
            var events = branch == "unavailable" ? Envelope.Resolve(frame, 0, 1).Events : F.Ordered(frame, 0, 1).Events;
            Assert.IsFalse(events.OfType<SourceMoveCopiedEvent>().Any());
            Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Any()); F.Deny(events, single);
        }
    }

    [TestMethod]
    public void SketchIgnoresAccuracyAndProtectAccordingToItsNoAccuracySourceBranch()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 1, b => b with { Protected = true, EvasionStage = 12 });
            frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 });
            var result = F.Ordered(frame, 0, 1); Check(result.Events, single);
            Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());
        }
    }

    [TestMethod]
    public void SketchCanCopyTheLastPrintedMoveWhileItsDonorIsActuallyChargingFly()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false);
            var flyer = ConditionalRecoveryAnimationKernelFixture.MachineMon("PIDGEY", "HM02", "FLY");
            var tackle = Kernel.Mon("PIDGEY", "TACKLE", 100).Pokemon.Moves[0];
            frame = F.Body(frame, 1, _ => flyer with { SpeedStage = 0, AttackStage = 0, AccuracyStage = 12,
                Pokemon = flyer.Pokemon with { Moves = [tackle, flyer.Pokemon.Moves[0]] } });
            frame = Envelope.Resolve(frame, 1, 0).State;
            var setup = SourceBattleKernel.ResolveAction(Kernel.Mechanics.Catalog, frame,
                new(frame.Slots[1].Occupant!.Value, frame.Revision, 1, new(0)));
            Assert.IsTrue(setup.Succeeded, setup.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(setup.State));
            Assert.IsTrue(setup.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "called-charge" && e.After == 1));
            Assert.AreEqual(Kernel.Id("TACKLE"), setup.State.Slots[1].Registers.History!.LastPrintedMove);
            var result = F.Ordered(setup.State, 0, 1); Check(result.Events, single);
            Assert.AreEqual(Kernel.Id("TACKLE"), result.Events.OfType<SourceMoveCopiedEvent>().Single().CopiedMove);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void SketchUsesItsSelectedSlotAndRejectsAnActuallyPreviouslySketchedMove(bool alreadyKnown)
    {
        foreach (bool single in new[] { false, true })
        {
            var first = Envelope.Resolve(Initial(single), 0, 1);
            Assert.AreEqual(Kernel.Id("SPLASH"), first.Events.OfType<SourceMoveCopiedEvent>().Single().CopiedMove);
            // Preserve the real copied move; restoring species-learnable Sketch
            // is a controlled legal learner fixture, not a replayed relearning route.
            var sketch = Kernel.Mon("SMEARGLE", "SKETCH").Pokemon.Moves[0];
            var frame = F.Body(first.State, 0, b => b with { Pokemon = b.Pokemon with { Moves = b.Pokemon.Moves.Add(sketch) } });
            if (!alreadyKnown)
            {
                frame = F.Body(frame, 1, _ => Kernel.Mon("PIDGEY", "GUST") with { SpeedStage = 0, SpecialAttackStageDelta = -6, AccuracyStage = 12 });
                frame = Envelope.Resolve(frame, 1, 0).State;
            }
            var result = F.Ordered(frame, 0, 1, moveSlot: 1);
            if (alreadyKnown)
            {
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "copy-already-known"));
                Assert.IsFalse(result.Events.OfType<SourceMoveCopiedEvent>().Any()); F.Deny(result.Events, single);
            }
            else
            {
                Check(result.Events, single); var copy = result.Events.OfType<SourceMoveCopiedEvent>().Single();
                Assert.AreEqual(1, copy.MoveSlot); Assert.AreEqual(Kernel.Id("GUST"), copy.CopiedMove);
                Assert.AreEqual(Kernel.Id("SPLASH"), result.State.Parties[0].Members[0].Pokemon.Moves[0].Move);
                Assert.AreEqual(copy.Replacement, result.State.Parties[0].Members[0].Pokemon.Moves[1]);
            }
        }
    }

    [TestMethod]
    public void ActuallyTransformedDittoCannotPermanentlySketch()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single, prime: false);
            frame = F.Body(frame, 0, _ => Kernel.Mon("DITTO", "TRANSFORM") with { SpeedStage = 12 });
            frame = F.Body(frame, 1, _ => Kernel.Mon("SMEARGLE", "SKETCH") with { SpeedStage = 0 });
            var transformed = Envelope.Resolve(frame, 0, 1); Assert.IsNotNull(transformed.State.Slots[0].Body!.Transform);
            frame = F.Body(transformed.State, 0, b => b with { SpeedStage = 12 });
            var result = F.Ordered(frame, 0, 1); Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "copy-transformed"));
            Assert.IsFalse(result.Events.OfType<SourceMoveCopiedEvent>().Any()); F.Deny(result.Events, single);
            Assert.AreEqual(Kernel.Id("TRANSFORM"), result.State.Parties[0].Members[0].Pokemon.Moves[0].Move);
        }
    }

    [TestMethod, DataRow("permanent"), DataRow("actor"), DataRow("target"), DataRow("move"), DataRow("slot"),
        DataRow("replacement"), DataRow("pp"), DataRow("max-pp"), DataRow("ups"), DataRow("missing"), DataRow("duplicate"),
        DataRow("turn"), DataRow("sequence"), DataRow("owner"), DataRow("called")]
    public void SketchRejectsMalformedAndUnownedPermanentCopyReceipts(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = F.Ordered(Initial(single), 0, 1).Events; var copy = events.OfType<SourceMoveCopiedEvent>().Single();
            int i = events.IndexOf(copy); var pp = events.OfType<SourcePpCommandEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
            events = mutation switch {
                "permanent" => events.SetItem(i, copy with { Permanent = false }), "actor" => events.SetItem(i, copy with { Actor = new(1) }),
                "target" => events.SetItem(i, copy with { Target = new(0) }), "move" => events.SetItem(i, copy with { Move = Kernel.Id("MIMIC") }),
                "slot" => events.SetItem(i, copy with { MoveSlot = 1 }), "replacement" => events.SetItem(i, copy with { Replacement = copy.Replacement with { Move = Kernel.Id("TACKLE") } }),
                "pp" => events.SetItem(i, copy with { Replacement = copy.Replacement with { CurrentPp = 5 } }),
                "max-pp" => events.SetItem(i, copy with { Replacement = copy.Replacement with { MaximumPp = 39 } }),
                "ups" => events.SetItem(i, copy with { Replacement = copy.Replacement with { PpUps = 4 } }),
                "missing" => Shared.Number(events.RemoveAt(i)), "duplicate" => Shared.Number(events.Insert(i + 1, copy)),
                "turn" => events.SetItem(i, copy with { Turn = copy.Turn + 1 }), "sequence" => events.SetItem(i, copy with { Sequence = pp.Sequence }),
                "owner" => events.SetItem(events.IndexOf(pp), pp with { PpOwner = new(1) }),
                _ => events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("SLEEP_TALK") }) };
            F.Deny(events, single);
        }
    }

    [TestMethod, DataRow("SKETCH"), DataRow("STRUGGLE"), DataRow("NONE"), DataRow("UNAVAILABLE")]
    public void SketchRejectsForbiddenOrUndefinedReplacementEvenWhenTheMarkerClaimsSuccess(string name)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = F.Ordered(Initial(single), 0, 1).Events; var copy = events.OfType<SourceMoveCopiedEvent>().Single();
            var move = Kernel.Id(name); int pp = Kernel.Mechanics.Catalog.Moves.TryGetValue(move, out var definition) ? definition.Pp : 1;
            F.Deny(events.SetItem(events.IndexOf(copy), copy with { CopiedMove = move, Replacement = new(move, pp, pp) }), single);
        }
        Assert.IsFalse(WildBattleSourceCallers.IsDirectMetronomeChoice(166), "Pinned source forbids Sketch as a Metronome leaf.");
    }

    [TestMethod]
    public void PublicEventDrainProjectsPermanentReplacementOnlyAfterTheOwnedCopyReceipt()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single); var result = F.Ordered(frame, 0, 1); Check(result.Events, single);
            var display = CampaignBattleDisplayBodies.FromSlots(frame.Slots); int steps = 0, copies = 0;
            while (true)
            {
                foreach (var row in result.Events)
                {
                    display = display.Apply(row);
                    if (row is SourcePpCommandEvent { Actor.Value: 0 })
                    { Assert.AreEqual(Move, display.Bodies[0].Pokemon.Moves[0].Move); Assert.AreEqual(0, display.Bodies[0].Pokemon.Moves[0].CurrentPp); }
                    if (row is SourceMoveCopiedEvent copy)
                    { copies++; Assert.AreEqual(copy.Replacement, display.Bodies[0].Pokemon.Moves[0]); Assert.IsNull(display.Bodies[0].CopyMoves); }
                }
                if (result.CompletedCleanTurn) break;
                Assert.IsTrue(++steps <= 32); Assert.IsNotNull(result.Continuation);
                result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
                Assert.IsTrue(result.Succeeded, result.Fault);
            }
            Assert.AreEqual(1, copies); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            Assert.AreEqual(JsonSerializer.Serialize(result.State.Parties[0].Members[0].Pokemon.Moves), JsonSerializer.Serialize(display.Bodies[0].Pokemon.Moves));
        }
    }
}
