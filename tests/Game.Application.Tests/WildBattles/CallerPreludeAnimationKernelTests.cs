using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CallerPreludeAnimationKernelTests
{
    [TestMethod, DataRow(118), DataRow(214)]
    public void NaturalCallerPreludeOwnsItsExactBoundaryInEverySinglesAndDoublesPosition(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int selected in new[] { actor, actor ^ 1 })
        {
            var result = Envelope.Resolve(Shared.Frame(id, single, actor), actor, selected);
            Shared.Check(result, id, single);
            Shared.DenyLeaf(result.Events, single, 33);
            Assert.AreEqual(1, result.Events.OfType<SourceCallerSelectionEvent>().Count());
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void ActualCallerPpOwnershipPreservesPressureAndSleepTalkNominalLeafCost(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(id); foreach (int slot in new[] { 1, 3 }) bodies[slot] = Kernel.Mon("ARTICUNO", "MIST", 100);
            var frame = Shared.Prepare(Kernel.Frame(bodies, single), id); var result = Envelope.Resolve(frame);
            Shared.Check(result, id, single);
            var commands = result.Events.OfType<SourcePpCommandEvent>().ToArray();
            Assert.AreEqual(id == 118 ? 1 : 2, commands.Length);
            foreach (var pp in commands) { Assert.AreEqual(new BattlerSlot(0), pp.PpOwner); Assert.AreEqual(2, pp.CommandCost); Assert.AreEqual(Shared.Move(id), pp.ChosenMove); }
            var leaf = commands.Single(p => p.CurrentMove == Kernel.Id("TACKLE"));
            Assert.AreEqual(id == 118 ? 0 : 1, leaf.PpSlot);
            Assert.AreEqual(id == 118 ? leaf.Before - 2 : leaf.Before, leaf.After);
            Assert.AreEqual(frame.Slots[0].Body!.Pokemon.Moves[0].CurrentPp - 2, result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
            if (id == 214) Assert.AreEqual(frame.Slots[0].Body!.Pokemon.Moves[1].CurrentPp, result.State.Slots[0].Body!.Pokemon.Moves[1].CurrentPp);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void CallerPreludeSurvivesTheCommittedLeafsProtectionFailure(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(id); foreach (int slot in new[] { 1, 3 }) bodies[slot] = bodies[slot] with { Protected = true };
            var result = Envelope.Resolve(Shared.Prepare(Kernel.Frame(bodies, single), id));
            Assert.IsTrue(result.Events.Any(e => e is SourceMoveFailedEvent f && f.Move == Kernel.Id("TACKLE") || e is SourceMoveMissedEvent m && m.Move == Kernel.Id("TACKLE")));
            Shared.Check(result, id, single);
        }
    }

    [TestMethod]
    public void SleepTalkWithoutEligibleLearnedMovesSpendsOnlyCallerPpAndHasNoPrelude()
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Envelope.Resolve(Shared.Frame(214, single, sleepingLeaf: null));
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Shared.Move(214) && e.Reason == "no-callable-move"));
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsFalse(result.Events.Any(e => e is SourceSleepTalkEvent or SourceCallerSelectionEvent or SourceCalledMoveTransitionEvent));
            Shared.DenyPrelude(result.Events, single, 214);
        }
    }

    [TestMethod, DataRow(0), DataRow(1)]
    public void AwakeAndJustWokenSleepTalkFailBeforeThePrelude(int initialSleep)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(214); bodies[0] = bodies[0] with { Status = (uint)initialSleep };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single));
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "user-awake"));
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsFalse(result.Events.Any(e => e is SourceCallerSelectionEvent or SourceCalledMoveTransitionEvent));
            Shared.DenyPrelude(result.Events, single, 214);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void ExhaustedChosenCallerPpFailsBeforeAnyPreludeOrSelection(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(id); var actor = bodies[0];
            bodies[0] = actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(0, actor.Pokemon.Moves[0] with { CurrentPp = 0 }) } };
            var result = Envelope.Resolve(Shared.Prepare(Kernel.Frame(bodies, single), id));
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Shared.Move(id) && e.Reason == "no-pp"));
            Assert.IsFalse(result.Events.Any(e => e is SourcePpCommandEvent or SourceCallerSelectionEvent or SourceCalledMoveTransitionEvent));
            Shared.DenyPrelude(result.Events, single, id);
        }
    }

    [TestMethod]
    public void SleepTalkCanSelectZeroPpLeafButItsLaterNoPpFailureDoesNotErasePrelude()
    {
        // Cmd_trychoosesleeptalkmove excludes MOVE_LIMITATION_PP, but the called
        // attackcanceler still tests the selected slot before its own ppreduce.
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(214); var actor = bodies[0];
            bodies[0] = actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(1, actor.Pokemon.Moves[1] with { CurrentPp = 0 }) } };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single));
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Kernel.Id("TACKLE") && e.Reason == "no-pp"));
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Shared.Check(result, 214, single);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void ActualTransformKeepsTheCallerPreludeOwnedByTheTransformedActor(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(id, 1); bodies[0] = Kernel.Mon("DITTO", "TRANSFORM", 100);
            var before = Kernel.Frame(bodies, single); var transformed = Envelope.Resolve(before);
            Assert.AreEqual(1, transformed.Events.OfType<SourceTransformedEvent>().Count());
            var frame = transformed.State; var actor = frame.Slots[0].Body!; Assert.IsNotNull(actor.Transform);
            if (id == 214)
            {
                // Controlled sleep on the already committed Transform state; retain
                // its original Ditto party identity and copied battle move slots.
                actor = actor with { Status = 3 };
                var party = frame.Parties[0];
                frame = frame with { Slots = frame.Slots.SetItem(0, frame.Slots[0] with { Body = actor }),
                    Parties = frame.Parties.SetItem(0, party with { Members = party.Members.SetItem(0, party.Members[0] with { Status = 3 }) }) };
            }
            Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
            var result = Envelope.Resolve(Shared.Prepare(frame, id)); Shared.Check(result, id, single);
            Assert.IsNotNull(result.State.Slots[0].Body!.Transform);
            Assert.AreEqual(new SpeciesId("pret:SPECIES_DITTO"), result.State.Parties[0].Members[0].Pokemon.Species);
            var display = CampaignBattleDisplayBodies.FromSlots(before.Slots);
            foreach (var receipt in transformed.Events) display = display.Apply(receipt);
            Assert.IsNotNull(display.Bodies[0]!.Transform); Assert.AreEqual(Shared.Move(id), display.Bodies[0]!.Pokemon.Moves[0].Move);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void PreludeDoesNotBorrowAnyFutureLeafPpOrOutcome(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Envelope.Resolve(Shared.Frame(id, single)); var events = result.Events;
            int end = events.IndexOf(events.OfType<SourceCalledMoveTransitionEvent>().Single());
            var prefix = events.Take(end + 1).ToImmutableArray(); var cue = Shared.Check(prefix, prefix, id, single);
            if (id == 118) Assert.IsNull(cue.PpSequence);
            else Assert.AreEqual(prefix.OfType<SourcePpCommandEvent>().Single().Sequence, cue.PpSequence);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void CallerPreludeRejectsMissingForeignOrCalledAttackHeaders(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Shared.Frame(id, single)).Events;
            var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == Shared.Move(id)); int at = events.IndexOf(attack);
            foreach (var bad in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = Kernel.Id("ASSIST") },
                attack with { CurrentMove = Kernel.Id("ASSIST") }, attack with { Turn = attack.Turn + 1 } })
                Shared.DenyPrelude(events.SetItem(at, bad), single, id);
            Shared.DenyPrelude(events.RemoveAt(at), single, id);
            if (id == 214)
            {
                var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == Shared.Move(id)); int ui = events.IndexOf(used);
                foreach (var bad in new[] { used with { Actor = new(1) }, used with { Slot = -1 }, used with { Slot = 4 },
                    used with { Turn = used.Turn + 1 }, used with { Sequence = attack.Sequence } })
                    Shared.DenyPrelude(events.SetItem(ui, bad), single, id);
            }
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void PreludeRejectsMissingDuplicateForeignOrUnorderedSelection(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Shared.Frame(id, single)).Events;
            var selected = events.OfType<SourceCallerSelectionEvent>().Single(); int at = events.IndexOf(selected);
            foreach (var bad in new[] { selected with { Actor = new(1) }, selected with { Caller = Kernel.Id("ASSIST") },
                selected with { Result = null }, selected with { Result = Kernel.Id("REST") }, selected with { Turn = selected.Turn + 1 },
                selected with { Sequence = events[at - 1].Sequence } }) Shared.DenyPrelude(events.SetItem(at, bad), single, id);
            Shared.DenyPrelude(events.RemoveAt(at), single, id);
            Shared.DenyPrelude(Number(events.Insert(at + 1, selected)), single, id);
            Shared.DenyPrelude(Number(events.SetItem(at, events[at + 1]).SetItem(at + 1, selected)), single, id);
            if (id == 214) Shared.DenyPrelude(events.SetItem(at, selected with { CandidateSourceMoveIds = [156] }), single, id);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void PreludeRequiresExactTypedTransitionFlagsAndOwnership(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Shared.Frame(id, single)).Events;
            var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single(); int at = events.IndexOf(transition); var own = transition.Invocation;
            foreach (var bad in new[] { own with { Actor = new(1) }, own with { PpOwner = new(1) }, own with { OriginalChosenMove = Kernel.Id("ASSIST") },
                own with { ChosenMove = Kernel.Id("ASSIST") }, own with { CurrentMove = Kernel.Id("REST") }, own with { ChosenSlot = -1 },
                own with { PpSlot = 4 }, own with { CancellerPhase = WildBattleAttackCancellerPhase.Start },
                own with { Flags = own.Flags | WildBattleInvocationFlags.AttackStringPrinted }, own with { Flags = own.Flags | WildBattleInvocationFlags.NoAttackString },
                own with { Flags = own.Flags ^ WildBattleInvocationFlags.NoPpDeduct }, own with { Flags = own.Flags & ~WildBattleInvocationFlags.Obeys },
                own with { Flags = own.Flags | (WildBattleInvocationFlags)128 }, own with { CalledMove = id == 118 ? Kernel.Id("TACKLE") : null } })
                Shared.DenyPrelude(events.SetItem(at, transition with { Invocation = bad }), single, id);
            if (single)
            {
                // The source-to-Wild adapter correctly rejects four-position rows.
                // Mutate the exported envelope to exercise the singles layout gate itself.
                int cursor = Array.FindIndex(events.ToArray(), e => id == 118
                    ? e is SourceAttackStringEvent a && a.CurrentMove == Shared.Move(id)
                    : e is SourceMoveUsedEvent u && u.Move == Shared.Move(id));
                var exported = Envelope.Single(events, cursor) with { SourceEvents = [] };
                var exportedTransition = (WildBattleCalledMoveTransitionEvent)exported.Events[at];
                var wrongTarget = exportedTransition with { Invocation = exportedTransition.Invocation with { Target = (WildBattleSide)2 } };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(exported with { Events = exported.Events.SetItem(at, wrongTarget) }, true, SourceBattleScene.On).Dispatch);
            }
            else Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BattlerSlot(4));
            Shared.DenyPrelude(events.RemoveAt(at), single, id); Shared.DenyPrelude(Number(events.Insert(at + 1, transition)), single, id);
            Shared.DenyPrelude(events.SetItem(at, transition with { Turn = transition.Turn + 1 }), single, id);
        }
    }

    [TestMethod]
    public void SleepTalkRequiresItsOwnedCallerPpAndSelectedSlotReceipt()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Shared.Frame(214, single)).Events;
            var pp = events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Shared.Move(214)); int pi = events.IndexOf(pp);
            var talk = events.OfType<SourceSleepTalkEvent>().Single(); int ti = events.IndexOf(talk);
            foreach (var bad in new[] { pp with { PpOwner = new(1) }, pp with { Actor = new(1) }, pp with { Target = new(0) }, pp with { PpSlot = 1 },
                pp with { ChosenMove = Kernel.Id("TACKLE") }, pp with { CurrentMove = Kernel.Id("TACKLE") }, pp with { CommandCost = 0 }, pp with { After = pp.Before } })
                Shared.DenyPrelude(events.SetItem(pi, bad), single, 214);
            foreach (var bad in new[] { talk with { Side = new(1) }, talk with { CalledSlot = 0 }, talk with { CalledSlot = 4 },
                talk with { CalledMove = Kernel.Id("REST") }, talk with { Turn = talk.Turn + 1 } }) Shared.DenyPrelude(events.SetItem(ti, bad), single, 214);
            var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == Shared.Move(214));
            foreach (int cost in new[] { 3, 4 })
                Shared.DenyPrelude(events.SetItem(pi, pp with { CommandCost = cost, After = pp.Before - cost })
                    .SetItem(events.IndexOf(used), used with { PpAfter = pp.Before - cost }), single, 214);
            Shared.DenyPrelude(events.RemoveAt(pi), single, 214); Shared.DenyPrelude(events.RemoveAt(ti), single, 214);
            Shared.DenyPrelude(Number(events.Insert(pi + 1, pp)), single, 214); Shared.DenyPrelude(Number(events.Insert(ti + 1, talk)), single, 214);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void SinglesPreludeUsesExportedTypedCallerReceiptsWithoutCanonicalBackchannel(int id)
    {
        var result = Envelope.Resolve(Shared.Frame(id, true)); var cue = Shared.Check(result, id, true);
        int at = Array.FindIndex(result.Events.ToArray(), e => e.Sequence == cue.Sequence);
        var single = Envelope.Single(result.Events, at) with { SourceEvents = [] };
        Assert.AreEqual(cue, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
        int selectionIndex = Array.FindIndex(single.Events.ToArray(), e => e is WildBattleCallerSelectionEvent);
        var selected = (WildBattleCallerSelectionEvent)single.Events[selectionIndex];
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(single with { Events = single.Events.SetItem(selectionIndex, selected with { Result = null }) }, true, SourceBattleScene.On).Dispatch);
        int transitionIndex = Array.FindIndex(single.Events.ToArray(), e => e is WildBattleCalledMoveTransitionEvent);
        var transition = (WildBattleCalledMoveTransitionEvent)single.Events[transitionIndex];
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(single with { Events = single.Events.SetItem(transitionIndex, transition with {
            Invocation = transition.Invocation with { PpOwner = WildBattleSide.Opponent } }) }, true, SourceBattleScene.On).Dispatch);
        if (id == 214)
        {
            int talkIndex = Array.FindIndex(single.Events.ToArray(), e => e is WildBattleSleepTalkEvent);
            var talk = (WildBattleSleepTalkEvent)single.Events[talkIndex];
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(single with { Events = single.Events.SetItem(talkIndex, talk with { CalledSlot = 0 }) }, true, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void CallerPreludeRequiresCurrentMetadataAndRendererAvailability(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Envelope.Resolve(Shared.Frame(id, single)); var cue = Shared.Check(result, id, single);
            int at = Array.FindIndex(result.Events.ToArray(), e => e.Sequence == cue.Sequence); var moves = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, moves.Remove(Shared.Move(id)), moves.SetItem(Shared.Move(id), moves[Shared.Move(id)] with { SourceMoveId = 1 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(result.Events, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(result.Events, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(result.Events, at), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(result.Events, at), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }

    [TestMethod, DataRow(118), DataRow(214)]
    public void CallerVisibilitySnapshotUsesOnlyItsAuthenticatedCurrentBoundary(int id)
    {
        var result = Envelope.Resolve(Shared.Frame(id, false)); var cue = Shared.Check(result, id, false);
        MoveId?[] entry = [null, null, null, null];
        var visible = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(result.Events, cue, entry); Assert.IsNotNull(visible);
        CollectionAssert.AreEqual(new[] { true, true, true, true }, visible);
        var anchor = result.Events.Single(e => e.Sequence == cue.SnapshotSequence);
        Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(result.Events.Remove(anchor), cue, entry));
        Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(result.Events.Add(anchor), cue, entry));
        if (id == 118)
        {
            var leafPp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(result.Events, cue with { Sequence = leafPp.Sequence }, entry));
        }
    }
}
