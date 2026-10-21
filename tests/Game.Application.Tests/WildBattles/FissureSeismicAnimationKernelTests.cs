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

// Natural learners and public kernel commands. Controlled HP, stages, volatile
// state and bounded seeds are integration fixtures, not natural-route evidence.
[TestClass, TestCategory("Game")]
public sealed class FissureSeismicAnimationKernelTests
{
    private static MoveId Move(int id) => Kernel.Id(id == 69 ? "SEISMIC_TOSS" : "FISSURE");
    private static SourceBattleFrame Initial(int id, bool single, int actor = 0, int level = 100)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon(id == 69 ? "MACHOP" : "DIGLETT", id == 69 ? "SEISMIC_TOSS" : "FISSURE", level) with {
            SpeedStage = 12, AccuracyStage = 12 };
        return Kernel.Frame(bodies, single);
    }
    private static SourceFourActionResult Success(SourceBattleFrame frame, int id, int actor = 0, int target = 1) => id == 90
        ? F.Find(frame, actor, target, r => Envelope.Hits(r.Events).Any(), "Fissure successful OHKO command") : F.Ordered(frame, actor, target);
    private static int Damage(SourceBattleEvent row) => row switch {
        SourceHpChangedEvent hp => hp.Damage, SourceSubstituteDamagedEvent sub => sub.Damage, _ => throw new ArgumentException("Expected a hit receipt.") };
    private static void Check(SourceFourActionResult result, int id, bool single)
    {
        string before = JsonSerializer.Serialize(result); var events = result.Events;
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single(); var hit = Envelope.Hits(events).Single();
        Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value), "Require the generated canonical ID and label.");
        Assert.AreEqual(Move(id), pp.ChosenMove); Assert.AreEqual(Move(id), pp.CurrentMove); Assert.AreEqual(used.Actor, pp.Actor);
        Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        int roots = 0;
        for (int i = 0; i < events.Length; i++) foreach (bool atResult in new[] { false, true })
        {
            var decision = F.Project(events, i, single, atResult); Assert.AreEqual(decision, F.Project(events, i, single, atResult));
            Assert.IsNull(F.Project(events, i, single, atResult, SourceBattleScene.Off).Dispatch); Assert.IsNull(F.Wave(events, i, single));
            if (events[i] != used || atResult) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence); Assert.AreEqual(hit.Sequence, cue.ResultSequence);
            Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target); Assert.AreEqual(hit is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.AreEqual(id == 69 ? (int?)Damage(hit) : null, cue.MoveDamage); Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(1, roots); Assert.AreEqual(before, JsonSerializer.Serialize(result));
        string source = JsonSerializer.Serialize(events);
        Assert.AreEqual(source, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(source)));
        if (single)
        {
            var battle = F.Single(events, events.IndexOf(used)); Assert.AreEqual(events, battle.Events);
            string legacy = JsonSerializer.Serialize(battle.Events);
            Assert.AreEqual(legacy, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(legacy)));
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, false, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod, DataRow(69), DataRow(90)]
    public void NaturalFixedAndOhkoLearnersOwnOneCommittedRootAcrossAllPositionsAndSelectedPartners(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : Enumerable.Range(0, 4).Where(t => t != actor).ToArray())
        {
            var result = Success(Initial(id, single, actor), id, actor, target); Check(result, id, single);
            var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.IsFalse(hp.Critical); Assert.AreEqual(100, hp.RandomPercent);
            Assert.AreEqual(id == 69 ? 100 : hp.HpBefore, hp.Damage); Assert.AreEqual(Math.Max(0, hp.HpBefore - hp.Damage), hp.HpAfter);
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".damage", StringComparison.Ordinal) || t.Purpose.EndsWith(".critical", StringComparison.Ordinal)));
        }
    }

    [TestMethod, DataRow(32), DataRow(33), DataRow(65), DataRow(66)]
    public void SeismicDamageBranchEdgesUseTheExactCommittedCommandValue(int level)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Success(Initial(69, single, level: level), 69); var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(level, hp.Damage); Check(result, 69, single);
            // The renderer owns the <33 / 33..65 / >65 branching. Application
            // passes this full value through without selecting or clipping a bucket.
            var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
            Assert.AreEqual(level, F.Project(result.Events, result.Events.IndexOf(used), single, false).Dispatch!.MoveDamage);
        }
    }

    [TestMethod, DataRow(69), DataRow(90)]
    public void BodyOverkillAndSubstituteClampingDoNotRewriteTheAnimationDamageRegister(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var frame = F.Body(Initial(id, single), 1, b => substitute
                ? b with { CalledVolatile = new() { SubstituteHp = 1 } }
                : b with { Pokemon = b.Pokemon with { CurrentHp = 1 } });
            int bodyHp = frame.Slots[1].Body!.Pokemon.CurrentHp; var result = Success(frame, id); Check(result, id, single);
            var hit = Envelope.Hits(result.Events).Single(); Assert.AreEqual(id == 69 ? 100 : bodyHp, Damage(hit));
            if (substitute)
            {
                var sub = (SourceSubstituteDamagedEvent)hit; Assert.AreEqual(1, sub.HpBefore); Assert.AreEqual(0, sub.HpAfter);
                Assert.IsTrue(sub.Damage > sub.HpBefore - sub.HpAfter); Assert.AreEqual(bodyHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
            }
            else
            {
                var hp = (SourceHpChangedEvent)hit; Assert.AreEqual(0, hp.HpAfter);
                if (id == 69) Assert.IsTrue(hp.Damage > hp.HpBefore - hp.HpAfter);
            }
        }
    }

    [TestMethod, DataRow(69, false), DataRow(69, true), DataRow(90, false), DataRow(90, true)]
    public void ActualEndureAndFocusBandAtOneHpStillOwnAZeroDamageInvocation(int id, bool band)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(id, single), 1, b => b with { Enduring = !band, HeldItem = band ? "ITEM_FOCUS_BAND" : "ITEM_NONE",
                Pokemon = b.Pokemon with { CurrentHp = 1 } });
            var result = F.Find(frame, 0, 1, r => r.Events.OfType<SourceDamageAdjustedEvent>().Any(e => e.DamageAfter == 0 && e.HungOn == band && e.Endured != band)
                && Envelope.Hits(r.Events).Any(), $"{id} one-HP {(band ? "Focus Band" : "Endure")}");
            var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
            var adjusted = result.Events.OfType<SourceDamageAdjustedEvent>().Single(); Assert.IsTrue(adjusted.DamageBefore > 0); Assert.IsTrue(adjusted.Sequence < hp.Sequence);
            Assert.IsFalse(result.Events.OfType<SourceFaintedEvent>().Any()); Check(result, id, single);
        }
    }

    [TestMethod, DataRow(69), DataRow(90)]
    public void SourceEndureDistinctionBehindSubstituteIsRetainedWithoutFinalHpInference(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(id, single), 1, b => b with { Enduring = true, Pokemon = b.Pokemon with { CurrentHp = 1 },
                CalledVolatile = new() { SubstituteHp = 1 } });
            var result = Success(frame, id); var sub = result.Events.OfType<SourceSubstituteDamagedEvent>().Single(); Check(result, id, single);
            Assert.AreEqual(id == 69 ? 100 : 0, sub.Damage); Assert.AreEqual(id == 69 ? 0 : 1, sub.HpAfter);
            Assert.AreEqual(id == 90, result.Events.OfType<SourceDamageAdjustedEvent>().Any(e => e.Endured));
            Assert.AreEqual(1, result.State.Slots[1].Body!.Pokemon.CurrentHp);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("ghost"), DataRow("miss")]
    public void SeismicTossPrimaryPreventionCannotBorrowAZeroDamageHit(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(69, single);
            if (branch == "protect") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "ghost") frame = F.Body(frame, 1, _ => Kernel.Mon("GASTLY", "LICK", 100) with { SpeedStage = 0 });
            if (branch == "miss") { frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 }); frame = F.Body(frame, 1, b => b with { EvasionStage = 12 }); }
            var result = branch == "miss" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Seismic accuracy miss") : F.Ordered(frame, 0, 1);
            if (branch == "ghost") Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            else if (branch == "protect") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Move(69) && e.Reason == "protected"));
            else Assert.IsTrue(result.Events.OfType<SourceMoveMissedEvent>().Any());
            F.Deny(result.Events, single);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("flying"), DataRow("levitate"), DataRow("sturdy"), DataRow("level"), DataRow("chance")]
    public void FissureRequiresActualSuccessfulTryKoAndNeverOrdinaryAccuracy(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(90, single, level: branch == "level" ? 50 : 100);
            if (branch == "protect") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "flying") frame = F.Body(frame, 1, _ => Kernel.Mon("PIDGEY", "GUST", 100) with { SpeedStage = 0 });
            if (branch == "levitate") frame = F.Body(frame, 1, _ => Kernel.Mon("GASTLY", "LICK", 100) with { SpeedStage = 0 });
            if (branch == "sturdy") frame = F.Body(frame, 1, _ => Kernel.Mon("PINECO", "TACKLE", 100) with { SpeedStage = 0 });
            var result = branch == "chance" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "ohko-miss"), "Fissure chance miss") : F.Ordered(frame, 0, 1);
            Assert.IsFalse(Envelope.Hits(result.Events).Any()); F.Deny(result.Events, single);
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            if (branch is "level" or "sturdy") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "ohko-" + branch));
            if (branch is "flying" or "levitate") Assert.IsTrue(result.Events.Any(e => e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } or SourceAbilityPreventedEvent));
        }
    }

    [TestMethod, DataRow(69), DataRow(90)]
    public void MissingDuplicateForeignOrCalledReceiptsCannotAuthenticateTheDirectRoot(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var frame = Initial(id, single); if (substitute) frame = F.Body(frame, 1, b => b with { CalledVolatile = new() { SubstituteHp = 10 } });
            var events = Success(frame, id).Events; var hit = Envelope.Hits(events).Single(); int at = events.IndexOf(hit);
            var pp = events.OfType<SourcePpCommandEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
            F.Deny(Shared.Number(events.Remove(hit)), single); F.Deny(Shared.Number(events.Insert(at + 1, hit)), single);
            F.Deny(Shared.Number(events.Remove(pp)), single); F.Deny(Shared.Number(events.Insert(events.IndexOf(pp) + 1, pp)), single);
            F.Deny(events.SetItem(events.IndexOf(pp), pp with { PpOwner = new(1) }), single);
            F.Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            var foreign = hit switch { SourceHpChangedEvent hp => (SourceBattleEvent)(hp with { Target = new(0) }),
                SourceSubstituteDamagedEvent sub => sub with { Target = new(0) }, _ => throw new InvalidOperationException() };
            F.Deny(events.SetItem(at, foreign), single);
            if (id == 69)
            {
                var negative = hit switch { SourceHpChangedEvent hp => (SourceBattleEvent)(hp with { Damage = -1 }),
                    SourceSubstituteDamagedEvent sub => sub with { Damage = -1 }, _ => throw new InvalidOperationException() };
                F.Deny(events.SetItem(at, negative), single);
            }
        }
    }

    [TestMethod, DataRow(69), DataRow(90)]
    public void ActualMetronomeSeismicOwnsItsHitButLevelBlockedFissureStaysSilent(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Kernel.Frame(CallerPreludeAnimationKernelFixture.Bodies(118), single) with {
                Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(id)) };
            var result = Envelope.Resolve(frame, 0, 1); Assert.IsTrue(result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Move(id)));
            if (id == 90)
            {
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Move(id) && e.Reason == "ohko-level"));
                Assert.AreEqual(0, Envelope.Hits(result.Events).Length);
                CallerPreludeAnimationKernelFixture.DenyLeaf(result.Events, single, id);
            }
            else
            {
                var hit = Envelope.Hits(result.Events).Single();
                var cue = CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, hit);
                Assert.AreEqual(Damage(hit), cue.MoveDamage);
            }
        }
    }

    [TestMethod, DataRow(69), DataRow(90)]
    public void SourceMetadataSceneAndPublishedCatalogRemainTheOnlyAdmissionAuthority(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Success(Initial(id, single), id).Events; var used = events.OfType<SourceMoveUsedEvent>().Single(); int index = events.IndexOf(used);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            var bad = new List<IReadOnlyDictionary<MoveId, PokemonMoveDefinition>?> { null, definitions.Remove(Move(id)),
                definitions.SetItem(Move(id), definition with { SourceMoveId = 1 }), definitions.SetItem(Move(id), definition with { Target = "UNKNOWN_TARGET" }) };
            if (id == 69) { bad.Add(definitions.SetItem(Move(id), definition with { Power = 0 })); bad.Add(definitions.SetItem(Move(id), definition with { Target = "MOVE_TARGET_USER" })); }
            foreach (var metadata in bad) Assert.IsNull(single
                ? AnimationDispatchFixture.AtAttackText(F.Single(events, index), true, SourceBattleScene.On, metadata).Dispatch
                : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(events, index), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(events, index) with { Phase = CampaignBattlePhase.Results }, true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index) with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        }
    }
}
