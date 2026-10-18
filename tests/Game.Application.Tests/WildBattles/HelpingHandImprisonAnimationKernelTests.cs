using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.SupportStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class HelpingHandImprisonAnimationKernelTests
{
    [TestMethod]
    public void HelpingHandNaturalLearnerRetargetsOnlyVisualCueToPartnerInEveryPosition()
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool pressure in new[] { false, true })
        {
            var bodies = Bodies(270, actor); if (pressure) bodies[actor ^ 2] = Pressure();
            var result = Envelope.Resolve(Kernel.Frame(bodies), actor, actor); var marker = Setup(result.Events, 270);
            Assert.IsNotNull(marker); var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(actor, used.Target.Value); Assert.AreEqual(actor, pp.Target.Value); Assert.AreEqual(1, pp.CommandCost);
            Assert.AreEqual(actor ^ 2, marker.Side.Value); Assert.AreEqual(0, marker.Before); Assert.AreEqual(1, marker.After);
            Assert.AreEqual(pp.Sequence + 1, marker.Sequence); Assert.AreEqual(result.Events.IndexOf(pp) + 1, result.Events.IndexOf(marker));
            Check(result, 270, false, marker);
            var ordered = Kernel.Ordered(Kernel.Frame(Bodies(270, actor)), actor, actor); Check(ordered, 270, false, Setup(ordered.Events, 270));
        }
    }

    [TestMethod]
    public void HelpingHandSinglesFailsAndExistingActorOrPartnerFlagCannotBorrowSuccessfulState()
    {
        for (int actor = 0; actor < 2; actor++)
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(270, actor), true), actor, actor);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "no-ally-in-single-battle"));
            Assert.IsNull(Setup(result.Events, 270)); Check(result, 270, true, null);
        }
        for (int actor = 0; actor < 4; actor++) foreach (int flagged in new[] { actor, actor ^ 2 })
        {
            var frame = Kernel.Frame(Bodies(270, actor)); var cell = frame.Get(new(flagged));
            frame = frame with { Slots = frame.Slots.SetItem(flagged, cell with { Registers = cell.Registers with { HelpingHand = true } }) };
            var result = Envelope.Resolve(frame, actor, actor);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "helping-hand-unavailable"));
            Check(result, 270, false, null);
        }
    }

    [TestMethod]
    public void HelpingHandReceiptRequiresAdjacentUniquePartnerSetupAndOriginalSelfPp()
    {
        var events = Envelope.Resolve(Kernel.Frame(Bodies(270)), 0, 0).Events; var marker = Setup(events, 270)!;
        int mi = events.IndexOf(marker); var pp = events.OfType<SourcePpCommandEvent>().Single(); int pi = events.IndexOf(pp);
        RejectCommonEnvelopeMutations(events, false); Deny(events.RemoveAt(mi), false); Deny(Number(events.Insert(mi + 1, marker)), false);
        foreach (var wrong in new[] { marker with { Side = new(0) }, marker with { Side = new(1) }, marker with { Before = 1 },
            marker with { After = 0 }, marker with { Volatile = "follow-me" }, marker with { Reason = "turn-end" },
            marker with { Turn = marker.Turn + 1 }, marker with { Sequence = pp.Sequence } }) Deny(events.SetItem(mi, wrong), false);
        Deny(Number(events.Insert(mi, new SourceMtMoonVolatileEvent(0, marker.Turn, new(0), "unrelated", 0, 1, "move"))), false);
        Deny(events.SetItem(pi, pp with { Target = marker.Side }), false);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        Deny(events.SetItem(pi, pp with { CommandCost = 2, After = pp.Before - 2 })
            .SetItem(events.IndexOf(used), used with { PpAfter = pp.Before - 2 }), false);
        Assert.IsFalse(WildBattleSourceCallers.IsDirectMetronomeChoice(270), "Do not invent a forbidden Metronome leaf.");
    }

    [TestMethod]
    public void ImprisonNaturalLearnerOwnsSelfSetupAcrossBothLayoutsAndEveryPosition()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(286, actor), single), actor, actor);
            var marker = Setup(result.Events, 286); Assert.IsNotNull(marker); Assert.AreEqual(actor, marker.Side.Value);
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Check(result, 286, single, marker);
        }
    }

    // Original C: battle_util.c:105-134 scans each opposing Pressure holder, then
    // the FIRST learned MOVE_IMPRISON slot, saturating at zero independently of the chosen slot.
    // battle_script_commands.c:9019-9056 checks already-imprisoning BEFORE this charge,
    // and checks shared moves AFTER it. The kernel aggregates those deductions in one extra PP receipt.
    [TestMethod]
    public void ImprisonPressureUsesOptionalSecondCommandAtEveryNaturalLearnedSlotIncludingZeroPpAfterPrimary()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        for (int selected = 0; selected < 4; selected++) foreach (int firstBefore in new[] { 1, 2, 3 })
        for (int pressureCount = 1; pressureCount <= (single ? 1 : 2); pressureCount++)
        {
            var bodies = Bodies(286, actor); var names = new List<string> { "GROWL", "PSYCHIC", "DOUBLE_TEAM" };
            names.Insert(selected, "IMPRISON"); bodies[actor] = NaturalMoves(bodies[actor], names.ToArray());
            int before = firstBefore;
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with {
                Moves = bodies[actor].Pokemon.Moves.SetItem(selected, bodies[actor].Pokemon.Moves[selected] with { CurrentPp = before }) } };
            bodies[actor ^ 1] = Pressure(); if (pressureCount == 2) bodies[actor ^ 3] = Pressure();
            var result = ResolveSlot(Kernel.Frame(bodies, single), actor, actor, selected);
            var commands = result.Events.OfType<SourcePpCommandEvent>().ToArray(); Assert.AreEqual(2, commands.Length);
            var first = commands[0]; var extra = commands[1]; Assert.AreEqual(selected, first.PpSlot); Assert.AreEqual(1, first.CommandCost);
            Assert.AreEqual(actor, extra.Actor.Value); Assert.AreEqual(actor, extra.PpOwner.Value); Assert.AreEqual(actor, extra.Target.Value);
            Assert.AreEqual(selected, extra.PpSlot); Assert.AreEqual(pressureCount, extra.CommandCost);
            Assert.AreEqual(first.After, extra.Before); Assert.AreEqual(Math.Max(0, extra.Before - pressureCount), extra.After);
            Assert.AreEqual(Move(286), extra.CurrentMove); Assert.AreEqual(Move(286), extra.ChosenMove);
            Assert.AreEqual(first.Sequence + 1, extra.Sequence); Assert.AreEqual(first.RngCursor, extra.RngCursor);
            var marker = Setup(result.Events, 286); Assert.IsNotNull(marker); Assert.AreEqual(extra.Sequence + 1, marker.Sequence);
            Check(result, 286, single, marker);
        }
    }

    [TestMethod]
    public void DuplicatePermanentImprisonSlotsRemainRejectedRatherThanMasqueradingAsNaturalEvidence()
    {
        var duplicate = NaturalMoves(Kernel.Mon("GARDEVOIR", "IMPRISON"), "IMPRISON", "IMPRISON", "PSYCHIC");
        Assert.AreEqual("pokemon_move_duplicate", Game.Domain.Pokemon.SourcePokemonValidator.ValidatePermanent(Kernel.Mechanics, duplicate.Pokemon));
    }

    [TestMethod]
    public void ImprisonFailuresRespectPressureOrderingAndCannotAnimateExistingState()
    {
        foreach (bool single in new[] { false, true }) foreach (bool already in new[] { false, true })
        {
            var bodies = Bodies(286); bodies[0] = NaturalMoves(bodies[0], "IMPRISON") with { Imprison = already };
            bodies[1] = Pressure(); var result = Envelope.Resolve(Kernel.Frame(bodies, single), 0, 0);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == (already ? "already-imprisoning" : "no-shared-move")));
            Assert.AreEqual(already ? 1 : 2, result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsNull(Setup(result.Events, 286)); Check(result, 286, single, null);
        }
    }

    [TestMethod]
    public void ImprisonRejectsMalformedSetupAndPressureReceiptsWithoutInventingSameSlotRequirement()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(286); bodies[1] = Pressure(); var events = Envelope.Resolve(Kernel.Frame(bodies, single), 0, 0).Events;
            var marker = Setup(events, 286)!; int mi = events.IndexOf(marker);
            var commands = events.OfType<SourcePpCommandEvent>().ToArray(); var extra = commands[1]; int ei = events.IndexOf(extra);
            RejectCommonEnvelopeMutations(events, single); Deny(events.RemoveAt(mi), single); Deny(Number(events.Insert(mi + 1, marker)), single);
            foreach (var wrong in new[] { marker with { Side = new(1) }, marker with { Before = 1 }, marker with { After = 0 },
                marker with { Reason = "turn-end" }, marker with { Volatile = "helping-hand" }, marker with { Turn = marker.Turn + 1 } }) Deny(events.SetItem(mi, wrong), single);
            foreach (var wrong in new[] { extra with { Actor = new(1) }, extra with { PpOwner = new(1) }, extra with { Target = new(1) },
                extra with { PpSlot = 1 }, extra with { CommandCost = 0 }, extra with { CommandCost = 3 },
                extra with { Before = commands[0].Before }, extra with { After = extra.Before }, extra with { CurrentMove = Kernel.Id("GROWL") },
                extra with { ChosenMove = Kernel.Id("METRONOME") }, extra with { Turn = extra.Turn + 1 }, extra with { RngCursor = extra.RngCursor + 1 } }) Deny(events.SetItem(ei, wrong), single);
            Deny(Number(events.Insert(ei + 1, extra)), single);
            Deny(Number(events.Insert(mi, new SourceMtMoonVolatileEvent(0, marker.Turn, new(0), "unrelated", 0, 1, "move"))), single);
        }
    }

    [TestMethod]
    public void ActualMetronomeImprisonOwnsItsCommittedSetupRoot()
    {
        uint? seed = null; for (uint s = 0; s < 65536; s++) if (WildBattleSourceCallers.Metronome(new(s)).SourceMoveId == 286) { seed = s; break; }
        Assert.IsNotNull(seed);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(286); bodies[0] = NaturalMoves(Kernel.Mon("TOGETIC", "METRONOME", 50), "METRONOME", "GROWL");
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(seed.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(286)));
            Assert.IsNotNull(Setup(result.Events, 286)); CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), 286, Setup(result.Events, 286)!);
        }
    }
}
