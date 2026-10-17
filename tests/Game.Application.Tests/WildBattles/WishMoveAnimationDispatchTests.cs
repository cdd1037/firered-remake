using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class WishMoveAnimationDispatchTests
{
    private static MoveId Wish => Id("WISH");
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("wish", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("wish", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, params string[] names)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var moves = names.Select(name => {
            var move = Id(name);
            Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
                $"Natural source learnset required: {species}/{level}/{name}.");
            int pp = Mechanics.Catalog.Moves[move].Pp;
            return new PokemonMoveSlot(move, pp, pp);
        }).ToImmutableArray();
        return new(pokemon with { Moves = moves });
    }

    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
        bodies[actor] = NaturalMon("TOGETIC", 29, "WISH") with { SpeedStage = 12 };
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(WildBattleTransformationRules.PersistentPokemon(body), body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(31) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int? target = null, int moveSlot = 0)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, moveSlot, new(target ?? actor)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        return result;
    }

    private static SourceFourActionResult Begin(SourceBattleFrame frame, int? selectedActor = null, int selectedMoveSlot = 0, int? selectedTarget = null) =>
        SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision,
            frame.Slots.Take(frame.BattlerCount).Select(c =>
                new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move,
                    c.Slot.Value == selectedActor ? selectedMoveSlot : 0,
                    c.Slot.Value == selectedActor && selectedTarget is { } target ? new BattlerSlot(target) : c.Slot)).ToImmutableArray());

    private static SourceFourActionResult Next(SourceFourActionResult result)
    {
        Assert.IsNotNull(result.Continuation, "Expected the retained public turn continuation.");
        var next = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        Assert.IsTrue(next.Succeeded, next.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(next.State));
        return next;
    }

    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index,
        bool single, SourceBattleScene scene = SourceBattleScene.On) => single
        ? AnimationDispatchFixture.AtAttackText(Single(events, index), true, scene, moves: Mechanics.Catalog.Moves)
        : AnimationDispatchFixture.AtAttackText(Double(events, index), scene, moves: Mechanics.Catalog.Moves);

    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame state,
        ImmutableArray<RandomTrace> trace, int actor, bool single = false)
    {
        string before = JsonSerializer.Serialize(events), savedState = JsonSerializer.Serialize(state), savedTrace = JsonSerializer.Serialize(trace);
        var rng = state.Rng;
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == Wish);
        var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == Wish);
        var pp = events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Wish);
        var scheduled = events.OfType<SourceCalledDelayedEvent>().Single(e => e.Move == Wish && e.Reason == "scheduled");
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(used.Actor, used.Target);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(Wish, attack.ChosenMove);
        Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.Target); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(Wish, pp.ChosenMove); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(1, pp.CommandCost); Assert.AreEqual(pp.Before - 1, pp.After);
        Assert.AreEqual(attack.Sequence + 1, used.Sequence); Assert.AreEqual(used.Sequence + 1, pp.Sequence);
        Assert.AreEqual(pp.Sequence + 1, scheduled.Sequence);
        Assert.AreEqual(actor, scheduled.Target.Value); Assert.AreEqual(0, scheduled.Before); Assert.AreEqual(2, scheduled.After);
        Assert.AreEqual(state.Slots[actor].Member!.Value.Index, scheduled.SourcePartyIndex);
        int dispatched = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var decision = Project(events, index, single);
            Assert.AreEqual(decision, Project(events, index, single));
            Assert.IsNull(Project(events, index, single, SourceBattleScene.Off).Dispatch);
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch);
            }
            else Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch);
            if (events[index] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); dispatched++;
            Assert.AreEqual("wish", cue.BattleId); Assert.AreEqual(273, cue.SourceMoveId);
            Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(actor, cue.Target.Value);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(scheduled.Sequence, cue.ResultSequence);
            Assert.IsNull(cue.MoveTurn); Assert.IsTrue(cue.AcknowledgesCurrentReceipt); Assert.IsFalse(cue.HitsSubstitute);
        }
        Assert.AreEqual(1, dispatched);
        Assert.AreEqual(before, JsonSerializer.Serialize(events)); Assert.AreEqual(savedState, JsonSerializer.Serialize(state));
        Assert.AreEqual(savedTrace, JsonSerializer.Serialize(trace)); Assert.AreEqual(rng, state.Rng);
    }

    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string label, bool single = false)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(Project(events, index, single).Dispatch, label);
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch, label);
                Assert.IsNull(Project(events, index, false).Dispatch, label + " doubles projection");
                Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch, label);
            }
            else Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch, label);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events), label);
    }

    [TestMethod]
    public void NaturalWishSetupOwnsOneReceiptInBothSinglesPositionsAndAllFourDoublesPositions()
    {
        var move = Mechanics.Catalog.Moves[Wish];
        Assert.AreEqual(273, move.SourceMoveId); Assert.AreEqual("EFFECT_WISH", move.Effect);
        Assert.AreEqual("MOVE_TARGET_USER", move.Target); Assert.AreEqual(10, move.Pp);
        Assert.IsTrue(move.Flags.Contains("FLAG_PROTECT_AFFECTED"));
        foreach (bool single in new[] { true, false }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Resolve(Frame(Bodies(actor), single), actor);
            AssertProjection(result.Events, result.State, result.Trace, actor, single);
            var wish = result.State.Slots[actor].Position.Wish; Assert.IsNotNull(wish);
            Assert.AreEqual(2, wish.TurnsRemaining); Assert.AreEqual(actor / 2, wish.SourcePartyIndex);
            Assert.IsFalse(result.Events.Any(e => e is SourceRecoveredEvent or SourceHpChangedEvent or SourceSubstituteDamagedEvent));
        }
    }

    [TestMethod]
    public void OrderedPublicActionsProduceTheSameSelfTargetReceiptAtEveryPosition()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Begin(Frame(Bodies(actor)));
            Assert.IsTrue(result.Succeeded, result.Fault);
            for (int step = 0; !result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Wish); step++)
            {
                Assert.IsTrue(step < 8, "The selected Wish must execute in the retained ordinary turn queue.");
                result = Next(result);
            }
            Assert.IsFalse(result.State.Slots[actor].Body!.Protected);
            AssertProjection(result.Events, result.State, result.Trace, actor);
        }
    }

    [TestMethod]
    public void ExistingActualTimerSpendsPpThenRejectsRepeatedSetupWithoutAnimation()
    {
        foreach (bool single in new[] { true, false }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var first = Resolve(Frame(Bodies(actor), single), actor);
            var second = Resolve(first.State, actor);
            Assert.AreEqual(first.State.Slots[actor].Position.Wish, second.State.Slots[actor].Position.Wish);
            var failed = second.Events.OfType<SourceMoveFailedEvent>().Single();
            Assert.AreEqual("wish-slot-occupied", failed.Reason); Assert.AreEqual(actor, failed.Actor.Value);
            Assert.AreEqual(failed.Actor, failed.Target); Assert.AreEqual(Wish, failed.Move);
            Assert.AreEqual(0, second.Events.OfType<SourceCalledDelayedEvent>().Count());
            var pp = second.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(9, pp.Before); Assert.AreEqual(8, pp.After); Assert.IsTrue(pp.Sequence < failed.Sequence);
            string state = JsonSerializer.Serialize(second.State), trace = JsonSerializer.Serialize(second.Trace);
            AssertDenied(second.Events, "occupied Wish timer", single);
            Assert.AreEqual(state, JsonSerializer.Serialize(second.State)); Assert.AreEqual(trace, JsonSerializer.Serialize(second.Trace));
        }
    }

    [TestMethod]
    public void SubstituteAndMinimumAccuracyDoNotReplaceTheSetupTimerReceipt()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(actor);
            bodies[actor] = bodies[actor] with { AccuracyStage = 0, EvasionStage = 12, CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(Frame(bodies), actor);
            Assert.AreEqual(1, result.State.Slots[actor].Body!.CalledVolatile!.SubstituteHp);
            Assert.AreEqual(bodies[actor].Pokemon.CurrentHp, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            AssertProjection(result.Events, result.State, result.Trace, actor);
        }
        // Do not seed Protected=true here: Wish's source attackcanceler can mark
        // that artificial self-protection miss while trywish still schedules.
    }

    [TestMethod]
    [DataRow(false, false)] [DataRow(false, true)] [DataRow(true, false)] [DataRow(true, true)]
    public void ActualDelayedCountdownHealingAndFullHpFailureNeverReplaySetup(bool single, bool injured)
    {
        int actor = single ? 0 : 2; var bodies = Bodies(actor);
        if (injured) bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = bodies[actor].Pokemon.Stats.MaxHp - 20 } };
        var frame = Frame(bodies, single); int scheduled = 0, countdown = 0, resolved = 0, healed = 0, full = 0, occupied = 0, cues = 0;
        for (int turn = 0; turn < 2; turn++)
        {
            var result = Begin(frame);
            for (int step = 0; ; step++)
            {
                Assert.IsTrue(step < 32, "Two ordinary turns must drain through the retained public lifecycle.");
                Assert.IsTrue(result.Succeeded, result.Fault);
                string before = JsonSerializer.Serialize(result); var rng = result.State.Rng;
                scheduled += result.Events.OfType<SourceCalledDelayedEvent>().Count(e => e.Move == Wish && e.Before == 0 && e.After == 2 && e.Reason == "scheduled");
                countdown += result.Events.OfType<SourceCalledDelayedEvent>().Count(e => e.Move == Wish && e.Before == 2 && e.After == 1 && e.Reason == "countdown");
                resolved += result.Events.OfType<SourceCalledDelayedEvent>().Count(e => e.Move == Wish && e.Before == 1 && e.After == 0 && e.Reason == "resolved");
                healed += result.Events.OfType<SourceRecoveredEvent>().Count(e => e.Move == Wish && e.Side.Value == actor && e.HpAfter > e.HpBefore);
                full += result.Events.OfType<SourceMoveFailedEvent>().Count(e => e.Move == Wish && e.Reason == "already-full-hp");
                occupied += result.Events.OfType<SourceMoveFailedEvent>().Count(e => e.Move == Wish && e.Reason == "wish-slot-occupied");
                for (int index = 0; index < result.Events.Length; index++)
                {
                    var decision = Project(result.Events, index, single);
                    Assert.AreEqual(decision, Project(result.Events, index, single));
                    if (decision.Dispatch?.SourceMoveId == 273)
                    {
                        cues++; Assert.AreEqual(0, turn); Assert.IsInstanceOfType<SourceMoveUsedEvent>(result.Events[index]);
                        Assert.AreEqual(actor, decision.Dispatch.Actor.Value); Assert.AreEqual(actor, decision.Dispatch.Target.Value);
                        Assert.IsTrue(result.Events.OfType<SourceCalledDelayedEvent>().Any(e =>
                            e.Reason == "scheduled" && e.Sequence == decision.Dispatch.ResultSequence));
                    }
                    var hit = single ? AnimationDispatchFixture.AtHitResult(Single(result.Events, index), true, SourceBattleScene.On)
                        : AnimationDispatchFixture.AtHitResult(Double(result.Events, index), SourceBattleScene.On);
                    Assert.AreNotEqual(273, hit.Dispatch?.SourceMoveId ?? 0);
                }
                Assert.AreEqual(before, JsonSerializer.Serialize(result)); Assert.AreEqual(rng, result.State.Rng);
                if (result.CompletedCleanTurn) { frame = result.State; break; }
                result = Next(result);
            }
        }
        Assert.AreEqual(1, scheduled); Assert.AreEqual(1, countdown); Assert.AreEqual(1, resolved); Assert.AreEqual(1, occupied); Assert.AreEqual(1, cues);
        Assert.AreEqual(injured ? 1 : 0, healed); Assert.AreEqual(injured ? 0 : 1, full);
        Assert.IsNull(frame.Slots[actor].Position.Wish);
    }

    [TestMethod]
    public void ExactScheduledMetadataCannotBeBorrowedFromOtherDelayedReceipts()
    {
        var result = Resolve(Frame(Bodies(), single: true)); var events = result.Events;
        var scheduled = events.OfType<SourceCalledDelayedEvent>().Single(); int at = events.IndexOf(scheduled);
        foreach (var invalid in new[] { scheduled with { Target = new(1) }, scheduled with { Move = Id("FUTURE_SIGHT") },
            scheduled with { Before = 1 }, scheduled with { After = 3 }, scheduled with { Reason = "countdown" },
            scheduled with { Reason = "resolved" }, scheduled with { SourcePartyIndex = null },
            scheduled with { SourcePartyIndex = -1 }, scheduled with { SourcePartyIndex = 6 },
            scheduled with { Turn = scheduled.Turn + 1 }, scheduled with { Sequence = scheduled.Sequence - 1 } })
        {
            AssertDenied(events.SetItem(at, invalid), "malformed scheduled receipt");
            AssertDenied(events.SetItem(at, invalid), "malformed scheduled receipt", single: true);
        }
        AssertDenied(events.RemoveAt(at), "missing scheduled receipt", single: true);
        AssertDenied(events.Add(scheduled with { Sequence = events[^1].Sequence + 1 }), "duplicate scheduled receipt", single: true);
        AssertProjection(result.Events, result.State, result.Trace, 0, single: true);
    }

    [TestMethod]
    public void AttackHeaderPpOwnershipAndOptionsAuthenticateTheOrdinaryInvocation()
    {
        var result = Resolve(Frame(Bodies(), single: true)); var events = result.Events;
        var attack = events.OfType<SourceAttackStringEvent>().Single(); int ai = events.IndexOf(attack);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); int ui = events.IndexOf(used);
        var pp = events.OfType<SourcePpCommandEvent>().Single(); int pi = events.IndexOf(pp);
        foreach (var invalid in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = Id("METRONOME") },
            attack with { CurrentMove = Id("GROWL") }, attack with { Turn = attack.Turn + 1 }, attack with { Sequence = used.Sequence } })
            AssertDenied(events.SetItem(ai, invalid), "unmatched attack header", single: true);
        foreach (var invalid in new[] { pp with { Actor = new(1) }, pp with { Target = new(1) }, pp with { PpOwner = new(1) },
            pp with { ChosenMove = Id("METRONOME") }, pp with { CurrentMove = Id("GROWL") }, pp with { PpSlot = 1 },
            pp with { Before = 0 }, pp with { After = pp.Before }, pp with { CommandCost = 0 },
            pp with { Turn = pp.Turn + 1 }, pp with { Sequence = used.Sequence } })
            AssertDenied(events.SetItem(pi, invalid), "unmatched PP command", single: true);
        AssertDenied(events.SetItem(ui, used with { Target = new(1) }), "non-self target", single: true);
        AssertDenied(events.SetItem(ui, used with { Slot = -1 }), "invalid move slot", single: true);
        AssertDenied(events.RemoveAt(ai), "missing attack header", single: true);
        AssertDenied(events.RemoveAt(pi), "missing PP command", single: true);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, ui) with { Stage = CampaignDoubleBattleStage.Intro }, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, -1), true, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, events.Length), SourceBattleScene.On).Dispatch);
        AssertProjection(result.Events, result.State, result.Trace, 0, single: true);
    }

    [TestMethod]
    public void ActualMetronomeWishIsOutsideTheOrdinaryDirectInvocation()
    {
        var bodies = Bodies(); bodies[0] = NaturalMon("TOGETIC", 29, "METRONOME");
        var frame = Frame(bodies, single: true); uint? selectedSeed = null;
        for (uint seed = 0; seed < 65536; seed++)
            if (WildBattleSourceCallers.Metronome(new(seed)).SourceMoveId == 273) { selectedSeed = seed; break; }
        Assert.IsNotNull(selectedSeed, "A bounded source caller seed must select Wish without editing a receipt.");
        var result = Resolve(frame with { Rng = new(selectedSeed.Value) });
        Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Id("METRONOME") && e.Result == Wish));
        Assert.IsTrue(result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Move == Wish && e.Reason == "scheduled"));
        Assert.IsTrue(result.Events.OfType<SourceAttackStringEvent>().Any(e => e.CurrentMove == Wish && e.ChosenMove == Id("METRONOME")));
        Assert.IsFalse(result.State.Slots[0].Body!.Protected);
        AssertDenied(result.Events, "called Wish remains outside direct admission", single: true);
    }

    [TestMethod]
    public void ActualMimicCopiesWishThenNextTurnsDirectSelectionUsesTheCasterPosition()
    {
        var bodies = Bodies(1);
        bodies[0] = NaturalMon("JIGGLYPUFF", 39, "MIMIC");
        var first = Begin(Frame(bodies, single: true), selectedActor: 0, selectedTarget: 1);
        int copied = 0;
        for (int step = 0; ; step++)
        {
            Assert.IsTrue(step < 32, "Actual Wish followed by Mimic must drain through one ordinary turn.");
            Assert.IsTrue(first.Succeeded, first.Fault);
            copied += first.Events.OfType<SourceMoveCopiedEvent>().Count(e =>
                e.Actor.Value == 0 && e.Move == Id("MIMIC") && e.CopiedMove == Wish);
            if (first.CompletedCleanTurn) break;
            first = Next(first);
        }
        Assert.AreEqual(1, copied);
        Assert.AreEqual(Wish, first.State.Slots[0].Body!.Pokemon.Moves[0].Move);
        Assert.IsFalse(first.State.Slots[0].Body!.Protected);
        Assert.IsNull(first.State.Slots[0].Position.Wish);
        var second = Begin(first.State);
        for (int step = 0; !second.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 0 && e.Move == Wish); step++)
        {
            Assert.IsTrue(step < 8, "The copied move executes only on the next selected action.");
            second = Next(second);
        }
        Assert.IsFalse(second.Events.OfType<SourceCalledMoveTransitionEvent>().Any());
        AssertProjection(second.Events, second.State, second.Trace, 0, single: true);
        Assert.AreEqual(5, second.Events.OfType<SourcePpCommandEvent>().Single().Before);
        Assert.AreEqual(4, second.Events.OfType<SourcePpCommandEvent>().Single().After);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ActualProtectClearsBeforeTheNextOrdinaryWishSelection(bool single)
    {
        int actor = single ? 0 : 2; var bodies = Bodies(actor);
        var machines = SharedMachineCatalog.LoadProject(TestAssets.Root); ItemId tm = new("pret:ITEM_TM17");
        var protect = machines.Definition(tm);
        Assert.AreEqual(Id("PROTECT"), protect.Move); Assert.AreEqual(182, protect.SourceMoveId);
        Assert.IsTrue(machines.Compatibility(tm, bodies[actor].Pokemon.Species).CanLearn);
        // Source-compatible learned moves only; this does not claim to replay obtaining or teaching TM17.
        bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with {
            Moves = bodies[actor].Pokemon.Moves.Add(new(protect.Move, protect.MovePp, protect.MovePp)) } };
        var result = Begin(Frame(bodies, single), selectedActor: actor, selectedMoveSlot: 1);
        Assert.IsTrue(result.Succeeded, result.Fault);
        for (int step = 0; !result.Events.OfType<SourceProtectAttemptEvent>().Any(e => e.Side.Value == actor); step++)
        {
            Assert.IsTrue(step < 8, "The legal Protect action must execute in its retained queue position.");
            result = Next(result);
        }
        Assert.IsTrue(result.Events.OfType<SourceProtectAttemptEvent>().Single(e => e.Side.Value == actor).Succeeded);
        Assert.IsTrue(result.State.Slots[actor].Body!.Protected);
        Assert.IsFalse(result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Wish));

        // Separate API-composition diagnostic: ResolveAction is a public recipe seam,
        // not the ordinary turn scheduler. Its Wish path does not require current queue ownership.
        // A second raw action on this intermediate state can therefore expose the receipt gap.
        // No presentation assertion admits this off-scheduler self-Protect state.
        string ordinaryState = JsonSerializer.Serialize(result.State);
        var recomposed = Resolve(result.State, actor);
        Assert.IsTrue(recomposed.State.Slots[actor].Body!.Protected);
        Assert.IsTrue(recomposed.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Move == Wish && e.Reason == "scheduled"));
        Assert.IsFalse(recomposed.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Wish));
        Assert.AreEqual(ordinaryState, JsonSerializer.Serialize(result.State));

        for (int step = 0; !result.CompletedCleanTurn; step++)
        {
            Assert.IsTrue(step < 32, "Protect's ordinary turn must reach its cleanup boundary.");
            result = Next(result);
            Assert.IsFalse(result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == Wish));
        }
        Assert.IsFalse(result.State.Slots[actor].Body!.Protected);
        Assert.IsNull(result.State.Slots[actor].Position.Wish);
        result = Begin(result.State);
        for (int step = 0; !result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == Wish); step++)
        {
            Assert.IsTrue(step < 8, "Next turn's ordinary direct Wish must execute after Protect cleanup.");
            result = Next(result);
        }
        Assert.IsFalse(result.State.Slots[actor].Body!.Protected);
        AssertProjection(result.Events, result.State, result.Trace, actor, single);
    }
}
