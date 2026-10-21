using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Charge = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Actual public-kernel receipts from source-natural learners. Controlled stages,
// HP, Substitute and RNG are integration fixtures, not natural-route evidence.
[TestClass, TestCategory("Game")]
public sealed class RevengeExtremeSpinAirAnimationKernelTests
{
    private static (string Name, string Species, string Learnset, int Level, string Effect, int Power, int Priority) Spec(int id) => id switch {
        279 => ("REVENGE", "MACHOP", "Machop", 25, "EFFECT_REVENGE", 60, -4),
        245 => ("EXTREME_SPEED", "ARCANINE", "Arcanine", 49, "EFFECT_QUICK_ATTACK", 80, 1),
        229 => ("RAPID_SPIN", "SQUIRTLE", "Squirtle", 23, "EFFECT_RAPID_SPIN", 20, 0),
        314 => ("AIR_CUTTER", "ZUBAT", "Zubat", 31, "EFFECT_HIGH_CRITICAL", 55, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    private static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id); bodies[actor] = Kernel.Mon(spec.Species, spec.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static int Target(SourceBattleEvent hit) => hit switch {
        SourceHpChangedEvent h => h.Target.Value, SourceSubstituteDamagedEvent s => s.Target.Value,
        _ => throw new InvalidOperationException("Expected an actual damage receipt.") };
    private static SourceBattleEvent Power(SourceBattleEvent hit, int? value) => hit switch {
        SourceHpChangedEvent h => h with { PreparedBasePower = value },
        SourceSubstituteDamagedEvent s => s with { PreparedBasePower = value }, _ => throw new InvalidOperationException() };

    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int id, int actor = 0, int target = 1,
        Action<SourceFourActionResult>? earlier = null)
    {
        if (id != 279) return Charge.Ordered(frame, actor, target);
        string before = JsonSerializer.Serialize(frame);
        var choices = frame.Slots.Take(frame.BattlerCount).Select(cell => {
            var definition = Kernel.Mechanics.Catalog.Moves[cell.Body!.Pokemon.Moves[0].Move];
            int recipient = cell.Slot.Value == actor ? target : definition.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"
                ? cell.Slot.Value : actor;
            return new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var next = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, choices);
        Assert.IsTrue(next.Succeeded, next.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(next.State));
        Assert.AreEqual(actor, next.State.Queue[^1].Slot.Value, "Source Revenge priority -4 must follow ordinary selected actions.");
        for (int step = 0; step < 8; step++)
        {
            Assert.IsNotNull(next.Continuation);
            next = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, next.State, next.Continuation.Value);
            Assert.IsTrue(next.Succeeded, next.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(next.State));
            if (next.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == Move(id)))
            { Assert.AreEqual(before, JsonSerializer.Serialize(frame)); return next; }
            earlier?.Invoke(next);
        }
        throw new InvalidOperationException("The bounded public turn never executed Revenge.");
    }
    private static SourceFourActionResult Find(SourceBattleFrame frame, int id, Func<SourceFourActionResult, bool> predicate)
    {
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Ordered(frame with { Rng = new(seed) }, id);
            if (!predicate(result)) continue;
            Console.WriteLine($"{Spec(id).Name}: actual public-turn-start seed={seed}"); return result;
        }
        throw new InvalidOperationException($"No requested {Spec(id).Name} branch in 1024 public-turn seeds.");
    }
    private static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? outcome)
    {
        string before = JsonSerializer.Serialize(result); var events = result.Events;
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.IsTrue(attack.Sequence < used.Sequence);
        var commands = events.OfType<SourcePpCommandEvent>().ToArray(); Assert.IsTrue(commands.Length > 0);
        foreach (var pp in commands)
        {
            Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(used.Slot, pp.PpSlot);
            Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove); Assert.IsTrue(used.Sequence < pp.Sequence);
        }
        Assert.AreEqual(used.PpBefore, commands[0].Before); Assert.AreEqual(used.PpAfter, commands[0].After);
        Assert.AreEqual(Math.Max(0, commands[0].Before - commands[0].CommandCost), commands[0].After);
        int count = 0;
        for (int i = 0; i < events.Length; i++) foreach (bool atResult in new[] { false, true })
        {
            // Charge.Project exercises the shared canonical dispatch through the single envelope.
            var decision = Charge.Project(events, i, single, atResult);
            Assert.AreEqual(decision, Charge.Project(events, i, single, atResult));
            Assert.IsNull(Charge.Project(events, i, single, atResult, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Charge.Wave(events, i, single));
            if (single) Assert.IsNull(atResult ? AnimationDispatchFixture.AtResult(Charge.Single(events, i), false, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtAttackText(Charge.Single(events, i), false, SourceBattleScene.On).Dispatch);
            if (outcome is null || atResult || events[i] != used) { Assert.IsNull(decision.Dispatch, decision.Reason); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            var ownedPp = commands.Single(p => p.Target.Value == Target(outcome));
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(Target(outcome), cue.Target.Value);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(ownedPp.Sequence, cue.PpSequence); Assert.AreEqual(outcome.Sequence, cue.ResultSequence);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.AreEqual(outcome is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.AreEqual(id == 229 ? (ushort?)20 : null, cue.MovePower); Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MoveTurn);
        }
        Assert.AreEqual(outcome is null ? 0 : 1, count); Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229), DataRow(314)]
    public void PinnedSourceMetadataAndNaturalLearnersMatchTheRealCatalog(int id)
    {
        var spec = Spec(id); var move = Kernel.Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, move.SourceMoveId); Assert.AreEqual(spec.Effect, move.Effect);
        Assert.AreEqual(spec.Power, move.Power); Assert.AreEqual(spec.Priority, move.Priority);
        Assert.AreEqual(id == 314 ? "MOVE_TARGET_BOTH" : "MOVE_TARGET_SELECTED", move.Target);
        string root = Path.Combine(TestAssets.Root, "content/raw/pokefirered/037335f4");
        string moves = File.ReadAllText(Path.Combine(root, "src/data/battle_moves.h"));
        var row = Regex.Match(moves, @"\[MOVE_" + spec.Name + @"\]\s*=\s*\{(?<body>.*?)\},", RegexOptions.Singleline);
        Assert.IsTrue(row.Success); StringAssert.Contains(row.Value, ".effect = " + spec.Effect);
        StringAssert.Contains(row.Value, ".power = " + spec.Power + ","); StringAssert.Contains(row.Value, ".priority = " + spec.Priority + ",");
        string learners = File.ReadAllText(Path.Combine(root, "src/data/pokemon/level_up_learnsets.h"));
        var learner = Regex.Match(learners, @"s" + spec.Learnset + @"LevelUpLearnset\[\]\s*=\s*\{.*?\};", RegexOptions.Singleline);
        Assert.IsTrue(learner.Success); StringAssert.Contains(learner.Value, $"LEVEL_UP_MOVE({spec.Level}, MOVE_{spec.Name})");
        _ = Kernel.Mon(spec.Species, spec.Name, spec.Level);
        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "data/battle_anim_scripts.s")), "Move_" + spec.Name + ":");
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229), DataRow(314)]
    public void ActualOrderedHitsOwnOneRootForAllSlotsSinglesAndLegalSelectedAllies(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : id == 314 ? new[] { actor ^ 1, actor ^ 3 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Kernel.Frame(Bodies(id, actor), single), id, actor, target);
            var hits = Envelope.Hits(result.Events); Assert.AreEqual(id == 314 && !single ? 2 : 1, hits.Length);
            Check(result, id, single, hits[0]);
        }
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229), DataRow(314)]
    public void BodySubstituteBreakAndZeroLossEndureUseCommittedHitOwnership(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[1] = substitute ? bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } }
                : bodies[1] with { Enduring = true, Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
            var result = Ordered(Kernel.Frame(bodies, single), id); var hit = Envelope.Hits(result.Events).First();
            if (substitute) Assert.AreEqual(0, ((SourceSubstituteDamagedEvent)hit).HpAfter);
            else
            {
                var hp = (SourceHpChangedEvent)hit; Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Any(e => e.Target.Value == 1 && e.Endured && e.DamageAfter == 0));
            }
            Check(result, id, single, hit);
        }
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229), DataRow(314)]
    public void ProtectedAndMissedPassesDoNotBorrowAHitRoot(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool miss in new[] { false, true })
        {
            var bodies = Bodies(id);
            foreach (int target in single || id != 314 ? new[] { 1 } : new[] { 1, 3 })
                bodies[target] = bodies[target] with { Protected = !miss, EvasionStage = 12 };
            if (miss) bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single); int expected = !single && id == 314 ? 2 : 1;
            var result = miss ? Find(frame, id, r => r.Events.OfType<SourceMoveMissedEvent>().Count() == expected) : Ordered(frame, id);
            if (!miss) Assert.AreEqual(expected, result.Events.OfType<SourceMoveFailedEvent>().Count(e => e.Reason == "protected"));
            Check(result, id, single, null);
        }
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229)]
    public void ActualGhostImmunityRejectsEvenZeroLossDamageReceipts(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[1] = Kernel.Mon("GASTLY", "SPITE", 100) with { SpeedStage = 0 };
            var result = Ordered(Kernel.Frame(bodies, single), id);
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Assert.AreEqual(bodies[1].Pokemon.CurrentHp, result.State.Slots[1].Body!.Pokemon.CurrentHp); Check(result, id, single, null);
        }
    }

    [TestMethod, DataRow("selected"), DataRow("other"), DataRow("substitute")]
    public void RevengeUsesActualEarlierSelectedSourceDamageRatherThanCoarseFinalFlags(string branch)
    {
        var bodies = Bodies(279); int source = branch == "other" ? 3 : 1;
        bodies[1] = Kernel.Mon("WAILORD", source == 1 ? "WATER_GUN" : "SPLASH") with { AccuracyStage = 12, SpeedStage = 0 };
        bodies[3] = Kernel.Mon("WAILORD", source == 3 ? "WATER_GUN" : "SPLASH") with { AccuracyStage = 12, SpeedStage = 0 };
        if (branch == "substitute") bodies[0] = bodies[0] with { CalledVolatile = new() { SubstituteHp = 100 } };
        var prior = new List<SourceBattleEvent>();
        var result = Ordered(Kernel.Frame(bodies), 279, earlier: r => prior.AddRange(r.Events));
        var first = prior.Single(e => e is SourceHpChangedEvent h && h.Actor.Value == source && h.Target.Value == 0 ||
            e is SourceSubstituteDamagedEvent s && s.Actor.Value == source && s.Target.Value == 0);
        var hit = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.IsTrue(first.Sequence < hit.Sequence);
        bool retaliates = branch == "selected";
        var expected = SourceBattleDamage.Calculate(Kernel.Mechanics.Catalog, bodies[0] with { DamagedByOpponentThisTurn = retaliates },
            bodies[1], Kernel.Mechanics.Catalog.Moves[Move(279)], hit.Critical, hit.RandomPercent, sourceDoubleBattle: true);
        Assert.AreEqual(expected.Damage, hit.Damage);
        if (branch == "substitute") Assert.IsInstanceOfType<SourceSubstituteDamagedEvent>(first);
        else
        {
            var damage = result.State.Slots[0].Registers.Damage; Assert.IsNotNull(damage);
            Assert.AreEqual(new BattlerSlot(source), damage.SpecialSource); Assert.IsTrue(damage.SpecialDamage > 0);
        }
        Check(result, 279, false, hit);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void RapidSpinAuthenticallyClearsBindSeedAndSpikesAfterItsOwnedBodyOrSubstituteHit(bool substitute)
    {
        var bodies = Bodies(229); bodies[1] = Kernel.Mon("ONIX", "BIND", 10) with { AccuracyStage = 12, SpeedStage = 0 };
        bodies[2] = Kernel.Mon("BULBASAUR", "LEECH_SEED") with { AccuracyStage = 12, SpeedStage = 0 };
        bodies[3] = Kernel.Mon("PINECO", "SPIKES") with { SpeedStage = 0 };
        var bound = Envelope.Find(Kernel.Frame(bodies), 1, 0, r => r.Events.OfType<SourceBindingChangedEvent>().Any(e => e.After > 0), "Rapid Spin actual Bind setup");
        var seeded = Envelope.Resolve(bound.State, 2, 0); Assert.IsTrue(seeded.Events.OfType<SourceLeechSeedChangedEvent>().Any(e => e.After == new BattlerSlot(2)));
        var spiked = Envelope.Resolve(seeded.State, 3, 0); Assert.AreEqual(1, spiked.State.Sides[0].Spikes);
        var frame = substitute ? Charge.Body(spiked.State, 1, b => b with { CalledVolatile = new() { SubstituteHp = 1 } }) : spiked.State;
        var result = Ordered(frame, 229); var hit = Envelope.Hits(result.Events).Single();
        var bind = result.Events.OfType<SourceBindingChangedEvent>().Single(e => e.Reason == "rapid-spin");
        var seed = result.Events.OfType<SourceLeechSeedChangedEvent>().Single(e => e.Reason == "rapid-spin");
        var spikes = result.Events.OfType<SourceCalledSpikesChangedEvent>().Single();
        Assert.IsTrue(hit.Sequence < bind.Sequence && bind.Sequence < seed.Sequence && seed.Sequence < spikes.Sequence);
        Assert.AreEqual(0, bind.After); Assert.IsNull(seed.After); Assert.AreEqual(0, spikes.After);
        Assert.AreEqual(0, result.State.Slots[0].Body!.BindTurnsRemaining); Assert.IsNull(result.State.Slots[0].Registers.WrappedBy);
        Assert.IsNull(result.State.Slots[0].Registers.LeechSeedSource); Assert.AreEqual(0, result.State.Sides[0].Spikes);
        Check(result, 229, false, hit);
        Charge.Deny(result.Events.Remove(hit), false);
        var prefix = result.Events.Take(result.Events.IndexOf(hit) + 1).ToImmutableArray();
        Check(result with { Events = prefix }, 229, false, hit);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void RapidSpinFixedMetadataPowerRejectsContradictoryPreparedBodyOrSubstitutePower(bool substitute)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(229); if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            var result = Ordered(Kernel.Frame(bodies, single), 229); var hit = Envelope.Hits(result.Events).Single();
            Assert.IsNull(hit is SourceHpChangedEvent hp ? hp.PreparedBasePower : ((SourceSubstituteDamagedEvent)hit).PreparedBasePower);
            Check(result, 229, single, hit);
            foreach (int invalid in new[] { -1, 0, 19, 21, 65536 }) Charge.Deny(result.Events.Replace(hit, Power(hit, invalid)), single);
        }
    }

    [TestMethod, DataRow("first-protected"), DataRow("second-protected"), DataRow("both-protected"), DataRow("first-substitute")]
    public void AirCutterRetainsPpOnItsSecondPassAndOwnsOnlyTheFirstEligibleHit(string branch)
    {
        var bodies = Bodies(314);
        if (branch is "first-protected" or "both-protected") bodies[1] = bodies[1] with { Protected = true };
        if (branch is "second-protected" or "both-protected") bodies[3] = bodies[3] with { Protected = true };
        if (branch == "first-substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
        var result = Ordered(Kernel.Frame(bodies), 314); var commands = result.Events.OfType<SourcePpCommandEvent>().ToArray();
        Assert.AreEqual(2, commands.Length); Assert.AreEqual(1, commands[0].Target.Value); Assert.AreEqual(3, commands[1].Target.Value);
        Assert.AreEqual(commands[0].Before - commands[0].CommandCost, commands[0].After);
        Assert.AreEqual(commands[0].After, commands[1].Before); Assert.AreEqual(commands[1].Before, commands[1].After);
        var hits = Envelope.Hits(result.Events); var owner = branch == "both-protected" ? null : hits.First();
        if (owner is not null) Assert.AreEqual(branch == "first-protected" ? 3 : 1, Target(owner));
        Check(result, 314, false, owner);
        Charge.Deny(result.Events.Replace(commands[1], commands[1] with { After = commands[1].After - 1 }), false);
    }

    [TestMethod]
    public void ExtremeSpeedSourcePriorityWinsEvenAgainstMuchFasterOrdinaryActors()
    {
        var bodies = Bodies(245); bodies[0] = bodies[0] with { SpeedStage = 0 };
        for (int slot = 1; slot < 4; slot++) bodies[slot] = bodies[slot] with { SpeedStage = 12 };
        var result = Ordered(Kernel.Frame(bodies), 245); Check(result, 245, false, Envelope.Hits(result.Events).Single());
    }

    [TestMethod]
    public void AirCutterFirstActualMissLeavesTheSecondHitAndRetainedPressureCommandAsOwner()
    {
        var bodies = Bodies(314); bodies[0] = bodies[0] with { AccuracyStage = 6 };
        bodies[1] = Kernel.Mon("ARTICUNO", "MIST", 100) with { SpeedStage = 0, EvasionStage = 12 };
        bodies[3] = Kernel.Mon("DUSCLOPS", "LEER", 100) with { SpeedStage = 0 };
        var result = Find(Kernel.Frame(bodies), 314, r => r.Events.OfType<SourceMoveMissedEvent>().Any(e => e.Target.Value == 1) &&
            Envelope.Hits(r.Events).Any(e => Target(e) == 3));
        var pp = result.Events.OfType<SourcePpCommandEvent>().ToArray(); Assert.AreEqual(2, pp.Length);
        Assert.AreEqual(3, pp[0].CommandCost); Assert.AreEqual(3, pp[1].CommandCost);
        Assert.AreEqual(pp[0].Before - 3, pp[0].After); Assert.AreEqual(pp[0].After, pp[1].Before); Assert.AreEqual(pp[1].Before, pp[1].After);
        Check(result, 314, false, Envelope.Hits(result.Events).Single());
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229), DataRow(314)]
    public void ActualMetronomeHitLeavesOwnOnlyTheirFirstCommittedPass(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(id)) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(id)));
            Assert.IsTrue(Envelope.Hits(result.Events).Length > 0); var outcome = Envelope.Hits(result.Events).First();
            var cue = CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, outcome,
                target: new BattlerSlot(Target(outcome)));
            Assert.AreEqual(id == 229 ? (ushort?)20 : null, cue.MovePower);
        }
    }

    [TestMethod, DataRow(279), DataRow(245), DataRow(229), DataRow(314)]
    public void EveryRootRequiresOwnedAttackPpAndActualCanonicalScriptMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Ordered(Kernel.Frame(Bodies(id), single), id).Events;
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().First();
            Charge.Deny(events.Remove(attack), single); Charge.Deny(events.Remove(pp), single);
            Charge.Deny(events.Replace(attack, attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Charge.Deny(events.Replace(pp, pp with { PpOwner = new(1) }), single);
            Charge.Deny(events.Replace(pp, pp with { Turn = pp.Turn + 1 }), single);
            int at = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single()); var moves = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, moves.Remove(Move(id)), moves.SetItem(Move(id), moves[Move(id)] with { SourceMoveId = id == 229 ? 245 : 229 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Charge.Single(events, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(events, at), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, at), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }
}
