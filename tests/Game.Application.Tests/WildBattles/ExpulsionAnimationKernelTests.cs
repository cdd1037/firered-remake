using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.ExpulsionAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ExpulsionAnimationKernelTests
{
    [TestMethod, DataRow(18), DataRow(46)]
    public void OrdinaryDoubleExpulsionUsesTheChosenPositionAndItsOwnedReserve(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Shared.Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual("EFFECT_ROAR", definition.Effect);
        for (int actor = 0; actor < 4; actor++) foreach (int target in new[] { actor ^ 1, actor ^ 3, actor ^ 2 })
        {
            var frame = Shared.Frame(Shared.Bodies(id, actor), false);
            string before = JsonSerializer.Serialize(frame); var result = Shared.Ordered(frame, actor, target);
            var forced = result.Events.OfType<SourceForcedOutEvent>().Single();
            var switched = result.Events.OfType<SourceCalledPartySwitchedEvent>().Single();
            Assert.AreEqual(actor, forced.Actor.Value); Assert.AreEqual(target, forced.Target.Value); Assert.IsTrue(forced.TrainerBattle);
            Assert.AreEqual(forced.Target, switched.Side); Assert.AreEqual(WildBattleCalledSwitchKind.Forced, switched.Kind);
            Assert.AreEqual(frame.Slots[target].Member!.Value.Index, switched.OutgoingIndex); Assert.AreEqual(2, switched.IncomingIndex);
            Assert.IsTrue(forced.Sequence < switched.Sequence); Assert.IsNotNull(switched.Replacement);
            Assert.AreEqual(frame.Party(forced.Target.Side).Members[2].Pokemon, switched.Replacement.Body.Pokemon);
            Assert.AreEqual(new PartyMemberKey(forced.Target.Side, 2), result.State.Slots[target].Member);
            Assert.AreEqual(frame.Slots[target].EntrySerial + 1, result.State.Slots[target].EntrySerial);
            Assert.AreEqual(frame.Slots[target ^ 2].Member, result.State.Slots[target ^ 2].Member);
            Assert.IsFalse(result.Events.OfType<SourceEndedEvent>().Any()); Shared.Check(result.Events, id, false);
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod, DataRow(18), DataRow(46)]
    public void SinglesUseActualTrainerReplacementOrWildEscapeReceipts(int id)
    {
        var trainer = Shared.TrainerSingle(id); var switched = Shared.Ordered(trainer, target: 1);
        Assert.IsTrue(switched.Events.OfType<SourceForcedOutEvent>().Single().TrainerBattle);
        Assert.AreEqual(1, switched.Events.OfType<SourceCalledPartySwitchedEvent>().Single().IncomingIndex);
        Assert.AreEqual(WildBattleOutcome.Ongoing, switched.State.Outcome); Shared.Check(switched.Events, id, true);
        foreach (int actor in new[] { 0, 1 })
        {
            var frame = Shared.Frame(Shared.Bodies(id, actor), true); var result = Shared.Direct(frame, actor);
            var marker = result.Events.OfType<SourceForcedOutEvent>().Single(); var ended = result.Events.OfType<SourceEndedEvent>().Single();
            Assert.IsFalse(marker.TrainerBattle); Assert.AreEqual(actor, marker.Actor.Value); Assert.AreEqual(actor ^ 1, marker.Target.Value);
            Assert.AreEqual(marker.Sequence + 1, ended.Sequence); Assert.AreEqual(WildBattleOutcome.Escaped, ended.Outcome);
            Assert.IsFalse(result.Events.OfType<SourceCalledPartySwitchedEvent>().Any()); Assert.AreEqual(WildBattleOutcome.Escaped, result.State.Outcome);
            Shared.Check(result.Events, id, true);
        }
    }

    [TestMethod, DataRow(18), DataRow(46)]
    public void OutgoingSubstituteAndMembershipSurviveEveryPrefixBeforeTheSwitchReceipt(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = single ? Shared.TrainerSingle(id) : Shared.Frame(Shared.Bodies(id), false);
            frame = ChargeCopyAnimationKernelFixture.Body(frame, 1, b => b with { CalledVolatile = new() { SubstituteHp = 7 } });
            SourceBattleFrame? actionBefore = null;
            var result = Shared.Ordered(frame, target: 1, beforeAction: f => actionBefore = f); var events = result.Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var forced = events.OfType<SourceForcedOutEvent>().Single();
            var switched = events.OfType<SourceCalledPartySwitchedEvent>().Single(); var cue = Shared.Check(events, id, single);
            Assert.IsFalse(cue.HitsSubstitute, "Phasing owns a successful force-out, never a substitute-damage hit.");
            Assert.IsFalse(events.OfType<SourceSubstituteDamagedEvent>().Any());
            Assert.AreEqual(0, result.State.Slots[1].Body!.CalledVolatile?.SubstituteHp ?? 0);
            Assert.IsNotNull(actionBefore);
            var seed = CampaignBattleDisplayBodies.FromSlots(actionBefore.Slots); var sequential = new CampaignBattleDisplayBatch(seed);
            foreach (var e in events)
            {
                sequential = sequential.AdvanceThrough(events, e.Sequence);
                var fresh = new CampaignBattleDisplayBatch(seed).AdvanceThrough(events, e.Sequence);
                Assert.AreEqual(JsonSerializer.Serialize(sequential), JsonSerializer.Serialize(fresh));
                if (e.Sequence < switched.Sequence)
                {
                    Assert.AreEqual(seed.Members[1], fresh.Display.Members[1]);
                    Assert.AreEqual(seed.Bodies[1].Pokemon.Species, fresh.Display.Bodies[1].Pokemon.Species);
                    Assert.AreEqual(7, fresh.Display.Bodies[1].CalledVolatile!.SubstituteHp);
                }
                else
                {
                    Assert.AreEqual(new PartyMemberKey(BattleSide.Opponent, switched.IncomingIndex), fresh.Display.Members[1]);
                    Assert.AreEqual(switched.Replacement!.Body.Pokemon.Species, fresh.Display.Bodies[1].Pokemon.Species);
                    Assert.AreEqual(0, fresh.Display.Bodies[1].CalledVolatile?.SubstituteHp ?? 0);
                }
            }
            Assert.IsTrue(used.Sequence < cue.SnapshotSequence && cue.SnapshotSequence < forced.Sequence && forced.Sequence < switched.Sequence);
        }
    }

    [TestMethod, DataRow(18), DataRow(46)]
    public void SuctionCupsRootsProtectMissAndNoReserveNeverBorrowAForceOutResult(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "suction-cups", "rooted", "protected", "miss", "no-reserve" })
        {
            if (single && branch == "no-reserve") continue; // Wild phasing needs no reserve.
            var bodies = Shared.Bodies(id);
            if (branch == "suction-cups")
            {
                bodies[1] = Kernel.Mon("OCTILLERY", "WATER_GUN", 100) with { SpeedStage = 0 };
                Assert.AreEqual("ABILITY_SUCTION_CUPS", WildBattleEffective.Ability(bodies[1]));
            }
            if (branch == "rooted") bodies[1] = bodies[1] with { Rooted = true };
            if (branch == "protected") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "miss") { bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 }; }
            var frame = Shared.Frame(bodies, single, branch != "no-reserve");
            ImmutableArray<SourceBattleEvent> events = [];
            for (uint seed = 0; seed < (branch == "miss" ? 1024u : 1u); seed++)
            {
                events = single ? Shared.Direct(frame with { Rng = new(seed) }).Events : Shared.Ordered(frame, target: 1, actionSeed: seed).Events;
                if (branch != "miss" || events.OfType<SourceMoveMissedEvent>().Any()) break;
            }
            Assert.IsFalse(events.OfType<SourceForcedOutEvent>().Any(), branch);
            Assert.IsFalse(events.OfType<SourceCalledPartySwitchedEvent>().Any()); Assert.IsFalse(events.OfType<SourceEndedEvent>().Any());
            Assert.AreEqual(1, events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsTrue(branch switch {
                "suction-cups" => events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_SUCTION_CUPS"),
                "miss" => events.OfType<SourceMoveMissedEvent>().Any(),
                _ => events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == (branch == "no-reserve" ? "no-force-out-reserve" : branch)) }, branch);
            Shared.Deny(events, single);
        }
    }

    [TestMethod]
    public void SoundproofBlocksRoarAfterPpButNotWhirlwind()
    {
        foreach (bool single in new[] { false, true }) foreach (int id in new[] { 18, 46 })
        {
            var bodies = Shared.Bodies(id); bodies[1] = Kernel.Mon("MR_MIME", "BARRIER", 100) with { SpeedStage = 0 };
            Assert.AreEqual("ABILITY_SOUNDPROOF", WildBattleEffective.Ability(bodies[1]));
            var frame = Shared.Frame(bodies, single); var events = single ? Shared.Direct(frame).Events : Shared.Ordered(frame, target: 1).Events;
            Assert.AreEqual(1, events.OfType<SourcePpCommandEvent>().Count());
            if (id == 46) { Assert.IsTrue(events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_SOUNDPROOF")); Shared.Deny(events, single); }
            else Shared.Check(events, id, single);
        }
    }

    [TestMethod, DataRow(18), DataRow(46)]
    public void LowerLevelSuccessAndFailureUseTheCommittedSourceRoll(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool success in new[] { false, true })
        {
            var bodies = Shared.Bodies(id); var spec = Shared.Spec(id);
            bodies[0] = Kernel.Mon(spec.Species, spec.Name, 25) with { AccuracyStage = 12, SpeedStage = 12 };
            var frame = Shared.Frame(bodies, single); bool found = false;
            for (uint seed = 0; seed < 1024; seed++)
            {
                var result = single ? null : Shared.Ordered(frame, target: 1, actionSeed: seed);
                var direct = single ? Shared.Direct(frame with { Rng = new(seed) }) : null;
                var events = direct?.Events ?? result!.Events; var trace = direct?.Trace ?? result!.Trace;
                if (events.OfType<SourceForcedOutEvent>().Any() != success) continue;
                Assert.IsTrue(trace.Any(t => t.Purpose.EndsWith("force-out-level", StringComparison.Ordinal)));
                if (success) Shared.Check(events, id, single);
                else { Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "force-out-level")); Shared.Deny(events, single); }
                found = true; break;
            }
            Assert.IsTrue(found, $"Real lower-level {(success ? "success" : "failure")} must occur in the bounded action seeds.");
        }
    }

    [TestMethod]
    public void TeleportAnimatesOnlySuccessfulWildSelfEscapeAndNeverTrainerFailure()
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Shared.Move(100)]; Assert.AreEqual("EFFECT_TELEPORT", definition.Effect);
        Assert.AreEqual("MOVE_TARGET_USER", definition.Target);
        foreach (int actor in new[] { 0, 1 })
        {
            var frame = Shared.Frame(Shared.Bodies(100, actor), true); var result = Shared.Direct(frame, actor, actor);
            Assert.AreEqual(WildBattleOutcome.Escaped, result.State.Outcome); Assert.IsFalse(result.Events.OfType<SourceForcedOutEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceCalledPartySwitchedEvent>().Any()); Shared.Check(result.Events, 100, true);
        }
        foreach (bool single in new[] { false, true })
        {
            var frame = single ? Shared.TrainerSingle(100) : Shared.Frame(Shared.Bodies(100), false);
            var result = Shared.Ordered(frame, target: 0);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Actor.Value == 0 && e.Target.Value == 0 && e.Reason == "trainer-battle"));
            Assert.AreEqual(WildBattleOutcome.Ongoing, result.State.Outcome); Shared.Deny(result.Events, single);
        }
    }

    [TestMethod, DataRow("rooted"), DataRow("shadow-tag"), DataRow("bind")]
    public void TeleportAuthenticatesTheEscapeReceiptRatherThanRecomputingTrapping(string branch)
    {
        foreach (bool smokeBall in new[] { false, true })
        {
            var bodies = Shared.Bodies(100);
            if (branch == "rooted") bodies[0] = bodies[0] with { Rooted = true };
            if (branch == "shadow-tag") bodies[1] = Kernel.Mon("WOBBUFFET", "SAFEGUARD", 100);
            if (smokeBall) bodies[0] = bodies[0] with { HeldItem = "ITEM_SMOKE_BALL" };
            var frame = Shared.Frame(bodies, true);
            if (branch == "bind") frame = frame with { Slots = frame.Slots.SetItem(0, frame.Slots[0] with {
                Body = frame.Slots[0].Body! with { BindTurnsRemaining = 2, BindingMove = Kernel.Id("BIND") },
                Registers = frame.Slots[0].Registers with { WrappedBy = new(1) } }) };
            var result = Shared.Direct(frame, target: 0);
            if (smokeBall) Shared.Check(result.Events, 100, true);
            else { Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "cannot-escape")); Shared.Deny(result.Events, true); }
        }
    }

    [TestMethod, DataRow(18), DataRow(46), DataRow(100)]
    public void MalformedMissingDuplicateForeignAndCalledOwnershipCannotAnimate(int id)
    {
        foreach (bool single in id == 100 ? new[] { true } : new[] { false, true })
        {
            var frame = Shared.Frame(Shared.Bodies(id), single);
            var events = single ? Shared.Direct(frame).Events : Shared.Ordered(frame, target: 1).Events;
            var marker = Shared.Marker(events, id); int mi = events.IndexOf(marker);
            var pp = events.OfType<SourcePpCommandEvent>().Single(); int pi = events.IndexOf(pp);
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var used = events.OfType<SourceMoveUsedEvent>().Single();
            Shared.Deny(events.Remove(marker), single); Shared.Deny(Shared.Number(events.Insert(mi + 1, marker)), single);
            Shared.Deny(events.SetItem(mi, marker with { Sequence = pp.Sequence }), single);
            Shared.Deny(events.Select(e => e.Sequence > pp.Sequence ? e with { Sequence = e.Sequence + 1 } : e).ToImmutableArray(), single);
            Shared.Deny(events.SetItem(mi, marker with { Turn = marker.Turn + 1 }), single);
            foreach (var bad in new[] { pp with { PpOwner = new(1) }, pp with { PpSlot = 3 }, pp with { CommandCost = 0 },
                pp with { ChosenMove = Kernel.Id("METRONOME") }, pp with { CurrentMove = Kernel.Id("SPLASH") } }) Shared.Deny(events.SetItem(pi, bad), single);
            Shared.Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            if (marker is SourceForcedOutEvent forced)
                foreach (var bad in new[] { forced with { Actor = new(1) }, forced with { Target = new(0) }, forced with { Move = Kernel.Id("TACKLE") } })
                    Shared.Deny(events.SetItem(mi, bad), single);
            if (marker is SourceEndedEvent ended)
                Shared.Deny(events.SetItem(mi, ended with { Outcome = WildBattleOutcome.Won }), single);
            var moves = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, moves.Remove(Shared.Move(id)), moves.SetItem(Shared.Move(id), moves[Shared.Move(id)] with { SourceMoveId = 1 }),
                moves.SetItem(Shared.Move(id), moves[Shared.Move(id)] with { Power = 1 }),
                moves.SetItem(Shared.Move(id), moves[Shared.Move(id)] with { Target = id == 100 ? "MOVE_TARGET_SELECTED" : "MOVE_TARGET_USER" }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Shared.Single(events, events.IndexOf(used)), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, events.IndexOf(used)), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Shared.Single(events, events.IndexOf(used)), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, events.IndexOf(used)), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }

    [TestMethod, DataRow(18), DataRow(46), DataRow(100)]
    public void ActualMetronomeExpulsionSuccessOwnsItsRootButTrainerTeleportStaysSilent(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Shared.Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 100) with { AccuracyStage = 12, SpeedStage = 12 };
            var frame = Shared.Frame(bodies, single); uint seed = CallerPreludeAnimationKernelFixture.MetronomeSeed(id);
            var events = single ? Shared.Direct(frame with { Rng = new(seed) }).Events : Shared.Ordered(frame, target: 1, actionSeed: seed).Events;
            Assert.IsTrue(events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Shared.Move(id)));
            if (id != 100 || single) Assert.IsNotNull(Shared.Marker(events, id));
            else Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Shared.Move(id) && e.Reason == "trainer-battle"));
            if (id == 100 && !single)
            {
                Assert.IsFalse(events.OfType<SourceEndedEvent>().Any(e => e.Outcome == WildBattleOutcome.Escaped));
                CallerPreludeAnimationKernelFixture.DenyLeaf(events, single, id);
            }
            else CalledLeafAnimationKernelFixture.Check(events, events, single, Kernel.Id("METRONOME"), id, Shared.Marker(events, id));
        }
    }
}
