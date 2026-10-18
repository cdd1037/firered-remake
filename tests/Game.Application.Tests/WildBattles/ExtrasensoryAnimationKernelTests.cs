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

// Natural Nuzleaf learner; controlled stages, volatile state and bounded source
// RNG searches are kernel fixtures, not natural route or renderer evidence.
[TestClass, TestCategory("Game")]
public sealed class ExtrasensoryAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("EXTRASENSORY");
    private static SourceBattleFrame Initial(bool single, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("NUZLEAF", "EXTRASENSORY") with { SpeedStage = 12, AccuracyStage = 12 };
        return Kernel.Frame(bodies, single);
    }
    private static void Check(ImmutableArray<SourceBattleEvent> rows, bool single)
    {
        string before = JsonSerializer.Serialize(rows); var definition = Kernel.Mechanics.Catalog.Moves[Move];
        Assert.AreEqual("EFFECT_FLINCH_MINIMIZE_HIT", definition.Effect); Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
        Assert.AreEqual(10, definition.SecondaryEffectChance); Assert.IsTrue(AnimationDispatchFixture.Supports(326, Move.Value));
        var used = rows.OfType<SourceMoveUsedEvent>().Single(); var attack = rows.OfType<SourceAttackStringEvent>().Single();
        var pp = rows.OfType<SourcePpCommandEvent>().Single(); var hit = Envelope.Hits(rows).Single();
        Assert.AreEqual(Move, used.Move); Assert.AreEqual(Move, attack.ChosenMove); Assert.AreEqual(Move, attack.CurrentMove);
        Assert.AreEqual(Move, pp.ChosenMove); Assert.AreEqual(Move, pp.CurrentMove); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot); Assert.AreEqual(used.PpBefore, pp.Before);
        Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        int roots = 0;
        for (int i = 0; i < rows.Length; i++) foreach (bool atResult in new[] { false, true })
        {
            var decision = F.Project(rows, i, single, atResult); Assert.AreEqual(decision, F.Project(rows, i, single, atResult));
            Assert.IsNull(F.Project(rows, i, single, atResult, SourceBattleScene.Off).Dispatch); Assert.IsNull(F.Wave(rows, i, single));
            if (rows[i] != used || atResult) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
            Assert.AreEqual(326, cue.SourceMoveId); Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence); Assert.AreEqual(hit.Sequence, cue.ResultSequence);
            Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target); Assert.AreEqual(hit is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveTurn);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(1, roots); Assert.AreEqual(before, JsonSerializer.Serialize(rows));
        if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(F.Single(rows, rows.IndexOf(used)), false, SourceBattleScene.On).Dispatch);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void NaturalSelectedHitOwnsOneRootAtEveryPositionIncludingSelectedAllies(bool substitute)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : Enumerable.Range(0, 4).Where(t => t != actor).ToArray())
        {
            var frame = Initial(single, actor);
            if (substitute) frame = F.Body(frame, target, b => b with { CalledVolatile = new() { SubstituteHp = 1 } });
            var result = F.Ordered(frame, actor, target); string before = JsonSerializer.Serialize(result); Check(result.Events, single);
            Assert.AreEqual(substitute, Envelope.Hits(result.Events).Single() is SourceSubstituteDamagedEvent);
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod]
    public void ActualPriorMinimizeIncreasesCommittedDamageWithoutAnotherRootOrNewInvocationInput()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 1, _ => Kernel.Mon("MUK", "MINIMIZE", 100) with { SpeedStage = 0 });
            var setup = Envelope.Resolve(frame, 1, 1);
            var marker = setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "minimized");
            Assert.AreEqual(new BattlerSlot(1), marker.Side); Assert.AreEqual(0, marker.Before); Assert.AreEqual(1, marker.After);
            Assert.IsTrue(setup.State.Slots[1].Body!.Minimized); Assert.IsFalse(frame.Slots[1].Body!.Minimized);
            var plain = F.Ordered(frame with { Rng = new(31) }, 0, 1);
            var minimized = F.Ordered(setup.State with { Rng = new(31) }, 0, 1);
            var first = plain.Events.OfType<SourceHpChangedEvent>().Single(); var second = minimized.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.IsTrue(second.Damage > first.Damage); Assert.AreEqual(first.Critical, second.Critical); Assert.AreEqual(first.RandomPercent, second.RandomPercent);
            Assert.AreEqual(plain.State.Rng, minimized.State.Rng); Assert.AreEqual(JsonSerializer.Serialize(plain.Trace), JsonSerializer.Serialize(minimized.Trace));
            Assert.IsTrue(second.HpAfter > 0); Check(plain.Events, single); Check(minimized.Events, single);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("dark"), DataRow("miss"), DataRow("sleep")]
    public void PrimaryPreventionCannotBorrowPpOrAnImmuneZeroDamageReceipt(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single);
            if (branch == "protect") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "dark") frame = F.Body(frame, 1, _ => Kernel.Mon("POOCHYENA", "TACKLE", 100) with { SpeedStage = 0 });
            if (branch == "sleep") frame = F.Body(frame, 0, b => b with { Status = 3 });
            if (branch == "miss") { frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 }); frame = F.Body(frame, 1, b => b with { EvasionStage = 12 }); }
            var result = branch == "miss" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Extrasensory accuracy miss") : F.Ordered(frame, 0, 1);
            if (branch == "dark") Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            else if (branch == "sleep") Assert.IsFalse(result.Events.OfType<SourcePpCommandEvent>().Any());
            else if (branch == "protect") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Move && e.Reason == "protected"));
            else Assert.IsTrue(result.Events.OfType<SourceMoveMissedEvent>().Any());
            F.Deny(result.Events, single);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void RealSecondaryFlinchChanceNeverOwnsOrGatesTheHitRoot(bool flinches)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = F.Find(Initial(single), 0, 1, r => r.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied) == flinches, "Extrasensory flinch=" + flinches);
            Check(result.Events, single);
            if (!flinches) continue;
            var flinch = result.Events.OfType<SourceFlinchEvent>().Single(); var hit = Envelope.Hits(result.Events).Single();
            Assert.AreEqual(new BattlerSlot(1), flinch.Side); Assert.IsTrue(flinch.Sequence > hit.Sequence);
            Check(result.Events.Remove(flinch), single);
            F.Deny(result.Events.Remove(hit), single);
        }
    }

    [TestMethod, DataRow("inner-focus"), DataRow("shield-dust"), DataRow("substitute")]
    public void AQualifyingButPreventedSecondaryFlinchKeepsItsCommittedRoot(string prevention)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 1, b => prevention switch {
                "inner-focus" => Kernel.Mon("ZUBAT", "LEECH_LIFE", 100) with { SpeedStage = 0 },
                "shield-dust" => Kernel.Mon("CATERPIE", "TACKLE", 100) with { SpeedStage = 0 },
                _ => b with { CalledVolatile = new() { SubstituteHp = 1 } } });
            var result = F.Find(frame, 0, 1, r => r.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 10),
                "Extrasensory qualifying flinch suppressed by " + prevention);
            Assert.IsFalse(result.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied)); Check(result.Events, single);
            Assert.IsTrue(result.State.Slots[1].Body!.Pokemon.CurrentHp > 0);
        }
    }

    [TestMethod]
    public void ActualMetronomeExtrasensoryOwnsOnlyItsCommittedHitRoot()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 0, _ => Kernel.Mon("CLEFAIRY", "METRONOME") with { SpeedStage = 12, AccuracyStage = 12 });
            // Flinch needs a genuine ordered action owner. Select a seed from
            // the public turn-start RNG, then execute that exact continuation.
            var choices = frame.Slots.Take(frame.BattlerCount).Select(cell => new SourceActionSelection(cell.Occupant!.Value,
                SourceActionKind.Move, 0, cell.Slot.Value == 0 ? new BattlerSlot(1) : cell.Slot)).ToImmutableArray();
            SourceFourActionResult? result = null;
            for (uint seed = 0; seed < 65536; seed++)
            {
                var started = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame with { Rng = new(seed) }, frame.Revision, choices);
                Assert.IsTrue(started.Succeeded, started.Fault); Assert.AreEqual(0, started.State.Queue[0].Slot.Value);
                if (WildBattleSourceCallers.Metronome(started.State.Rng).SourceMoveId != 326) continue;
                Assert.IsNotNull(started.Continuation);
                result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, started.State, started.Continuation.Value);
                Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State)); break;
            }
            Assert.IsNotNull(result, "Require a real ordered Metronome Extrasensory leaf within the bounded seeds.");
            Assert.AreEqual(Move, result.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            Assert.AreEqual(1, Envelope.Hits(result.Events).Length); CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), 326, Envelope.Hits(result.Events).Single());
        }
    }

    [TestMethod]
    public void HitOwnershipAndCanonicalMetadataAreRequired()
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single), 0, 1).Events; var pp = rows.OfType<SourcePpCommandEvent>().Single();
            var hit = rows.OfType<SourceHpChangedEvent>().Single(); var used = rows.OfType<SourceMoveUsedEvent>().Single(); int at = rows.IndexOf(used);
            F.Deny(rows.Remove(hit), single); F.Deny(Numbering.Number(rows.Insert(rows.IndexOf(hit) + 1, hit)), single);
            F.Deny(rows.SetItem(rows.IndexOf(hit), hit with { Actor = new(1) }), single);
            F.Deny(rows.SetItem(rows.IndexOf(pp), pp with { PpOwner = new(1) }), single);
            var definitions = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, definitions.Remove(Move), definitions.SetItem(Move, definitions[Move] with { SourceMoveId = 1 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }
}
