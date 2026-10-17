using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ShakeFireSeedMoveAnimationDispatchTests
{
    private static readonly (int Id, string Name, string Species, int Level)[] Roots =
        [(89, "EARTHQUAKE", "DIGLETT", 41), (99, "RAGE", "BEEDRILL", 25),
         (222, "MAGNITUDE", "DIGLETT", 16), (228, "PURSUIT", "BEEDRILL", 30),
         (73, "LEECH_SEED", "BULBASAUR", 7), (221, "SACRED_FIRE", "HO_OH", 77),
         (307, "BLAST_BURN", "CHARIZARD", 50)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static MoveTutorCatalog Tutors => TestAssets.Cached(() =>
        PretMoveTutors.Load(TestAssets.Root, Mechanics.Catalog).Catalog.WithCapeBrink(CapeBrinkTutorCatalog.Authenticate(Mechanics.Catalog)));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("shake-fire-seed", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("shake-fire-seed", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name, uint personality = 0)
    {
        var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        if (name == "BLAST_BURN")
        {
            var party = new PartyPokemon(1, pokemon with { Friendship = 255 }, "CHARIZARD", "TEST", 0, 2,
                "MAP_PALLET_TOWN", level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
            var offer = CapeBrinkTutorRules.Offer(Tutors.CapeBrink!, [party], ImmutableHashSet<string>.Empty);
            Assert.IsTrue(offer.Allowed, offer.Error); Assert.AreEqual(move, offer.Tutor!.Move);
            var taught = MoveTutorRules.Teach(Tutors, Mechanics.Catalog, party, offer.Tutor.Index,
                pokemon.Moves.Length == 4 ? 0 : null);
            Assert.IsTrue(taught.Succeeded, taught.Error); pokemon = taught.Pokemon.Pokemon;
            Assert.AreEqual(move, pokemon.Moves[taught.LearnedSlot].Move);
        }
        else Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learner required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        var root = Roots.Single(m => m.Id == id);
        bodies[actor] = NaturalMon(root.Species, root.Level, root.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
        new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false, uint seed = 31,
        WildBattleCombatant? playerReserve = null)
    {
        var players = (single ? new[] { Member(bodies[0]) } : new[] { Member(bodies[0]), Member(bodies[2]) }).ToImmutableArray();
        if (playerReserve is not null) players = players.Add(Member(playerReserve));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, players),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(seed) };
    }
    private static SourceFourActionResult Run(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        return new(result.State, result.Events, result.Trace, result.Fault);
    }
    private static SourceFourActionResult Find(SourceBattleFrame frame, Func<SourceFourActionResult, bool> predicate,
        string branch, int actor = 0, int target = 1)
    {
        // Bounded actual-kernel branch discovery; no synthetic positive receipts or independent RNG implementation.
        for (uint seed = 0; seed < 1024; seed++)
        { var result = Run(frame with { Rng = new(seed) }, actor, target); if (predicate(result)) return result; }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }
    private static SourceFourActionResult Ordered(SourceBattleFrame frame, bool switchPlayer = false, bool complete = false)
    {
        var choices = frame.Slots.Where(c => c.Body is not null).Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            if (switchPlayer && c.Slot.Value == 0)
                return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Switch,
                    Incoming: new(BattleSide.Player, frame.Layout == SourceBattleLayout.Singles ? 1 : 2));
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0,
                switchPlayer && move.Id == Id("PURSUIT") ? new(0) :
                    move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot : c.Slot.Opposite);
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        var events = result.Events; var trace = result.Trace;
        for (int step = 0; step < 16; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (complete ? result.CompletedCleanTurn : result.Events.OfType<SourcePursuitInterceptedEvent>().Any())
                return new(result.State, events, trace, result.Fault);
            Assert.IsNotNull(result.Continuation, "Expected the public ordered turn to reach its bounded result.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
            events = events.AddRange(result.Events); trace = trace.AddRange(result.Trace);
        }
        throw new InvalidOperationException("Expected result was not reached in one bounded public turn.");
    }
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
    private static bool QualifyingBurn(SourceFourActionResult result) =>
        result.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 50);
    private static SourceBattleEvent Hit(ImmutableArray<SourceBattleEvent> events, int actor, int target) =>
        events.Single(e => e is SourceHpChangedEvent hp && hp.Actor.Value == actor && hp.Target.Value == target ||
            e is SourceSubstituteDamagedEvent sub && sub.Actor.Value == actor && sub.Target.Value == target);

    private static void AssertRoots(SourceFourActionResult result, int id, bool single,
        params (int Actor, int Target, byte? MoveTurn)[] expected)
    {
        string snapshot = JsonSerializer.Serialize(result); var events = result.Events;
        var move = Id(Roots.Single(m => m.Id == id).Name); int count = 0;
        foreach (var wanted in expected)
            Assert.AreEqual(1, events.OfType<SourceMoveUsedEvent>().Count(e => e.Actor.Value == wanted.Actor && e.Move == move));
        for (int index = 0; index < events.Length; index++)
        {
            var decision = AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On);
            Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On));
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.Off).Dispatch);
            if (events[index] is SourceMoveUsedEvent used && used.Move == move)
            {
                var wanted = expected.Single(e => e.Actor == used.Actor.Value);
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                var attack = events.Take(index).OfType<SourceAttackStringEvent>().Last();
                var pp = events.Skip(index + 1).OfType<SourcePpCommandEvent>().First();
                var hit = id == 73 ? events.OfType<SourceLeechSeedChangedEvent>().Single(e => e.Side.Value == wanted.Target && e.Reason == "move")
                    : Hit(events, wanted.Actor, wanted.Target);
                Assert.AreEqual(id, dispatch.SourceMoveId); Assert.AreEqual("shake-fire-seed", dispatch.BattleId);
                Assert.AreEqual(wanted.Actor, dispatch.Actor.Value); Assert.AreEqual(wanted.Target, dispatch.Target.Value);
                Assert.AreEqual(used.Sequence, dispatch.Sequence); Assert.AreEqual(attack.Sequence, dispatch.AttackSequence);
                Assert.AreEqual(pp.Sequence, dispatch.PpSequence); Assert.AreEqual(hit.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(hit is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(wanted.MoveTurn, dispatch.MoveTurn); Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);
                ushort? power = id == 222 ? (ushort)events.OfType<SourceMagnitudeEvent>().Single().Power : id == 228 ? (ushort)40 : null;
                int? damage = id == 99 ? hit switch { SourceHpChangedEvent h => h.Damage, SourceSubstituteDamagedEvent s => s.Damage, _ => null } : null;
                Assert.AreEqual(power, dispatch.MovePower); Assert.AreEqual(damage, dispatch.MoveDamage);
            }
            else if (decision.Dispatch?.SourceMoveId == id)
                Assert.Fail("Only this root's attack-text receipt may launch it; secondary, drain, switch and recharge pages cannot replay it.");
            if (!single) continue;
            Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On));
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.Off).Dispatch);
        }
        Assert.AreEqual(expected.Length, count);
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(result), "Projection cannot mutate state, committed events or RNG trace.");
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, bool single = true)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch);
            if (!single) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    [DataRow(89)] [DataRow(99)] [DataRow(222)] [DataRow(228)] [DataRow(73)] [DataRow(221)] [DataRow(307)]
    public void NaturalLearnsetsAndCapeBrinkTeachingReachOneRootInActualSingleAndDoubleLayouts(int id)
    {
        foreach (bool single in new[] { true, false })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Run(Frame(Bodies(id, actor), single), actor, actor ^ 1);
            int target = !single && id is 89 or 222 ? Enumerable.Range(0, 4).First(i => i != actor) : actor ^ 1;
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(1, pp.CommandCost);
            Assert.AreEqual(pp.Before - 1, pp.After);
            AssertRoots(result, id, single, (actor, target, id == 228 ? (byte)0 : null));
        }
    }

    [TestMethod] [DataRow(89)] [DataRow(222)]
    public void AllOtherTraversalUsesTheFirstEligibleTargetIncludingThePartner(int id)
    {
        // Actor 0's initially selected foe 3 is not the first numeric target.
        var bodies = Bodies(id); bodies[1] = NaturalMon("PIDGEY", 100, "SAND_ATTACK");
        var result = Run(Frame(bodies), target: 3);
        Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Target.Value == 1 && e.Effectiveness == WildBattleEffectiveness.NoEffect));
        Assert.AreEqual(3, result.Events.OfType<SourcePpCommandEvent>().Single().Target.Value);
        AssertRoots(result, id, false, (0, 2, null));
        bodies[1] = Bodies(id)[1] with { Protected = true };
        result = Run(Frame(bodies));
        Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Target.Value == 1 && e.Reason == "protected"));
        AssertRoots(result, id, false, (0, 2, null));
        bodies = Bodies(id); bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 };
        result = Find(Frame(bodies), r => r.Events.OfType<SourceMoveMissedEvent>().Any(e => e.Target.Value == 1) &&
            r.Events.OfType<SourceHpChangedEvent>().Any(e => e.Target.Value == 2), "first-target miss and successful partner hit", target: 3);
        AssertRoots(result, id, false, (0, 2, null));
    }

    [TestMethod] [DataRow(89)] [DataRow(222)]
    public void AllPreventedTargetsDoNotLaunchAndLaterFailuresDoNotEraseTheFirstHit(int id)
    {
        foreach (bool immune in new[] { false, true })
        {
            var bodies = Bodies(id);
            for (int i = 1; i < 4; i++) bodies[i] = immune ? NaturalMon("GASTLY", 100, "LICK") : bodies[i] with { Protected = true };
            var denied = Run(Frame(bodies)); AssertDenied(denied.Events, single: false);
        }
        var mixed = Bodies(id); mixed[2] = mixed[2] with { Protected = true }; mixed[3] = NaturalMon("PIDGEY", 100, "SAND_ATTACK");
        AssertRoots(Run(Frame(mixed)), id, false, (0, 1, null));
    }

    [TestMethod] [DataRow(89)] [DataRow(222)]
    public void AllOtherPressureCountsBothFoesAndPartnerOnceAndClampsLastPp(int id)
    {
        foreach (int actor in new[] { 0, 1, 2, 3 })
        foreach (int remaining in new[] { 1, 5 })
        {
            var bodies = Bodies(id, actor);
            for (int i = 0; i < 4; i++) if (i != actor) bodies[i] = NaturalMon("RAIKOU", 100, "ROAR");
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with {
                Moves = bodies[actor].Pokemon.Moves.SetItem(0, bodies[actor].Pokemon.Moves[0] with { CurrentPp = remaining }) } };
            var result = Run(Frame(bodies), actor, actor ^ 1); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(4, pp.CommandCost); Assert.AreEqual(remaining, pp.Before);
            Assert.AreEqual(Math.Max(0, remaining - 4), pp.After); Assert.AreEqual(actor, pp.PpOwner.Value);
            AssertRoots(result, id, false, (actor, Enumerable.Range(0, 4).First(i => i != actor), null));
        }
    }

    [TestMethod] [DataRow(89)] [DataRow(222)]
    public void FirstEligibleSubstituteAndZeroLossEndureStillOwnGroundAnimation(int id)
    {
        foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[1] = bodies[1] with { Enduring = !substitute,
                Pokemon = bodies[1].Pokemon with { CurrentHp = 1 }, CalledVolatile = new() { SubstituteHp = substitute ? 1 : 0 } };
            var result = Run(Frame(bodies)); var hit = Hit(result.Events, 0, 1);
            if (substitute) Assert.IsInstanceOfType<SourceSubstituteDamagedEvent>(hit);
            else { var hp = (SourceHpChangedEvent)hit; Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage); }
            AssertRoots(result, id, false, (0, 1, null));
        }
    }

    [TestMethod] [DataRow(89)] [DataRow(222)]
    public void AllOtherMalformedTargetOrderDuplicateHitAndForeignPpFailClosed(int id)
    {
        var result = Run(Frame(Bodies(id))); var events = result.Events;
        var hits = events.OfType<SourceHpChangedEvent>().ToArray(); Assert.AreEqual(3, hits.Length);
        AssertDenied(events.Replace(hits[0], hits[0] with { Target = new(0) }), single: false);
        AssertDenied(events.Replace(hits[1], hits[1] with { Target = hits[0].Target }), single: false);
        AssertDenied(events.Replace(hits[0], hits[0] with { Damage = -1 }), single: false);
        var reversed = events.Replace(hits[0], hits[0] with { Target = hits[2].Target })
            .Replace(hits[2], hits[2] with { Target = hits[0].Target });
        AssertDenied(reversed, single: false);
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        AssertDenied(events.Replace(pp, pp with { PpOwner = new(2) }), single: false);
        AssertDenied(events.Replace(pp, pp with { CommandCost = 5 }), single: false);
        AssertDenied(Number(events.Insert(events.IndexOf(pp) + 1, pp)), single: false);
        if (id == 89) AssertDenied(Number(events.Insert(events.IndexOf(pp) + 1,
            new SourceMagnitudeEvent(0, pp.Turn, new(0), 7, 70))), single: false);
    }

    [TestMethod]
    public void MagnitudeUsesEveryAuthenticStrengthBucketAndRejectsMalformedContext()
    {
        var expected = new Dictionary<int, int> { [4] = 10, [5] = 30, [6] = 50, [7] = 70, [8] = 90, [9] = 110, [10] = 150 };
        var observed = new HashSet<int>(); SourceFourActionResult? sample = null;
        for (uint seed = 0; seed < 1024 && observed.Count < expected.Count; seed++)
        {
            var result = Run(Frame(Bodies(222), single: true, seed: seed)); var magnitude = result.Events.OfType<SourceMagnitudeEvent>().Single();
            Assert.AreEqual(expected[magnitude.Magnitude], magnitude.Power);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".magnitude", StringComparison.Ordinal)));
            if (observed.Add(magnitude.Magnitude)) AssertRoots(result, 222, true, (0, 1, null));
            sample ??= result;
        }
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), observed.ToArray()); Assert.IsNotNull(sample);
        var events = sample.Events; var receipt = events.OfType<SourceMagnitudeEvent>().Single();
        AssertDenied(events.Remove(receipt));
        foreach (var malformed in new[] { receipt with { Actor = new(1) }, receipt with { Turn = receipt.Turn + 1 },
            receipt with { Magnitude = 3 }, receipt with { Magnitude = 11 }, receipt with { Power = 0 },
            receipt with { Power = 100 }, receipt with { Power = expected[receipt.Magnitude] + 1 } })
            AssertDenied(events.Replace(receipt, malformed));
        AssertDenied(Number(events.Insert(events.IndexOf(receipt) + 1, receipt)));
        var moved = events.Remove(receipt).ToList(); moved.Add(receipt); AssertDenied(Number(moved));
    }

    [TestMethod]
    public void RageShakeUsesCommittedDamageBeforeHpClampAndAfterSurvivalAdjustment()
    {
        foreach (string branch in new[] { "overkill", "substitute", "endure", "focus-band" })
        {
            var bodies = Bodies(99); bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 },
                Enduring = branch == "endure", HeldItem = branch == "focus-band" ? "ITEM_FOCUS_BAND" : "ITEM_NONE",
                CalledVolatile = new() { SubstituteHp = branch == "substitute" ? 1 : 0 } };
            // Direct singles actions require the turn lifecycle to own a KO. Use the existing
            // doubles faint boundary for overkill and unsuccessful Focus Band discovery seeds;
            // receipts still involve slots 0/1, so the normalized singles parity checks remain.
            var frame = Frame(bodies, single: branch is "substitute" or "endure");
            var result = branch == "focus-band" ? Find(frame, r => r.Events.OfType<SourceDamageAdjustedEvent>().Any(a => a.HungOn), "Focus Band save") : Run(frame);
            var hit = Hit(result.Events, 0, 1);
            int damage = hit is SourceHpChangedEvent hp ? hp.Damage : ((SourceSubstituteDamagedEvent)hit).Damage;
            if (branch is "endure" or "focus-band") Assert.AreEqual(0, damage);
            else Assert.IsTrue(damage > 1, "The shake consumes raw committed damage, not one point of actual HP loss.");
            AssertRoots(result, 99, true, (0, 1, null));
            AssertDenied(result.Events.Replace(hit, hit is SourceHpChangedEvent h ? h with { Damage = -1 }
                : ((SourceSubstituteDamagedEvent)hit) with { Damage = -1 }));
        }
    }

    [TestMethod] [DataRow(99)] [DataRow(228)]
    public void SelectedShakeMovesRejectActualProtectAndAccuracyMiss(int id)
    {
        var bodies = Bodies(id); bodies[1] = bodies[1] with { Protected = true };
        var protect = Run(Frame(bodies, single: true));
        Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected")); AssertDenied(protect.Events);
        bodies = Bodies(id); bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 };
        AssertDenied(Find(Frame(bodies, single: true), r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "selected shake miss").Events);
        if (id == 99)
        {
            bodies = Bodies(id); bodies[1] = NaturalMon("GASTLY", 100, "LICK");
            var immune = Run(Frame(bodies, single: true));
            Assert.IsTrue(immune.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect)); AssertDenied(immune.Events);
        }
    }

    [TestMethod] [DataRow(true)] [DataRow(false)]
    public void OrderedPursuitInterceptionKeepsPowerFortyTurnOneAndCurrentReceiptOwnership(bool single)
    {
        var bodies = Bodies(228, 1);
        if (!single) bodies[3] = NaturalMon("BEEDRILL", 30, "PURSUIT") with { AccuracyStage = 12 };
        var result = Ordered(Frame(bodies, single, playerReserve: NaturalMon("SNORLAX", 100, "DEFENSE_CURL")), switchPlayer: true);
        var markers = result.Events.OfType<SourcePursuitInterceptedEvent>().ToArray();
        CollectionAssert.AreEqual(single ? new[] { 1 } : new[] { 3, 1 }, markers.Select(m => m.Actor.Value).ToArray());
        Assert.AreEqual(single ? 1 : 2, result.State.Slots[0].Member!.Value.Index);
        AssertRoots(result, 228, single, single ? [(1, 0, (byte?)1)] : [(3, 0, (byte?)1), (1, 0, (byte?)1)]);
        Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)), "Interception omits the ordinary accuracy command.");
        if (!single) return;
        var events = result.Events; var marker = markers.Single();
        foreach (var malformed in new[] { marker with { Actor = new(0) }, marker with { SwitchingSide = new(1) },
            marker with { Slot = 1 }, marker with { Turn = marker.Turn + 1 } }) AssertDenied(events.Replace(marker, malformed));
        AssertDenied(Number(events.Insert(events.IndexOf(marker), marker)));
        // An absent marker cannot prove interception from the current schema, so no missing-marker assertion is made.
    }

    [TestMethod]
    public void LeechSeedOnlyUsesTheMatchingNewSeedReceiptAndNeverLaterDrainOrRemoval()
    {
        var result = Run(Frame(Bodies(73), single: true)); AssertRoots(result, 73, true, (0, 1, null));
        var events = result.Events; var seed = events.OfType<SourceLeechSeedChangedEvent>().Single();
        Assert.IsNull(seed.Before); Assert.AreEqual(new BattlerSlot(0), seed.After); Assert.AreEqual("move", seed.Reason);
        AssertDenied(events.Remove(seed));
        foreach (var malformed in new[] { seed with { Side = new(0) }, seed with { Before = new(0) }, seed with { After = null },
            seed with { After = new(1) }, seed with { Reason = "rapid-spin" }, seed with { Turn = seed.Turn + 1 } })
            AssertDenied(events.Replace(seed, malformed));
        AssertDenied(Number(events.Insert(events.IndexOf(seed) + 1, seed)));
        var full = Ordered(Frame(Bodies(73), single: true), complete: true);
        Assert.AreEqual(1, full.Events.OfType<SourceLeechSeedDrainEvent>().Count()); AssertRoots(full, 73, true, (0, 1, null));
        var rapid = NaturalMon("SQUIRTLE", 25, "RAPID_SPIN");
        var clearing = Frame([NaturalMon("SNORLAX", 100, "DEFENSE_CURL"), rapid, Bodies(73)[2], Bodies(73)[3]], single: true);
        clearing = clearing with { Slots = clearing.Slots.SetItem(1, clearing.Slots[1] with {
            Registers = clearing.Slots[1].Registers with { LeechSeedSource = new(0) } }) };
        var cleared = Run(clearing, 1, 0); var removed = cleared.Events.OfType<SourceLeechSeedChangedEvent>().Single();
        Assert.AreEqual("rapid-spin", removed.Reason); Assert.IsNull(removed.After);
        for (int i = 0; i < cleared.Events.Length; i++)
            Assert.AreNotEqual(73, AnimationDispatchFixture.AtAttackText(Single(cleared.Events, i), true, SourceBattleScene.On).Dispatch?.SourceMoveId);
    }

    [TestMethod]
    public void LeechSeedActualSubstituteGrassAlreadySeededProtectAndMissDoNotAnimate()
    {
        foreach (string branch in new[] { "substitute", "grass", "seeded", "protect", "miss" })
        {
            var bodies = Bodies(73);
            if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 50 } };
            if (branch == "grass") bodies[1] = NaturalMon("BULBASAUR", 100, "GROWL");
            if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "miss") { bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 }; }
            var frame = Frame(bodies, single: true);
            if (branch == "seeded") frame = frame with { Slots = frame.Slots.SetItem(1, frame.Slots[1] with {
                Registers = frame.Slots[1].Registers with { LeechSeedSource = new(0) } }) };
            var result = branch == "miss" ? Find(frame, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Leech Seed miss") : Run(frame);
            Assert.AreEqual(0, result.Events.OfType<SourceLeechSeedChangedEvent>().Count()); AssertDenied(result.Events);
        }
    }

    [TestMethod]
    public void SacredFireBurnAndSilentWaterVeilPreventionKeepTheSuccessfulHitRoot()
    {
        var burn = Find(Frame(Bodies(221), single: true), r => r.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Side.Value == 1 && e.After == WildBattleStatus.Burn), "Sacred Fire burn");
        Assert.IsTrue(QualifyingBurn(burn)); AssertRoots(burn, 221, true, (0, 1, null));
        var bodies = Bodies(221); bodies[1] = NaturalMon("WAILMER", 100, "SPLASH");
        Assert.AreEqual("ABILITY_WATER_VEIL", bodies[1].Pokemon.AbilityId);
        var prevented = Find(Frame(bodies, single: true), QualifyingBurn, "qualifying Water Veil burn prevention");
        Assert.AreEqual(WildBattleStatus.None, prevented.State.Slots[1].Body!.Status);
        Assert.AreEqual(0, prevented.Events.OfType<SourceAbilityPreventedEvent>().Count());
        Assert.AreEqual(0, prevented.Events.OfType<SourceStatusChangedEvent>().Count()); AssertRoots(prevented, 221, true, (0, 1, null));
    }

    [TestMethod]
    public void SacredFireThawsItsActorAndTargetWithoutReplayingTheRoot()
    {
        var bodies = Bodies(221); bodies[0] = bodies[0] with { Status = WildBattleStatus.Freeze };
        var self = Find(Frame(bodies, single: true), r => r.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Side.Value == 0 && e.Reason == "thawed-by-move"), "move-thaw after failed natural thaw");
        Assert.AreEqual(WildBattleStatus.None, self.State.Slots[0].Body!.Status);
        Assert.AreEqual(1, self.Trace.Count(t => t.Purpose.EndsWith(".freeze-thaw", StringComparison.Ordinal)));
        AssertRoots(self, 221, true, (0, 1, null));
        bodies = Bodies(221); bodies[1] = bodies[1] with { Status = WildBattleStatus.Freeze };
        var target = Run(Frame(bodies, single: true));
        Assert.IsTrue(target.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Side.Value == 1 && e.Reason == "fire-hit-thaw"));
        AssertRoots(target, 221, true, (0, 1, null));
    }

    [TestMethod] [DataRow(221)] [DataRow(307)]
    public void FireFlashFireImmunityProtectAndMissRejectButSubstituteAndEndureRetainOneHit(int id)
    {
        var bodies = Bodies(id); bodies[1] = NaturalMon("VULPIX", 100, "TAIL_WHIP");
        var flash = Run(Frame(bodies, single: true));
        Assert.IsTrue(flash.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_FLASH_FIRE")); AssertDenied(flash.Events);
        bodies = Bodies(id); bodies[1] = bodies[1] with { Protected = true }; AssertDenied(Run(Frame(bodies, single: true)).Events);
        bodies = Bodies(id); bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 };
        AssertDenied(Find(Frame(bodies, single: true), r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "fire accuracy miss").Events);
        foreach (bool substitute in new[] { true, false })
        {
            bodies = Bodies(id); bodies[1] = bodies[1] with { Enduring = !substitute,
                Pokemon = bodies[1].Pokemon with { CurrentHp = 1 }, CalledVolatile = new() { SubstituteHp = substitute ? 1 : 0 } };
            var hit = Run(Frame(bodies, single: true)); AssertRoots(hit, id, true, (0, 1, null));
            if (substitute) Assert.AreEqual(0, hit.Events.OfType<SourceStatusChangedEvent>().Count());
        }
    }

    [TestMethod]
    public void BlastBurnRechargeDoesNotReplayConsumePpOrDrawRandomness()
    {
        var hit = Run(Frame(Bodies(307), single: true)); AssertRoots(hit, 307, true, (0, 1, null));
        Assert.AreEqual(WildBattleCalledLockKind.Recharge, hit.State.Slots[0].Registers.Turn!.Lock!.Kind);
        var recharge = Run(hit.State);
        Assert.IsTrue(recharge.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "must-recharge"));
        Assert.AreEqual(0, recharge.Events.OfType<SourceMoveUsedEvent>().Count()); Assert.AreEqual(0, recharge.Events.OfType<SourcePpCommandEvent>().Count());
        Assert.AreEqual(hit.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp, recharge.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
        Assert.AreEqual(hit.State.Rng, recharge.State.Rng); Assert.AreEqual(0, recharge.Trace.Length); AssertDenied(recharge.Events);
    }

    [TestMethod]
    [DataRow(89)] [DataRow(99)] [DataRow(222)] [DataRow(228)] [DataRow(73)] [DataRow(221)] [DataRow(307)]
    public void MissingMetadataWrongHeadersForeignPpAndInvalidCursorFailClosed(int id)
    {
        var result = Run(Frame(Bodies(id), single: true)); var events = result.Events;
        var used = events.OfType<SourceMoveUsedEvent>().Single(); int index = events.IndexOf(used);
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        AssertDenied(events.Remove(attack)); AssertDenied(events.Replace(attack, attack with { CurrentMove = Id("TACKLE") }));
        AssertDenied(events.Remove(pp)); AssertDenied(events.Replace(pp, pp with { PpOwner = new(1) }));
        AssertDenied(events.Replace(pp, pp with { CommandCost = 0 })); AssertDenied(events.Replace(pp, pp with { PpSlot = 1 }));
        AssertDenied(events.Replace(pp, pp with { Sequence = used.Sequence }));
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On, moves: null).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On, moves: null).Dispatch);
        var wrong = Mechanics.Catalog.Moves.ToDictionary(p => p.Key, p => p.Value);
        wrong[used.Move] = wrong[used.Move] with { SourceMoveId = 1 };
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On, wrong).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On, wrong).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index) with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index) with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        foreach (int cursor in new[] { -1, events.Length })
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, cursor), true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, cursor), SourceBattleScene.On).Dispatch);
        }
    }
}
