using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Natural source learners and actual committed kernel actions. Controlled screens,
// counters and speed are integration fixtures, not a claim of natural route acquisition.
[TestClass, TestCategory("Game")]
public sealed class TwisterBrickFuryAnimationKernelTests
{
    private static MoveId Move(int id) => Kernel.Id(id == 239 ? "TWISTER" : id == 280 ? "BRICK_BREAK" : "FURY_CUTTER");
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon(id == 239 ? "HORSEA" : id == 280 ? "HITMONLEE" : "FARFETCHD",
            id == 239 ? "TWISTER" : id == 280 ? "BRICK_BREAK" : "FURY_CUTTER") with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Screens(SourceBattleFrame frame, int side, int mask) => frame with {
        Sides = frame.Sides.SetItem(side, frame.Sides[side] with { ReflectTurnsRemaining = (mask & 1) == 0 ? 0 : 5,
            LightScreenTurnsRemaining = (mask & 2) == 0 ? 0 : 3 }) };
    private static SourceMtMoonVolatileEvent[] Removed(ImmutableArray<SourceBattleEvent> events) =>
        events.OfType<SourceMtMoonVolatileEvent>().Where(e => e.Reason == "brick-break").ToArray();
    private static int Target(SourceBattleEvent hit) => hit switch {
        SourceHpChangedEvent h => h.Target.Value, SourceSubstituteDamagedEvent s => s.Target.Value, _ => throw new InvalidOperationException() };
    private static int? Counter(SourceBattleEvent hit) => hit switch {
        SourceHpChangedEvent h => h.PreparedFuryCutterCounter, SourceSubstituteDamagedEvent s => s.PreparedFuryCutterCounter, _ => throw new InvalidOperationException() };
    private static SourceBattleEvent WithCounter(SourceBattleEvent hit, int? value) => hit switch {
        SourceHpChangedEvent h => h with { PreparedFuryCutterCounter = value }, SourceSubstituteDamagedEvent s => s with { PreparedFuryCutterCounter = value }, _ => throw new InvalidOperationException() };
    private static CampaignMoveAnimationDispatch? Cue(ImmutableArray<SourceBattleEvent> events, bool single) =>
        Kernel.Project(events, Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent), single).Dispatch;
    private static void Check(ImmutableArray<SourceBattleEvent> events, object result, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(result); var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single(); Assert.AreEqual(Move(id), used.Move);
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Kernel.Project(events, i, single); Assert.AreEqual(decision, Kernel.Project(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Envelope.Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Envelope.Double(events, i), SourceBattleScene.On).Dispatch);
            if (owner is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Sequence, cue.Sequence);
            Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
            int target = owner is SourceHpChangedEvent or SourceSubstituteDamagedEvent ? Target(owner) : used.Target.Value;
            Assert.AreEqual(target, cue.Target.Value); var pp = events.OfType<SourcePpCommandEvent>().Single(p => p.Target.Value == target);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(used.Move, pp.CurrentMove);
            Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary);
            Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            bool substitute = events.OfType<SourceSubstituteDamagedEvent>().Any(s => s.Target.Value == target);
            Assert.AreEqual(substitute, cue.HitsSubstitute);
        }
        Assert.AreEqual(owner is null ? 0 : 1, count); Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }
    private static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner) => Check(result.Events, result, id, single, owner);
    private static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner) => Check(result.Events, result, id, single, owner);

    [TestMethod, DataRow(239), DataRow(280), DataRow(210)]
    public void NaturalLearnersOwnOneRootAcrossAllPositionsSinglesAndLegalSelectedAllies(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single || id == 239 ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id, actor), single), actor, target);
            var hits = Envelope.Hits(result.Events); Assert.AreEqual(id == 239 && !single ? 2 : 1, hits.Length);
            Check(result, id, single, hits[0]);
            if (id == 210) Assert.AreEqual(1, (int?)Cue(result.Events, single)!.FuryCutterCounter);
            if (id == 280) Assert.AreEqual((byte)0, Cue(result.Events, single)!.MoveTurn);
        }
    }

    [TestMethod, DataRow("first-protected"), DataRow("second-protected"), DataRow("both-protected"), DataRow("first-wonder-guard"), DataRow("first-substitute")]
    public void TwisterOwnsOnlyTheFirstEligibleSpreadPassWithOnePpDebit(string branch)
    {
        var bodies = Bodies(239);
        if (branch is "first-protected" or "both-protected") bodies[1] = bodies[1] with { Protected = true };
        if (branch is "second-protected" or "both-protected") bodies[3] = bodies[3] with { Protected = true };
        if (branch == "first-wonder-guard") bodies[1] = Kernel.Mon("SHEDINJA", "HARDEN", 100) with { SpeedStage = 0 };
        if (branch == "first-substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
        var result = Kernel.Ordered(Kernel.Frame(bodies)); var pp = result.Events.OfType<SourcePpCommandEvent>().ToArray();
        Assert.AreEqual(2, pp.Length); Assert.AreEqual(pp[0].Before - pp[0].CommandCost, pp[0].After);
        Assert.AreEqual(pp[0].After, pp[1].Before); Assert.AreEqual(pp[1].Before, pp[1].After);
        int target = branch is "first-protected" or "first-wonder-guard" ? 3 : 1;
        var owner = branch == "both-protected" ? null : Envelope.Hits(result.Events).Single(h => Target(h) == target);
        Check(result, 239, false, owner);
        if (branch == "first-substitute") Assert.IsInstanceOfType<SourceSubstituteDamagedEvent>(owner);
    }

    [TestMethod, DataRow("FLY", "RAYQUAZA", true), DataRow("BOUNCE", "PONYTA", true), DataRow("DIG", "DIGLETT", false)]
    public void TwisterRespectsActualChargeStateAndItsSourceAirMultiplier(string charge, string species, bool hits)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(239); bodies[1] = Kernel.Mon(species, charge, 100) with { SpeedStage = 0 };
            var frame = Kernel.Frame(bodies, single); var charged = Envelope.Resolve(frame, 1, 0);
            Assert.AreEqual(Kernel.Id(charge), charged.State.Slots[1].Registers.Turn!.Lock!.Move);
            var result = Kernel.Ordered(charged.State with { Rng = frame.Rng });
            if (!hits)
            {
                Assert.IsTrue(result.Events.OfType<SourceMoveMissedEvent>().Any(e => e.Target.Value == 1));
                Check(result, 239, single, single ? null : Envelope.Hits(result.Events).Single(h => Target(h) == 3)); continue;
            }
            var actual = result.Events.OfType<SourceHpChangedEvent>().Single(h => h.Target.Value == 1);
            var ground = Kernel.Ordered(frame); var ordinary = ground.Events.OfType<SourceHpChangedEvent>().Single(h => h.Target.Value == 1);
            Assert.AreEqual(ordinary.Critical, actual.Critical); Assert.AreEqual(ordinary.RandomPercent, actual.RandomPercent);
            Assert.IsTrue(actual.Damage >= 2 * ordinary.Damage && actual.Damage <= 2 * ordinary.Damage + 1,
                "Source multiplier precedes final random-percent truncation.");
            Check(result, 239, single, actual);
        }
    }

    [TestMethod, DataRow("success"), DataRow("no-proc"), DataRow("inner-focus"), DataRow("substitute")]
    public void TwisterSecondaryFlinchDoesNotCreateAnotherRoot(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(239); if (branch == "inner-focus") bodies[1] = Kernel.Mon("DRATINI", "WRAP", 100) with { SpeedStage = 0 };
            if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            var frame = Kernel.Frame(bodies, single);
            var result = branch is "success" or "no-proc" ? Kernel.Find(frame, 0, 1,
                r => r.Events.OfType<SourceFlinchEvent>().Any(e => e.Side.Value == 1 && e.Applied) == (branch == "success"), "Twister flinch " + branch)
                : Kernel.Ordered(frame);
            if (branch is "inner-focus" or "substitute") Assert.IsFalse(result.Events.OfType<SourceFlinchEvent>().Any(e => e.Side.Value == 1 && e.Applied));
            Check(result, 239, single, Envelope.Hits(result.Events).First());
        }
    }

    [TestMethod, DataRow(1), DataRow(2), DataRow(3)]
    public void BrickBreakOwnsOpposingSideRemovalBeforeDamageEvenWithSelectedAlly(int mask)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var frame = Screens(Screens(Kernel.Frame(Bodies(280, actor), single), actor & 1, 3), (actor & 1) ^ 1, mask);
            var result = Envelope.Resolve(frame, actor, target); var removed = Removed(result.Events); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            CollectionAssert.AreEqual((mask == 1 ? new[] { "reflect" } : mask == 2 ? new[] { "light-screen" } : new[] { "reflect", "light-screen" }), removed.Select(e => e.Volatile).ToArray());
            int affected = (target & 1) == ((actor & 1) ^ 1) ? target : (actor & 1) ^ 1;
            foreach (var row in removed) { Assert.AreEqual(affected, row.Side.Value); Assert.IsTrue(row.Before > 0); Assert.AreEqual(0, row.After); }
            Assert.AreEqual(pp.Sequence + 1, removed[0].Sequence); Assert.IsTrue(removed[^1].Sequence < Envelope.Hits(result.Events).Single().Sequence);
            Assert.AreEqual(0, result.State.Sides[(actor & 1) ^ 1].ReflectTurnsRemaining); Assert.AreEqual(0, result.State.Sides[(actor & 1) ^ 1].LightScreenTurnsRemaining);
            Assert.AreEqual(5, result.State.Sides[actor & 1].ReflectTurnsRemaining); Assert.AreEqual(3, result.State.Sides[actor & 1].LightScreenTurnsRemaining);
            Check(result, 280, single, removed[0]); Assert.AreEqual((byte)1, Cue(result.Events, single)!.MoveTurn);
        }
    }

    [TestMethod, DataRow(0), DataRow(1), DataRow(2), DataRow(3)]
    public void BrickBreakWallAnimationSurvivesGhostImmunityButNoWallDoesNot(int mask)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(280); bodies[1] = Kernel.Mon("GASTLY", "LICK", 100);
            if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            var result = Envelope.Resolve(Screens(Kernel.Frame(bodies, single), 1, mask));
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Assert.AreEqual(bodies[1].Pokemon.CurrentHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
            if (substitute) Assert.AreEqual(100, result.State.Slots[1].Body!.CalledVolatile!.SubstituteHp);
            Check(result, 280, single, mask == 0 ? null : Removed(result.Events)[0]);
            if (mask != 0) Assert.AreEqual((byte)1, Cue(result.Events, single)!.MoveTurn);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("miss")]
    public void BrickBreakFailedAccuracyLeavesScreensIntactAndOwnsNoRoot(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(280); if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            else bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Screens(Kernel.Frame(bodies, single), 1, 3);
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Brick Break accuracy") : Envelope.Resolve(frame);
            Assert.AreEqual(0, Removed(result.Events).Length); Assert.AreEqual(frame.Sides[1], result.State.Sides[1]); Check(result, 280, single, null);
        }
    }

    [TestMethod]
    public void BrickBreakRequiresUniqueAdjacentSourceOwnedOrderedRemovalReceipts()
    {
        foreach (bool single in new[] { false, true })
        {
            // Ghost makes removal the sole successful animation authority.
            var bodies = Bodies(280); bodies[1] = Kernel.Mon("GASTLY", "LICK", 100);
            var events = Envelope.Resolve(Screens(Kernel.Frame(bodies, single), 1, 3)).Events; var rows = Removed(events); int at = events.IndexOf(rows[0]);
            foreach (var wrong in new[] { rows[0] with { Side = new(0) }, rows[0] with { Before = 0 }, rows[0] with { After = 1 },
                rows[0] with { Reason = "expired" }, rows[0] with { Volatile = "safeguard" }, rows[0] with { Turn = rows[0].Turn + 1 } }) Shared.Deny(events.SetItem(at, wrong), single);
            Shared.Deny(Shared.Number(events.Insert(at + 1, rows[0])), single);
            Shared.Deny(events.SetItem(at, rows[1] with { Sequence = rows[0].Sequence }).SetItem(at + 1, rows[0] with { Sequence = rows[1].Sequence }), single);
            Shared.Deny(Shared.Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, rows[0].Turn, new(0), "unrelated", 0, 1, "move"))), single);
            Shared.Deny(events.Remove(rows[0]).Remove(rows[1]), single);
            Shared.Deny(events.Remove(Envelope.Hits(events).Single()), single);
        }
    }

    [TestMethod, DataRow(0), DataRow(1), DataRow(2), DataRow(3), DataRow(4), DataRow(5)]
    public void FuryCutterFreezesPostincrementCounterIncludingSaturationWithoutAVolatile(int before)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(210); bodies[0] = bodies[0] with { FuryCutterCounter = before };
            if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var hit = Envelope.Hits(result.Events).Single(); int expected = Math.Min(5, before + 1);
            Assert.AreEqual(expected, Counter(hit)); Assert.AreEqual(expected, result.State.Slots[0].Body!.FuryCutterCounter);
            CollectionAssert.AreEqual(new[] { "accuracy", "critical", "damage", "secondary-effect" },
                result.Trace.Select(t => t.Purpose[(t.Purpose.LastIndexOf('.') + 1)..]).ToArray());
            CollectionAssert.AreEqual(new[] { 100, 16, 16, 100 }, result.Trace.Select(t => t.Bound).ToArray());
            var marks = result.Events.OfType<SourceMtMoonVolatileEvent>().Where(e => e.Volatile == "fury-cutter").ToArray();
            Assert.AreEqual(before == 5 ? 0 : 1, marks.Length);
            if (marks.Length != 0) { Assert.AreEqual(before, marks[0].Before); Assert.AreEqual(expected, marks[0].After); Assert.AreEqual("hit", marks[0].Reason); Assert.IsTrue(marks[0].Sequence < hit.Sequence); }
            Check(result, 210, single, hit); Assert.AreEqual(expected, (int?)Cue(result.Events, single)!.FuryCutterCounter);
            var export = OverlayHitAnimationKernelFixture.Single([hit], 0).Events[0];
            Assert.AreEqual(Counter(hit), export switch { SourceHpChangedEvent h => h.PreparedFuryCutterCounter,
                SourceSubstituteDamagedEvent s => s.PreparedFuryCutterCounter, _ => throw new InvalidOperationException() });
        }
    }

    [TestMethod, DataRow("protect"), DataRow("miss"), DataRow("wonder-guard")]
    public void FuryCutterSourceFailureResetsCounterWithoutInventingAnAnimation(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(210); bodies[0] = bodies[0] with { FuryCutterCounter = 4 };
            if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            if (branch == "wonder-guard") bodies[1] = Kernel.Mon("SHEDINJA", "HARDEN", 100);
            var frame = Kernel.Frame(bodies, single); var result = branch == "miss" ? Envelope.Find(frame, 0, 1,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Fury Cutter accuracy") : Envelope.Resolve(frame);
            Assert.AreEqual(0, result.State.Slots[0].Body!.FuryCutterCounter); Assert.AreEqual(0, Envelope.Hits(result.Events).Length);
            Assert.IsTrue(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "fury-cutter" && e.After == 0));
            Check(result, 210, single, null);
        }
    }

    [TestMethod]
    public void FuryCutterRequiresLegalPreparedCounterAndSinglesUsesItsExportedReceipt()
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(210); bodies[0] = bodies[0] with { FuryCutterCounter = 5 };
            if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            var events = Envelope.Resolve(Kernel.Frame(bodies, single)).Events; var hit = Envelope.Hits(events).Single(); int at = events.IndexOf(hit);
            foreach (int? invalid in new int?[] { null, -1, 0, 6, 255 }) Shared.Deny(events.SetItem(at, WithCounter(hit, invalid)), single);
            if (!single) continue;
            int ui = Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent); var battle = Envelope.Single(events, ui);
            Assert.AreEqual(5, (int?)AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch!.FuryCutterCounter);
            SourceBattleEvent wrong = battle.Events[at] switch { SourceHpChangedEvent h => h with { PreparedFuryCutterCounter = null },
                SourceSubstituteDamagedEvent s => s with { PreparedFuryCutterCounter = null }, _ => throw new InvalidOperationException() };
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(at, wrong) }, true, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void FuryCutterPreparedCounterMustAgreeWithTheUniqueOwnedIncrement()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(210), single)).Events;
            var hit = Envelope.Hits(events).Single(); var marker = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "fury-cutter");
            int at = events.IndexOf(marker), hi = events.IndexOf(hit);
            Shared.Deny(events.RemoveAt(at), single);
            Shared.Deny(events.SetItem(hi, WithCounter(hit, 2)), single);
            Shared.Deny(Shared.Number(events.Insert(at + 1, marker)), single);
            foreach (var wrong in new[] { marker with { Side = new(1) }, marker with { Before = 1 }, marker with { After = 2 },
                marker with { Reason = "missed" }, marker with { Turn = marker.Turn + 1 } }) Shared.Deny(events.SetItem(at, wrong), single);
        }
    }

    [TestMethod]
    public void FuryCutterCounterReceiptSurvivesLaterRoughSkinFaintAndCounterReset()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(210); bodies[0] = bodies[0] with { FuryCutterCounter = 1, Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
            bodies[1] = Kernel.Mon("CARVANHA", "BITE", 100); Assert.AreEqual("ABILITY_ROUGH_SKIN", WildBattleEffective.Ability(bodies[1]));
            // Public queued ownership is required for the singles FaintClearSetData lifecycle.
            var result = Kernel.Ordered(Kernel.Frame(bodies, single)); var hit = Envelope.Hits(result.Events).Single();
            Assert.AreEqual(0, result.State.Slots[0].Body!.Pokemon.CurrentHp); Assert.AreEqual(0, result.State.Slots[0].Body!.FuryCutterCounter);
            Assert.AreEqual(2, Counter(hit)); Check(result, 210, single, hit); Assert.AreEqual(2, (int?)Cue(result.Events, single)!.FuryCutterCounter);
        }
    }

    [TestMethod, DataRow(239), DataRow(280), DataRow(210)]
    public void SleepingActorNeverProducesAHitOrBorrowsAnAnimationFromOldState(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = bodies[0] with { Status = 3 };
            var frame = Screens(Kernel.Frame(bodies, single), 1, 3);
            var choices = frame.Slots.Take(frame.BattlerCount).Select(c => new SourceActionSelection(c.Occupant!.Value,
                SourceActionKind.Move, 0, c.Slot.Value == 0 ? new(1) : c.Slot)).ToImmutableArray();
            var start = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, choices);
            Assert.IsTrue(start.Succeeded, start.Fault); Assert.AreEqual(0, start.State.Queue[0].Slot.Value); Assert.IsNotNull(start.Continuation);
            var result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, start.State, start.Continuation.Value);
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            Assert.IsFalse(result.Events.OfType<SourcePpCommandEvent>().Any()); Assert.AreEqual(0, Envelope.Hits(result.Events).Length);
            Assert.IsFalse(Removed(result.Events).Any()); Shared.Deny(result.Events, single);
        }
    }

    [TestMethod, DataRow(280), DataRow(210)]
    public void ActualMetronomeBrickBreakAndFuryCutterOwnTheirSourceInputs(int id)
    {
        uint? seed = null; for (uint candidate = 0; candidate < 65536; candidate++)
            if (WildBattleSourceCallers.Metronome(new(candidate)).SourceMoveId == id) { seed = candidate; break; }
        Assert.IsNotNull(seed);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Screens(Kernel.Frame(bodies, single), 1, 3) with { Rng = new(seed.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(id)));
            Assert.AreEqual(1, Envelope.Hits(result.Events).Length); SourceBattleEvent outcome = id == 280
                ? Removed(result.Events)[0]
                : Envelope.Hits(result.Events).Single();
            var cue = CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, outcome);
            if (id == 280) Assert.AreEqual((byte?)1, cue.MoveTurn);
            else Assert.AreEqual((byte?)1, cue.FuryCutterCounter);
        }
    }

    [TestMethod, DataRow(239), DataRow(280), DataRow(210)]
    public void NewRootsRejectMalformedForeignAndCalledInvocationOwnership(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Kernel.Ordered(Kernel.Frame(Bodies(id), single)).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
            var pp = events.OfType<SourcePpCommandEvent>().First(); int pi = events.IndexOf(pp), ai = events.IndexOf(attack);
            Shared.Deny(events.RemoveAt(pi), single); Shared.Deny(events.SetItem(pi, pp with { PpOwner = new(1) }), single);
            Shared.Deny(events.SetItem(pi, pp with { PpSlot = 1 }), single); Shared.Deny(events.SetItem(pi, pp with { CurrentMove = Kernel.Id("TACKLE") }), single);
            Shared.Deny(events.SetItem(pi, pp with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Shared.Deny(events.SetItem(ai, attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Shared.Deny(events.SetItem(ai, attack with { Actor = new(1) }), single);
            Shared.Deny(events.SetItem(pi, pp with { Turn = pp.Turn + 1 }), single);
            Shared.Deny(Shared.Number(events.Insert(pi + 1, pp)), single);
            if (id != 239)
            {
                var hit = (SourceHpChangedEvent)Envelope.Hits(events).Single();
                Shared.Deny(events.Replace(hit, hit with { Actor = new(1) }), single);
                Shared.Deny(events.Replace(hit, hit with { Target = new(0) }), single);
                Shared.Deny(Shared.Number(events.Insert(events.IndexOf(hit) + 1, hit)), single);
            }
        }
    }
}
